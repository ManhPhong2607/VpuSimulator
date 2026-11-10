using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using VpuSimulator.Domain;
using VpuSimulator.Domain.Entities;
using VpuSimulator.Domain.RuleContracts;

namespace VpuSimulator.Rules.AbnormalDetection;

public sealed record AbnormalActivity(string Name, int? MinIntervalMs);

public static class AbnormalActivities
{
    public static IReadOnlyList<AbnormalActivity> Read(JsonElement rawParams)
    {
        var result = new List<AbnormalActivity>();
        if(rawParams.ValueKind != JsonValueKind.Object)
            return result;
        if(!rawParams.TryGetProperty("activities", out var arr) || arr.ValueKind != JsonValueKind.Array)
            return result;

        foreach (var item in arr.EnumerateArray())
        {
            var name = item.TryGetProperty("activity", out var a)
                ? a.GetString() ?? "" : "";
            int? minInterval = item.TryGetProperty("min_interval", out var mi)
                && mi.TryGetInt32(out var v) ? v : null;
            result.Add(new AbnormalActivity(name, minInterval));
        }
        return result;
    }
}

// #20: "Abnormal Detection". Cần ít nhất một activity trong cấu hình, nếu không thì Precondition
public sealed class AbnormalDetectionRuleHandler : IRuleHandler
{
    public string Name => "Abnormal Detection";
    public ValueTask<RuleDecision> ExecuteAsync(
        UnitInfo unit,
        RuleContext rule,
        TickContext tick,
        ActiveFollowUpState? active,
        CancellationToken ct)
    {
        var activities = AbnormalActivities.Read(rule.RawParams);
        return ValueTask.FromResult(new RuleDecision(
            activities.Count > 0 ? RuleOutcome.Emitted : RuleOutcome.Precondition));
    }
}


