namespace VpuSimulator.Domain;

// Kết quả trả về từ IRuleHandler.Execute() - Thuần quyết định, không mang Output
public sealed class RuleDecision
{
    public required RuleOutcome Outcome { get; init; }
    public string? ErrorMessage { get; init; }
    public FollowUpDecision? FollowUpDecision { get; init; }
}
