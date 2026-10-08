using System;
using VpuSimulator.Domain;

namespace VpuSimulator.Application.Interfaces;

// Quyết định bao lâu nữa thì unit này phát sự kiện tiếp theo
public interface ITickPolicy
{
    TimeSpan NextInterval(EmissionUnit unit, RuleContext context);
}
