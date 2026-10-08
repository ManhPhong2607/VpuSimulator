namespace VpuSimulator.Domain;

public enum EventState
{
    Idle = 0,
    Early = 1,
    Lately = 2,
    Confirmed = 3,
    Discarded = 4,
    Deactivate = 5,
    Suspect = 6,
    Unknown = 7
}