using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using VpuSimulator.Domain;
using VpuSimulator.Domain.Events;
using VpuSimulator.Domain.RuleContracts;

namespace VpuSimulator.Rules.Density;

// Phần dùng chung của hai rule mật độ: Traffic Metering (#3) và TRAFFIC FLOW (#4). Chỉ khác Name.
// Quyết định ở handler, dựng sự kiện ở DensityEventBuilder, công thức tổng hợp ở ComputeAggregate.
public static class DensityRuleCore
{
    public static readonly IReadOnlyDictionary<string, int> ValidLabels = new Dictionary<string, int>
    {
        ["bus"] = 0,
        ["car"] = 1,
        ["motorbike"] = 2,
        ["truck"] = 3,
    };

    // Nhãn hợp lệ trong affect_object (loại trùng). Nhãn lạ như "bicycle" bị loại.
    public static IReadOnlyList<string> ValidAffectLabels(RuleContext rule)
        => (rule.AffectObject ?? Array.Empty<AffectObjectConfig>())
        .Select(a => a.Label).Where(ValidLabels.ContainsKey).Distinct().ToList();

    // Không có nhãn hợp lệ thì Precondition (FR-GEN-7). Không rút số ngẫu nhiên.
    public static ValueTask<RuleDecision> Decide(RuleContext rule)
        => ValueTask.FromResult(new RuleDecision(ValidAffectLabels(rule).Count > 0 
            ? RuleOutcome.Emitted
            : RuleOutcome.Precondition));

    // rule_conf.reset_timer, số nguyên giây (DR-2). Thiếu hoặc ≤ 0 thì 0.
    public static int ReadResetTimerSeconds(JsonElement raw) =>
        raw.ValueKind == JsonValueKind.Object
        && raw.TryGetProperty("reset_timer", out var rt)
        && rt.TryGetInt32(out var seconds)
        && seconds > 0
            ? seconds
            : 0;

    // Chu kỳ phát cố định = reset_timer (FR-SIM-13): rule density bỏ qua profile. null = theo profile.
    public static TimeSpan? FixedPeriod(RuleContext rule)
    {
        var seconds = ReadResetTimerSeconds(rule.RawParams);
        return seconds > 0 ? TimeSpan.FromSeconds(seconds) : null;
    }
    // Công thức tổng hợp (SRS 6.4.4): total = Σ volume; avg_speed = trung bình có trọng số volume; occupancy = total/200*100.
    public static DensityEvent ComputeAggregate(
        IReadOnlyList<DensityDataPoint> points, int roiId, string eventKey,
        DateTimeOffset startTime, DateTimeOffset endTime,
        string sourceId = "test-source", string sourceName = "test-source-name", string task = "test-task")
    {
        var total = points.Sum(p => p.Volume);
        var weightedAvgSpeed = total == 0 ? 0.0 : points.Sum(p => p.AvgSpeed * p.Volume) / total;
        var avgOccupancy = total / 200.0 * 100.0;

        return new DensityEvent
        {
            EventKey = eventKey,
            RoiId = roiId,
            EventState = EventState.Lately,
            SourceId = sourceId,
            SourceName = sourceName,
            Task = task,
            StartTime = startTime,
            EndTime = endTime,
            Data = points,
            Total = total,
            AvgSpeed = Math.Round(weightedAvgSpeed, 1),
            AvgOccupancy = Math.Round(avgOccupancy, 1),
            PartialData = Array.Empty<object>(),      // SRS ghi luôn rỗng (mâu thuẫn với AI_ModelPayloads.md, chưa chốt)
        };
    }
}

