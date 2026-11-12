using Microsoft.AspNetCore.Mvc;
using VpuSimulator.Api.Hosting;
using VpuSimulator.Application.Commands;
using VpuSimulator.Application.Identity;
using VpuSimulator.Application.Interfaces.Simulation;
using VpuSimulator.Application.Options;
using VpuSimulator.Application.Scheduling;
using VpuSimulator.Application.State;
using VpuSimulator.Domain;
using VpuSimulator.Domain.Enums;
using VpuSimulator.Rules;

namespace VpuSimulator.Api.Endpoints;

/// <summary>Các endpoint ĐỌC của Control API (SRS 3.1.10). Chỉ đọc trạng thái, không thay đổi gì.</summary>
public static class ControlApiEndpoints
{
    public static IEndpointRouteBuilder MapControlApiEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api/v1").WithTags("Control API");

        api.MapGet("/status", GetStatus);
        api.MapGet("/identities", GetIdentities);
        api.MapGet("/tasks", GetTasks);
        api.MapGet("/units", GetUnits);
        api.MapGet("/units/{appId}/{task}/{roi}", GetUnit);
        api.MapGet("/profiles", GetProfiles);
        api.MapGet("/rules", GetRules);
        
        api.MapDelete("/tasks", StopAllTasks);
        api.MapPost("/units/{appId}/{task}/{roi}/trigger", TriggerUnit);
        api.MapPost("/units/{appId}/{task}/{roi}/auto", SetUnitAuto);
        api.MapPost("/profiles/apply", ApplyProfile);
        api.MapPost("/identities", AddIdentity);
        api.MapDelete("/identities/{appId}", RemoveIdentity);
        return app;
    }

    private static StatusDto GetStatus(
        IdentityLifecycleService identities,
        IUnitRegistry units,
        CommandStats stats,
        TimeProvider time,
        AppClock clock,
            EpsMeter eps)
    {
        var statuses = identities.GetStatuses();
        var snapshots = units.Snapshots();
        var reading = eps.Current;

        return new StatusDto(
            Identities: statuses.Count,
            IdentitiesConnected: statuses.Count(s => s.IsConnected),
            Units: snapshots.Count,
            UnitsAuto: snapshots.Count(s => s.Auto),
            Seed: units.Seed.ToString(System.Globalization.CultureInfo.InvariantCulture),
            UptimeSeconds: clock.UptimeSeconds(time),
            EmittedTotal: snapshots.Sum(s => s.Emitted),
            NoMessageTotal: snapshots.Sum(s => s.NoMessage),
            PreconditionTotal: snapshots.Sum(s => s.Precondition),
            ErrorsTotal: snapshots.Sum(s => s.Errors),
            Commands: stats.Snapshot()
                .OrderBy(kv => kv.Key, StringComparer.Ordinal)
                .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal),
            EventsTotal: snapshots.Sum(s => s.Emitted),
            EpsActual: reading.Total,
            EpsActualByIdentity: reading.ByIdentity,
            EpsTarget: reading.Target);
    }

    private static IReadOnlyList<IdentityDto> GetIdentities(
        IdentityLifecycleService identities,
        ITaskStore tasks,
        IUnitRegistry units,
        EpsMeter eps)
    {
        var unitCounts = units.Snapshots()
            .GroupBy(s => s.Key.AppId, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

        return identities.GetStatuses()
            .Select(s => new IdentityDto(
                s.AppId,
                s.IsConnected,
                s.HeartbeatFailures,
                s.LastHeartbeatAt,
                tasks.List(s.AppId).Count,
                unitCounts.GetValueOrDefault(s.AppId),
                units.IdentityProfileName(s.AppId),
                eps.Current.ByIdentity.GetValueOrDefault(s.AppId)))
            .ToList();
    }

    private static IReadOnlyList<TaskDto> GetTasks(
        IdentityLifecycleService identities,
        ITaskStore tasks,
        [FromQuery(Name = "app_id")] string? appId,
        EpsMeter eps)
    {
        var appIds = appId is null
            ? identities.AppIds.OrderBy(a => a, StringComparer.Ordinal).ToList()
            : [appId];

        return appIds
            .SelectMany(id => tasks.List(id).Select(t => ToTaskDto(id, t, eps.Current)))
            .ToList();
    }
    
    private static IReadOnlyList<UnitDto> GetUnits(
        IUnitRegistry units,
        ITaskStore tasks,
        IRuleHandlerRegistry registry,
        [FromQuery(Name = "app_id")] string? appId,
        EpsMeter eps) =>
        units.Snapshots(appId).Select(s => ToUnitDto(s, tasks, registry, eps.Current)).ToList();

    // GET /api/v1/units/{appID}/{task}/{roi}[?rule=]: chi tiết một unit kèm rule_conf gốc của VMS.
    // 404 khi không có; 409 khi nhiều rule dùng chung roi mà thiếu ?rule= (mục 10.1 của contract).
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

        return Results.Ok(ToUnitDto(matches[0], tasks, registry, eps.Current, includeRuleConf: true));
    }

    // DELETE /api/v1/tasks?confirm=true: dừng mọi task của mọi identity, identity vẫn kết nối (SRS 3.1.10).
    private static IResult StopAllTasks(
        IdentityLifecycleService identities,
        ITaskStore tasks,
        ILoggerFactory loggerFactory,
        [FromQuery] bool confirm = false)
    {
        if (!confirm)
        {
            return Results.BadRequest(new ErrorDto("Thao tác này dừng mọi task; cần thêm tham số confirm=true."));
        }

        var affected = 0;
        var stopped = 0;
        foreach (var appId in identities.AppIds.OrderBy(a => a, StringComparer.Ordinal))
        {
            var removed = tasks.Clear(appId); // kho tự báo UnitRegistry gỡ các unit (giống stop-all của VMS)
            if (removed.Count > 0)
            {
                affected++;
                stopped += removed.Count;
            }
        }

        loggerFactory.CreateLogger("ControlApi")
            .LogInformation("Control API: DELETE /tasks dừng {Tasks} task của {Identities} identity", stopped,
                affected);

        return Results.Ok(new StopTasksResultDto(affected, stopped));
    }

    // POST /api/v1/units/{appID}/{task}/{roi}/trigger[?rule=]: phát một lần ngay (FR-SIM-4).
    // 202 khi nhận; 409 khi unit không tồn tại hoặc đang phát (SRS 3.1.10). 🔵 ?rule= và 409 khi mơ hồ là đề xuất, chưa có trong SRS.
    private static IResult TriggerUnit(
        IUnitRegistry units,
        string appId,
        string task,
        int roi,
        [FromQuery] string? rule)
    {
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

    // POST /api/v1/units/{appID}/{task}/{roi}/auto[?rule=] body {"auto": true|false}: bật/tắt phát tự động (FR-SIM-3).
    // 200; 404. 🔵 ?rule= và 409 khi mơ hồ là đề xuất, chưa có trong SRS. Trả giá trị HIỆU LỰC của auto.
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

    // POST /api/v1/profiles/apply body {"profile", "app_id"?, "task"?}: đổi profile theo phạm vi (FR-SIM-9).
    // 200 {units_updated}; 400. W-10: chỉ đổi unit đang tồn tại.
    private static IResult ApplyProfile(
        IUnitRegistry units,
        SimulationSettings settings,
        ILoggerFactory loggerFactory,
        ApplyProfileRequest? body)
    {
        if (body is null || string.IsNullOrWhiteSpace(body.Profile))
        {
            return Results.BadRequest(new ErrorDto("Thiếu trường bắt buộc: profile."));
        }

        if (!settings.Profiles.TryGetValue(body.Profile, out var profile))
        {
            return Results.BadRequest(new ErrorDto(
                $"Profile '{body.Profile}' không tồn tại. Hiện có: {string.Join(", ", settings.Profiles.Keys)}."));
        }

        if (body.Task is not null && body.AppId is null)
        {
            return Results.BadRequest(new ErrorDto("task cần đi kèm app_id."));
        }

        var updated = units.ApplyProfile(profile, body.AppId, body.Task);

        loggerFactory.CreateLogger("ControlApi").LogInformation(
            "Control API: áp profile {Profile} cho {Units} unit (app_id={AppId}, task={Task})",
            profile.Name, updated, body.AppId ?? "*", body.Task ?? "*");

        return Results.Ok(new ApplyProfileResultDto(updated));
    }
    
    // POST /api/v1/identities body {"app_id", "profile"?, "heartbeat"?}: thêm identity lúc runtime (FR-ID-5).
    // 201; 400 sai dữ liệu; 409 trùng app_id; 502 không tạo được session broker (SRS 3.1.10).
    private static async Task<IResult> AddIdentity(
        IdentityLifecycleService identities,
        IUnitRegistry units,
        SimulationSettings settings,
        ILoggerFactory loggerFactory,
        CreateIdentityRequest? body,
        CancellationToken cancellationToken)
    {
        if (body is null || string.IsNullOrWhiteSpace(body.AppId))
        {
            return Results.BadRequest(new ErrorDto("Thiếu trường bắt buộc: app_id."));
        }

        var options = new IdentityOptions { AppId = body.AppId, Heartbeat = body.Heartbeat, Profile = body.Profile };
        var result = await identities.AddAsync(options, cancellationToken);

        switch (result)
        {
            case IdentityOpResult.Added:
                loggerFactory.CreateLogger("ControlApi")
                    .LogInformation("Control API: POST /identities thêm identity {AppId}", body.AppId);
                var status = identities.GetStatuses().FirstOrDefault(s => s.AppId == body.AppId);
                var dto = new IdentityDto(
                    body.AppId, status?.IsConnected ?? false, status?.HeartbeatFailures ?? 0,
                    status?.LastHeartbeatAt, 0, 0, units.IdentityProfileName(body.AppId), 0);
                return Results.Created($"/api/v1/identities/{body.AppId}", dto);
            case IdentityOpResult.Duplicate:
                return Results.Conflict(new ErrorDto($"app_id '{body.AppId}' đã tồn tại."));
            case IdentityOpResult.InvalidAppId:
                return Results.BadRequest(new ErrorDto(
                    $"app_id không hợp lệ: tối đa {AppIdRules.MaxLength} ký tự, chỉ gồm chữ, số, '-' và '_'."));
            case IdentityOpResult.InvalidHeartbeat:
                return Results.BadRequest(new ErrorDto(
                    $"heartbeat '{body.Heartbeat}' không hợp lệ, ví dụ 500ms, 30s, 5m."));
            case IdentityOpResult.UnknownProfile:
                return Results.BadRequest(new ErrorDto(
                    $"Profile '{body.Profile}' không tồn tại. Hiện có: {string.Join(", ", settings.Profiles.Keys)}."));
            case IdentityOpResult.UnknownBroker:
                return Results.BadRequest(new ErrorDto("Target broker của identity không tồn tại."));
            default:
                return Results.Json(
                    new ErrorDto("Không tạo được session broker cho identity, xem log để biết lý do."),
                    statusCode: StatusCodes.Status502BadGateway);
        }
    }

    // DELETE /api/v1/identities/{appID}?confirm=true: gỡ identity (FR-ID-6). 204; 400 thiếu confirm; 404.
    private static async Task<IResult> RemoveIdentity(
        IdentityLifecycleService identities,
        ILoggerFactory loggerFactory,
        string appId,
        [FromQuery] bool confirm = false)
    {
        if (!confirm)
        {
            return Results.BadRequest(new ErrorDto(
                "Thao tác này gỡ identity cùng mọi task và unit của nó; cần thêm tham số confirm=true."));
        }

        // Không truyền token của request: client ngắt kết nối giữa chừng không được làm dở việc đóng session.
        if (!await identities.RemoveAsync(appId))
        {
            return Results.NotFound(new ErrorDto($"Không có identity '{appId}'."));
        }

        loggerFactory.CreateLogger("ControlApi")
            .LogInformation("Control API: DELETE /identities gỡ identity {AppId}", appId);
        return Results.NoContent();
    }
    
    // GET /api/v1/profiles: mọi profile (có sẵn + khai báo thêm), đơn vị giây.
    private static IReadOnlyList<ProfileDto> GetProfiles(SimulationSettings settings) =>
        settings.Profiles.Values
            .OrderBy(p => p.Name, StringComparer.Ordinal)
            .Select(p => new ProfileDto(
                p.Name,
                SimulationProfiles.Defaults.ContainsKey(p.Name),
                string.Equals(p.Name, settings.DefaultProfile.Name, StringComparison.Ordinal),
                p.Interval.TotalSeconds,
                p.JitterMin.TotalSeconds,
                p.JitterMax.TotalSeconds,
                p.MinGap.TotalSeconds,
                p.BypassFrames,
                p.CoalesceTimestamps,
                p.RampFrom,
                p.RampTo,
                p.RampDuration?.TotalSeconds))
            .ToList();

    private static readonly IReadOnlyList<RuleCatalogItemDto> RuleItems = BuildRuleItems();

    private static IReadOnlyList<RuleCatalogItemDto> BuildRuleItems()
    {
        // Nhãn nhóm lấy từ chú thích của RuleDefinition.EmittedGroup.
        static string? GroupName(int? g) => g switch
        {
            1 => "density", 2 => "violation", 3 => "recognition", 4 => "abnormal", _ => null,
        };

        return RuleCatalog.All
            .Select(d => new RuleCatalogItemDto(
                d.Name,
                GroupName(d.EmittedGroup),
                d.EmittedGroup,
                d.Handler.AiFlowCodes?.Select(c => (int)c).Order().ToList(),
                "registered"))
            .OrderBy(r => r.Name, StringComparer.Ordinal)
            .ToList();
    }

    // GET /api/v1/rules
    private static IReadOnlyList<RuleCatalogItemDto> GetRules() => RuleItems;

    // ---------------------------------------------------------------- ánh xạ

    private static TaskDto ToTaskDto(string appId, ParsedTask t, EpsReading eps) => new(
        appId,
        t.TaskId,
        t.StreamUuid,
        t.Enable,
        t.TaskType,
        t.Mode,
        t.Rules.Select(r => new RuleDto(r.RoiId, r.Name, r.Type, r.AiFlowCode, r.Enable)).ToList(),
        t.SkippedRules.Count,
        t.Warnings.Count,
        t.SourceInfo is null ? null : new SourceInfoDto(t.SourceInfo.Name, t.SourceInfo.Address, t.SourceInfo.Location),
        t.Url,
        t.Capability is null ? null : new CapabilityDto(t.Capability.Width, t.Capability.Height, t.Capability.Fps),
        eps.ForTask(appId, t.TaskId));

    private static UnitDto ToUnitDto(
        UnitSnapshot s, ITaskStore tasks, IRuleHandlerRegistry registry, EpsReading eps, bool includeRuleConf = false)
    {
        var rule = tasks.Get(s.Key.AppId, s.Key.TaskId)?.Rules
            .FirstOrDefault(r => r.RoiId == s.Key.RoiId && string.Equals(r.Name, s.Key.RuleName, StringComparison.Ordinal));

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
            includeRuleConf && rule is not null ? rule.Raw : null);
    }

    private static UnitRefDto ToRef(UnitKey k) => new(k.AppId, k.TaskId, k.RoiId, k.RuleName);
    
    // Giá trị theo contract (UnitResult). null = unit chưa chạy lần nào. "off" khi auto đang tắt.
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
}