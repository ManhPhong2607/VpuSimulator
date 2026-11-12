using System.Text.Json;
using VpuSimulator.Domain;
using VpuSimulator.Domain.Entities;
using VpuSimulator.Domain.Events;
using VpuSimulator.Domain.RuleContracts;

namespace VpuSimulator.Rules.CrowdingDetection;

// "Crowding Detection", type 67, nhóm 4 abnormal (SRS.md FR-RULE-1 và bảng sinh dữ liệu simulated).
// Không phụ thuộc affect_object: object_label cố định "person" (giả định, giống virtual fence).
// Các tham số inactivity_timeout, proximity, density: SRS không nêu ý nghĩa ở simulated nên bỏ qua.

public sealed class CrowdingDetectionRuleHandler : IRuleHandler
{
    public string Name => "Crowding Detection";

    public ValueTask<RuleDecision> ExecuteAsync(
        UnitInfo unit,
        RuleContext rule,
        TickContext tick,
        ActiveFollowUpState? active,
        CancellationToken ct)
    {
        return ValueTask.FromResult(new RuleDecision(RuleOutcome.Emitted));
    }
}

