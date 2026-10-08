using System;

namespace VpuSimulator.Domain;

public sealed class EmissionUnit
{
    public required string AppId { get; set; }
    public required string TaskId { get; set; }
    public required string SourceId { get; set; }
    public required string SourceName { get; set; }
    public required int RoiId { get; set; }
    public bool IsEmitting { get; set; }
    public DateTimeOffset? LastEmitAt { get; set; }
    public ActiveFollowUpState? ActiveFollowUp { get; set; }
}
