namespace VpuSimulator.Domain;

public interface IEventPayloadBuilder
{
    AiFlowTypePayload AiFlowTypePayload { get; }

    RuleOutputEvent Build(
        EmissionUnit unit,
        RuleContext context,
        ICorrelationStore correlation,
        FollowUpDecision? followUpDecision);
}
