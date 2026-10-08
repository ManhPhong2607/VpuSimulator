using System.Collections.Generic;

namespace VpuSimulator.Domain;

public sealed class RecognitionOutput : RuleOutputEvent
{
    public override int Type => 3;
    public int EventType { get; init; } = 3;
    public required int ObjectType { get; init; }
    public required string ObjectLabel { get; init; }
    public required int Tracker { get; init; }
    public required Bbox Bbox { get; init; }
    public string? ImageUrl { get; init; }
    public string? ObjectImageUrl { get; init; }
    public string? ObjectCropUrl { get; init; }
    public string? VideoUrl { get; init; }
    public double? AccuracyRatio { get; init; } = 0;
    public int? AiFlowGroupType { get; init; } = 0;
    public string? EventTypeString { get; init; }
    public string? ProfileObjectId { get; init; }
    public IReadOnlyList<object>? Groups { get; init; }
    public object? Attributes { get; init; }
}