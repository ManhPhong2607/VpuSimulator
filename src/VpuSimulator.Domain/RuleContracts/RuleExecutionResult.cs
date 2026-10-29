using VpuSimulator.Domain.Events;

namespace VpuSimulator.Domain;

public sealed class RuleExecutionResult
{
    public required RuleOutcome Outcome { get; init; }
    public RuleOutputEvent? Output { get; init; }
    public string? ErrorMessage { get; init; }
}
