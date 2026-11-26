using Microsoft.AspNetCore.Mvc;
using VpuSimulator.Application.Scheduling;

namespace VpuSimulator.Api.Endpoints;

public static partial class ControlApiEndpoints
{
    // GET /api/v1/profiles: mọi profile (có sẵn + khai báo thêm + tạo lúc runtime), đơn vị giây
    private static IReadOnlyList<ProfileDto> GetProfiles(ProfileCatalog catalog) =>
        catalog.All().Select(p => ToProfileDto(p, catalog)).ToList();

    // POST /api/v1/profiles/apply body {"profile", "app_id"?, "task"?}: đổi profile theo phạm vi (FR-SIM-9)
    // 200 {units_updated}; 400. W-10: chỉ đổi unit đang tồn tại
    private static IResult ApplyProfile(
        IUnitRegistry units,
        ProfileCatalog catalog,
        ILoggerFactory loggerFactory,
        ApplyProfileRequest? body)
    {
        if (body is null || string.IsNullOrWhiteSpace(body.Profile))
        {
            return Results.BadRequest(new ErrorDto("Thiếu trường bắt buộc: profile."));
        }

        if (catalog.Get(body.Profile) is not { } profile)
        {
            return Results.BadRequest(new ErrorDto(
                $"Profile '{body.Profile}' không tồn tại. Hiện có: {string.Join(", ", catalog.Names())}."));
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

    // POST /api/v1/profiles: tạo profile mới (contract 4.5). 201; 400; 409 trùng tên
    private static IResult CreateProfile(
        ProfileService profiles,
        ProfileCatalog catalog,
        ILoggerFactory loggerFactory,
        ProfileRequest? body)
    {
        if (body is null)
        {
            return Results.BadRequest(new ErrorDto("Thiếu body."));
        }

        if (string.IsNullOrWhiteSpace(body.Name))
        {
            return Results.BadRequest(new ErrorDto("Thiếu trường bắt buộc: name."));
        }

        var result = profiles.Create(body.Name, body.ToInput());
        if (result.Outcome != ProfileOutcome.Done)
        {
            return ProfileError(result);
        }

        loggerFactory.CreateLogger("ControlApi").LogInformation(
            "Control API: POST /profiles tạo profile {Profile}", body.Name);
        return Results.Created($"/api/v1/profiles/{body.Name}", ToProfileDto(result.Profile!, catalog));
    }

    // PUT /api/v1/profiles/{name}: ghi đè TOÀN BỘ định nghĩa, các unit đang dùng được cập nhật theo (D2). 200; 400; 404
    private static IResult UpdateProfile(
        string name,
        ProfileService profiles,
        ProfileCatalog catalog,
        ILoggerFactory loggerFactory,
        ProfileRequest? body)
    {
        if (body is null)
        {
            return Results.BadRequest(new ErrorDto("Thiếu body."));
        }

        // Giống PUT rule-conf: tên trong body (nếu có) phải khớp đường dẫn, tránh sửa nhầm profile
        if (body.Name is not null && !string.Equals(body.Name, name, StringComparison.Ordinal))
        {
            return Results.BadRequest(new ErrorDto(
                $"name trong body ('{body.Name}') phải khớp đường dẫn ('{name}')."));
        }

        var result = profiles.Update(name, body.ToInput());
        if (result.Outcome != ProfileOutcome.Done)
        {
            return ProfileError(result);
        }

        loggerFactory.CreateLogger("ControlApi").LogInformation(
            "Control API: PUT /profiles/{Profile} cập nhật, {Units} unit đang dùng được cập nhật theo", name, result.UnitsUpdated);
        return Results.Ok(ToProfileDto(result.Profile!, catalog));
    }

    // DELETE /api/v1/profiles/{name}?confirm=true: 204; 400 thiếu confirm; 404; 409 (profile mặc định, có sẵn, hoặc đang được dùng)
    private static IResult DeleteProfile(
        string name,
        ProfileService profiles,
        ILoggerFactory loggerFactory,
        [FromQuery] bool confirm = false)
    {
        if (!confirm)
        {
            return Results.BadRequest(new ErrorDto("Thao tác này xoá profile; cần thêm tham số confirm=true."));
        }

        var result = profiles.Delete(name);
        if (result.Outcome != ProfileOutcome.Done)
        {
            return ProfileError(result);
        }

        loggerFactory.CreateLogger("ControlApi").LogInformation("Control API: DELETE /profiles/{Profile}", name);
        return Results.NoContent();
    }

    // POST /api/v1/profiles/{name}/reset?confirm=true: khôi phục giá trị gốc (SRS FR-SIM-6). 200; 400; 404
    private static IResult ResetProfile(
        string name,
        ProfileService profiles,
        ProfileCatalog catalog,
        ILoggerFactory loggerFactory,
        [FromQuery] bool confirm = false)
    {
        if (!confirm)
        {
            return Results.BadRequest(new ErrorDto(
                "Thao tác này ghi đè profile bằng giá trị gốc; cần thêm tham số confirm=true."));
        }

        var result = profiles.Reset(name);
        if (result.Outcome != ProfileOutcome.Done)
        {
            return ProfileError(result);
        }

        loggerFactory.CreateLogger("ControlApi").LogInformation(
            "Control API: POST /profiles/{Profile}/reset, {Units} unit đang dùng được cập nhật theo", name, result.UnitsUpdated);
        return Results.Ok(ToProfileDto(result.Profile!, catalog));
    }

    // Đổi kết quả thất bại của ProfileService thành mã HTTP (contract 4.5, 10.1)
    private static IResult ProfileError(ProfileResult result) => result.Outcome switch
    {
        ProfileOutcome.NotFound => Results.NotFound(new ErrorDto(result.Message ?? "Không có profile này.")),
        ProfileOutcome.Duplicate or ProfileOutcome.Protected or ProfileOutcome.InUse =>
            Results.Conflict(new ErrorDto(result.Message ?? "Thao tác xung đột với trạng thái hiện tại.")),
        _ => Results.BadRequest(new ErrorDto(result.Message ?? "Dữ liệu không hợp lệ.")),
    };

    private static ProfileDto ToProfileDto(SimulationProfile p, ProfileCatalog catalog) => new(
        p.Name,
        ProfileCatalog.IsBuiltin(p.Name),
        catalog.IsSystemDefault(p.Name),
        p.Interval.TotalSeconds,
        p.JitterMin.TotalSeconds,
        p.JitterMax.TotalSeconds,
        p.MinGap.TotalSeconds,
        p.BypassFrames,
        p.CoalesceTimestamps,
        p.RampFrom,
        p.RampTo,
        p.RampDuration?.TotalSeconds);
}

