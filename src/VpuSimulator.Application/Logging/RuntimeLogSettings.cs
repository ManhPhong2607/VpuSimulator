namespace VpuSimulator.Application.Logging;

/// <summary>
/// Trạng thái cấu hình log có thể thay đổi lúc runtime (FR-OBS-2, FR-OBS-3, FR-CFG-1).
/// </summary>
public sealed class RuntimeLogSettings
{
    private bool _verbose;

    public RuntimeLogSettings(bool verbose = false, int maxBodyLog = 4096)
    {
        _verbose = verbose;
        MaxBodyLog = maxBodyLog > 0 ? maxBodyLog : 4096;
    }

    public bool Verbose
    {
        get => Volatile.Read(ref _verbose);
        set => Volatile.Write(ref _verbose, value);
    }

    public int MaxBodyLog { get; }
}