using VpuSimulator.Domain;
using VpuSimulator.Domain.Entities;
using VpuSimulator.Domain.FollowUp;
using VpuSimulator.Domain.RuleContracts;
using VpuSimulator.Rules.AbnormalDetection;
using VpuSimulator.Rules.Common;
using VpuSimulator.Rules.CrossLine;
using VpuSimulator.Rules.Density;
using VpuSimulator.Rules.DisasterDetection;
using VpuSimulator.Rules.FaceRecognition;
using VpuSimulator.Rules.IllegalStop;
using VpuSimulator.Rules.Intrusion;
using VpuSimulator.Rules.InvalidLineInstruction;
using VpuSimulator.Rules.PersonRecognition;
using VpuSimulator.Rules.TrafficFlow;
using VpuSimulator.Rules.TrafficMetering;
using VpuSimulator.Rules.TrafficSignalViolation;
using VpuSimulator.Rules.VehicleRecognition;
using VpuSimulator.Rules.WrongWay;

namespace VpuSimulator.Rules;

/// <summary>
/// Một dòng của bảng rule. EmittedGroup: 1 density, 2 violation, 3 recognition, 4 abnormal (đúng theo SRS FR-RULE-1);
/// null = rule không bao giờ phát bản tin (Traffic Light). RuleDefinition của bạn A thay cho record này.
/// </summary>
public sealed record RuleEntry(IRuleHandler Handler, IEventPayloadBuilder Builder, int? EmittedGroup)
{
    public string Name => Handler.Name;
}

public static class RuleCatalog
{
    /// 15 dòng. CROSSLINE có hai dòng: mặc định (violation, WRONGLINE) và aiFlowCode 20 (virtual fence, abnormal).
    public static IReadOnlyList<RuleEntry> Create(IFollowUpPolicy policy) => new RuleEntry[]
    {
        // --- nhóm 1: density ---
        new(new TrafficMeteringRuleHandler(), new DensityEventBuilder(), 1),
        new(new TrafficFlowRuleHandler(), new DensityEventBuilder(), 1),

        // --- nhóm 2: violation ---
        new(new WrongWayRuleHandler(), new SimpleViolationBuilder("INVERSEDIRECTION"), 2),
        new(new IntrusionRuleHandler(), new SimpleViolationBuilder("INTRUSION"), 2),
        new(new InvalidLineInstructionRuleHandler(), new SimpleViolationBuilder("LINECROSSING"), 2),
        new(new CrossLineRuleHandler(), new SimpleViolationBuilder("WRONGLINE"), 2),
        new(new IllegalStopRuleHandler(policy), new IllegalStopEventBuilder(), 2),
        new(new TrafficSignalViolationRuleHandler(policy), new TrafficSignalViolationEventBuilder(), 2),
        new(new TrafficLightRuleHandler(), new NeverEmitsBuilder(), null),

        // --- nhóm 3: recognition ---
        new(new VehicleRecognitionRuleHandler(), new VehicleRecognitionEventBuilder(), 3),
        new(new PersonRecognitionRuleHandler(), new AttributeRecognitionBuilder("person", false), 3),
        new(new FaceRecognitionRuleHandler(), new AttributeRecognitionBuilder("face", true), 3),

        // --- nhóm 4: abnormal ---
        new(new CrossLineVirtualFenceRuleHandler(), new CrossLineVirtualFenceEventBuilder(), 4),
        new(new DisasterDetectionRuleHandler(), new DisasterDetectionEventBuilder(), 4),
        new(new AbnormalDetectionRuleHandler(), new AbnormalDetectionEventBuilder(), 4),
    };
}

/// <summary>Traffic Light không bao giờ trả Emitted nên Build không bao giờ được gọi.</summary>
public sealed class NeverEmitsBuilder : IEventPayloadBuilder
{
    public BuildResult Build(UnitInfo unit, RuleContext rule, TickContext tick, RuleDecision decision, ActiveFollowUpState? active) =>
        throw new InvalidOperationException("Rule này không bao giờ phát bản tin.");
}