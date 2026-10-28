using System;
using System.Collections.Generic;
using System.Text;

namespace VpuSimulator.Domain
{
    // thong tin bat bien cua 1 unit
    public sealed record UnitInfo(
        string AppId,
        string TaskId, 
        string SourceId, 
        string SourceName,
        int RoiId, 
        int FrameWidth, 
        int FrameHeight
    );

    // Bất biến. Chỉ đổi bằng cách thay cả bản ghi, qua đề xuất SetActiveFollowUp/ClearActiveFollowUp
    public sealed record ActiveFollowUpState(
        string EventKey, 
        string Label, 
        int ObjectType, 
        int Tracker, 
        string? RefId);
    public enum SimulationMode { Simulated, Model }
}
