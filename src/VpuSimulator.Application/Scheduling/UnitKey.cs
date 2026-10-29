namespace VpuSimulator.Application.Scheduling;

/// <summary>
/// Khoá của một emission unit (FR-SIM-2)
/// (Abnormal Detection và Crowding Detection)
/// </summary>
public readonly record struct UnitKey(string AppId, string TaskId, int RoiId, string RuleName)
{
    public override string ToString() => $"{AppId}/{TaskId}/{RoiId}/{RuleName}";
}