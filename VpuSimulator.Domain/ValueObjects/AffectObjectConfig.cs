using System.Collections.Generic;

namespace VpuSimulator.Domain;

public sealed record AffectObjectConfig(
    string Label,
    double OverlapRatio,
    IReadOnlyList<double> DimRatio,
    int MinArea,
    IReadOnlyList<string> Movement,
    int Direction);
