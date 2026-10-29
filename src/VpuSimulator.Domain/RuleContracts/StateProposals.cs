namespace VpuSimulator.Domain.RuleContracts;

// Tập đóng các thay đổi trạng thái. Chỉ được áp qua UnitRunner.TryCommit sau khi publish
public abstract record StateProposal;

public sealed record SetActiveFollowUp(ActiveFollowUpState State) : StateProposal;

public sealed record ClearActiveFollowUp : StateProposal;

public sealed record WriteCorrelation(string Key, object Value) : StateProposal;

