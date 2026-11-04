using VpuSimulator.Domain;
using VpuSimulator.Rules.TrafficSignalViolation;
using VpuSimulator.Rules.VehicleRecognition;

namespace VpuSimulator.Rules;

/// <summary>
/// Danh mục định nghĩa các bài toán / luật AI của simulator (kết hợp Handler + Builder + EmittedGroup).
/// Bài #1: Nhận diện biển số (Vehicle Recognition) - Nhóm 3 (Recognition).
/// Bài #6: Vi phạm đèn tín hiệu (Traffic Signal Violation) - Nhóm 2 (Violation).
/// </summary>
public static class RuleCatalog
{
    public static IReadOnlyList<RuleDefinition> All { get; } = new List<RuleDefinition>
    {
        // Bài #1: Nhận diện biển số (License Plate)
        new(
            new VehicleRecognitionRuleHandler(),
            new VehicleRecognitionEventBuilder(),
            EmittedGroup: 3
        ),

        // Bài #6: Vi phạm tín hiệu đèn giao thông (Traffic Signal Violation)
        new(
            new TrafficSignalViolationRuleHandler(),
            new TrafficSignalViolationEventBuilder(),
            EmittedGroup: 2
        ),
    };
}
