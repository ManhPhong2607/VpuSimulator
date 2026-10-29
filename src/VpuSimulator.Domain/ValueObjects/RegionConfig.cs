namespace VpuSimulator.Domain;

public sealed record RegionConfig(
    string Path,
    string? Head,
    TargetDims TargetDims,
    string Anchor);
