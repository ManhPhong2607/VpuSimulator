using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Serilog.Core;
using Serilog.Events;
using VpuSimulator.Api.Hosting;
using VpuSimulator.Api.Logging;
using VpuSimulator.Application.Commands;
using VpuSimulator.Application.Identity;
using VpuSimulator.Application.Interfaces.Simulation;
using VpuSimulator.Application.Logging;
using VpuSimulator.Application.Options;
using VpuSimulator.Application.Scenario;
using VpuSimulator.Application.Scheduling;
using VpuSimulator.Application.State;
using VpuSimulator.Domain;
using VpuSimulator.Domain.Enums;
using VpuSimulator.Domain.FollowUp;
using VpuSimulator.Infrastructure.Persistence;
using VpuSimulator.Rules;

namespace VpuSimulator.Api.Endpoints;

//Các endpoint ĐỌC của Control API (SRS 3.1.10). Chỉ đọc trạng thái, không thay đổi gì
public static class ControlApiEndpoints
{
    public static IEndpointRouteBuilder MapControlApiEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api/v1").WithTags("Control API");

        api.MapGet("/status", GetStatus);
        api.MapGet("/identities", GetIdentities);
        api.MapPost("/identities", AddIdentity);
        api.MapDelete("/identities/{appId}", RemoveIdentity);
        api.MapPatch("/identities/{appId}", PatchIdentity);

        api.MapGet("/rules", GetRules);
        api.MapPut("/units/{appId}/{task}/{roi}/rule-conf", PutRuleConf);

        api.MapGet("/scenario", GetScenario);
        api.MapPost("/scenario", PostScenario);
        api.MapDelete("/scenario", DeleteScenario);

        api.MapGet("/tasks", GetTasks);
        api.MapGet("/tasks/{appId}/{task}", GetTask);
        api.MapDelete("/tasks", StopAllTasks);
        api.MapPatch("/tasks/{appId}/{task}", PatchTask);
        api.MapDelete("/tasks/{appId}/{task}", RemoveTask);

        api.MapGet("/units", GetUnits);
        api.MapGet("/units/{appId}/{task}/{roi}", GetUnit);
        api.MapPost("/units/{appId}/{task}/{roi}/trigger", TriggerUnit);
        api.MapPost("/units/{appId}/{task}/{roi}/auto", SetUnitAuto);

        api.MapGet("/profiles", GetProfiles);
        api.MapPost("/profiles/apply", ApplyProfile);

        api.MapGet("/correlation", GetCorrelation);
        api.MapDelete("/correlation", ClearCorrelation);
        api.MapDelete("/correlation/{appId}/{key}", DeleteCorrelationEntry);

        api.MapGet("/logs", GetLogs);
        api.MapPost("/logs/level", SetLogLevel);
        api.MapPost("/logs/verbose", SetLogVerbose);
        api.MapGet("/config", GetConfig);

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

    //disabled nếu enabled=false; waiting_vms khi chưa có VPS; heartbeat_off khi heartbeat tắt; còn lại online
    private static string IdentityStatusName(bool enabled, int tasks, bool heartbeatEnabled) =>
        !enabled ? "disabled"
        : tasks == 0 ? "waiting_vms"
        : !heartbeatEnabled ? "heartbeat_off"
        : "online";

    private static IdentityDto ToIdentityDto(IdentityStatus s, int tasks, int units, string profile, double eps) =>
        new(s.AppId, s.IsConnected, s.HeartbeatFailures, s.LastHeartbeatAt, tasks, units, profile, eps,
            HeartbeatDto.From(s.HeartbeatEnabled, s.HeartbeatInterval, s.LastHeartbeatAt),
            s.Enabled,
            IdentityStatusName(s.Enabled, tasks, s.HeartbeatEnabled));

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
            .Select(s => ToIdentityDto(
                s,
                tasks.List(s.AppId).Count,
                unitCounts.GetValueOrDefault(s.AppId),
                units.IdentityProfileName(s.AppId),
                eps.Current.ByIdentity.GetValueOrDefault(s.AppId)))
            .ToList();
    }

    private static IReadOnlyList<TaskDto> GetTasks(
        IdentityLifecycleService identities,
        ITaskStore tasks,
        IUnitRegistry units,
        [FromQuery(Name = "app_id")] string? appId,
        EpsMeter eps)
    {
        var appIds = appId is null
            ? identities.AppIds.OrderBy(a => a, StringComparer.Ordinal).ToList()
            : [appId];

        return appIds
            .SelectMany(id => tasks.List(id).Select(t => ToTaskDto(id, t, units, eps.Current)))
            .ToList();
    }

    // GET /api/v1/tasks/{appID}/{task}: một task. 404 khi không có
    private static IResult GetTask(
        ITaskStore tasks,
        IUnitRegistry units,
        EpsMeter eps,
        string appId,
        string task)
    {
        var found = tasks.Get(appId, task);
        return found is null
            ? Results.NotFound(new ErrorDto($"Không có task '{task}' của identity '{appId}'."))
            : Results.Ok(ToTaskDto(appId, found, units, eps.Current));
    }

    // PATCH /api/v1/tasks/{appID}/{task} body {"enabled": true|false}: bật/tắt tay cả task (VPS On/Off)
    // Cờ lưu riêng, không ghi đè `enable` của VMS. 200 trả task sau khi đổi; 400; 404
    private static IResult PatchTask(
        ITaskStore tasks,
        IUnitRegistry units,
        EpsMeter eps,
        ILoggerFactory loggerFactory,
        string appId,
        string task,
        PatchTaskRequest? body)
    {
        if (body?.Enabled is null)
        {
            return Results.BadRequest(new ErrorDto("Thiếu trường bắt buộc: enabled (true hoặc false)."));
        }

        if (tasks.Get(appId, task) is null)
        {
            return Results.NotFound(new ErrorDto($"Không có task '{task}' của identity '{appId}'."));
        }

        units.SetTaskManuallyOff(appId, task, off: !body.Enabled.Value);

        // Task có thể bị VMS gỡ ngay giữa hai bước: không để cờ mồ côi ảnh hưởng task cùng id thêm sau
        var after = tasks.Get(appId, task);
        if (after is null)
        {
            units.SetTaskManuallyOff(appId, task, off: false);
            return Results.NotFound(new ErrorDto($"Không có task '{task}' của identity '{appId}'."));
        }

        loggerFactory.CreateLogger("ControlApi").LogInformation(
            "Control API: PATCH /tasks task {Task} của {AppId} enabled={Enabled}", task, appId, body.Enabled.Value);

        return Results.Ok(ToTaskDto(appId, after, units, eps.Current));
    }

    // DELETE /api/v1/tasks/{appID}/{task}?confirm=true: gỡ một task, tương đương end-task của VMS. 204; 400; 404
    private static IResult RemoveTask(
        ITaskStore tasks,
        ILoggerFactory loggerFactory,
        string appId,
        string task,
        [FromQuery] bool confirm = false)
    {
        if (!confirm)
        {
            return Results.BadRequest(new ErrorDto(
                "Thao tác này gỡ task cùng mọi unit của nó (tương đương end-task); cần thêm tham số confirm=true."));
        }

        if (tasks.Remove(appId, new[] { task }).Count == 0)
        {
            return Results.NotFound(new ErrorDto($"Không có task '{task}' của identity '{appId}'."));
        }

        loggerFactory.CreateLogger("ControlApi")
            .LogInformation("Control API: DELETE /tasks gỡ task {Task} của {AppId}", task, appId);
        return Results.NoContent();
    }

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

    // DELETE /api/v1/tasks?confirm=true: dừng mọi task của mọi identity, identity vẫn kết nối (SRS 3.1.10)
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

    // POST /api/v1/profiles/apply body {"profile", "app_id"?, "task"?}: đổi profile theo phạm vi (FR-SIM-9)
    // 200 {units_updated}; 400. W-10: chỉ đổi unit đang tồn tại
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

    // POST /api/v1/identities body {"app_id", "profile"?, "heartbeat"?}: thêm identity lúc runtime (FR-ID-5)
    // 201; 400 sai dữ liệu; 409 trùng app_id; 502 không tạo được session broker
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
                var status = identities.GetStatuses().FirstOrDefault(s => s.AppId == body.AppId)
                             ?? new IdentityStatus(body.AppId, false, 0, null, TimeSpan.FromSeconds(30), true, true);
                var dto = ToIdentityDto(status, 0, 0, units.IdentityProfileName(body.AppId), 0);
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

    // DELETE /api/v1/identities/{appID}?confirm=true: gỡ identity (FR-ID-6). 204; 400 thiếu confirm; 404
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

        // Không truyền token của request: client ngắt kết nối giữa chừng không được làm dở việc đóng session
        if (!await identities.RemoveAsync(appId))
        {
            return Results.NotFound(new ErrorDto($"Không có identity '{appId}'."));
        }

        loggerFactory.CreateLogger("ControlApi")
            .LogInformation("Control API: DELETE /identities gỡ identity {AppId}", appId);
        return Results.NoContent();
    }

    // PATCH /api/v1/identities/{appId} body {"enabled"?, "heartbeat":{"interval"?, "enabled"?}} → 200 + identity
    // profile → 400 (dùng POST /profiles/apply, W-10). display_name → 400 (BE không lưu, FE theo BE).
    private static IResult PatchIdentity(
        IdentityLifecycleService identities,
        ITaskStore tasks,
        IUnitRegistry units,
        EpsMeter eps,
        string appId,
        PatchIdentityRequest? body)
    {
        if (!identities.AppIds.Contains(appId))
        {
            return Results.NotFound(new ErrorDto($"Không có identity '{appId}'."));
        }

        if (body is null)
        {
            return Results.BadRequest(new ErrorDto("Thiếu body."));
        }

        if (body.Profile is not null)
        {
            return Results.BadRequest(new ErrorDto(
                "Không đổi profile qua PATCH identity. Dùng POST /api/v1/profiles/apply (W-10: chỉ đổi unit đang có, " +
                "không đổi profile mặc định của identity)."));
        }

        if (body.DisplayName is not null)
        {
            return Results.BadRequest(new ErrorDto("BE không lưu display_name."));
        }

        var interval = body.Heartbeat?.Interval;
        var heartbeatEnabled = body.Heartbeat?.Enabled;
        if (interval is null && heartbeatEnabled is null && body.Enabled is null)
        {
            return Results.BadRequest(new ErrorDto(
                "Không có trường nào để sửa. Hỗ trợ: enabled, heartbeat.enabled, heartbeat.interval (ví dụ \"30s\")."));
        }

        // Chu kỳ có thể bị từ chối nên làm trước: lỗi thì không áp dụng gì cả.
        if (interval is not null)
        {
            switch (identities.SetHeartbeatInterval(appId, interval))
            {
                case HeartbeatUpdateResult.NotFound:
                    return Results.NotFound(new ErrorDto($"Không có identity '{appId}'."));
                case HeartbeatUpdateResult.Invalid:
                    return Results.BadRequest(new ErrorDto(
                        $"heartbeat.interval '{interval}' không hợp lệ, ví dụ 500ms, 30s, 5m."));
            }
        }

        if (heartbeatEnabled is { } hb)
        {
            identities.SetHeartbeatEnabled(appId, hb);
        }

        if (body.Enabled is { } on)
        {
            identities.SetEnabled(appId, on);
        }

        var status = identities.GetStatuses().FirstOrDefault(s => s.AppId == appId);
        if (status is null)
        {
            return Results.NotFound(new ErrorDto($"Không có identity '{appId}'."));
        }

        return Results.Ok(ToIdentityDto(
            status,
            tasks.List(appId).Count,
            units.Snapshots().Count(s => s.Key.AppId == appId),
            units.IdentityProfileName(appId),
            eps.Current.ByIdentity.GetValueOrDefault(appId)));
    }

    // GET /api/v1/scenario: 204 nếu chưa nạp (contract 9.10)
    private static IResult GetScenario(ScenarioLoader loader)
    {
        var s = loader.Current;
        return s is null
            ? Results.NoContent()
            : Results.Ok(new ScenarioInfoDto(s.VpuCount, s.VpsCount, s.TaskCount, s.RuleCount, s.Seed, s.LoadedAt));
    }

    // POST /api/v1/scenario[?validate_only=true]
    // validate_only: luôn 200 kèm issues. Nạp thật: 400 nếu có lỗi (không áp dụng gì), 201 nếu áp dụng được.
    private static async Task<IResult> PostScenario(
        ScenarioLoader loader,
        IdentityLifecycleService identities,
        IUnitRegistry units,
        SimulationSettings settings,
        IRuleHandlerRegistry registry,
        ILoggerFactory loggerFactory,
        [FromBody] JsonElement body,
        [FromQuery(Name = "validate_only")] bool validateOnly = false,
        CancellationToken cancellationToken = default)
    {
        var plan = ScenarioPlanner.Plan(
            body,
            units.Seed,
            identities.AppIds,
            name => settings.Profiles.ContainsKey(name),
            (name, code) => registry.Resolve(name,
                code is { } c && Enum.IsDefined(typeof(AiFlowCode), c) ? (AiFlowCode)c : null) is not null);

        static IReadOnlyList<ScenarioIssueDto> Dto(IEnumerable<ScenarioIssue> issues) =>
            issues.Select(i => new ScenarioIssueDto(i.Severity, i.Title, i.Detail)).ToList();

        if (validateOnly)
        {
            return Results.Ok(new ScenarioValidationDto(Dto(plan.Issues), plan.OkIdentities, plan.OkTasks));
        }

        if (plan.HasErrors)
        {
            return Results.BadRequest(new ScenarioErrorDto(
                "Scenario không hợp lệ, không áp dụng gì cả.", Dto(plan.Issues)));
        }

        // Không truyền token của request: client ngắt giữa chừng không được làm dở việc áp dụng (W-11)
        var applied = await loader.ApplyAsync(plan, CancellationToken.None);
        var all = plan.Issues.Concat(applied.Issues).ToList();

        loggerFactory.CreateLogger("ControlApi").LogInformation(
            "Control API: POST /scenario thêm {Identities} identity, {Tasks} task, {Rules} rule",
            applied.Identities, applied.Tasks, applied.Rules);

        if (applied.Identities == 0)
        {
            return Results.Conflict(new ScenarioErrorDto("Không thêm được identity nào.", Dto(all)));
        }

        return Results.Created("/api/v1/scenario",
            new ScenarioAppliedDto(applied.Identities, applied.Tasks, applied.Rules, Dto(all)));
    }

    // DELETE /api/v1/scenario?confirm=true: gỡ mọi identity (FR-CTL-8, FR-ID-6)
    private static async Task<IResult> DeleteScenario(
        ScenarioLoader loader,
        ILoggerFactory loggerFactory,
        [FromQuery] bool confirm = false)
    {
        if (!confirm)
        {
            return Results.BadRequest(new ErrorDto(
                "Thao tác này gỡ MỌI identity cùng task và unit của chúng; cần thêm tham số confirm=true."));
        }

        var cleared = await loader.ClearAsync(CancellationToken.None);
        loggerFactory.CreateLogger("ControlApi").LogInformation(
            "Control API: DELETE /scenario gỡ {Identities} identity, {Tasks} task", cleared.Identities, cleared.Tasks);
        return Results.Ok(new ScenarioClearedDto(cleared.Identities, cleared.Tasks));
    }

    // GET /api/v1/profiles: mọi profile (có sẵn + khai báo thêm), đơn vị giây
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
        // Nhãn nhóm lấy từ chú thích của RuleDefinition.EmittedGroup
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

    // GET /api/v1/correlation?app_id=&n=
    // Các mục còn hạn của kho correlation, mới nhất trước (FR-COR-5). n mặc định 100. total là tổng số mục khớp
    // bộ lọc (trước khi cắt còn n mục) để Console biết còn bao nhiêu mục chưa hiển thị
    private static IResult GetCorrelation(
        ICorrelationStore store,
        CorrelationOptions options,
        [FromQuery(Name = "app_id")] string? appId,
        [FromQuery] int n = 100)
    {
        if (n < 1)
        {
            return Results.BadRequest(new ErrorDto("n phải là số nguyên từ 1 trở lên."));
        }

        // List đã loại mục hết hạn và sắp xếp mới nhất trước, nên chỉ cần lấy n mục đầu
        var all = store.List(string.IsNullOrWhiteSpace(appId) ? null : appId);
        var items = all
            .Take(n)
            .Select(e => new CorrelationItemDto(
                e.AppId,
                e.Key, // khoá của mục chính là event_key
                e.Value is LicensePlateCorrelationValue plate ? plate.Label : null,
                e.WrittenAt))
            .ToList();

        return Results.Ok(new CorrelationDto(
            all.Count, DurationFormat.Format(options.Ttl), options.MaxPerIdentity, items));
    }

    // DELETE /api/v1/correlation?confirm=true: xoá toàn bộ kho của mọi identity (FR-COR-5, FR-CTL-5). 204; 400 nếu thiếu confirm
    private static IResult ClearCorrelation(
        ICorrelationStore store,
        ILoggerFactory loggerFactory,
        [FromQuery] bool confirm = false)
    {
        if (!confirm)
        {
            return Results.BadRequest(new ErrorDto(
                "Thao tác này xoá toàn bộ correlation của mọi identity; cần thêm tham số confirm=true."));
        }

        var removed = store.List().Count;
        store.ClearAll();
        loggerFactory.CreateLogger("ControlApi")
            .LogInformation("Control API: DELETE /correlation xoá {Count} mục", removed);
        return Results.NoContent();
    }

    // DELETE /api/v1/correlation/{appId}/{key}?confirm=true: xoá một mục theo cặp (app_id, event_key) (contract 10.1).
    // Dùng chính TryDelete của kho nên có nghĩa như "mục này đã bị tiêu thụ": rule Traffic Signal Violation sẽ không tìm thấy nó nữa.
    // 204; 400 nếu thiếu confirm; 404 nếu không có mục (hoặc đã hết hạn)
    private static IResult DeleteCorrelationEntry(
        ICorrelationStore store,
        ILoggerFactory loggerFactory,
        string appId,
        string key,
        [FromQuery] bool confirm = false)
    {
        if (!confirm)
        {
            return Results.BadRequest(
                new ErrorDto("Thao tác này xoá một mục correlation; cần thêm tham số confirm=true."));
        }

        if (!store.TryDelete(appId, key))
        {
            return Results.NotFound(new ErrorDto($"Không có mục correlation '{key}' của identity '{appId}'."));
        }

        loggerFactory.CreateLogger("ControlApi")
            .LogInformation("Control API: DELETE /correlation/{AppId}/{Key}", appId, key);
        return Results.NoContent();
    }

    // GET /api/v1/logs?n=100: n dòng log gần nhất, mới nhất trước (contract 9.9, FR-OBS-4).
    private static IResult GetLogs(LogBuffer buffer, int n = 100)
    {
        if (n < 1) return Results.BadRequest(new ErrorDto("n phải ≥ 1."));
        return Results.Ok(buffer.Latest(n).Select(LogLineDto.From).ToList());
    }
    
        // POST /api/v1/logs/level: đổi mức log lúc runtime (FR-OBS-2, contract 4.1).
    private static IResult SetLogLevel(
        LoggingLevelSwitch levelSwitch,
        ILoggerFactory loggerFactory,
        SetLogLevelRequest? body)
    {
        if (body is null || string.IsNullOrWhiteSpace(body.Level))
            return Results.BadRequest(new ErrorDto("Thiếu trường bắt buộc: level."));

        if (!LogLevels.TryParse(body.Level, out var target))
            return Results.BadRequest(new ErrorDto(
                $"Mức log '{body.Level}' không hợp lệ. Chấp nhận: debug, info, warn, error."));

        levelSwitch.MinimumLevel = target;
        var canonical = LogLevels.Name(target);
        loggerFactory.CreateLogger("ControlApi")
            .LogInformation("Control API: POST /logs/level đổi mức log sang {Level}", canonical);

        return Results.Ok(new { level = canonical });
    }

    // POST /api/v1/logs/verbose: bật/tắt ghi payload bản tin vào log (FR-OBS-3, contract 4.1).
    private static IResult SetLogVerbose(
        RuntimeLogSettings runtimeLog,
        ILoggerFactory loggerFactory,
        SetLogVerboseRequest? body)
    {
        if (body?.Enabled is null)
            return Results.BadRequest(new ErrorDto("Thiếu trường bắt buộc: enabled."));

        runtimeLog.Verbose = body.Enabled.Value;
        loggerFactory.CreateLogger("ControlApi")
            .LogInformation("Control API: POST /logs/verbose đặt verbose={Verbose}", runtimeLog.Verbose);

        return Results.Ok(new { verbose = runtimeLog.Verbose });
    }

    // GET /api/v1/config: cấu hình hiệu lực, phản ánh thay đổi runtime, che secret khác rỗng bằng "***" (FR-CFG-1, FR-CFG-6).
    private static IResult GetConfig(
        MessagingOptions messaging,
        SimulationSettings simulation,
        IUnitRegistry units,
        CorrelationOptions correlation,
        LoggingLevelSwitch levelSwitch,
        RuntimeLogSettings runtimeLog,
        LogBuffer logBuffer,
        IConfiguration configuration)
    {
        static string? MaskSecret(string? secret) =>
            string.IsNullOrEmpty(secret) ? secret : "***";

        var brokers = messaging.Brokers.ToDictionary(
            kv => kv.Key,
            kv => new
            {
                host = kv.Value.Host,
                port = kv.Value.Port,
                virtual_host = kv.Value.VirtualHost,
                username = kv.Value.Username,
                password = MaskSecret(kv.Value.Password),
                exchange = kv.Value.Exchange,
                prefetch = kv.Value.Prefetch,
                auto_ack = kv.Value.AutoAck,
                reconnect_min = DurationFormat.Format(kv.Value.ReconnectMin),
                reconnect_max = DurationFormat.Format(kv.Value.ReconnectMax),
                publish_timeout = DurationFormat.Format(kv.Value.PublishTimeout),
            },
            StringComparer.OrdinalIgnoreCase);

        return Results.Ok(new
        {
            api = new
            {
                listen = configuration["Api:Listen"] ?? "http://127.0.0.1:18080",
                token = MaskSecret(configuration["Api:Token"]),
            },
            log = new
            {
                level = LogLevels.Name(levelSwitch.MinimumLevel),
                verbose = runtimeLog.Verbose,
                buffer_size = logBuffer.Capacity,
                max_body_log = runtimeLog.MaxBodyLog,
            },
            scheduler = new
            {
                arm_delay = DurationFormat.Format(simulation.ArmDelay),
                default_profile = simulation.DefaultProfile.Name,
            },
            simulation = new
            {
                seed = units.Seed.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ai_backend = "simulated",
            },
            correlation = new
            {
                ttl = DurationFormat.Format(correlation.Ttl),
                max_per_identity = correlation.MaxPerIdentity,
            },
            brokers,
        });
    }

    // GET /api/v1/rules
    private static IReadOnlyList<RuleCatalogItemDto> GetRules() => RuleItems;

    // ---------------------------------------------------------------- ánh xạ

    private static TaskDto ToTaskDto(string appId, ParsedTask t, IUnitRegistry units, EpsReading eps)
    {
        // off_reason của VPS là manual (tắt tay qua API) hoặc VMS gửi enable:false
        var offReason = units.IsIdentityOff(appId) ? "vpu_off"
            : units.IsTaskManuallyOff(appId, t.TaskId) ? "manual"
            : t.Enable == false ? "vms"
            : null;

        return new TaskDto(
            appId,
            t.TaskId,
            t.StreamUuid,
            t.Enable,
            t.TaskType,
            t.Mode,
            t.Rules.Select(r => new RuleDto(r.RoiId, r.Name, r.Type, r.AiFlowCode, r.Enable)).ToList(),
            t.SkippedRules.Count,
            t.Warnings.Count,
            t.SourceInfo is null
                ? null
                : new SourceInfoDto(t.SourceInfo.Name, t.SourceInfo.Address, t.SourceInfo.Location),
            t.Url,
            t.Capability is null ? null : new CapabilityDto(t.Capability.Width, t.Capability.Height, t.Capability.Fps),
            eps.ForTask(appId, t.TaskId),
            offReason is null,
            offReason is null ? "running" : "off",
            offReason);
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