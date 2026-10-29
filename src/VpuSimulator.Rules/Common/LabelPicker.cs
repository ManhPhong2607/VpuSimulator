using VpuSimulator.Domain;

namespace VpuSimulator.Rules.Common;

public static class LabelPicker
{
    // Nhãn: cố định nếu bật, ngược lại chọn đều trong affect_object. null nếu affect_object rỗng.
    public static string? Pick(RuleContext rule, TickContext tick)
    {
        var objs = rule.AffectObject ?? Array.Empty<AffectObjectConfig>();
        if (objs.Count == 0) return null;
        if (tick.FixedValues is { Enabled: true, ObjectLabel: { Length: > 0 } fixedLabel }) return fixedLabel;
        return objs[tick.Rng.Next(objs.Count)].Label;
    }
}