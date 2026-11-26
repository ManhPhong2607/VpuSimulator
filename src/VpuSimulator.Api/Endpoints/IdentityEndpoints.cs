using Microsoft.AspNetCore.Mvc;
using VpuSimulator.Application.Identity;
using VpuSimulator.Application.Options;
using VpuSimulator.Application.Scheduling;
using VpuSimulator.Application.State;

namespace VpuSimulator.Api.Endpoints;

public static partial class ControlApiEndpoints
{
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

    // POST /api/v1/identities body {"app_id", "profile"?, "heartbeat"?}: thêm identity lúc runtime (FR-ID-5)
    // 201; 400 sai dữ liệu; 409 trùng app_id; 502 không tạo được session broker
    private static async Task<IResult> AddIdentity(
        IdentityLifecycleService identities,
        IUnitRegistry units,
        ProfileCatalog catalog,
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
                    $"Profile '{body.Profile}' không tồn tại. Hiện có: {string.Join(", ", catalog.Names())}."));

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
}

