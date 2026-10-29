using Microsoft.AspNetCore.Mvc;
using VpuSimulator.Api.Hosting;
using VpuSimulator.Application.Commands;
using VpuSimulator.Application.Identity;
using VpuSimulator.Application.Scheduling;
using VpuSimulator.Application.State;

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

        api.MapDelete("/tasks", StopAllTasks);
        api.MapPost("/units/{appId}/{task}/{roi}/trigger", TriggerUnit);
        
        return app;
    }

    private static StatusDto GetStatus(
        IdentityLifecycleService identities,
        IUnitRegistry units,
        CommandStats stats,
        TimeProvider time,
        AppClock clock)
    {
        var statuses = identities.GetStatuses();
        var snapshots = units.Snapshots();
        

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
                .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal));
    }

    private static IReadOnlyList<IdentityDto> GetIdentities(
        IdentityLifecycleService identities,
        ITaskStore tasks,
        IUnitRegistry units)
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
                unitCounts.GetValueOrDefault(s.AppId)))
            .ToList();
    }

    private static IReadOnlyList<TaskDto> GetTasks(
        IdentityLifecycleService identities,
        ITaskStore tasks,
        [FromQuery(Name = "app_id")] string? appId)
    {
        var appIds = appId is null
            ? identities.AppIds.OrderBy(a => a, StringComparer.Ordinal).ToList()
            : [appId];

        return appIds
            .SelectMany(id => tasks.List(id).Select(t => ToTaskDto(id, t)))
            .ToList();
    }

    private static IReadOnlyList<UnitDto> GetUnits(
        IUnitRegistry units,
        [FromQuery(Name = "app_id")] string? appId) =>
        units.Snapshots(appId).Select(ToUnitDto).ToList();
    
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
            var removed = tasks.Clear(appId);   // kho tự báo UnitRegistry gỡ các unit (giống stop-all của VMS)
            if (removed.Count > 0)
            {
                affected++;
                stopped += removed.Count;
            }
        }

        loggerFactory.CreateLogger("ControlApi")
            .LogInformation("Control API: DELETE /tasks dừng {Tasks} task của {Identities} identity", stopped, affected);

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
            UnitTriggerOutcome.Busy => Results.Conflict(new ErrorDto($"Unit {result.Matches[0]} đang phát, thử lại sau.")),
            UnitTriggerOutcome.Ambiguous => Results.Conflict(new ErrorDto(
                $"Nhiều rule dùng roi {roi}; thêm ?rule=<tên rule>. Các rule: {string.Join(", ", result.Matches.Select(m => m.RuleName))}.")),
            _ => Results.Conflict(new ErrorDto("Không có unit khớp (app_id, task, roi, rule).")),
        };
    }
    
    // ---------------------------------------------------------------- ánh xạ

    private static TaskDto ToTaskDto(string appId, ParsedTask t) => new(
        appId,
        t.TaskId,
        t.StreamUuid,
        t.Enable,
        t.TaskType,
        t.Mode,
        t.Rules.Select(r => new RuleDto(r.RoiId, r.Name, r.Type, r.AiFlowCode, r.Enable)).ToList(),
        t.SkippedRules.Count,
        t.Warnings.Count);

    private static UnitDto ToUnitDto(UnitSnapshot s) => new(
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
        s.TriggersRejected);
    
    private static UnitRefDto ToRef(UnitKey k) => new(k.AppId, k.TaskId, k.RoiId, k.RuleName);
}