using System.Collections.Frozen;
using VpuSimulator.Application.Interfaces.Simulation;
using VpuSimulator.Domain;
using VpuSimulator.Domain.Enums;
using VpuSimulator.Domain.RuleContracts;

namespace VpuSimulator.Infrastructure.Rules;

public sealed class RuleHandlerRegistry : IRuleHandlerRegistry
{
    private readonly FrozenDictionary<string, RuleDefinition[]> _byName;

    public RuleHandlerRegistry(IEnumerable<RuleDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        var all = definitions.ToList();

        foreach (var d in all)
        {
            ArgumentNullException.ThrowIfNull(d.Handler);
            ArgumentNullException.ThrowIfNull(d.Builder);

            if (d.EmittedGroup is < 1 or > 4)
                throw new InvalidOperationException($"'{d.Handler.Name}': EmittedGroup {d.EmittedGroup} ngoài 1..4");

            if (d.Handler.AiFlowCodes is { Count: 0 })
                throw new InvalidOperationException($"'{d.Handler.Name}': AiFlowCodes là tập rỗng, định nghĩa này không bao giờ được chọn");
        }

        _byName = all
            .GroupBy(d => d.Handler.Name, StringComparer.Ordinal)
            .ToFrozenDictionary(g => g.Key, g => g.ToArray(), StringComparer.Ordinal);
        
        // Kiểm tra lúc khởi động: không để mơ hồ, lỗi cấu hình phải lộ ra ngay
        foreach (var (name, list) in _byName)
        {
            if (list.Count(d => d.Handler.AiFlowCodes is null) > 1)
                throw new InvalidOperationException($"'{name}': có nhiều definition không ràng buộc aiFlowCode");

            var dup = list.Where(d => d.Handler.AiFlowCodes is not null)
                          .SelectMany(d => d.Handler.AiFlowCodes!)
                          .GroupBy(c => c)
                          .FirstOrDefault(g => g.Count() > 1);
            if (dup != null)
                throw new InvalidOperationException($"'{name}': aiFlowCode {dup.Key} được nhiều definition nhận");
        }
        SupportedNames = _byName.Keys.Order(StringComparer.Ordinal).ToArray();
    }

    public RuleDefinition? Resolve(string name, AiFlowCode? aiFlowCode)
    {
        if (!_byName.TryGetValue(name, out var list)) return null;

        if (aiFlowCode.HasValue)
        {
            var match = list.FirstOrDefault(d => d.Handler.AiFlowCodes?.Contains(aiFlowCode.Value) == true);
            if (match != null) return match;
        }

        return list.FirstOrDefault(d => d.Handler.AiFlowCodes is null);
    }

    public IReadOnlyCollection<string> SupportedNames { get; }
}
