using VpuSimulator.Domain.Entities;

namespace VpuSimulator.Domain.RuleContracts;

public interface IEventPayloadBuilder
{
    // Hàm gần như thuần: ngoại lệ duy nhất là chiếm correlation bằng ICorrelationStore.TryDelete (nguyên tử).
    BuildResult Build(
        UnitInfo unit, 
        RuleContext rule, 
        TickContext tick,
        RuleDecision decision,
        ActiveFollowUpState? active);
}
