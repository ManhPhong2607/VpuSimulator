namespace VpuSimulator.Application.Scheduling;

//Kết quả áp min_gap: thời điểm phát (có thể đã bị lùi) và cờ cho biết có bị lùi hay không (để đếm)
public readonly record struct MinGapResult(DateTimeOffset Due, bool Deferred);

/// <summary>
/// Các phép tính thời điểm phát. Hàm thuần: không đọc đồng hồ, không giữ trạng thái, Random do bên gọi đưa vào
/// Thời điểm kế tiếp được tính từ lúc lần phát trước hoàn tất; việc đó thuộc về scheduler
/// </summary>
public static class TickScheduleCalculator
{
    /// <summary>
    /// Khoảng chờ tới tick kế tiếp
    /// Density: reset_timer &gt; 0 thì phát đúng mỗi reset_timer giây, bỏ qua interval, jitter và ramp (FR-SIM-13); không rút số ngẫu nhiên
    /// Còn lại: interval + jitter_min + U[0, jitter_max − jitter_min) (FR-SIM-7). Chỉ rút số khi khoảng jitter &gt; 0,
    /// để số lần rút của một unit chỉ phụ thuộc cấu hình, không phụ thuộc thời gian
    /// </summary>
    public static TimeSpan NextDelay(SimulationProfile profile, double? resetTimerSeconds, Random random)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(random);

        if (resetTimerSeconds is > 0)
        {
            return TimeSpan.FromSeconds(resetTimerSeconds.Value);
        }

        var spread = profile.JitterMax - profile.JitterMin;
        var jitter = profile.JitterMin;
        if (spread > TimeSpan.Zero)
        {
            jitter += TimeSpan.FromTicks((long)(random.NextDouble() * spread.Ticks));
        }

        return profile.Interval + jitter;
    }

    /// <summary>
    /// min_gap (FR-SIM-12): tick đến sớm hơn last_emit + min_gap thì lùi tới đó và được đếm
    /// min_gap tính từ lần phát THÀNH CÔNG cuối; chưa có lần nào hoặc min_gap = 0 thì giữ nguyên
    /// Áp dụng cả cho density: SRS chỉ cho density bỏ qua interval, jitter và ramp
    /// </summary>
    public static MinGapResult ApplyMinGap(DateTimeOffset due, DateTimeOffset? lastSuccessfulEmit, TimeSpan minGap)
    {
        if (lastSuccessfulEmit is null || minGap <= TimeSpan.Zero)
        {
            return new MinGapResult(due, false);
        }

        var earliest = lastSuccessfulEmit.Value + minGap;
        return earliest > due ? new MinGapResult(earliest, true) : new MinGapResult(due, false);
    }
}