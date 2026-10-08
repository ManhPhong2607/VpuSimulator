namespace VpuSimulator.Domain;

public sealed class AbnormalOutput :  RuleOutputEvent
{
    public override int Type => 4;
    public int EventType { get; init; } = 4;
    public required int ObjectType { get; init; }
    public required string ObjectLabel { get; init; }
    public int? ObjectSize { get; init; }
    public Bbox Bbox { get; init; } = new Bbox(0, 0, 0, 0);
    public double? Prob { get; init; }
    public string? ImageUrl { get; init; }
    public string? VideoUrl { get; init; }
    public int? IncidentType { get; init; }
    public int? RefId { get; init; }
    public int? State { get; init; }
    public object? CrowdDetails { get; init; }
}