using VpuSimulator.Domain;
using VpuSimulator.Domain.Entities;
using VpuSimulator.Domain.RuleContracts;

namespace VpuSimulator.Rules.PersonRecognition;

//  #2: "Person Recognition", type 4. affect_object phải chứa label "person" (SRS 6.2.3.1), nếu không thì Precondition. Dùng AttributeRecognitionBuilder("person", false).</summary>
public sealed class PersonRecognitionRuleHandler : IRuleHandler
{
    public string Name => "Person Recognition";

    public ValueTask<RuleDecision> ExecuteAsync(UnitInfo unit, RuleContext rule, TickContext tick,
                                                ActiveFollowUpState? active, CancellationToken ct) =>
        ValueTask.FromResult(new RuleDecision(
            rule.AffectObject?.Any(a => a.Label == "person") == true ? RuleOutcome.Emitted : RuleOutcome.Precondition));
}