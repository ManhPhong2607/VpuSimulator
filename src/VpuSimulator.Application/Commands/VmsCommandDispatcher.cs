using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using VpuSimulator.Application.Interfaces;
using VpuSimulator.Application.Interfaces.Messaging;
using VpuSimulator.Application.Messaging;

namespace VpuSimulator.Application.Commands;

/// <summary>
/// Cửa vào của mọi lệnh VMS: parse envelope rồi chuyển cho <see cref="ICommandHandler"/> đúng loại
/// Không bao giờ ném exception ra ngoài vì dữ liệu xấu: bản tin hỏng được đếm, ghi log kèm tối đa 512 byte nội dung
/// rồi bỏ qua, subscription không bị ảnh hưởng 
/// </summary>
public sealed class VmsCommandDispatcher : IInboundMessageHandler
{
    private readonly Dictionary<string, ICommandHandler> _handlers;
    private readonly CommandStats _stats;
    private readonly ILogger _logger;

    public VmsCommandDispatcher(
        IEnumerable<ICommandHandler> handlers,
        CommandStats stats,
        ILogger<VmsCommandDispatcher>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(handlers);
        ArgumentNullException.ThrowIfNull(stats);
        _handlers = handlers.ToDictionary(h => h.Command, StringComparer.Ordinal);
        _stats = stats;
        _logger = logger ?? NullLogger<VmsCommandDispatcher>.Instance;
    }

    public async Task HandleAsync(string appId, IMessageBroker broker, InboundMessage message, CancellationToken cancellationToken)
    {
        var parsed = VmsCommandParser.Parse(message.Body);
        if (!parsed.IsSuccess)
        {
            var failure = parsed.Failure!;
            _stats.Increment(failure.Reason == VmsParseFailureReason.UnknownCommand ? CommandStatNames.Unknown : CommandStatNames.Malformed);
            _logger.LogWarning(
                "Identity {AppId}: bản tin trên '{RoutingKey}' bị bỏ ({Reason}). Nội dung (tối đa 512 byte): {Preview}",
                appId, message.RoutingKey, failure.Reason, failure.Preview);
            return;
        }

        var envelope = parsed.Envelope!;
        if (!_handlers.TryGetValue(envelope.Command, out var handler))
        {
            _stats.Increment(CommandStatNames.Unknown);
            _logger.LogWarning("Identity {AppId}: lệnh '{Command}' chưa có handler, bỏ qua", appId, envelope.Command);
            return;
        }

        _logger.LogInformation("Identity {AppId}: nhận lệnh {Command} (envelope uuid {Uuid})", appId, envelope.Command, envelope.Uuid);
        try
        {
            await handler.HandleAsync(new CommandContext(appId, broker, message, envelope), cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _stats.Increment(CommandStatNames.Failed);
            _logger.LogError(ex, "Identity {AppId}: xử lý lệnh {Command} gặp lỗi, bỏ qua lệnh này", appId, envelope.Command);
        }
    }
}
