using System.Collections.Concurrent;
using System.Threading.Channels;

namespace VpuSimulator.Application.Events;

public sealed record SseEvent(string Type, object Data);

/// <summary>
/// Bộ phát sự kiện thời gian thực qua SSE cho Console
/// </summary>
public sealed class SseEventBus
{
    private readonly ConcurrentDictionary<Guid, Channel<SseEvent>> _subscribers = new();
    // true khi có ít nhất một client SSE đang nghe. Nơi phát dùng để bỏ qua việc dựng dữ liệu khi k có ai nhận
    public bool HasSubscribers => !_subscribers.IsEmpty;
    public IDisposable Subscribe(out ChannelReader<SseEvent> reader, int capacity = 256)
    {
        var id = Guid.NewGuid();
        var channel = Channel.CreateBounded<SseEvent>(new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });
        _subscribers[id] = channel;
        reader = channel.Reader;
        return new Subscription(this, id, channel.Writer);
    }

    public void Publish(string type, object data)
    {
        if (!HasSubscribers) return;
        var ev = new SseEvent(type, data);
        foreach (var (_, ch) in _subscribers)
        {
            ch.Writer.TryWrite(ev);
        }
    }

    private sealed class Subscription(SseEventBus bus, Guid id, ChannelWriter<SseEvent> writer) : IDisposable
    {
        public void Dispose()
        {
            if (bus._subscribers.TryRemove(id, out _))
            {
                writer.TryComplete();
            }
        }
    }
}