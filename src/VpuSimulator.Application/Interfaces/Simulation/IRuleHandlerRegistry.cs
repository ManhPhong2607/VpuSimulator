using VpuSimulator.Domain;
using VpuSimulator.Domain.Enums;
using VpuSimulator.Domain.RuleContracts;

namespace VpuSimulator.Application.Interfaces.Simulation;

// Registry tra cứu handler xử lý nghiệp vụ theo tên rule_conf.name và aiFlowCode
public interface IRuleHandlerRegistry
{
    IRuleHandler? Resolve(string name, AiFlowCode aiFlowCode);
    IRuleHandler? GetHandler(string ruleName) => Resolve(ruleName, default);
    IReadOnlyCollection<string> SupportedNames { get; }
}
