using System.Reflection;
using VpuSimulator.Application;
using VpuSimulator.Domain;

namespace VpuSimulator.Infrastructure;

public sealed class RuleHandlerRegistry : IRuleHandlerRegistry
{
    private readonly Dictionary<string, List<IRuleHandler>> _byName;

    public RuleHandlerRegistry(IEnumerable<IRuleHandler> handlers)
    {
        _byName = handlers
            .GroupBy(h => h.Name, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);

        // Kiem tra luc khoi dong: khong de mo ho, loi cau hinh phai lo ra ngay chu khong doi toi luc chay.
        foreach (var (name, list) in _byName)
        {
            if (list.Count(h => h.AiFlowCodes is null) > 1)
                throw new InvalidOperationException($"'{name}': co nhieu handler khong rang buoc aiFlowCode");

            var dup = list.Where(h => h.AiFlowCodes is not null)
                          .SelectMany(h => h.AiFlowCodes!)
                          .GroupBy(c => c)
                          .FirstOrDefault(g => g.Count() > 1);
            if (dup != null)
                throw new InvalidOperationException($"'{name}': aiFlowCode {dup.Key} duoc nhieu handler nhan");
        }
    }

    public RuleHandlerRegistry(Assembly rulesAssembly)
        : this(rulesAssembly.GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false } && typeof(IRuleHandler).IsAssignableFrom(t))
            .Select(t => (IRuleHandler)Activator.CreateInstance(t)!))
    {
    }

    public IRuleHandler? Resolve(string name, AiFlowCode aiFlowCode)
    {
        if (!_byName.TryGetValue(name, out var list)) return null;
        return list.FirstOrDefault(h => h.AiFlowCodes?.Contains(aiFlowCode) == true)
               ?? list.FirstOrDefault(h => h.AiFlowCodes is null);
    }

    public IReadOnlyCollection<string> SupportedNames => _byName.Keys;
}
