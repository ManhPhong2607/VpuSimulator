using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using VpuSimulator.Application.State;

namespace VpuSimulator.Application.Commands;

/// <summary>
/// update-task (FR-CMD-8, FR-CMD-14): trộn thông tin bổ sung vào task đang chạy. Dạng <c>{"task_list":[Task]}</c> hoặc <c>{"task":{Task}}</c>.
/// Trường vắng mặt giữ nguyên; không có <c>rule_conf</c> thì tập rule và trạng thái unit giữ nguyên.
/// Có <c>rule_conf</c> thì tập rule bằng đúng <c>rule_conf</c> đó (SRS không nói bản vá rule có trộn từng rule hay không; cần nhóm xác nhận).
/// </summary>
public sealed class UpdateTaskCommandHandler : ICommandHandler
{
    private readonly TaskPatchApplier _applier;

    public UpdateTaskCommandHandler(
        ITaskStore store,
        CommandStats stats,
        Func<string, bool>? isSupportedRule = null,
        ILogger<UpdateTaskCommandHandler>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(stats);
        _applier = new TaskPatchApplier(store, stats, isSupportedRule, logger ?? NullLogger<UpdateTaskCommandHandler>.Instance);
    }

    public string Command => VmsCommandNames.UpdateTask;

    public Task HandleAsync(CommandContext context, CancellationToken cancellationToken)
    {
        if (!_applier.ApplyFromDetail(context.AppId, Command, context.Envelope.Detail))
        {
            _applier.ReportMalformed(context.AppId, Command, "detail không có task_list hay task (object)");
        }

        return Task.CompletedTask;
    }
}
