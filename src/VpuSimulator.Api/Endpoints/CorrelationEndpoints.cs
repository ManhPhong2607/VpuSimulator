using Microsoft.AspNetCore.Mvc;
using VpuSimulator.Application.Identity;
using VpuSimulator.Domain.FollowUp;
using VpuSimulator.Infrastructure.Persistence;

namespace VpuSimulator.Api.Endpoints;

public static partial class ControlApiEndpoints
{
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
}

