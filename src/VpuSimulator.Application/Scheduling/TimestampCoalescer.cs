namespace VpuSimulator.Application.Scheduling;

/// <summary>FR-SIM-8: gộp timestamp, làm tròn xuống đầu giây.</summary>
public static class TimestampCoalescer
{
    public static DateTimeOffset FloorToSecond(DateTimeOffset value) =>
        new(value.Ticks - value.Ticks % TimeSpan.TicksPerSecond, value.Offset);
}