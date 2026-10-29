using VpuSimulator.Domain;

namespace VpuSimulator.Rules.VehicleRecognition;

// Chỉ quyết định phát hay không: affect_object rỗng thì Precondition. Không rút số ngẫu nhiên
public sealed class VehicleRecognitionRuleHandler : IRuleHandler
{
    public string Name => "License Plate";    // rule_conf.name trên wire, không đổi theo tên bài toán

    public ValueTask<RuleDecision> ExecuteAsync(
        UnitInfo unit,
        RuleContext rule,
        TickContext tick,
        ActiveFollowUpState? active,
        CancellationToken ct
        )
    {
        return ValueTask.FromResult(new RuleDecision(
            rule.AffectObject is { Count: > 0 } ? RuleOutcome.Emitted : RuleOutcome.Precondition));
    }
}