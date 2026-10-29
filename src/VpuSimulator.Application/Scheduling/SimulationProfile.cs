namespace VpuSimulator.Application.Scheduling;

//>Profile nhịp phát (FR-SIM-6). Trường không khai báo mặc định bằng 0 / false / null
public sealed record SimulationProfile
{
    public required string Name { get; init; }
    public required TimeSpan Interval { get; init; }
    public TimeSpan JitterMin { get; init; }
    public TimeSpan JitterMax { get; init; }
    public TimeSpan MinGap { get; init; }
    public bool BypassFrames { get; init; }
    public bool CoalesceTimestamps { get; init; }
    public double? RampFrom { get; init; }
    public double? RampTo { get; init; }
    public TimeSpan? RampDuration { get; init; }

    //Ràng buộc theo bảng cấu hình của SRS: interval &gt; 0; 0 ≤ jitter_min ≤ jitter_max; min_gap ≥ 0; ramp_from, ramp_to &gt; 0 khi có ramp_duration
    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(Name))
        {
            errors.Add("profile: tên rỗng");
        }

        if (Interval <= TimeSpan.Zero)
        {
            errors.Add($"profile '{Name}': interval phải > 0");
        }

        if (JitterMin < TimeSpan.Zero)
        {
            errors.Add($"profile '{Name}': jitter_min phải ≥ 0");
        }

        if (JitterMax < JitterMin)
        {
            errors.Add($"profile '{Name}': jitter_max phải ≥ jitter_min");
        }

        if (MinGap < TimeSpan.Zero)
        {
            errors.Add($"profile '{Name}': min_gap phải ≥ 0");
        }

        if (RampDuration is not null)
        {
            if (RampDuration <= TimeSpan.Zero)
            {
                errors.Add($"profile '{Name}': ramp_duration phải > 0");
            }

            if (RampFrom is null or <= 0 || RampTo is null or <= 0)
            {
                errors.Add($"profile '{Name}': ramp_from và ramp_to phải > 0 khi có ramp_duration");
            }
        }

        return errors;
    }
}

//Hai profile mặc định theo FR-SIM-6
public static class SimulationProfiles
{
    public const string DefaultName = "normal";

    public static SimulationProfile Normal { get; } = new()
    {
        Name = "normal",
        Interval = TimeSpan.FromSeconds(2),
        JitterMin = TimeSpan.FromSeconds(5),
        JitterMax = TimeSpan.FromSeconds(20),
    };

    public static SimulationProfile Storm { get; } = new()
    {
        Name = "storm",
        Interval = TimeSpan.FromMilliseconds(500),
        MinGap = TimeSpan.FromSeconds(3),
        BypassFrames = true,
        CoalesceTimestamps = true,
    };

    public static IReadOnlyDictionary<string, SimulationProfile> Defaults { get; } =
        new Dictionary<string, SimulationProfile>(StringComparer.Ordinal)
        {
            [Normal.Name] = Normal,
            [Storm.Name] = Storm,
        };
}