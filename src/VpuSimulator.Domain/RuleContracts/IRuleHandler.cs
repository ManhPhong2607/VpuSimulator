using VpuSimulator.Domain.Entities;
using VpuSimulator.Domain.Enums;

namespace VpuSimulator.Domain.RuleContracts;

public interface IRuleHandler
{
    string Name { get; }   
    IReadOnlySet<AiFlowCode>? AiFlowCodes => null;
    //Chu kỳ phát cố định, null = nhịp do profile quyết định.
    TimeSpan? FixedPeriod(RuleContext rule) => null;
    // Chỉ quyết định phát hay không. Số ngẫu nhiên duy nhất được rút ở đây là xác suất follow-up (nếu có).
    ValueTask<RuleDecision> ExecuteAsync(
        UnitInfo unit, 
        RuleContext rule, 
        TickContext tick,
        ActiveFollowUpState? active, 
        CancellationToken ct);  
}
