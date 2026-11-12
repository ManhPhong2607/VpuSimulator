namespace VpuSimulator.Application.Scheduling;

public sealed record EpsReading(
    double Total,
    IReadOnlyDictionary<string, double> ByIdentity,
    double? Target,
    IReadOnlyDictionary<UnitKey, double>? ByUnit = null)
{
    public double ForUnit(UnitKey key) =>
        ByUnit is not null && ByUnit.TryGetValue(key, out var v) ? v : 0;

    public double ForTask(string appId, string taskId) =>
        ByUnit is null
            ? 0
            : ByUnit.Where(kv => kv.Key.AppId == appId && kv.Key.TaskId == taskId).Sum(kv => kv.Value);
}

/// <summary>Đo eps thực tế (FR-OBS-7). Gọi Sample mỗi giây; đọc Current ở /status. Chỉ đếm bản tin đã phát.</summary>
public sealed class EpsMeter
{
    private const int WindowSeconds = 5;

    private static readonly EpsReading Empty = new(
        0,
        new Dictionary<string, double>(StringComparer.Ordinal),
        null,
        new Dictionary<UnitKey, double>());

    private readonly object _gate = new();
    private readonly Dictionary<UnitKey, long> _last = new();
    private readonly Queue<Dictionary<UnitKey, long>> _window = new();
    private EpsReading _current = Empty;

    public EpsReading Current
    {
        get
        {
            lock (_gate)
            {
                return _current;
            }
        }
    }

    public void Sample(IReadOnlyList<UnitSnapshot> snapshots)
    {
        ArgumentNullException.ThrowIfNull(snapshots);

        lock (_gate)
        {
            var delta = new Dictionary<UnitKey, long>();
            var seen = new HashSet<UnitKey>();
            double? target = null;

            foreach (var s in snapshots)
            {
                seen.Add(s.Key);
                _last.TryGetValue(s.Key, out var previous);
                _last[s.Key] = s.Emitted;

                // Unit dựng lại thì bộ đếm về 0: không để hiệu số âm
                var d = Math.Max(0, s.Emitted - previous);
                if (d > 0)
                {
                    delta[s.Key] = d;
                }

                if (s.TargetEps is { } t)
                {
                    target = (target ?? 0) + t;
                }
            }

            foreach (var gone in _last.Keys.Where(k => !seen.Contains(k)).ToList())
            {
                _last.Remove(gone);
            }

            _window.Enqueue(delta);
            while (_window.Count > WindowSeconds)
            {
                _window.Dequeue();
            }

            var byUnit = new Dictionary<UnitKey, double>();
            foreach (var sample in _window)
            {
                foreach (var (key, count) in sample)
                {
                    byUnit[key] = byUnit.GetValueOrDefault(key) + count;
                }
            }

            var byIdentity = new Dictionary<string, double>(StringComparer.Ordinal);
            foreach (var key in byUnit.Keys.ToList())
            {
                byUnit[key] /= _window.Count;
                byIdentity[key.AppId] = byIdentity.GetValueOrDefault(key.AppId) + byUnit[key];
            }

            _current = new EpsReading(byIdentity.Values.Sum(), byIdentity, target, byUnit);
        }
    }
}