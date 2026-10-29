using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using VpuSimulator.Application.State;

namespace VpuSimulator.Application.Commands;

/// <summary>end-task (FR-CMD-4): dừng và xoá các task trong <c>detail.task_list</c> (mảng chuỗi task uuid). Id không tồn tại thì bỏ qua. Không phát status.</summary>
public sealed class EndTaskCommandHandler : ICommandHandler
{
    private readonly ITaskStore _store;
    private readonly CommandStats _stats;
    private readonly ILogger _logger;

    public EndTaskCommandHandler(ITaskStore store, CommandStats stats, ILogger<EndTaskCommandHandler>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(stats);
        _store = store;
        _stats = stats;
        _logger = logger ?? NullLogger<EndTaskCommandHandler>.Instance;
    }

    public string Command => VmsCommandNames.EndTask;

    public Task HandleAsync(CommandContext context, CancellationToken cancellationToken)
    {
        if (!JsonProps.TryGet(context.Envelope.Detail, "task_list", out var list) || list.ValueKind != JsonValueKind.Array)
        {
            _stats.Increment(CommandStatNames.Malformed);
            _logger.LogWarning("Identity {AppId}: end-task sai định dạng, bỏ qua: detail.task_list vắng mặt hoặc không phải mảng", context.AppId);
            return Task.CompletedTask;
        }

        var ids = new List<string>();
        foreach (var item in list.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(item.GetString()))
            {
                ids.Add(item.GetString()!);
            }
            else
            {
                _logger.LogWarning("Identity {AppId}: end-task có phần tử không phải task uuid, bỏ qua phần tử đó", context.AppId);
            }
        }

        var removed = _store.Remove(context.AppId, ids);
        _logger.LogInformation("Identity {AppId}: end-task dừng {Removed}/{Requested} task", context.AppId, removed.Count, ids.Count);
        return Task.CompletedTask;
    }
}
