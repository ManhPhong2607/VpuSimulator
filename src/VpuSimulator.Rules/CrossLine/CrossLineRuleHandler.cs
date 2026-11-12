using VpuSimulator.Domain;
using VpuSimulator.Domain.Entities;
using VpuSimulator.Domain.Enums;
using VpuSimulator.Domain.Events;
using VpuSimulator.Domain.RuleContracts;
using VpuSimulator.Rules.Common;

namespace VpuSimulator.Rules.CrossLine;

// CROSSLINE mặc định (aiFlowCode khác 20): vi phạm đơn giản, vio_type_str = WRONGLINE (khớp bài #23, chờ xác nhận).</summary>
public sealed class CrossLineRuleHandler : IRuleHandler
{
    public string Name => "CROSSLINE";

    public ValueTask<RuleDecision> ExecuteAsync(UnitInfo unit, RuleContext rule, TickContext tick,
                                                ActiveFollowUpState? active, CancellationToken ct) =>
        SimpleViolationRule.Decide(rule);
}


//  #19: CROSSLINE + aiFlowCode 20 (CrossingVirtualFence) phát nhóm ABNORMAL (FR-GEN-3). Khai báo AiFlowCodes = { 20 }
// nên được chọn trước handler mặc định. Không phụ thuộc affect_object (nhãn cố định "face").

public sealed class CrossLineVirtualFenceRuleHandler : IRuleHandler
{
    public string Name => "CROSSLINE";
    public IReadOnlySet<AiFlowCode>? AiFlowCodes { get; } = new HashSet<AiFlowCode> { AiFlowCode.CrossingVirtualFence };

    public ValueTask<RuleDecision> ExecuteAsync(UnitInfo unit, RuleContext rule, TickContext tick,
                                                ActiveFollowUpState? active, CancellationToken ct) =>
        ValueTask.FromResult(new RuleDecision(RuleOutcome.Emitted));
}

public sealed class CrossLineVirtualFenceEventBuilder : IEventPayloadBuilder
{
    public BuildResult Build(UnitInfo unit, RuleContext rule, TickContext tick, RuleDecision decision, ActiveFollowUpState? active)
    {
        var rng = tick.Rng;
        var ev = new AbnormalEvent
        {
            EventKey = Guid.NewGuid().ToString(),
            RoiId = rule.RoiId,
            EventState = EventState.Lately,                          // giả định, giống #15 và #20
            SourceId = unit.SourceId,
            SourceName = unit.SourceName,
            Task = unit.TaskId,
            StartTime = tick.Now.AddMilliseconds(-1000),             // cửa sổ 1000 ms
            EndTime = tick.Now,
            ObjectType = 8,
            ObjectLabel = "face",
            ObjectSize = 0,
            Bbox = BboxGenerator.Main(rng, unit.FrameWidth, unit.FrameHeight),   // hộp thật, không phải {0,0,0,0}
            Prob = rng.NextDouble(),
            VideoUrl = "",
            IncidentType = null,
        };
        return new BuildResult(ev, Array.Empty<StateProposal>());
    }
}