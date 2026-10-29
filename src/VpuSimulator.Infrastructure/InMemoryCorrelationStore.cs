using System.Collections.Concurrent;
using VpuSimulator.Domain;

namespace VpuSimulator.Infrastructure;

public sealed record CorrelationOptions(TimeSpan Ttl, int MaxPerIdentity)
{
    public static CorrelationOptions Default { get; } = new(TimeSpan.FromHours(1), 100_000);   // FR-COR-4
}

public sealed class InMemoryCorrelationStore : ICorrelationStore
{
    private sealed record Entry(object Value, DateTimeOffset WrittenAt, long Seq);

    private sealed class Bucket
    {
        public readonly object Gate = new();
        public readonly Dictionary<string, Entry> Items = new();
        public readonly Queue<(string Key, long Seq)> Order = new();   // thứ tự ghi, bỏ mục chết một cách "lười"
        public long NextSeq;
    }

    private readonly ConcurrentDictionary<string, Bucket> _byAppId = new();
    private readonly CorrelationOptions _options;
    private readonly TimeProvider _time;

    public InMemoryCorrelationStore(CorrelationOptions? options = null, TimeProvider? time = null)
    {
        _options = options ?? CorrelationOptions.Default;
        _time = time ?? TimeProvider.System;
    }

    private bool IsExpired(Entry e) => _time.GetUtcNow() - e.WrittenAt > _options.Ttl;

    public void Write(string appId, string key, object value)
    {
        var b = _byAppId.GetOrAdd(appId, _ => new Bucket());
        lock (b.Gate)
        {
            var seq = ++b.NextSeq;
            b.Items[key] = new Entry(value, _time.GetUtcNow(), seq);
            b.Order.Enqueue((key, seq));

            // Vượt giới hạn: bỏ mục cũ nhất còn sống (bỏ qua mục đã bị ghi đè hoặc đã xoá).
            while (b.Items.Count > _options.MaxPerIdentity && b.Order.Count > 0)
            {
                var (k, s) = b.Order.Dequeue();
                if (b.Items.TryGetValue(k, out var e) && e.Seq == s) b.Items.Remove(k);
            }

            // Hàng đợi chứa nhiều mục chết thì dựng lại, tránh phình bộ nhớ.
            if (b.Order.Count > 2 * b.Items.Count + 1024)
            {
                b.Order.Clear();
                foreach (var kv in b.Items.OrderBy(kv => kv.Value.Seq)) b.Order.Enqueue((kv.Key, kv.Value.Seq));
            }
        }
    }

    public object? Read(string appId, string key)
    {
        if (!_byAppId.TryGetValue(appId, out var b)) return null;
        lock (b.Gate)
        {
            if (!b.Items.TryGetValue(key, out var e)) return null;
            if (IsExpired(e)) { b.Items.Remove(key); return null; }
            return e.Value;
        }
    }

    public IReadOnlyDictionary<string, object> GetAll(string appId)
    {
        if (!_byAppId.TryGetValue(appId, out var b)) return new Dictionary<string, object>();
        lock (b.Gate)
        {
            foreach (var key in b.Items.Where(kv => IsExpired(kv.Value)).Select(kv => kv.Key).ToList())
                b.Items.Remove(key);
            return b.Items.ToDictionary(kv => kv.Key, kv => kv.Value.Value);
        }
    }

    public bool TryDelete(string appId, string key)
    {
        if (!_byAppId.TryGetValue(appId, out var b)) return false;
        lock (b.Gate)
        {
            if (!b.Items.TryGetValue(key, out var e)) return false;
            b.Items.Remove(key);
            return !IsExpired(e);          // đã hết hạn thì coi như không còn để chiếm
        }
    }

    public void Clear(string appId)
    {
        if (!_byAppId.TryGetValue(appId, out var b)) return;
        lock (b.Gate) { b.Items.Clear(); b.Order.Clear(); }
    }

    public void ClearAll()
    {
        foreach (var appId in _byAppId.Keys) Clear(appId);
    }
}