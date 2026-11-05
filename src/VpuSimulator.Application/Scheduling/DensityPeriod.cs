using System.Text.Json;
using VpuSimulator.Application.Interfaces.Simulation;
using VpuSimulator.Domain.Enums;

namespace VpuSimulator.Application.Scheduling;

//chu kỳ cố định (giây) của rule density, lấy từ reset_timer của rule_conf
public static class DensityPeriod
{
    private const int DensityGroup = 1;   // RuleDefinition.EmittedGroup: 1 = Density

    //Số giây khi rule là density và có reset_timer là số hữu hạn; ngược lại null (profile quyết định nhịp)
    // Giá trị ≤ 0 được trả nguyên: UnitRegistry sẽ chuẩn hoá về null
    public static double? Resolve(UnitConfigView view, IRuleHandlerRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(registry);

        var rule = view.Rule;
        AiFlowCode? aiFlowCode = rule.AiFlowCode is { } code && Enum.IsDefined(typeof(AiFlowCode), code)
            ? (AiFlowCode)code
            : null;

        var definition = registry.Resolve(rule.Name, aiFlowCode);
        return definition?.EmittedGroup == DensityGroup ? ReadResetTimer(rule.Raw) : null;
    }

    //Đọc reset_timer an toàn: sai kiểu, thiếu, NaN hay vô hạn đều coi là vắng mặt
    public static double? ReadResetTimer(JsonElement raw)
    {
        if (raw.ValueKind != JsonValueKind.Object
            || !raw.TryGetProperty("reset_timer", out var element)
            || element.ValueKind != JsonValueKind.Number
            || !element.TryGetDouble(out var seconds)
            || !double.IsFinite(seconds))
        {
            return null;
        }

        return seconds;
    }
}