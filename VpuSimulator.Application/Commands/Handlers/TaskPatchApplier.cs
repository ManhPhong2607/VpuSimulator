using System.Text.Json;
using Microsoft.Extensions.Logging;
using VpuSimulator.Application.State;

namespace VpuSimulator.Application.Commands;

/// <summary>
/// Phần dùng chung của cntrl-task và update-task: áp một "bản vá" lên task đang chạy
/// - Task không đang chạy: bỏ qua kèm cảnh báo, KHÔNG tạo task mới (FR-CMD-12)
/// - Bản trộn có rule sai kiểu: giữ nguyên task cũ, ghi cảnh báo
/// - Không phát lại status vòng đời (FR-CMD-6)
/// </summary>
internal sealed class TaskPatchApplier
{
    private readonly ITaskStore _store;
    private readonly CommandStats _stats;
    private readonly Func<string, bool>? _isSupportedRule;
    private readonly ILogger _logger;

    public TaskPatchApplier(ITaskStore store, CommandStats stats, Func<string, bool>? isSupportedRule, ILogger logger)
    {
        _store = store;
        _stats = stats;
        _isSupportedRule = isSupportedRule;
        _logger = logger;
    }

    public void ReportMalformed(string appId, string command, string reason)
    {
        _stats.Increment(CommandStatNames.Malformed);
        _logger.LogWarning("Identity {AppId}: {Command} sai định dạng, bỏ qua: {Reason}", appId, command, reason);
    }

    /// <summary>
    /// Đọc dạng danh sách: detail.task_list (mảng Task) hoặc detail.task (một object Task)
    /// Trả về false nếu detail không có dạng nào trong hai dạng đó (lệnh sai định dạng)
    /// </summary>
    public bool ApplyFromDetail(string appId, string command, JsonElement detail)
    {
        if (JsonProps.TryGet(detail, "task_list", out var list) && list.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in list.EnumerateArray())
            {
                ApplyPatch(appId, command, null, item);
            }

            return true;
        }

        if (JsonProps.TryGet(detail, "task", out var single) && single.ValueKind == JsonValueKind.Object)
        {
            ApplyPatch(appId, command, null, single);
            return true;
        }

        return false;
    }

    /// <param name="fallbackTaskId">Dùng khi bản vá không có trường task (FR-CMD-7: lấy id từ detail.task)</param>
    public void ApplyPatch(string appId, string command, string? fallbackTaskId, JsonElement patch)
    {
        if (patch.ValueKind != JsonValueKind.Object)
        {
            _logger.LogWarning("Identity {AppId}: {Command} có phần tử không phải object, bỏ qua", appId, command);
            return;
        }

        var idRead = JsonProps.GetString(patch, "task", out var taskId);
        if (idRead != ReadResult.Ok || string.IsNullOrWhiteSpace(taskId))
        {
            taskId = fallbackTaskId;
        }

        if (string.IsNullOrWhiteSpace(taskId))
        {
            _stats.Increment(CommandStatNames.TasksSkippedNoId);
            _logger.LogWarning("Identity {AppId}: {Command} có phần tử thiếu 'task' id, bỏ qua", appId, command);
            return;
        }

        string? rejection = null;
        var result = _store.Update(appId, taskId, existing =>
        {
            var merged = TaskMerge.Apply(existing, patch, _isSupportedRule);
            if (merged.IsRejected)
            {
                rejection = merged.Error;
                return existing;
            }

            return merged.Value!;
        });

        if (result is null)
        {
            _stats.Increment(CommandStatNames.UpdatesIgnoredNotRunning);
            _logger.LogWarning("Identity {AppId}: {Command} cho task {TaskId} không đang chạy, bỏ qua (không tạo task mới)",
                appId, command, taskId);
            return;
        }

        if (rejection is not null)
        {
            _logger.LogWarning("Identity {AppId}: {Command} cho task {TaskId} bị từ chối, giữ nguyên task cũ: {Reason}",
                appId, command, taskId, rejection);
            return;
        }

        foreach (var skipped in result.Current.SkippedRules)
        {
            if (skipped is { Reason: RuleSkipReason.Unsupported, Name: not null })
            {
                _stats.Increment(CommandStatNames.RuleUnsupported(skipped.Name));
            }
        }

        _logger.LogInformation("Identity {AppId}: {Command} đã cập nhật task {TaskId} ({Changes})",
            appId, command, taskId, string.Join(", ", result.RuleChanges.Select(c => $"{c.Key.Name}#{c.Key.RoiId}={c.Kind}")));
    }
}
