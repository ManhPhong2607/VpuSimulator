using VpuSimulator.Application.Interfaces.Messaging;

namespace VpuSimulator.Application.Identity;

/// <summary>
/// Vỏ bọc broker của một identity: khi cổng đóng (VPU Off) thì bản tin bị bỏ lặng lẽ, không ném lỗi.
/// Handler lệnh và unit phát qua vỏ này nên không cần biết VPU đang Off.
/// </summary>
public sealed class GatedMessageBroker(IMessageBroker inner, Func<bool> isOpen) : IMessageBroker
{
    private long _dropped;

    public long Dropped => Interlocked.Read(ref _dropped);

    public bool IsConnected => inner.IsConnected;

    public Task PublishAsync(string routingKey, object envelope, CancellationToken cancellationToken = default)
    {
        if (!isOpen())
        {
            Interlocked.Increment(ref _dropped);
            return Task.CompletedTask;
        }

        return inner.PublishAsync(routingKey, envelope, cancellationToken);
    }
}