using VpuSimulator.Domain;
using VpuSimulator.Domain.Entities;
using VpuSimulator.Domain.FollowUp;
using VpuSimulator.Domain.RuleContracts;

namespace VpuSimulator.Rules.TrafficSignalViolation;

public sealed class TrafficSignalViolationRuleHandler : IRuleHandler
{
    private readonly IFollowUpPolicy _policy;
    public TrafficSignalViolationRuleHandler(IFollowUpPolicy policy) => _policy = policy;   // vì vậy không quét reflection được nữa

    public string Name => "Traffic Signal Violation";

    public ValueTask<RuleDecision> ExecuteAsync(
        UnitInfo unit,
        RuleContext rule, 
        TickContext tick,
        ActiveFollowUpState? active,
        CancellationToken ct)
    {
        // Đang có sự kiện mở: rút số 1 lần để quyết định có discard không.
        if (active != null)
        {
            FollowUpKind? kind = _policy.Decide(Name, active, tick.Rng);

            if (kind.HasValue)
            {
                return ValueTask.FromResult(
                    new RuleDecision(RuleOutcome.Emitted, kind.Value));
            }
        }

        return ValueTask.FromResult(
            new RuleDecision(
                rule.AffectObject != null && rule.AffectObject.Count > 0
                    ? RuleOutcome.Emitted
                    : RuleOutcome.Precondition));
    }
}

// Rule đi kèm, không bao giờ phát 
public sealed class TrafficLightRuleHandler : IRuleHandler
{
    public string Name => "Traffic Light";

    public ValueTask<RuleDecision> ExecuteAsync(
        UnitInfo unit, 
        RuleContext rule, 
        TickContext tick,
        ActiveFollowUpState? active,
        CancellationToken ct)
    {
        return ValueTask.FromResult(new RuleDecision(RuleOutcome.NoMessage));
    }
        
}