namespace VpuSimulator.Application.Scheduling;

// Dữ liệu thô của một profile do client gửi (POST/PUT /profiles), đơn vị s
// Mọi trường là nullable để phân biệt "thiếu" với "bằng 0/false": cần gửi đủ trường,
// vì thiếu thì sẽ bị hiểu là 0 (FR-CFG-9). Riêng ramp_* được phép null (profile không có ramp)
public sealed record ProfileInput(
    string? Name,
    double? Interval,
    double? JitterMin,
    double? JitterMax,
    double? MinGap,
    bool? BypassFrames,
    bool? CoalesceTimestamps,
    double? RampFrom,
    double? RampTo,
    double? RampDuration)
{
    // Chuyển thành SimulationProfile có tên `name` (lấy từ đường dẫn). Trả false kèm MỌI lỗi tìm được (không dừng ở lỗi đầu tiên).
    public bool TryBuild(string name, out SimulationProfile? profile, out IReadOnlyList<string> errors)
    {
        var found = new List<string>();
        profile = null;
        
        if (Interval is null) found.Add("thiếu trường bắt buộc: interval");
        if (JitterMin is null) found.Add("thiếu trường bắt buộc: jitter_min");
        if (JitterMax is null) found.Add("thiếu trường bắt buộc: jitter_max");
        if (MinGap is null) found.Add("thiếu trường bắt buộc: min_gap");
        if (BypassFrames is null) found.Add("thiếu trường bắt buộc: bypass_frames");
        if (CoalesceTimestamps is null) found.Add("thiếu trường bắt buộc: coalesce_timestamps");

        var interval = Seconds(Interval, "interval");
        var jitterMin = Seconds(JitterMin, "jitter_min");
        var jitterMax = Seconds(JitterMax, "jitter_max");
        var minGap = Seconds(MinGap, "min_gap");
        TimeSpan? rampDuration = RampDuration is null ? null : Seconds(RampDuration, "ramp_duration");

        // Chỉ kiểm luật giá trị khi đã đủ trường: thiếu trường thì giá trị 0 giả sẽ sinh thêm lỗi sai
        if (found.Count == 0)
        {
            var candidate = new SimulationProfile
            {
                Name = name,
                Interval = interval,
                JitterMin = jitterMin,
                JitterMax = jitterMax,
                MinGap = minGap,
                BypassFrames = BypassFrames!.Value,
                CoalesceTimestamps = CoalesceTimestamps!.Value,
                RampFrom = RampFrom,
                RampTo = RampTo,
                RampDuration = rampDuration,
            };
            found.AddRange(candidate.Validate());
            if (found.Count == 0) profile = candidate;
        }

        errors = found;
        return profile is not null;

        // Số giây -> TimeSpan. Số vô hạn hoặc quá lớn sẽ làm TimeSpan.FromSeconds ném lỗi, nên đổi thành lỗi 400.
        TimeSpan Seconds(double? value, string field)
        {
            if (value is null) return TimeSpan.Zero;
            if (!double.IsFinite(value.Value) || Math.Abs(value.Value) > TimeSpan.MaxValue.TotalSeconds)
            {
                found.Add($"{field} = {value}: giá trị không hợp lệ");
                return TimeSpan.Zero;   
            }

            return TimeSpan.FromSeconds(value.Value);
        }
    }
}