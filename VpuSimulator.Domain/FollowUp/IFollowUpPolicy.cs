namespace VpuSimulator.Domain;

public enum FollowUpDecision
{
    Keep,
    Deactivate,
    Discard
}

public interface IFollowUpPolicy
{
    FollowUpDecision Decide(string ruleName, ActiveFollowUpState currentState);
}
