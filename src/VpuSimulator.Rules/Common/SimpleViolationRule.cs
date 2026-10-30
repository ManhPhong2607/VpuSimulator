using VpuSimulator.Domain;
using VpuSimulator.Domain.Entities;
using VpuSimulator.Domain.Events;
using VpuSimulator.Domain.RuleContracts;
using VpuSimulator.Domain.ValueObjects;

namespace VpuSimulator.Rules.Common;

// Khuôn chung "violation đơn giản" : Wrong Way, INTRUSION, Invalid Line Instruction, CROSSLINE mặc định
// và nhánh sự kiện mới của Illegal Stop. Chỉ khác nhau ở vio_type_str. Không correlation.
public static class SimpleViolationRule
{
    // Quyết định chung: affect_object rỗng thì Precondition. Không rút số ngẫu nhiên.
    public static ValueTask<RuleDecision> Decide(RuleContext rule) =>
        ValueTask.FromResult(new RuleDecision(
            rule.AffectObject != null && rule.AffectObject.Count > 0
                    ? RuleOutcome.Emitted
                    : RuleOutcome.Precondition));

    public static ViolationEvent BuildNewEvent(UnitInfo unit, RuleContext rule, TickContext tick, string vioTypeStr, string label)
    {
        var rng = tick.Rng;
        var fixedValues = tick.FixedValues;
        var refId = fixedValues.Enabled
            ? fixedValues.RefId
            : null;
        return new ViolationEvent
        {
            EventKey = Guid.NewGuid().ToString(),
            RoiId = rule.RoiId,
            EventState = EventState.Lately,
            SourceId = unit.SourceId,
            SourceName = unit.SourceName,
            Task = unit.TaskId,
            StartTime = tick.Now.AddMilliseconds(-100),
            EndTime = tick.Now,
            ObjectType = 0,
            ObjectLabel = label,
            Tracker = rng.Next(1, 5000),
            Bbox = BboxGenerator.Main(rng,unit.FrameWidth,unit.FrameHeight),
            Attributes = BuildIdentity(tick),
            RefId = refId ?? "",
            State = 3,
            VioTypeStr = vioTypeStr,
            DetailsType = 4,
        };
    }

    // Rule đơn giản không đọc biển số: value rỗng, prob 0, bbox null; chỉ khi bật fixed values (FR-GEN-5) mới điền text cố định.
    public static AttributeItem BuildIdentity(TickContext tick)
    {
        var fixedValues = tick.FixedValues;
        var text = fixedValues.Enabled
            ? fixedValues.PlateText
            : null;

        return new AttributeItem(
            Value: text ?? "",
            Prob: 0.0,
            BBox: null);
    }
}

    public sealed class SimpleViolationBuilder : IEventPayloadBuilder
{
    private readonly string _vioTypeStr;
    public SimpleViolationBuilder(string vioTypeStr) => _vioTypeStr = vioTypeStr;

    public BuildResult Build(UnitInfo unit, RuleContext rule, TickContext tick, RuleDecision decision, ActiveFollowUpState? active)
    {
        var label = LabelPicker.Pick(rule, tick)
            ?? throw new InvalidOperationException("affect_object rỗng: ExecuteAsync lẽ ra đã trả Precondition");
        return new BuildResult(SimpleViolationRule.BuildNewEvent(unit, rule, tick, _vioTypeStr, label), Array.Empty<StateProposal>());
    }
}