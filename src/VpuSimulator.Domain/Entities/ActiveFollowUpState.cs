namespace VpuSimulator.Domain;

public sealed class ActiveFollowUpState
{
    public required string EventKey { get; set; }
    public required string Label { get; set; }
    public required int ObjectType { get; set; }
    public required int Tracker { get; set; }
    public string? RefId { get; set; }
}