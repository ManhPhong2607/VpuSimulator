namespace VpuSimulator.Domain.Entities;

public sealed record UnitInfo(
    string AppId,
    string TaskId, 
    string SourceId, 
    string SourceName,
    int RoiId, 
    int FrameWidth, 
    int FrameHeight
);

