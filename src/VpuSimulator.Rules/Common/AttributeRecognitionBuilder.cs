using VpuSimulator.Domain;
using VpuSimulator.Domain.Entities;
using VpuSimulator.Domain.Events;
using VpuSimulator.Domain.RuleContracts;
using VpuSimulator.Domain.ValueObjects;

namespace VpuSimulator.Rules.Common;

// Nhận diện người hoặc khuôn mặt: object_type 1, nhãn cố định, identity rỗng (khuôn mặt có thể cố định text theo FR-GEN-5)
public sealed class AttributeRecognitionBuilder : IEventPayloadBuilder
{
    private readonly string _label;
    private readonly bool _fixedTextAllowed;
    public AttributeRecognitionBuilder(string label, bool fixedTextAllowed) { _label = label; _fixedTextAllowed = fixedTextAllowed; }

    public BuildResult Build(UnitInfo unit, RuleContext rule, TickContext tick, RuleDecision decision, ActiveFollowUpState? active)
    {
        var rng = tick.Rng;
        var mainBox = BboxGenerator.Main(rng, unit.FrameWidth, unit.FrameHeight);
        var plateText = tick.FixedValues.PlateText;

        var text = _fixedTextAllowed
                   && tick.FixedValues.Enabled
                   && plateText != null
            ? plateText
            : "";
        var identity = new AttributeItem(text, Math.Round(rng.NextDouble(), 2), BboxGenerator.Identity(rng, mainBox));

        var ev = new RecognitionEvent
        {
            EventKey = Guid.NewGuid().ToString(),
            RoiId = rule.RoiId,
            EventState = EventState.Lately,                          
            SourceId = unit.SourceId,
            SourceName = unit.SourceName,
            Task = unit.TaskId,
            StartTime = tick.Now.AddMilliseconds(-100),            
            EndTime = tick.Now,
            ObjectType = 1,
            ObjectLabel = _label,
            Tracker = rng.Next(1, 5000),
            Bbox = mainBox,
            Attributes = identity,
        };
        return new BuildResult(ev, Array.Empty<StateProposal>());
    }
}