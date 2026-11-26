using Microsoft.AspNetCore.Mvc;
using VpuSimulator.Application.Commands;
using VpuSimulator.Application.Identity;
using VpuSimulator.Application.Scheduling;
using VpuSimulator.Application.State;

namespace VpuSimulator.Api.Endpoints;

public static partial class ControlApiEndpoints
{
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
}

