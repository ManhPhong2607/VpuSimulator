namespace VpuSimulator.Domain;

public enum AiFlowCode
{
    VehicleRecognition = 0,
    HumanRecognition = 2,
    DrivingAgainstTraffic = 4,
    RunningRedLight = 5,
    IllegalParking = 6,
    Speeding = 7,
    LaneChangeViolation = 8,
    DrivingWrongLane = 9,
    EnteringProhibitedArea = 10,
    ViolationOfLaneMarkings = 11,
    TrespassingRestrictedAreas = 14,
    LaneIntrusionAndObstacleDetection = 16,
    CrossingVirtualFence = 20,
    AbnormalStop = 21,
    BasicTrafficDensity = 23,
    FireSmokeDetection = 26,
    AdvancedTrafficDensity = 33,
    AbnormalActivity = 35,
    CrossingSolidLine = 36,
    StoppingOnYellowBoxJunction = 37,
    IllegalRacingDangerousZigzagDriving = 38
}