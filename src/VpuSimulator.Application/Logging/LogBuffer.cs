namespace VpuSimulator.Application.Logging;

public sealed record LogMessageInfo(
    string Event, string RoutingKey, string Kind, int SizeBytes, string? Payload, bool Truncated);

public sealed record LogEntry(
    String Id,
    DateTimeOffset At,
    String Outcome,
    String? Identity,
    String? TaskId,
    String? Rule,
    String Detail,
    LogMessageInfo? Message = null);

// Bộ đệm vòng giữ N dòng log gần nhất trong bộ nhớ (FR-OBS-4, mặc định 2000).
// Đầy thì dòng mới ghi đè dòng cũ nhất, nên bộ nhớ không bao giờ phình ra.
// Nhiều luồng ghi log cùng lúc, nên mọi thao tác đều nằm trong lock.
public sealed class LogBuffer
{
    private readonly object _gate = new();
    private readonly LogEntry[] _ring;
    private int _next;  // vị trí sẽ ghi tiếp theo
    private int _count; // số dòng đang giữ (tối đa = Capacity)
    private long _seq;  // số thứ tự đã cấp, dùng làm id

    public LogBuffer(int capacity = 2000)
    {
        if( capacity < 1 ) throw new ArgumentOutOfRangeException(nameof(capacity), "Sức chứa phải >= 1");
        _ring = new LogEntry[capacity];
    }
    
    public int Capacity => _ring.Length;

    public int Count
    {
        get { lock (_gate) return _count;}
    }

    public LogEntry Add(DateTimeOffset at, string outcome, string? identity, string? taskId, string? rule,
        string detail, LogMessageInfo? message = null)
    {
        lock (_gate)
        {
            var entry = new LogEntry("l" + (++_seq), at, outcome, identity, taskId, rule, detail, message);
            _ring[_next] = entry;
            _next = (_next + 1) % _ring.Length;
            if (_count <  _ring.Length) _count++;
            return entry;
        }
    }
    
    // n dòng gần nhất, MỚI NHẤT TRƯỚC. n lớn hơn số dòng đang có thì trả hết, n < 1 thì trả rỗng.
    public IReadOnlyList<LogEntry> Latest(int n)
    {
        if (n < 1) return [];
        lock (_gate)
        {
            var take = Math.Min(n, _count);
            var result = new List<LogEntry>(take);
            for (var i = 1; i <= take; i++)
                result.Add(_ring[(_next - i +_ring.Length) % _ring.Length]);
            return result;
        }
    }
}