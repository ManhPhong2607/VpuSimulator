using System;
using System.Collections.Generic;
using System.Text;
using VpuSimulator.Domain;
using VpuSimulator.Domain.Entities;
using VpuSimulator.Domain.RuleContracts;
using VpuSimulator.Rules.Common;

namespace VpuSimulator.Rules.InvalidLineInstruction;

// #13: loi de vach lien
public sealed class InvalidLineInstructionRuleHandler : IRuleHandler
{
    public string Name => "Invalid Line Instruction";

    public ValueTask<RuleDecision> ExecuteAsync(
        UnitInfo unit,
        RuleContext rule,
        TickContext tick,
        ActiveFollowUpState? active,
        CancellationToken ct)
    {
        return SimpleViolationRule.Decide(rule);
    }
}
