using VpuSimulator.Domain;

namespace VpuSimulator.Application.Interfaces;

public interface IRuleMetadataRegistry
{
    bool TryResolve(string ruleName, out AiFlowCode aiFlowCode);
}