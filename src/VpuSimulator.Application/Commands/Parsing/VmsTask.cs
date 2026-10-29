using System.Text.Json;

namespace VpuSimulator.Application.Commands;

/// <summary>
/// Khóa của một emission unit trong phạm vi một task: roi id, tên rule
/// Cùng một vùng ROI có thể được gán đồng thời nhiều bài toán AI khác nhau, nên cùng id nhưng khác name là hợp lệ.
/// </summary>
public readonly record struct RuleKey(int RoiId, string Name);

/// Một phần tử của rule_conf. Chỉ giữ các trường pipeline cần; phần còn lại nằm trong Raw"</summary>
/// Enable: null = vắng mặt; false = tắt rõ ràng
/// Raw Toàn bộ object rule gốc (bản sao riêng), để handler của rule đọc tham số riêng: reset_timer, activities, region...
public sealed record ParsedRule(
    int RoiId,
    string Name,
    int? Type,
    int? AiFlowCode,
    bool? Enable,
    JsonElement Raw)
{
    public RuleKey Key => new(RoiId, Name);
}

public enum RuleSkipReason
{
    /// <summary>name vắng mặt hoặc rỗng (IF-RULE-1).</summary>
    MissingName,

    /// <summary>Tên rule chưa được hỗ trợ (FR-CMD-11).</summary>
    Unsupported,

    /// <summary>Thiếu id, nên không có roi_id.</summary>
    MissingId,

    /// <summary>Trùng cả (id, name) với rule đứng trước.</summary>
    Duplicate,
}

/// <summary>Một rule bị bỏ, dạng có cấu trúc, để đếm theo tên (FR-CMD-11). Cùng nội dung có trong Warnings dạng chữ.</summary>
public sealed record SkippedRule(int Index, string? Name, RuleSkipReason Reason);

public sealed record VmsSourceInfo(string? Model, string? Name, IReadOnlyList<double>? GpsCoord, string? Location, string? Address);

public sealed record VmsCapability(int? Width, int? Height, int? Fps, int? OutFps);

public sealed record VmsDetector(string? Guid, double? Rate);

/// <summary>
/// Một task đã parse. Mọi trường đều tuỳ chọn trên wire, trừ TaskId
/// null nghĩa là VẮNG MẶT; chuỗi rỗng, số 0 hay false là giá trị có mặt
/// </summary>
/// <param name="TaskId">Trường task: task uuid của VMS, định danh task.</param>
/// <param name="StreamUuid">Trường uuid: stream uuid, thành source_id của sự kiện.</param>
/// <param name="RuleConfPresent">Phân biệt "không có rule_conf" với "rule_conf rỗng": cập nhật từng phần (FR-CMD-8) cần biết.</param>
/// <param name="Rules">Các rule hợp lệ, đã bỏ rule lỗi và rule trùng khóa.</param>
/// <param name="SkippedRules">Các rule bị bỏ kèm lý do.</param>
/// <param name="Warnings">Mọi thứ bị bỏ qua hoặc sai kiểu nhưng không đủ để từ chối task. Dành cho log.</param>
/// <param name="Raw">Toàn bộ object task gốc (bản sao riêng), để trộn khi update-task.</param>
public sealed record ParsedTask(
    string TaskId,
    bool? Enable,
    string? TaskType,
    string? StreamUuid,
    string? Url,
    string? UrlDis,
    string? Mode,
    string? StartTime,
    string? EndTime,
    int? TotalSegment,
    int? CurrentSegment,
    bool? LastSegment,
    VmsSourceInfo? SourceInfo,
    VmsCapability? Capability,
    IReadOnlyList<VmsDetector>? Detectors,
    bool RuleConfPresent,
    IReadOnlyList<ParsedRule> Rules,
    IReadOnlyList<SkippedRule> SkippedRules,
    IReadOnlyList<string> Warnings,
    JsonElement Raw);

/// <summary>Kết quả parse một task: hoặc <see cref="Value"/>, hoặc <see cref="Error"/> (task bị từ chối).</summary>
/// <param name="TaskId">Có thể có ngay cả khi bị từ chối, để phát status failed cấp task. null nếu không đọc được.</param>
public sealed record TaskParseResult(ParsedTask? Value, string? TaskId, string? Error)
{
    public bool IsRejected => Value is null;
}

/// <param name="Tasks">null khi detail.task_list không dùng được; khi đó <see cref="Error"/> cho biết lý do.</param>
/// <param name="DetailAppId">Trường detail.appId, chỉ để log. Không dùng chọn identity (IF-CMD-6).</param>
public sealed record AddTaskParseResult(IReadOnlyList<TaskParseResult>? Tasks, string? DetailAppId, string? Error);
