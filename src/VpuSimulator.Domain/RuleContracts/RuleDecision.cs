using VpuSimulator.Domain.Events;
using VpuSimulator.Domain.FollowUp;
using VpuSimulator.Domain.RuleContracts;

namespace VpuSimulator.Domain;

// Kết quả trả về từ IRuleHandler.Execute() - Thuần quyết định, không mang Output
// Quyết định của handler: có phát không và có phải follow-up không. Không chứa sự kiện.
public sealed record RuleDecision(
    RuleOutcome Outcome, 
    FollowUpKind? FollowUp = null,
    string? Error = null);

public sealed record BuildResult(
    RuleOutputEvent Event, 
    IReadOnlyList<StateProposal> Proposals
);
// Điểm nối cho chế độ model
public interface IModelOutput { }

public enum RuleOutcome { Emitted, NoMessage, Precondition, Error }
