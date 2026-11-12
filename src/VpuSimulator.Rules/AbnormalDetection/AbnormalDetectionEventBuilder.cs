using System;
using System.Collections.Generic;
using System.Text;
using VpuSimulator.Domain;
using VpuSimulator.Domain.Entities;
using VpuSimulator.Domain.Events;
using VpuSimulator.Domain.RuleContracts;

namespace VpuSimulator.Rules.AbnormalDetection;

public sealed class AbnormalDetectionEventBuilder : IEventPayloadBuilder
{
    private const int IncidentRunning = 16777216;
    private const int IncidentLoitering = 33554432;
    private const int ObjectTypePerson = 1;
    private const int DefaultLoiteringWindowMs = 5000;

    public BuildResult Build(UnitInfo unit, RuleContext rule, TickContext tick, RuleDecision decision, ActiveFollowUpState? active)
    {
        var rng = tick.Rng;
        var activities = AbnormalActivities.Read(rule.RawParams);
        var chosen = activities[rng.Next(activities.Count)];    // chọn đều một hoạt động
        var (incidentType, windowMs) = Resolve(chosen);

        var ev = new AbnormalEvent
        {
            EventKey = Guid.NewGuid().ToString(),
            RoiId = rule.RoiId,
            EventState = EventState.Lately,
            SourceId = unit.SourceId,
            SourceName = unit.SourceName,
            Task = unit.TaskId,
            StartTime = tick.Now.AddMilliseconds(-windowMs),
            EndTime = tick.Now,
            ObjectType = ObjectTypePerson,
            ObjectLabel = "person",
            ObjectSize = null,
            Bbox = Bbox.Zero,
            Prob = rng.NextDouble(),
            VideoUrl = "",
            IncidentType = incidentType,
        };
        return new BuildResult(ev, Array.Empty<StateProposal>());
    }

    private static (int IncidentType, int WindowMs) Resolve(AbnormalActivity a)
    {
        return a.Name switch
        {
            "motion" => (IncidentRunning, 5000),
            "loitering" => (IncidentLoitering, a.MinIntervalMs ?? DefaultLoiteringWindowMs),
            _ => (0, 100),           // activity không xác định : không phân loại được
        };
    }
}
