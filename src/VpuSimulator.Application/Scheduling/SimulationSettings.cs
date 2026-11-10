using System.Diagnostics.CodeAnalysis;
using VpuSimulator.Application.Configuration;
using VpuSimulator.Application.Identity;
using VpuSimulator.Application.Options;

namespace VpuSimulator.Application.Scheduling;

/// <summary>Cấu hình mô phỏng đã gộp và kiểm tra: seed, arm delay, profile mặc định, mọi profile (có sẵn + khai báo thêm).</summary>
public sealed record SimulationSettings(
    long Seed,
    TimeSpan ArmDelay,
    SimulationProfile DefaultProfile,
    IReadOnlyDictionary<string, SimulationProfile> Profiles)
{
    public static bool TryResolve(
        SchedulerOptions scheduler,
        SimulationOptions simulation,
        IReadOnlyDictionary<string, ProfileOptions> profiles,
        [NotNullWhen(true)] out SimulationSettings? settings,
        out IReadOnlyList<string> errors)
    {
        ArgumentNullException.ThrowIfNull(scheduler);
        ArgumentNullException.ThrowIfNull(simulation);
        ArgumentNullException.ThrowIfNull(profiles);

        var found = new List<string>();

        if (simulation.Seed < 0)
        {
            found.Add($"Simulation:Seed = {simulation.Seed}: phải là số nguyên không âm (0 = ngẫu nhiên).");
        }

        if (!DurationParser.TryParse(scheduler.ArmDelay, out var armDelay) || armDelay < TimeSpan.Zero)
        {
            found.Add($"Scheduler:ArmDelay = '{scheduler.ArmDelay}': cần thời lượng ≥ 0, ví dụ 500ms, 5s, 1m.");
        }

        var resolved = new Dictionary<string, SimulationProfile>(SimulationProfiles.Defaults, StringComparer.Ordinal);
        foreach (var (name, options) in profiles)
        {
            var path = $"Profiles:{name}";
            var start = SimulationProfiles.Defaults.TryGetValue(name, out var builtIn)
                ? builtIn
                : new SimulationProfile { Name = name, Interval = TimeSpan.Zero };   // profile mới: bắt buộc khai báo interval

            var profile = start with
            {
                Interval = Duration(options.Interval, $"{path}:Interval", start.Interval),
                JitterMin = Duration(options.JitterMin, $"{path}:JitterMin", start.JitterMin),
                JitterMax = Duration(options.JitterMax, $"{path}:JitterMax", start.JitterMax),
                MinGap = Duration(options.MinGap, $"{path}:MinGap", start.MinGap),
                BypassFrames = options.BypassFrames ?? start.BypassFrames,
                CoalesceTimestamps = options.CoalesceTimestamps ?? start.CoalesceTimestamps,
                RampFrom = options.RampFrom ?? start.RampFrom,
                RampTo = options.RampTo ?? start.RampTo,
                RampDuration = options.RampDuration is null
                    ? start.RampDuration
                    : (TimeSpan?)Duration(options.RampDuration, $"{path}:RampDuration", TimeSpan.Zero),
            };

            found.AddRange(profile.Validate());
            resolved[name] = profile;
        }

        if (!resolved.TryGetValue(scheduler.DefaultProfile ?? string.Empty, out var defaultProfile))
        {
            found.Add($"Scheduler:DefaultProfile = '{scheduler.DefaultProfile}': profile không tồn tại. "
                      + $"Hiện có: {string.Join(", ", resolved.Keys.OrderBy(k => k, StringComparer.Ordinal))}.");
        }

        errors = found;
        if (found.Count > 0)
        {
            settings = null;
            return false;
        }

        settings = new SimulationSettings(simulation.Seed, armDelay, defaultProfile!, resolved);
        return true;

        TimeSpan Duration(string? text, string key, TimeSpan fallback)
        {
            if (text is null)
            {
                return fallback;
            }

            if (DurationParser.TryParse(text, out var value))
            {
                return value;
            }

            found.Add($"{key} = '{text}': thời lượng không hợp lệ, ví dụ 500ms, 5s, 1m.");
            return fallback;
        }
    }
}