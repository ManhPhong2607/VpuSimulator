using System.Collections.Generic;

namespace VpuSimulator.Domain;

public sealed class ViolationOutput : RuleOutputEvent
{
    public override int Type => 2;
    public int EventType { get; init; } = 2;
    public required int DetailsType { get; init; }
    public required int ObjectType { get; init; }
    public required string ObjectLabel { get; init; }
    public required int Tracker { get; init; }
    public required Bbox Bbox { get; init; }
    public string? ImageUrl { get; init; }
    public string? VideoUrl { get; init; }
    public IReadOnlyList<string>? ImageCtxtUrl { get; init; }
    public object? Attributes { get; init; }
    public string? RefId { get; init; }
    public int? State { get; init; }
    public string? VioTypeStr { get; init; }
}