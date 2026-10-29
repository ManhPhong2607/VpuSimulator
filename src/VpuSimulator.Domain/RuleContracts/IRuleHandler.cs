using System.Threading;

namespace VpuSimulator.Domain;

public interface IRuleHandler
{
    string Name { get; }
    RuleDecision Execute(EmissionUnit unit, RuleContext context, CancellationToken cancellationToken = default);
}
