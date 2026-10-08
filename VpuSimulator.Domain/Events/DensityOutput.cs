using System;
using System.Collections.Generic;

namespace VpuSimulator.Domain;

public readonly record struct DensityDataItem(double AvgSpeed, int ClassId, string Label, int Volume);

public sealed class DensityOutput : RuleOutputEvent
{
    public override int Type => 1;
    public int EventType { get; init; } = 1;
    public required int Total { get; init; }
    public required double AvgOccupancy { get; init; }
    public required double AvgSpeed { get; init; }
    public required IReadOnlyList<DensityDataItem> Data { get; init; }
    public IReadOnlyList<object> PartialData { get; init; } = Array.Empty<object>();
}