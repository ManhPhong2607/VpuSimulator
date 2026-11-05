using System;
using System.Collections.Generic;
using System.Text;
using VpuSimulator.Domain;
using VpuSimulator.Domain.Entities;
using VpuSimulator.Domain.Events;
using VpuSimulator.Domain.RuleContracts;

namespace VpuSimulator.Rules.DisasterDetection;

public sealed class DisasterDetectionEventBuilder : IEventPayloadBuilder
{
    private const int IncidentFire = 262144;
    private const int IncidentSmoke = 524288;
    private const int ObjectTypeDisaster = 1;
    private const int Windows = 3000;

    public BuildResult Build(UnitInfo unit, RuleContext rule, TickContext tick, RuleDecision decision, ActiveFollowUpState? active)
    {
        var rng = tick.Rng;
        var objs = rule.AffectObject ?? Array.Empty<AffectObjectConfig>();
        var label = objs[rng.Next(objs.Count)].Label;

        var ev = new AbnormalEvent
        {
            EventKey = Guid.NewGuid().ToString(),
            RoiId = rule.RoiId,
            EventState = EventState.Lately,
            SourceId = unit.SourceId,
            SourceName = unit.SourceName,
            Task = unit.TaskId,
            StartTime = tick.Now.AddMilliseconds(-Windows),
            EndTime = tick.Now,
            ObjectType = ObjectTypeDisaster,
            ObjectLabel = label,
            ObjectSize = null,
            Bbox = Bbox.Zero,
            Prob = rng.NextDouble(),
            VideoUrl = "",
            IncidentType = MapInCidentType(label),
        };

        return new BuildResult(ev, Array.Empty<StateProposal>());
    }

    public static int MapInCidentType(string label)
    {
        return label switch
        {
            "fire" => IncidentFire,
            "smoke" => IncidentSmoke,
            _ => 0,
        };
    }
}
