using System;
using System.Collections.Generic;
using System.Text;

namespace VpuSimulator.Domain
{
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
}
