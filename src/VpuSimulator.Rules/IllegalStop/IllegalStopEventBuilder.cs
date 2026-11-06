using System;
using System.Collections.Generic;
using System.Text;
using VpuSimulator.Domain;
using VpuSimulator.Domain.Entities;
using VpuSimulator.Domain.Events;
using VpuSimulator.Domain.FollowUp;
using VpuSimulator.Domain.RuleContracts;
using VpuSimulator.Rules.Common;

namespace VpuSimulator.Rules.IllegalStop;

public sealed class IllegalStopEventBuilder : IEventPayloadBuilder
{
    private const string VioTypeStr = "ILLEGALSTOP";

    public BuildResult Build(UnitInfo unit, RuleContext rule, TickContext tick, RuleDecision decision, ActiveFollowUpState? active)
    {
        if (decision.FollowUp == FollowUpKind.Deactivate && active is not null)
        {
            var close = new ViolationEvent
            {
                EventKey = active.EventKey,                          // đóng đúng sự kiện đang mở
                RoiId = rule.RoiId,
                EventState = EventState.Early,
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
                Attributes = null,                                   // follow-up: attributes null
                RefId = active.RefId,
                State = 5,
                VioTypeStr = VioTypeStr,
                DetailsType = 4,                                     // không đổi (khác #6)
                FollowUpKind = FollowUpKind.Deactivate,
            };
            return new BuildResult(close, new StateProposal[] { new ClearActiveFollowUp() });
        }

        var label = LabelPicker.Pick(rule, tick)
            ?? throw new InvalidOperationException("affect_object rỗng: ExecuteAsync lẽ ra đã trả Precondition");
        var ev = SimpleViolationRule.BuildNewEvent(unit, rule, tick, VioTypeStr, label);
        var state = new ActiveFollowUpState(ev.EventKey, label, ev.ObjectType, ev.Tracker, ev.RefId);
        return new BuildResult(ev, new StateProposal[] { new SetActiveFollowUp(state) });
    }
}
