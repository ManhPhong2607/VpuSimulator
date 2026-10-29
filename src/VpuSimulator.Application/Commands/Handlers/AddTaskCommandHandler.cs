using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using VpuSimulator.Application.Messaging;
using VpuSimulator.Application.State;

namespace VpuSimulator.Application.Commands;

/// <summary>
/// add-task: đăng ký từng task có task id, phát running cho mỗi task MỚI khởi động.
/// - Task có rule sai kiểu: không chạy, nhận status failed
/// - <c>task_list</c> rỗng hoặc vắng mặt: lệnh thất bại.
/// - Phần tử thiếu <c>task</c> id: bỏ qua kèm cảnh báo; các phần tử khác vẫn chạy.
/// - Gửi lại add-task cho task đang chạy: thay định nghĩa, không nhân đôi, KHÔNG phát lại running
///   (SRS không nói; chọn giống cntrl-task "không phát lại status vòng đời". Cần nhóm xác nhận
/// </summary>
public sealed class AddTaskCommandHandler : ICommandHandler
{
    private readonly ITaskStore _store;
    private readonly TimeProvider _time;
    private readonly CommandStats _stats;
    private readonly Func<string, bool>? _isSupportedRule;
    private readonly ILogger _logger;

    public AddTaskCommandHandler(
        ITaskStore store,
        TimeProvider time,
        CommandStats stats,
        Func<string, bool>? isSupportedRule = null,
        ILogger<AddTaskCommandHandler>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(stats);
        _store = store;
        _time = time;
        _stats = stats;
        _isSupportedRule = isSupportedRule;
        _logger = logger ?? NullLogger<AddTaskCommandHandler>.Instance;
    }

    public string Command => VmsCommandNames.AddTask;

    public async Task HandleAsync(CommandContext context, CancellationToken cancellationToken)
    {
        var appId = context.AppId;
        var parsed = VmsTaskParser.ParseAddTask(context.Envelope.Detail, _isSupportedRule);

        if (parsed.Tasks is null)
        {
            _stats.Increment(CommandStatNames.Malformed);
            _logger.LogWarning("Identity {AppId}: add-task sai định dạng, bỏ qua: {Reason}", appId, parsed.Error);
            return;
        }

        if (parsed.Tasks.Count == 0)
        {
            _stats.Increment(CommandStatNames.Malformed);
            _logger.LogWarning("Identity {AppId}: add-task có task_list rỗng, lệnh thất bại", appId);
            return;
        }

        foreach (var result in parsed.Tasks)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (result.IsRejected)
            {
                await HandleRejectedAsync(context, result, cancellationToken).ConfigureAwait(false);
                continue;
            }

            var task = result.Value!;
            foreach (var warning in task.Warnings)
            {
                _logger.LogWarning("Identity {AppId}, task {TaskId}: {Warning}", appId, task.TaskId, warning);
            }

            foreach (var skipped in task.SkippedRules)
            {
                if (skipped is { Reason: RuleSkipReason.Unsupported, Name: not null })
                {
                    _stats.Increment(CommandStatNames.RuleUnsupported(skipped.Name));
                }
            }

            var applied = _store.Upsert(appId, task);
            if (applied.Created)
            {
                _logger.LogInformation("Identity {AppId}: task {TaskId} khởi động với {Rules} rule", appId, task.TaskId, task.Rules.Count);
                await PublishAsync(context, StatusMessages.TaskRunning(task.TaskId, _time.GetUtcNow()), cancellationToken).ConfigureAwait(false);
            }
            else
            {
                _logger.LogInformation("Identity {AppId}: task {TaskId} đã chạy, thay định nghĩa ({Changes})", appId, task.TaskId,
                    string.Join(", ", applied.RuleChanges.Select(c => $"{c.Key.Name}#{c.Key.RoiId}={c.Kind}")));
            }
        }
    }

    private async Task HandleRejectedAsync(CommandContext context, TaskParseResult result, CancellationToken cancellationToken)
    {
        if (result.TaskId is null)
        {
            // Không có id thì không định danh được task và cũng không có uuid để phát failed.
            _stats.Increment(CommandStatNames.TasksSkippedNoId);
            _logger.LogWarning("Identity {AppId}: add-task có phần tử bị bỏ qua: {Reason}", context.AppId, result.Error);
            return;
        }

        _stats.Increment(CommandStatNames.TasksFailed);
        _logger.LogWarning("Identity {AppId}: task {TaskId} bị từ chối, phát status failed: {Reason}", context.AppId, result.TaskId, result.Error);
        await PublishAsync(context, StatusMessages.TaskFailed(result.TaskId, _time.GetUtcNow()), cancellationToken).ConfigureAwait(false);
    }

    // Không đệm (FR-ID-10): mất kết nối thì status không tới VMS, task vẫn nằm trong kho.
    private async Task PublishAsync(CommandContext context, StatusMessage message, CancellationToken cancellationToken)
    {
        try
        {
            await context.Broker.PublishAsync(RoutingKeys.Status(context.AppId), message, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Identity {AppId}: không phát được status của task {TaskId}", context.AppId, message.Uuid);
        }
    }
}
