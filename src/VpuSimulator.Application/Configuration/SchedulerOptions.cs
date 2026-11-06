using VpuSimulator.Application.Scheduling;

namespace VpuSimulator.Application.Configuration;

/// <summary>scheduler.arm_delay / default_profile (SRS Phụ lục A)</summary>
public sealed class SchedulerOptions
{
    public string ArmDelay { get; set; } = "5s";
    public string DefaultProfile { get; set; } = SimulationProfiles.DefaultName;
}