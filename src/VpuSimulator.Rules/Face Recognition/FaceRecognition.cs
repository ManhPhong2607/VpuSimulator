using VpuSimulator.Domain;
using VpuSimulator.Domain.Entities;
using VpuSimulator.Domain.RuleContracts;

namespace VpuSimulator.Rules.FaceRecognition;

// #18: "Face Recognition", type 24. SRS không nói label bắt buộc nên chỉ đòi affect_object không rỗng.
// Dùng AttributeRecognitionBuilder("face", true): FR-GEN-5 cho phép cố định text định danh.
public sealed class FaceRecognitionRuleHandler : IRuleHandler
{
    public string Name => "Face Recognition";

    public ValueTask<RuleDecision> ExecuteAsync(UnitInfo unit, RuleContext rule, TickContext tick,
                                                ActiveFollowUpState? active, CancellationToken ct) =>
        ValueTask.FromResult(new RuleDecision(
            rule.AffectObject != null && rule.AffectObject.Count > 0
            ? RuleOutcome.Emitted
            : RuleOutcome.Precondition));
}