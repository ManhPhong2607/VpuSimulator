using VpuSimulator.Domain;
using VpuSimulator.Domain.Enums;
using VpuSimulator.Domain.RuleContracts;

namespace VpuSimulator.Application.Interfaces.Simulation;

// Registry tra cứu định nghĩa rule (Handler + Builder + EmittedGroup) theo tên rule_conf.name và aiFlowCode
public interface IRuleHandlerRegistry
{
    RuleDefinition? Resolve(string name, AiFlowCode? aiFlowCode);
    IRuleHandler? GetHandler(string ruleName) => Resolve(ruleName, null)?.Handler;
    IReadOnlyCollection<string> SupportedNames { get; }
}
