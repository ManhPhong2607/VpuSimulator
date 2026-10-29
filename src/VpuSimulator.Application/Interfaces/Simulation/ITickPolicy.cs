using VpuSimulator.Domain;
using VpuSimulator.Domain.Entities;

namespace VpuSimulator.Application.Interfaces.Simulation;

// Quyết định bao lâu nữa thì unit này phát sự kiện tiếp theo
public interface ITickPolicy
{
    TimeSpan NextInterval(UnitInfo unit, RuleContext context);
}
