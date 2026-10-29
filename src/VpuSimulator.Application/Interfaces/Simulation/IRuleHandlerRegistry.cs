using VpuSimulator.Domain;
using VpuSimulator.Domain.RuleContracts;

namespace VpuSimulator.Application.Interfaces.Simulation;

// Registry tra cứu handler xử lý nghiệp vụ theo tên rule_conf.name
public interface IRuleHandlerRegistry
{
    IRuleHandler? GetHandler(string ruleName);
}
