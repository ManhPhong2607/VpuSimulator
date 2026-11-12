using VpuSimulator.Domain.RuleContracts;

namespace VpuSimulator.Domain;

public sealed record RuleDefinition(
    IRuleHandler Handler,
    IEventPayloadBuilder Builder,
    int? EmittedGroup) // 1: Density, 2: Violation, 3: Recognition, 4: Abnormal, null: never emits (Traffic Light)
{
    public string Name => Handler.Name;
}