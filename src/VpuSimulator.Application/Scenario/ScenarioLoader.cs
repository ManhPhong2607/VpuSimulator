using VpuSimulator.Application.Commands.Parsing;
using VpuSimulator.Application.Identity;
using VpuSimulator.Application.State;

namespace VpuSimulator.Application.Scenario;

public sealed record ScenarioInfo(
    int VpuCount, int VpsCount, int TaskCount, int RuleCount, long? Seed, DateTimeOffset LoadedAt);

public sealed record ScenarioApplyResult(IReadOnlyList<ScenarioIssue> Issues, int Identities, int Tasks, int Rules);

public sealed record ScenarioClearResult(int Identities, int Tasks);

/// <summary>
/// Áp dụng / gỡ scenario. Không giao dịch (W-11): identity nào lỗi thì bỏ qua, những identity đã thêm được giữ.
/// Nạp lần hai là THÊM (app_id mới) nên số đếm cộng dồn. Số đếm là số đã nạp, không theo dõi việc gỡ lẻ qua /identities.
/// </summary>
public sealed class ScenarioLoader
{
    private readonly IdentityLifecycleService _identities;
    private readonly ITaskStore _tasks;
    private readonly TimeProvider _time;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private ScenarioInfo? _current;

    public ScenarioLoader(IdentityLifecycleService identities, ITaskStore tasks, TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(identities);
        ArgumentNullException.ThrowIfNull(tasks);
        ArgumentNullException.ThrowIfNull(time);
        _identities = identities;
        _tasks = tasks;
        _time = time;
    }

    public ScenarioInfo? Current => Volatile.Read(ref _current);

    public async Task<ScenarioApplyResult> ApplyAsync(ScenarioPlan plan, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var issues = new List<ScenarioIssue>();
            int identities = 0, tasks = 0, rules = 0;

            foreach (var identity in plan.Identities)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var result = await _identities.AddAsync(identity.Options, cancellationToken).ConfigureAwait(false);
                if (result != IdentityOpResult.Added)
                {
                    issues.Add(new ScenarioIssue(ScenarioSeverity.Error,
                        $"Không thêm được identity: {identity.Options.AppId}",
                        $"Kết quả: {result}. Các identity đã thêm trước đó được giữ lại (W-11)."));
                    continue;
                }

                identities++;
                foreach (var task in identity.Tasks)
                {
                    _tasks.Upsert(identity.Options.AppId, task);
                    tasks++;
                    rules += task.Rules.Count;
                }
            }

            if (identities > 0)
            {
                var prev = _current;
                Volatile.Write(ref _current, new ScenarioInfo(
                    (prev?.VpuCount ?? 0) + identities,
                    (prev?.VpsCount ?? 0) + identities,
                    (prev?.TaskCount ?? 0) + tasks,
                    (prev?.RuleCount ?? 0) + rules,
                    plan.Seed ?? prev?.Seed,
                    _time.GetUtcNow()));
            }

            return new ScenarioApplyResult(issues, identities, tasks, rules);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Gỡ MỌI identity đang chạy (FR-CTL-8), kể cả identity khai báo trong cấu hình.</summary>
    public async Task<ScenarioClearResult> ClearAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            int removed = 0, tasks = 0;
            foreach (var appId in _identities.AppIds)
            {
                var count = _tasks.List(appId).Count;
                if (await _identities.RemoveAsync(appId, cancellationToken).ConfigureAwait(false))
                {
                    removed++;
                    tasks += count;
                }
            }

            Volatile.Write(ref _current, null);
            return new ScenarioClearResult(removed, tasks);
        }
        finally
        {
            _gate.Release();
        }
    }
}