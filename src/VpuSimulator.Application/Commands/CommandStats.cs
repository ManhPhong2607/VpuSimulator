using System.Collections.Concurrent;

namespace VpuSimulator.Application.Commands;

/// <summary>Tên các bộ đếm xử lý lệnh (SRS FR-CMD-3, 9, 10, 11, 12). Control API và /metrics đọc qua CommandStats.Snapshot</summary>
public static class CommandStatNames
{
    public const string Malformed = "commands_malformed";           //Lệnh sai định dạng (FR-CMD-9)
    public const string Unknown = "commands_unknown";               //Lệnh không xác định (FR-CMD-10)
    public const string Failed = "commands_failed";                 //Handler ném exception ngoài dự kiến
    public const string TasksFailed = "tasks_failed";               //Task bị từ chối và nhận status failed
    public const string TasksSkippedNoId = "tasks_skipped_no_id";   //Phần tử task thiếu task id nên bị bỏ qua 
    public const string UpdatesIgnoredNotRunning = "updates_ignored_not_running";   //cntrl-task / update-task cho task không đang chạy 
    public const string ControlWordUnsupported = "control_words_unsupported";       //cntrl-task có control-word khác reload
    public static string RuleUnsupported(string ruleName) => $"rules_unsupported:{ruleName}";   //Rule bị bỏ vì tên chưa hỗ trợ, đếm riêng theo tên
}

/// <summary>Bộ đếm trong bộ nhớ, an toàn khi gọi từ nhiều luồng.</summary>
public sealed class CommandStats
{
    private readonly ConcurrentDictionary<string, long> _counts = new(StringComparer.Ordinal);

    public void Increment(string name, long by = 1) =>
        _counts.AddOrUpdate(name, by, (_, current) => current + by);

    public long Get(string name) => _counts.TryGetValue(name, out var value) ? value : 0;

    public IReadOnlyDictionary<string, long> Snapshot() => new Dictionary<string, long>(_counts);
}
