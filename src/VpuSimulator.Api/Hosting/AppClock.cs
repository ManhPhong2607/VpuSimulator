namespace VpuSimulator.Api.Hosting;

/// <summary>Thời điểm tiến trình khởi động, ghi một lần lúc đăng ký DI.</summary>
public sealed class AppClock(DateTimeOffset startedAt)
{
    public DateTimeOffset StartedAt { get; } = startedAt;

    public long UptimeSeconds(TimeProvider time) =>
        (long)Math.Max(0, (time.GetUtcNow() - StartedAt).TotalSeconds);
}