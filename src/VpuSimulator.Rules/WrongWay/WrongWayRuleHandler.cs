using System;
using System.Collections.Generic;
using System.Text;
using VpuSimulator.Domain;
using VpuSimulator.Domain.Entities;
using VpuSimulator.Domain.RuleContracts;
using VpuSimulator.Rules.Common;
namespace VpuSimulator.Rules.WrongWay;

// #5 : di nguoc chieu
public sealed class WrongWayRuleHandler : IRuleHandler
{
    public string Name => "Wrong Way";

    public ValueTask<RuleDecision> ExecuteAsync(
        UnitInfo unit, 
        RuleContext rule, 
        TickContext tick,
        ActiveFollowUpState? active,
        CancellationToken ct) 
        => SimpleViolationRule.Decide(rule);
}
