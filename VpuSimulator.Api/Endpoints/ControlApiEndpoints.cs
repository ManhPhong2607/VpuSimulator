using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using VpuSimulator.Application.Commands;
using VpuSimulator.Application.Identity;
using VpuSimulator.Application.Scheduling;
using VpuSimulator.Application.State;

namespace VpuSimulator.Api.Endpoints;

/// <summary>Các endpoint ĐỌC của Control API (SRS 3.1.10). Chỉ đọc trạng thái, không thay đổi gì.</summary>
public static class ControlApiEndpoints
{
    public static IEndpointRouteBuilder MapControlApiReadEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api/v1").WithTags("Control API");

        api.MapGet("/status", GetStatus);
        api.MapGet("/identities", GetIdentities);
        api.MapGet("/tasks", GetTasks);
        api.MapGet("/units", GetUnits);

        return app;
    }

    private static StatusDto GetStatus(
        IdentityLifecycleService identities,
        IUnitRegistry units,
        CommandStats stats,
        TimeProvider time)
    {
        var statuses = identities.GetStatuses();
        var snapshots = units.Snapshots();

        using var process = Process.GetCurrentProcess();
        var startedAt = new DateTimeOffset(process.StartTime.ToUniversalTime());
        var uptime = (long)Math.Max(0, (time.GetUtcNow() - startedAt).TotalSeconds);

        return new StatusDto(
            Identities: statuses.Count,
            IdentitiesConnected: statuses.Count(s => s.IsConnected),
            Units: snapshots.Count,
            UnitsAuto: snapshots.Count(s => s.Auto),
            Seed: units.Seed.ToString(System.Globalization.CultureInfo.InvariantCulture),
            UptimeSeconds: uptime,
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
}