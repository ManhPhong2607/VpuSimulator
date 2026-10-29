using System;

namespace VpuSimulator.Domain;

public abstract class RuleOutputEvent
{
    public required string EventKey { get; init; }
    public required EventState EventState { get; init; }
    public required string SourceId { get; init; }
    public required string SourceName { get; init; }
    public required string Task { get; init; }
    public required DateTimeOffset StartTime { get; init; }
    public DateTimeOffset? EndTime { get; init; }
    public required int RoiId { get; init; }
    public int Class { get; init; } = 1;
    public int Version { get; init; } = 2;
    public abstract int Type { get; }
}