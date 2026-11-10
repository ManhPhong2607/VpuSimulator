namespace VpuSimulator.Application.Configuration;

/// <summary>simulation.seed. 0 = ngẫu nhiên, không đổi được lúc runtime (FR-CFG-7)</summary>
public sealed class SimulationOptions
{
    public long Seed { get; set; }
}