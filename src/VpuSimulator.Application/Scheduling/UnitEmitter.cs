using System.Text.Json;
using Microsoft.Extensions.Logging;
using VpuSimulator.Application.Commands;
using VpuSimulator.Application.Interfaces.Messaging;
using VpuSimulator.Application.Interfaces.Simulation;
using VpuSimulator.Application.Messaging;
using VpuSimulator.Domain;
using VpuSimulator.Domain.Entities;
using VpuSimulator.Domain.Enums;
using VpuSimulator.Domain.FollowUp;
using VpuSimulator.Domain.RuleContracts;

namespace VpuSimulator.Application.Scheduling;

/// <summary>
/// Hàm phát sự kiện thật sự của Emission Unit
/// Thực thi đúng 11 bước theo đặc tả Contract v4 §4.5:
/// 1. Đọc config.Current (ParsedTask, ParsedRule)
/// 2. Ánh xạ sang UnitInfo và RuleContext
/// 3. Dựng TickContext (Rng, Now, CorrelationStore, FixedValues, Mode)
/// 4. Kiểm tra ActiveFollowUpState (nếu cùng RunToken)
/// 5. Tra cứu RuleDefinition qua IRuleHandlerRegistry
/// 6. def.Handler.ExecuteAsync -> RuleDecision
/// 7. def.Builder.Build -> BuildResult (Event + Proposals)
/// 8. Ánh xạ EnvelopeMapper -> DataEventEnvelope
/// 9. Publish lên RabbitMQ (inbound.data.vpu.<task_id>)
/// 10. Commit proposals qua UnitRunner.TryCommit
/// 11. Trả về RuleOutcome.Emitted
/// </summary>
public static class UnitEmitter
{
    public static UnitEmitterFactory CreateFactory(
        IRuleHandlerRegistry registry,
        Func<string, IMessageBroker?> brokerResolver,
        ICorrelationStore correlationStore,
        ILogger? logger = null,
        FixedValues? fixedValues = null,
        SimulationMode mode = SimulationMode.Simulated)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(brokerResolver);
        ArgumentNullException.ThrowIfNull(correlationStore);

        return (UnitKey key, IUnitConfigSource config) =>
        {
            // Trạng thái follow-up được lưu riêng cho từng emission unit
            long savedRunToken = 0;
            ActiveFollowUpState? activeFollowUp = null;

            // Dữ liệu dẫn xuất từ cấu hình: chỉ dựng lại khi task/rule đổi (update-task), không dựng mỗi tick
            ParsedTask? cachedTask = null;
            ParsedRule? cachedParsedRule = null;
            UnitInfo? cachedUnit = null;
            RuleContext? cachedRule = null;

            // Chống ngập log: chỉ cảnh báo 1 lần cho mỗi đợt sự cố
            string? unresolvedLoggedFor = null;
            bool brokerDownLogged = false;

            return async (UnitEmitContext context, CancellationToken cancellationToken) =>
            {
                var cfg = config.Current;
                if (cfg.Rule.Enable == false)
                {
                    return RuleOutcome.NoMessage;
                }

                if (!ReferenceEquals(cfg.Task, cachedTask) || !ReferenceEquals(cfg.Rule, cachedParsedRule))
                {
                    var (frameWidth, frameHeight) = FrameSize.Resolve(
                        cfg.Task.Capability?.Width, cfg.Task.Capability?.Height);

                    cachedUnit = new UnitInfo(
                        AppId: key.AppId,
                        TaskId: cfg.Task.TaskId,
                        SourceId: cfg.Task.StreamUuid ?? string.Empty,
                        SourceName: cfg.Task.SourceInfo?.Name ?? string.Empty,
                        RoiId: cfg.Rule.RoiId,
                        FrameWidth: frameWidth,
                        FrameHeight: frameHeight
                    );
                    cachedRule = MapRuleContext(cfg.Rule);
                    cachedTask = cfg.Task;
                    cachedParsedRule = cfg.Rule;
                }

                var unit = cachedUnit!;
                var rule = cachedRule!;

                var tick = new TickContext(
                    Rng: context.PayloadRandom,
                    Now: context.Now,
                    Correlation: correlationStore,
                    FixedValues: fixedValues ?? FixedValues.Off,
                    Mode: mode
                );

                var active = (savedRunToken == context.RunToken) ? activeFollowUp : null;

                var def = registry.Resolve(rule.Name, rule.AiFlowCode);
                if (def is null)
                {
                    var warnKey = $"{rule.Name}|{rule.AiFlowCode}";
                    if (unresolvedLoggedFor != warnKey)
                    {
                        logger?.LogWarning(
                            "Unit {Unit}: không tìm thấy rule definition cho {RuleName} (aiFlowCode: {AiFlowCode})",
                            key, rule.Name, rule.AiFlowCode);
                        unresolvedLoggedFor = warnKey;
                    }

                    return RuleOutcome.Precondition;
                }

                unresolvedLoggedFor = null;

                // Resolve broker TRƯỚC khi chạy handler/builder (Build có thể TryDelete correlation)
                var broker = brokerResolver(unit.AppId);
                if (broker is null || !broker.IsConnected)
                {
                    if (!brokerDownLogged)
                    {
                        logger?.LogWarning("Unit {Unit}: broker của identity '{AppId}' chưa sẵn sàng, tạm dừng phát",
                            key, unit.AppId);
                        brokerDownLogged = true;
                    }

                    return RuleOutcome.Error;
                }

                brokerDownLogged = false;
                // Bước 1 rút số ngẫu nhiên: Handler ExecuteAsync
                var decision = await def.Handler.ExecuteAsync(unit, rule, tick, active, cancellationToken)
                    .ConfigureAwait(false);
                if (decision.Outcome != RuleOutcome.Emitted)
                {
                    return decision.Outcome;
                }

                // Bước 2 rút số ngẫu nhiên: Builder Build (dùng chung tick.Rng)
                var build = def.Builder.Build(unit, rule, tick, decision, active);

                // Dựng wire envelope
                var stamp = context.CoalesceTimestamps ? TimestampCoalescer.FloorToSecond(context.Now) : context.Now;
                var envelope = EnvelopeMapper.Map(unit.AppId, unit.TaskId, stamp, build.Event);;

                var routingKey = RoutingKeys.Data(unit.TaskId);
                await broker.PublishAsync(routingKey, envelope, cancellationToken).ConfigureAwait(false);

                // Sau khi publish thành công -> áp dụng state proposals qua TryCommit
                void ApplyProposals()
                {
                    if (build.Proposals is null) return;
                    foreach (var proposal in build.Proposals)
                    {
                        switch (proposal)
                        {
                            case SetActiveFollowUp setActive:
                                activeFollowUp = setActive.State;
                                savedRunToken = context.RunToken;
                                break;
                            case ClearActiveFollowUp:
                                activeFollowUp = null;
                                break;
                            case WriteCorrelation write:
                                correlationStore.Write(unit.AppId, write.Key, write.Value);
                                break;
                            default:
                                logger?.LogWarning(
                                    "Unit {Unit}: bỏ qua loại state proposal chưa được hỗ trợ {Proposal}",
                                    key, proposal.GetType().Name);
                                break;
                        }
                    }
                }

                if (context.TryCommit is null)
                {
                    logger?.LogWarning("Unit {Unit}: thiếu TryCommit, bỏ qua {Count} state proposal", key,
                        build.Proposals?.Count ?? 0);
                }
                else
                {
                    context.TryCommit(ApplyProposals);
                }

                return RuleOutcome.Emitted;
            };
        };
    }

    public static RuleContext MapRuleContext(ParsedRule rule)
    {
        var raw = rule.Raw;
        var affectObjects = ParseAffectObjects(raw);

        AiFlowCode? aiFlowCode = null;
        if (rule.AiFlowCode.HasValue && Enum.IsDefined(typeof(AiFlowCode), rule.AiFlowCode.Value))
        {
            aiFlowCode = (AiFlowCode)rule.AiFlowCode.Value;
        }

        return new RuleContext
        {
            RoiId = rule.RoiId,
            Name = rule.Name,
            ZoneType = rule.Type ?? 0,
            AiFlowCode = aiFlowCode,
            Enable = rule.Enable ?? true,
            AffectObject = affectObjects,
            RawParams = raw
        };
    }

    public static IReadOnlyList<AffectObjectConfig>? ParseAffectObjects(JsonElement raw)
    {
        if (raw.ValueKind != JsonValueKind.Object || !raw.TryGetProperty("affect_object", out var prop) ||
            prop.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var list = new List<AffectObjectConfig>();
        foreach (var item in prop.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                var str = item.GetString();
                if (!string.IsNullOrWhiteSpace(str))
                {
                    list.Add(new AffectObjectConfig(str, 0.0, Array.Empty<double>(), 0, Array.Empty<string>(), 0));
                }

                continue;
            }

            if (item.ValueKind != JsonValueKind.Object) continue;

            var label = item.TryGetProperty("label", out var l) && l.ValueKind == JsonValueKind.String
                ? l.GetString() ?? ""
                : "";
            var overlapRatio = ReadDouble(item, "overlap_ratio");
            var minArea = ReadInt(item, "min_area");
            var direction = ReadInt(item, "direction");

            var dimRatio = new List<double>();
            if (item.TryGetProperty("dim_ratio", out var dr) && dr.ValueKind == JsonValueKind.Array)
            {
                foreach (var el in dr.EnumerateArray())
                {
                    if (el.ValueKind == JsonValueKind.Number && el.TryGetDouble(out var v)) dimRatio.Add(v);
                }
            }

            var movement = new List<string>();
            if (item.TryGetProperty("movement", out var mov) && mov.ValueKind == JsonValueKind.Array)
            {
                foreach (var el in mov.EnumerateArray())
                {
                    if (el.ValueKind != JsonValueKind.String) continue;
                    var s = el.GetString();
                    if (s != null) movement.Add(s);
                }
            }

            list.Add(new AffectObjectConfig(label, overlapRatio, dimRatio, minArea, movement, direction));
        }

        return list.Count > 0 ? list : null;
    }

    private static double ReadDouble(JsonElement obj, string name) =>
        obj.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.Number && p.TryGetDouble(out var d)
            ? d
            : 0.0;

    private static int ReadInt(JsonElement obj, string name) =>
        obj.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.Number && p.TryGetInt32(out var i) ? i : 0;
}