using VpuSimulator.Domain;
using VpuSimulator.Domain.Entities;
using VpuSimulator.Domain.RuleContracts;
using VpuSimulator.Rules.Density;

namespace VpuSimulator.Rules.TrafficMetering;

// #3: "Traffic Metering". Phát đúng mỗi reset_timer giây (FixedPeriod), bỏ qua profile
public sealed class TrafficMeteringRuleHandler : IRuleHandler
{
    public string Name => "Traffic Metering";

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