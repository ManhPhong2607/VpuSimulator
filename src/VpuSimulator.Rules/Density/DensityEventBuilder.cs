using System;
using System.Collections.Generic;
using System.Text;
using VpuSimulator.Domain;
using VpuSimulator.Domain.Entities;
using VpuSimulator.Domain.Events;
using VpuSimulator.Domain.RuleContracts;

namespace VpuSimulator.Rules.Density;

public sealed class DensityEventBuilder : IEventPayloadBuilder
{
    public BuildResult Build(UnitInfo unit, RuleContext rule, TickContext tick, RuleDecision decision, ActiveFollowUpState? active)
    {
        var rng = tick.Rng;

        // Mỗi nhãn hợp lệ một điểm dữ liệu. Thứ tự rút số: tốc độ rồi volume, từng nhãn một.
        var points = DensityRuleCore.ValidAffectLabels(rule).Select(label => new DensityDataPoint(
            AvgSpeed: Math.Round(rng.NextDouble() * 80, 1),
            ClassId: DensityRuleCore.ValidLabels[label],
            Label: label,
            Volume: rng.Next(1, 75))).ToList();

        // Cửa sổ thời gian = reset_timer giây (0 nếu thiếu), lùi một độ trễ ngẫu nhiên 0–5 s.
        var resetTimerSec = DensityRuleCore.ReadResetTimerSeconds(rule.RawParams);
        var deltaMs = rng.Next(0, 5000);
        var start = tick.Now.AddMilliseconds(-(resetTimerSec * 1000) - deltaMs);
        var end = tick.Now.AddMilliseconds(-deltaMs);

        var ev = DensityRuleCore.ComputeAggregate(points, rule.RoiId, Guid.NewGuid().ToString(), start, end,
                                                  unit.SourceId, unit.SourceName, unit.TaskId);
        return new BuildResult(ev, Array.Empty<StateProposal>());
    }
}
