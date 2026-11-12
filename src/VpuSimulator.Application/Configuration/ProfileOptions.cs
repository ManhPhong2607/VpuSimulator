namespace VpuSimulator.Application.Configuration;

/// <summary>
/// Một mục trong Profiles. Mọi trường đều nullable: không khai báo nghĩa là GIỮ giá trị của profile có sẵn
/// (FR-CFG-9), không phải về 0
/// </summary>
public sealed class ProfileOptions
{
    public string? Interval { get; set; }
    public string? JitterMin { get; set; }
    public string? JitterMax { get; set; }
    public string? MinGap { get; set; }
    public bool? BypassFrames { get; set; }
    public bool? CoalesceTimestamps { get; set; }
    public double? RampFrom { get; set; }
    public double? RampTo { get; set; }
    public string? RampDuration { get; set; }
}