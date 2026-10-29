using VpuSimulator.Domain;

namespace VpuSimulator.Application.Interfaces.Simulation;

public interface IRuleMetadataRegistry
{
    bool TryResolve(string ruleName, out AiFlowCode aiFlowCode);
}