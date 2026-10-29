namespace VpuSimulator.Domain.Entities;

// Bất biến. Chỉ đổi bằng cách thay cả bản ghi, qua đề xuất SetActiveFollowUp/ClearActiveFollowUp
public sealed record ActiveFollowUpState(
    string EventKey,
    string Label,
    int ObjectType,
    int Tracker,
    string? RefId);