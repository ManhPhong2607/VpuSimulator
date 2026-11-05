using VpuSimulator.Domain;
using VpuSimulator.Domain.Entities;
using VpuSimulator.Domain.RuleContracts;
using VpuSimulator.Rules.Density;

namespace VpuSimulator.Rules.TrafficFlow;

// #4: "TRAFFIC FLOW". Cùng khuôn với Traffic Metering. sub_regions bị bỏ qua (partial_data luôn rỗng)
public sealed class TrafficFlowRuleHandler : IRuleHandler
{
    public string Name => "TRAFFIC FLOW";

    public TimeSpan? FixedPeriod(RuleContext rule) => DensityRuleCore.FixedPeriod(rule);

    public ValueTask<RuleDecision> ExecuteAsync(
        UnitInfo unit, 
        RuleContext rule, 
        TickContext tick,
        ActiveFollowUpState? active, 
        CancellationToken ct)
    {
        return DensityRuleCore.Decide(rule);
    }
}