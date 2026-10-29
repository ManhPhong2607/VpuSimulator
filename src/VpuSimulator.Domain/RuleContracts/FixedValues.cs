namespace VpuSimulator.Domain.RuleContracts
{
    public sealed record FixedValues(
        bool Enabled = false,
        string? ObjectLabel = null,   // cố định object_label (License Plate, violation đơn giản)
        string? PlateText = null,     // cố định text định danh: biển số (License Plate) hoặc text khuôn mặt (Face Recognition)
        string? RefId = null)         // cố định ref_id (violation, Traffic Signal Violation ghi đè cả correlation)
    {
        public static readonly FixedValues Off = new();
    }
}
