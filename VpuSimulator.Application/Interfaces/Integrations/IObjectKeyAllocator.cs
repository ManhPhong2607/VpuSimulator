using VpuSimulator.Domain;

namespace VpuSimulator.Application.Interfaces;

public interface IObjectKeyAllocator
{
    string Allocate(RuleOutputEvent output);
}
