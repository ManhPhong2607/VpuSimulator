using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using VpuSimulator.Application.State;

namespace VpuSimulator.Application.Commands;

/// <summary>stop-all (FR-CMD-5): dừng mọi task của identity. Identity vẫn giữ subscription và heartbeat. <c>detail</c> tuỳ chọn và không dùng.</summary>
public sealed class StopAllCommandHandler : ICommandHandler
{
    private readonly ITaskStore _store;
    private readonly ILogger _logger;

    public StopAllCommandHandler(ITaskStore store, ILogger<StopAllCommandHandler>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        _store = store;
        _logger = logger ?? NullLogger<StopAllCommandHandler>.Instance;
    }

    public string Command => VmsCommandNames.StopAll;

    public Task HandleAsync(CommandContext context, CancellationToken cancellationToken)
    {
        var removed = _store.Clear(context.AppId);
        _logger.LogInformation("Identity {AppId}: stop-all dừng {Count} task", context.AppId, removed.Count);
        return Task.CompletedTask;
    }
}
