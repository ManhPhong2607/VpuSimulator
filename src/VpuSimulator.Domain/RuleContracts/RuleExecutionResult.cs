namespace VpuSimulator.Domain;

public enum RuleOutcome
{
    Emitted,
    NoMessage,
    Precondition,
    Error
}

public sealed class RuleExecutionResult
{
    public required RuleOutcome Outcome { get; init; }
    public RuleOutputEvent? Output { get; init; }
    public string? ErrorMessage { get; init; }
}
