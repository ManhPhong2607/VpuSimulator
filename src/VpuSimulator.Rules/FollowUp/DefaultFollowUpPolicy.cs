using VpuSimulator.Domain;

namespace VpuSimulator.Rules.FollowUp;

public sealed record FollowUpOptions(double IllegalStopDeactivateChance = 0.35, double TrafficSignalDiscardChance = 0.75);

public sealed class DefaultFollowUpPolicy : IFollowUpPolicy
{
    private readonly FollowUpOptions _options;
    public DefaultFollowUpPolicy(FollowUpOptions? options = null) => _options = options ?? new FollowUpOptions();

    public FollowUpKind? Decide(string ruleName, ActiveFollowUpState active, Random rng)
    {
        var (kind, chance) = ruleName switch
        {
            "Illegal Stop" => (FollowUpKind.Deactivate, _options.IllegalStopDeactivateChance),
            "Traffic Signal Violation" => (FollowUpKind.Discard, _options.TrafficSignalDiscardChance),
            _ => (FollowUpKind.Deactivate, -1.0),
        };
        if (chance < 0) return null;                          // rule không có follow-up: không rút số nào
        return rng.NextDouble() < chance ? kind : null;       // đúng MỘT lần rút số
    }
}