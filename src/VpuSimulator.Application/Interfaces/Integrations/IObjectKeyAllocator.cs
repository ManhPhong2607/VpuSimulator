using VpuSimulator.Domain;
using VpuSimulator.Domain.Events;

namespace VpuSimulator.Application.Interfaces;

public interface IObjectKeyAllocator
{
    string Allocate(RuleOutputEvent output);
}
