using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;
using VpuSimulator.Application.State;

namespace VpuSimulator.Application.Commands;

/// <summary>
/// cntrl-task (FR-CMD-6, FR-CMD-7): cập nhật task đang chạy tại chỗ. Hai dạng:
/// - reload: <c>{"control-word":"reload","task":"&lt;uuid&gt;","data":{Task}}</c>. Id lấy từ <c>data.task</c>, nếu thiếu thì từ <c>detail.task</c>.
/// - danh sách: <c>{"task_list":[Task]}</c> hoặc <c>{"task":{Task}}</c>.
/// Bản vá được TRỘN vào task cũ (xem <see cref="TaskMerge"/>): ví dụ C.1 của SRS cho thấy <c>data</c> có thể chỉ gồm <c>task</c> và
/// <c>rule_conf</c>, nên không thể coi là thay nguyên định nghĩa. Cần nhóm xác nhận.
/// <c>control-word</c> khác <c>reload</c>: SRS không định nghĩa, bỏ qua và đếm.
/// </summary>
public sealed class ControlTaskCommandHandler : ICommandHandler
{
    private readonly CommandStats _stats;
    private readonly ILogger _logger;
    private readonly TaskPatchApplier _applier;

    public ControlTaskCommandHandler(
        ITaskStore store,
        CommandStats stats,
        Func<string, bool>? isSupportedRule = null,
        ILogger<ControlTaskCommandHandler>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(stats);
        _stats = stats;
        _logger = logger ?? NullLogger<ControlTaskCommandHandler>.Instance;
        _applier = new TaskPatchApplier(store, stats, isSupportedRule, _logger);
    }

    public string Command => VmsCommandNames.CntrlTask;

    public Task HandleAsync(CommandContext context, CancellationToken cancellationToken)
    {
        var appId = context.AppId;
        var detail = context.Envelope.Detail;

        var controlRead = JsonProps.GetString(detail, "control-word", out var controlWord);
        if (controlRead == ReadResult.WrongType)
        {
            _applier.ReportMalformed(appId, Command, "'control-word' không phải chuỗi");
            return Task.CompletedTask;
        }

        if (controlRead == ReadResult.Ok)
        {
            if (!string.Equals(controlWord, "reload", StringComparison.OrdinalIgnoreCase))
            {
                _stats.Increment(CommandStatNames.ControlWordUnsupported);
                _logger.LogWarning("Identity {AppId}: cntrl-task có control-word '{ControlWord}' chưa được hỗ trợ, bỏ qua", appId, controlWord);
                return Task.CompletedTask;
            }

            var taskRead = JsonProps.GetString(detail, "task", out var detailTaskId);
            if (taskRead != ReadResult.Ok || !JsonProps.TryGet(detail, "data", out var data) || data.ValueKind != JsonValueKind.Object)
            {
                _applier.ReportMalformed(appId, Command, "reload cần 'task' là chuỗi và 'data' là object");
                return Task.CompletedTask;
            }

            _applier.ApplyPatch(appId, Command, detailTaskId, data);
            return Task.CompletedTask;
        }

        if (!_applier.ApplyFromDetail(appId, Command, detail))
        {
            _applier.ReportMalformed(appId, Command, "không có control-word, task_list hay task (object)");
        }

        return Task.CompletedTask;
    }
}
