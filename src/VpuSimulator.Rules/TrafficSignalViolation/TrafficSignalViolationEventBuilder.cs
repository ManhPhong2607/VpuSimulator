using VpuSimulator.Domain;
using VpuSimulator.Rules.Common;

namespace VpuSimulator.Rules.TrafficSignalViolation;

public sealed class TrafficSignalViolationEventBuilder : IEventPayloadBuilder
{
    private static readonly EventState[] RandomStates = { EventState.Early, EventState.Lately, EventState.Confirmed };

    public BuildResult Build(
        UnitInfo unit,
        RuleContext rule, 
        TickContext tick, 
        RuleDecision decision,
        ActiveFollowUpState? active)
    {
        return decision.FollowUp == FollowUpKind.Discard && active != null
            ? BuildDiscard(unit, rule, tick, active)
            : BuildNew(unit, rule, tick);
    }
       
    // Bản tin discard: dùng lại event_key và các field của sự kiện đang mở.
    private static BuildResult BuildDiscard(UnitInfo unit, RuleContext rule, TickContext tick, ActiveFollowUpState active)
    {
        var ev = new ViolationEvent
        {
            EventKey = active.EventKey,
            RoiId = rule.RoiId,
            EventState = EventState.Discarded,                   // header = 4
            SourceId = unit.SourceId,
            SourceName = unit.SourceName,
            Task = unit.TaskId,
            StartTime = tick.Now,
            EndTime = tick.Now,
            ObjectType = active.ObjectType,
            ObjectLabel = active.Label,
            Tracker = active.Tracker,
            Bbox = Bbox.Zero,
            ImageUrl = "",
            VideoUrl = "",
            ImageCtxtUrl = Array.Empty<string>(),
            Attributes = null,
            RefId = active.RefId,
            State = 4,                                           // details.state = discarded
            VioTypeStr = "REDLIGHT",
            DetailsType = 2,                                     // đổi thành 2 (khác #7 giữ 4)
            FollowUp = FollowUpKind.Discard,
        };
        return new BuildResult(ev, new StateProposal[] { new ClearActiveFollowUp() });
    }

    private static BuildResult BuildNew(UnitInfo unit, RuleContext rule, TickContext tick)
    {
        var rng = tick.Rng;
        var label = LabelPicker.Pick(rule, tick)
            ?? throw new InvalidOperationException("affect_object rỗng: ExecuteAsync lẽ ra đã trả Precondition");

        // Chiếm correlation nguyên tử ngay lúc build (ngoại lệ duy nhất của "builder thuần"), rồi FR-GEN-5 ghi đè kết quả.
        var (refId, plateText) = ConsumeLatestPlate(tick.Correlation, unit.AppId);

        var fixedValues = tick.FixedValues;
        if (fixedValues != null && fixedValues.Enabled)
        {
            refId = fixedValues.RefId ?? refId;
            plateText = fixedValues.PlateText ?? plateText;
        }

        var eventKey = Guid.NewGuid().ToString();
        var tracker = rng.Next(1, 5000);

        var ev = new ViolationEvent
        {
            EventKey = eventKey,
            RoiId = rule.RoiId,
            EventState = RandomStates[rng.Next(RandomStates.Length)],    // header: ngẫu nhiên {1,2,3}
            SourceId = unit.SourceId,
            SourceName = unit.SourceName,
            Task = unit.TaskId,
            StartTime = tick.Now.AddMilliseconds(-100),
            EndTime = tick.Now,
            ObjectType = 0,
            ObjectLabel = label,
            Tracker = tracker,
            Bbox = BboxGenerator.Main(rng, unit.FrameWidth, unit.FrameHeight),
            Attributes = new AttributeItem(Value: plateText, Prob: 0, BBox: null),
            RefId = refId,
            State = (int)RandomStates[rng.Next(RandomStates.Length)],    // details.state: rút riêng, độc lập với header
            VioTypeStr = "REDLIGHT",
            DetailsType = 4,
            FollowUp = null,
        };

        return new BuildResult(ev, new StateProposal[]
        {
            new SetActiveFollowUp(new ActiveFollowUpState(eventKey, label, ObjectType: 0, Tracker: tracker, RefId: refId)),
        });
    }

    // Mục mới nhất còn hạn (store tự loại mục hết hạn). TryDelete thua cuộc đua thì thử mục kế tiếp.
    private static (string? RefId, string PlateText) ConsumeLatestPlate(ICorrelationStore store, string appId)
    {
        foreach (var entry in store.GetAll(appId).Values.OfType<LicensePlateCorrelationValue>().OrderByDescending(e => e.WrittenAt))
            if (store.TryDelete(appId, entry.EventKey))
                return (entry.EventKey, entry.Plate ?? "");
        return (null, "");
    }
}