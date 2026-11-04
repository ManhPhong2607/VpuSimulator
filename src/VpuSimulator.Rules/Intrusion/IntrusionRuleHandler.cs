using System;
using System.Collections.Generic;
using System.Text;
using VpuSimulator.Domain;
using VpuSimulator.Domain.Entities;
using VpuSimulator.Domain.RuleContracts;
using VpuSimulator.Rules.Common;

namespace VpuSimulator.Rules.Intrusion;

//#19: Loi di vao duong cam/Xam nhap
public sealed class IntrusionRuleHandler : IRuleHandler
{
    public string Name => "INTRUSION";

    public ValueTask<RuleDecision> ExecuteAsync(
        UnitInfo unit,
        RuleContext rule,
        TickContext tick,
        ActiveFollowUpState? active,
        CancellationToken ct)
     => SimpleViolationRule.Decide(rule);

}
