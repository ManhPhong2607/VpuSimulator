using VpuSimulator.Application.Commands;

namespace VpuSimulator.Application.State;

public enum RuleChangeKind
{
    Unchanged,  //Rule không đổi: giữ nguyên unit và trạng thái sinh ngẫu nhiên
    Changed,    //Rule đổi nội dung: giữ unit nhưng nhận cấu hình và nhịp mới
    Added,      //Rule mới: tạo unit mới
    Removed,    //Rule không còn: xoá unit
}

public readonly record struct RuleChange(RuleKey Key, RuleChangeKind Kind);

/// <param name="Created">true nếu task chưa tồn tại trước đó (task mới khởi động).</param>
/// <param name="Current">Task sau khi áp dụng.</param>
/// <param name="RuleChanges">So với task trước đó. Task mới thì mọi rule đều là RuleChangeKind.Added. Scheduler dùng để đồng bộ emission unit.</param>
public sealed record TaskApplyResult(bool Created, ParsedTask Current, IReadOnlyList<RuleChange> RuleChanges);

/// <summary>
/// Kho task đang chạy của mọi identity. Chỉ nằm trong bộ nhớ: khởi động lại thì mất, VMS phải gửi lại add-task (W-3).
/// Mọi thao tác an toàn khi gọi từ nhiều luồng. Task của identity này không bao giờ lẫn sang identity khác (FR-ID-2).
/// </summary>
public interface ITaskStore
{
    /// <summary>add-task: thêm task, hoặc thay định nghĩa nếu task đang chạy (FR-CMD-13).</summary>
    TaskApplyResult Upsert(string appId, ParsedTask task);

    /// <summary>
    /// Sửa task đang chạy một cách nguyên tử (cntrl-task, update-task). Trả về null nếu task không đang chạy,
    /// và khi đó <paramref name="transform"/> không được gọi (FR-CMD-12: không tạo task mới).
    /// </summary>
    TaskApplyResult? Update(string appId, string taskId, Func<ParsedTask, ParsedTask> transform);

    /// <summary>end-task: gỡ các task trong danh sách. Id không tồn tại thì bỏ qua. Trả về các task thực sự bị gỡ.</summary>
    IReadOnlyList<ParsedTask> Remove(string appId, IEnumerable<string> taskIds);

    /// <summary>stop-all: gỡ mọi task của identity. Trả về các task bị gỡ.</summary>
    IReadOnlyList<ParsedTask> Clear(string appId);

    ParsedTask? Get(string appId, string taskId);

    /// <summary>Các task của identity, sắp theo task id.</summary>
    IReadOnlyList<ParsedTask> List(string appId);
}
