using System;
using System.Collections.Generic;
using System.Text;
using VpuSimulator.Domain;
using VpuSimulator.Domain.Entities;
using VpuSimulator.Domain.RuleContracts;

namespace VpuSimulator.Rules.DisasterDetection;

// #15: "Disaster Detection". affect_object rỗng thì Precondition
public sealed class DisasterDetectionRuleHandler : IRuleHandler
{
    public string Name => "Disaster Detection";

    public ValueTask<RuleDecision> ExecuteAsync(
        UnitInfo unit, 
        RuleContext rule, 
        TickContext tick,
        ActiveFollowUpState? active,
        CancellationToken ct)
    {
        return ValueTask.FromResult(new RuleDecision(
            rule.AffectObject != null && rule.AffectObject.Count > 0
            ? RuleOutcome .Emitted
            : RuleOutcome.Precondition));
    }    
}