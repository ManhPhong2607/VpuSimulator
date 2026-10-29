using System;
using System.Collections.Generic;
using System.Text;

namespace VpuSimulator.Domain
{
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
}
