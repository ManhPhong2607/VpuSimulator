using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using VpuSimulator.Domain;
using VpuSimulator.Domain.Entities;
using VpuSimulator.Domain.Events;
using VpuSimulator.Domain.RuleContracts;

namespace VpuSimulator.Rules.CrowdingDetection;

public sealed class CrowdingDetectionEventBuilder : IEventPayloadBuilder
{
    private const int IncidentCrowding = 131072;
    private const int DefaultMinIntervalMs = 300_000;
    private const int DefaultObjectCount = 2;
    private const int ObjectTypePerson = 1;      // SRS không nêu object_type cho rule này: giả định như các rule abnormal khác (người)

    public BuildResult Build(UnitInfo unit, RuleContext rule, TickContext tick, RuleDecision decision, ActiveFollowUpState? active)
    {
        var rng = tick.Rng;
        var windowMs = PositiveIntOrDefault(rule.RawParams, "min_interval", DefaultMinIntervalMs);
        var objectCount = PositiveIntOrDefault(rule.RawParams, "object_count", DefaultObjectCount);

        // Thứ tự rút số: crowd_id, crowd_size, rồi prob.
        var crowdId = rng.Next(1, 100_000);                                   // [1, 99999]
        var crowdSize = rng.Next(objectCount, objectCount + Math.Max(1, objectCount));   // [n, n + max(1,n) - 1]

        var ev = new AbnormalEvent
        {
            EventKey = Guid.NewGuid().ToString(),
            RoiId = rule.RoiId,
            EventState = EventState.Lately,                                   // giả định, giống các rule abnormal khác
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
            IncidentType = IncidentCrowding,
            CrowdDetails = new Dictionary<string, int> { ["crowd_id"] = crowdId, ["crowd_size"] = crowdSize },
        };
        return new BuildResult(ev, Array.Empty<StateProposal>());
    }

    private static int PositiveIntOrDefault(JsonElement raw, string property, int fallback) =>
     raw.ValueKind == JsonValueKind.Object
     && raw.TryGetProperty(property, out var value)
     && value.ValueKind == JsonValueKind.Number
     && value.TryGetInt32(out var number)
     && number > 0
         ? number
         : fallback;
}
