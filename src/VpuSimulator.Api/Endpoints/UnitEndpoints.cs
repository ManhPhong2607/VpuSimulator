using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using VpuSimulator.Application.Commands;
using VpuSimulator.Application.Interfaces.Simulation;
using VpuSimulator.Application.Scheduling;
using VpuSimulator.Application.State;
using VpuSimulator.Domain;
using VpuSimulator.Domain.Enums;

namespace VpuSimulator.Api.Endpoints;

public static partial class ControlApiEndpoints
{
    private static IReadOnlyList<UnitDto> GetUnits(
        IUnitRegistry units,
        ITaskStore tasks,
        IRuleHandlerRegistry registry,
        [FromQuery(Name = "app_id")] string? appId,
        EpsMeter eps) =>
        units.Snapshots(appId).Select(s => ToUnitDto(s, units, tasks, registry, eps.Current)).ToList();

    // GET /api/v1/units/{appID}/{task}/{roi}[?rule=]: chi tiết một unit kèm rule_conf gốc của VMS
    // 404 khi không có; 409 khi nhiều rule dùng chung roi mà thiếu ?rule= (mục 10.1 của contract)
    private static IResult GetUnit(
        IUnitRegistry units,
        ITaskStore tasks,
        IRuleHandlerRegistry registry,
        string appId,
        string task,
        int roi,
        [FromQuery] string? rule,
        EpsMeter eps)
    {
        var matches = units.Snapshots(appId)
            .Where(s => s.Key.TaskId == task && s.Key.RoiId == roi && (rule is null || s.Key.RuleName == rule))
            .ToList();

        if (matches.Count == 0)
        {
            return Results.NotFound(new ErrorDto("Không có unit khớp (app_id, task, roi, rule)."));
        }

        if (matches.Count > 1)
        {
            return Results.Conflict(new ErrorDto(
                $"Nhiều rule dùng roi {roi}; thêm ?rule=<tên rule>. Các rule: {string.Join(", ", matches.Select(m => m.Key.RuleName))}."));
        }

        return Results.Ok(ToUnitDto(matches[0], units, tasks, registry, eps.Current, includeRuleConf: true));
    }

    // POST /api/v1/units/{appID}/{task}/{roi}/trigger[?rule=]: phát một lần ngay (FR-SIM-4)
    // 202 khi nhận; 409 khi unit không tồn tại hoặc đang phát (SRS 3.1.10). 🔵 ?rule= và 409 khi mơ hồ là đề xuất, chưa có trong SRS
    private static IResult TriggerUnit(
        IUnitRegistry units,
        string appId,
        string task,
        int roi,
        [FromQuery] string? rule)
    {
        if (units.IsIdentityOff(appId))
        {
            return Results.Conflict(new ErrorDto($"VPU '{appId}' đang Off, không phát được."));
        }

        var result = units.Trigger(appId, task, roi, rule);

        return result.Outcome switch
        {
            UnitTriggerOutcome.Accepted => Results.Accepted(value: ToRef(result.Matches[0])),
            UnitTriggerOutcome.Busy => Results.Conflict(
                new ErrorDto($"Unit {result.Matches[0]} đang phát, thử lại sau.")),
            UnitTriggerOutcome.Ambiguous => Results.Conflict(new ErrorDto(
                $"Nhiều rule dùng roi {roi}; thêm ?rule=<tên rule>. Các rule: {string.Join(", ", result.Matches.Select(m => m.RuleName))}.")),
            _ => Results.Conflict(new ErrorDto("Không có unit khớp (app_id, task, roi, rule).")),
        };
    }

    // POST /api/v1/units/{appID}/{task}/{roi}/auto[?rule=] body {"auto": true|false}: bật/tắt phát tự động (FR-SIM-3)
    // 200; 404. 🔵 ?rule= và 409 khi mơ hồ là đề xuất, chưa có trong SRS. Trả giá trị HIỆU LỰC của auto
    private static IResult SetUnitAuto(
        IUnitRegistry units,
        string appId,
        string task,
        int roi,
        [FromQuery] string? rule,
        SetAutoRequest? body)
    {
        if (body?.Auto is null)
        {
            return Results.BadRequest(new ErrorDto("Thiếu trường bắt buộc: auto (true hoặc false)."));
        }

        var result = units.SetAuto(appId, task, roi, body.Auto.Value, rule);

        return result.Outcome switch
        {
            UnitAutoOutcome.Applied => Results.Ok(new UnitAutoDto(
                result.Matches[0].AppId, result.Matches[0].TaskId, result.Matches[0].RoiId, result.Matches[0].RuleName,
                result.Auto)),
            UnitAutoOutcome.Ambiguous => Results.Conflict(new ErrorDto(
                $"Nhiều rule dùng roi {roi}; thêm ?rule=<tên rule>. Các rule: {string.Join(", ", result.Matches.Select(m => m.RuleName))}.")),
            _ => Results.NotFound(new ErrorDto("Không có unit khớp (app_id, task, roi, rule).")),
        };
    }

    // PUT /api/v1/units/{appID}/{task}/{roi}/rule-conf[?rule=] body: nguyên object rule_conf của rule đó
    // Cập nhật tại chỗ qua đường của update-task: unit giữ trạng thái chạy, VMS gửi lại add-task thì ghi đè (FR-CMD-13)
    // 204; 400 body sai (id/name không khớp, sai kiểu); 404; 409 khi nhiều rule cùng roi mà thiếu ?rule=
    private static IResult PutRuleConf(
        ITaskStore tasks,
        ILoggerFactory loggerFactory,
        IRuleHandlerRegistry registry,
        string appId,
        string task,
        int roi,
        [FromQuery] string? rule,
        [FromBody] JsonElement body)
    {
        var matches = tasks.Get(appId, task)?.Rules
            .Where(r => r.RoiId == roi && (rule is null || string.Equals(r.Name, rule, StringComparison.Ordinal)))
            .ToList() ?? [];

        if (matches.Count == 0)
        {
            return Results.NotFound(new ErrorDto("Không có unit khớp (app_id, task, roi, rule)."));
        }

        if (matches.Count > 1)
        {
            return Results.Conflict(new ErrorDto(
                $"Nhiều rule dùng roi {roi}; thêm ?rule=<tên rule>. Các rule: {string.Join(", ", matches.Select(m => m.Name))}."));
        }

        var result = RuleConfEditor.Replace(tasks, appId, task, roi, matches[0].Name, body,
            isSupportedRule: (name, code) => registry.Resolve(
                name, code is { } c && Enum.IsDefined(typeof(AiFlowCode), c) ? (AiFlowCode)c : null) is not null);

        switch (result.Outcome)
        {
            case RuleConfReplaceOutcome.Replaced:
                loggerFactory.CreateLogger("ControlApi").LogInformation(
                    "Control API: PUT rule-conf của unit {AppId}/{Task}/{Roi}/{Rule}", appId, task, roi,
                    matches[0].Name);
                return Results.NoContent();
            case RuleConfReplaceOutcome.Rejected:
                return Results.BadRequest(new ErrorDto(result.Error ?? "rule_conf không hợp lệ."));
            default:
                return Results.NotFound(new ErrorDto("Không có unit khớp (app_id, task, roi, rule)."));
        }
    }

    private static UnitDto ToUnitDto(
        UnitSnapshot s, IUnitRegistry units, ITaskStore tasks, IRuleHandlerRegistry registry, EpsReading eps,
        bool includeRuleConf = false)
    {
        var task = tasks.Get(s.Key.AppId, s.Key.TaskId);
        var rule = task?.Rules
            .FirstOrDefault(r =>
                r.RoiId == s.Key.RoiId && string.Equals(r.Name, s.Key.RuleName, StringComparison.Ordinal));

        // reset_timer_sec chỉ có nghĩa với rule density (FR-SIM-13: bỏ qua profile).
        double? resetTimer = null;
        if (rule is not null)
        {
            AiFlowCode? code = rule.AiFlowCode is { } c && Enum.IsDefined(typeof(AiFlowCode), c)
                ? (AiFlowCode)c
                : null;
            if (registry.Resolve(rule.Name, code)?.EmittedGroup == 1
                && DensityPeriod.ReadResetTimer(rule.Raw) is { } seconds && seconds > 0)
            {
                resetTimer = seconds;
            }
        }

        return new UnitDto(
            s.Key.AppId,
            s.Key.TaskId,
            s.Key.RoiId,
            s.Key.RuleName,
            s.Auto,
            s.IsEmitting,
            s.Epoch,
            s.NextDueAt,
            s.LastEmitAt,
            s.Emitted,
            s.NoMessage,
            s.Precondition,
            s.Errors,
            s.TicksDropped,
            s.TicksDeferredByMinGap,
            s.TriggersRejected,
            s.Profile,
            rule?.Type,
            rule?.AiFlowCode,
            resetTimer,
            eps.ForUnit(s.Key),
            LastResult(s),
            UnitOffReason(s, task, rule, units.IsTaskManuallyOff(s.Key.AppId, s.Key.TaskId),
                units.IsIdentityOff(s.Key.AppId)),
            includeRuleConf && rule is not null ? rule.Raw : null);
    }

    private static UnitRefDto ToRef(UnitKey k) => new(k.AppId, k.TaskId, k.RoiId, k.RuleName);

    // Giá trị theo contract (UnitResult). null = unit chưa chạy lần nào. "off" khi auto đang tắt
    private static string? LastResult(UnitSnapshot s) => !s.Auto
        ? "off"
        : s.LastOutcome switch
        {
            RuleOutcome.Emitted => "emitted",
            RuleOutcome.NoMessage => "no_message",
            RuleOutcome.Precondition => "precondition",
            RuleOutcome.Error => "error",
            _ => null,
        };

    // Contract mục 3: ưu tiên vps_off (task cha tắt), sau đó lý do riêng của unit (vms hoặc manual)
    // vpu_off sẽ thêm khi chốt ngữ nghĩa "VPU Off". Unit đang bật thì không có lý do
    private static string? UnitOffReason(UnitSnapshot s, ParsedTask? task, ParsedRule? rule, bool taskManuallyOff,
        bool identityOff)
    {
        if (s.Auto)
        {
            return null;
        }

        if (identityOff)
        {
            return "vpu_off";
        }

        if (taskManuallyOff || task?.Enable == false)
        {
            return "vps_off";
        }

        return rule?.Enable == false ? "vms" : "manual";
    }
}

