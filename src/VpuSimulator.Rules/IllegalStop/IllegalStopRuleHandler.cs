using VpuSimulator.Domain;
using VpuSimulator.Domain.Entities;
using VpuSimulator.Domain.Events;
using VpuSimulator.Domain.FollowUp;
using VpuSimulator.Domain.RuleContracts;
using VpuSimulator.Rules.Common;

namespace VpuSimulator.Rules.IllegalStop;

// #7: "Illegal Stop", type 18, follow-up DEACTIVATE. Header: sự kiện mới = Lately (2), deactivate = Early (1).
// details.type luôn 4 (khác #6 đổi thành 2 lúc discard). Xác suất deactivate nằm ở IFollowUpPolicy (mặc định 0,35).

public sealed class IllegalStopRuleHandler : IRuleHandler
{
    private readonly IFollowUpPolicy _policy;
    public IllegalStopRuleHandler(IFollowUpPolicy policy) => _policy = policy;

    public string Name => "Illegal Stop";

    public ValueTask<RuleDecision> ExecuteAsync(UnitInfo unit, RuleContext rule, TickContext tick,
                                                ActiveFollowUpState? active, CancellationToken ct)
    {
        // Đang có sự kiện mở: rút số MỘT lần để quyết định có deactivate không.
        if (active != null)
        {
            FollowUpKind? kind = _policy.Decide(Name, active, tick.Rng);

            if (kind.HasValue)
                return ValueTask.FromResult(new RuleDecision(RuleOutcome.Emitted, kind.Value));
        }

        return SimpleViolationRule.Decide(rule);
    }
}


