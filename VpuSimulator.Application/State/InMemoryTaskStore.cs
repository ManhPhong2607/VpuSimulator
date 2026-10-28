using System.Text;
using System.Text.Json;
using VpuSimulator.Application.Commands;

namespace VpuSimulator.Application.State;

public sealed class InMemoryTaskStore : ITaskStore
{
    private readonly object _gate = new();
    private readonly Dictionary<string, Dictionary<string, ParsedTask>> _byApp = new(StringComparer.Ordinal);
    private readonly ITaskObserver? _observer;

    public InMemoryTaskStore(ITaskObserver? observer = null)
    {
        _observer = observer;
    }
    
    public TaskApplyResult Upsert(string appId, ParsedTask task)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appId);
        ArgumentNullException.ThrowIfNull(task);

        TaskApplyResult result;
        lock (_gate)
        {
            if (!_byApp.TryGetValue(appId, out var tasks))
            {
                tasks = new Dictionary<string, ParsedTask>(StringComparer.Ordinal);
                _byApp[appId] = tasks;
            }

            tasks.TryGetValue(task.TaskId, out var previous);
            tasks[task.TaskId] = task;
            result = new TaskApplyResult(previous is null, task, Diff(previous, task));
        }

        _observer?.TaskApplied(appId, result);
        return result;
    }

    public TaskApplyResult? Update(string appId, string taskId, Func<ParsedTask, ParsedTask> transform)
    {
        ArgumentNullException.ThrowIfNull(transform);

        ArgumentNullException.ThrowIfNull(transform);

        TaskApplyResult result;
        lock (_gate)
        {
            if (!_byApp.TryGetValue(appId, out var tasks) || !tasks.TryGetValue(taskId, out var previous))
            {
                return null;
            }

            var next = transform(previous);
            tasks[taskId] = next;
            result = new TaskApplyResult(false, next, Diff(previous, next));
        }

        _observer?.TaskApplied(appId, result);
        return result;
    }

    public IReadOnlyList<ParsedTask> Remove(string appId, IEnumerable<string> taskIds)
    {
        ArgumentNullException.ThrowIfNull(taskIds);

        var removed = new List<ParsedTask>();
        lock (_gate)
        {
            if (!_byApp.TryGetValue(appId, out var tasks))
            {
                return removed;
            }

            foreach (var id in taskIds)
            {
                if (tasks.Remove(id, out var task))
                {
                    removed.Add(task);
                }
            }

            if (tasks.Count == 0)
            {
                _byApp.Remove(appId);
            }
        }

        if (removed.Count > 0)
        {
            _observer?.TasksRemoved(appId, removed);
        }

        return removed;
    }

    public IReadOnlyList<ParsedTask> Clear(string appId)
    {
        List<ParsedTask> removed;
        lock (_gate)
        {
            if (!_byApp.Remove(appId, out var tasks))
            {
                return new List<ParsedTask>();
            }

            removed = tasks.Values.OrderBy(t => t.TaskId, StringComparer.Ordinal).ToList();
        }

        if (removed.Count > 0)
        {
            _observer?.TasksRemoved(appId, removed);
        }

        return removed;
    }

    public ParsedTask? Get(string appId, string taskId)
    {
        lock (_gate)
        {
            return _byApp.TryGetValue(appId, out var tasks) && tasks.TryGetValue(taskId, out var task) ? task : null;
        }
    }

    public IReadOnlyList<ParsedTask> List(string appId)
    {
        lock (_gate)
        {
            return _byApp.TryGetValue(appId, out var tasks)
                ? tasks.Values.OrderBy(t => t.TaskId, StringComparer.Ordinal).ToList()
                : new List<ParsedTask>();
        }
    }

    /// <summary>
    /// So rule cũ với rule mới theo khóa (id, name). "Không đổi" nghĩa là nội dung JSON giống hệt; khoảng trắng
    /// thừa không tính là thay đổi (rộng hơn "byte-by-byte" của FR-CMD-6 một chút, nhưng không bao giờ coi rule khác là giống).
    /// </summary>
    private static IReadOnlyList<RuleChange> Diff(ParsedTask? previous, ParsedTask next)
    {
        var before = new Dictionary<RuleKey, ParsedRule>();
        foreach (var rule in previous?.Rules ?? Array.Empty<ParsedRule>())
        {
            before[rule.Key] = rule;
        }

        var changes = new List<RuleChange>();
        var nextKeys = new HashSet<RuleKey>();
        foreach (var rule in next.Rules)
        {
            nextKeys.Add(rule.Key);
            if (!before.TryGetValue(rule.Key, out var old))
            {
                changes.Add(new RuleChange(rule.Key, RuleChangeKind.Added));
            }
            else
            {
                var same = string.Equals(Canonical(old.Raw), Canonical(rule.Raw), StringComparison.Ordinal);
                changes.Add(new RuleChange(rule.Key, same ? RuleChangeKind.Unchanged : RuleChangeKind.Changed));
            }
        }

        foreach (var key in before.Keys)
        {
            if (!nextKeys.Contains(key))
            {
                changes.Add(new RuleChange(key, RuleChangeKind.Removed));
            }
        }

        return changes;
    }

    private static string Canonical(JsonElement element)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            element.WriteTo(writer);
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
