using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using VpuSimulator.Application.Commands;
using VpuSimulator.Application.State;

namespace VpuSimulator.Application.Scheduling;

/// <summary>Cấu hình hiện tại của một unit: task chứa nó và rule của nó. Đọc và thay cùng lúc nên luôn nhất quán.</summary>
public sealed record UnitConfigView(ParsedTask Task, ParsedRule Rule);

/// <summary>Hàm phát lấy cấu hình hiện tại qua đây (rule có thể đổi khi VMS gửi update-task).</summary>
public interface IUnitConfigSource
{
    UnitConfigView Current { get; }
}

/// <summary>Tạo hàm "phát một lần" cho một unit mới. Được gọi trong khoá của registry nên phải nhanh, không await.</summary>
public delegate UnitEmitDelegate UnitEmitterFactory(UnitKey key, IUnitConfigSource config);

public enum UnitTriggerOutcome
{
    Accepted,
    Busy,        // unit đang phát 
    NotFound,    // không có unit này (hoặc vừa bị gỡ)
    Ambiguous,   // nhiều rule cùng roi mà không nói rõ rule nào
}

/// Matches Accepted/Busy: đúng một khoá. Ambiguous: các khoá trùng. NotFound: rỗng
public sealed record UnitTriggerResult(UnitTriggerOutcome Outcome, IReadOnlyList<UnitKey> Matches);

public interface IUnitRegistry : ITaskObserver
{
    long Seed { get; }

    IReadOnlyList<UnitSnapshot> Snapshots(string? appId = null);
    
    //FR-SIM-4: phát một lần ngay, bất kể auto. ruleName null: chỉ hợp lệ khi (appId, task, roi) khớp đúng một unit
    UnitTriggerResult Trigger(string appId, string taskId, int roiId, string? ruleName = null);

    //Gỡ identity: quên bộ đếm epoch của identity đó. Gọi SAU khi kho task đã Clear
    void ForgetIdentity(string appId);
}

/// <summary>
/// Giữ mọi <see cref="UnitRunner"/> và đồng bộ chúng với kho task: mỗi rule của mỗi task đang chạy có đúng một unit.
/// Đối chiếu (reconcile) chứ không dựa vào Diff: gọi lại bao nhiêu lần kết quả vẫn như nhau.
/// </summary>
public sealed class UnitRegistry : IUnitRegistry, IAsyncDisposable
{
    private sealed class UnitConfig(UnitConfigView initial) : IUnitConfigSource
    {
        private volatile UnitConfigView _current = initial;

        public UnitConfigView Current => _current;

        public void Replace(UnitConfigView view) => _current = view;
    }

    private sealed class Entry(UnitRunner runner, UnitConfig config)
    {
        public UnitRunner Runner { get; } = runner;

        public UnitConfig Config { get; } = config;
    }

    private readonly SimulationProfile _profile;
    private readonly TimeSpan _armDelay;
    private readonly TimeProvider _time;
    private readonly UnitEmitterFactory _emitterFactory;
    private readonly Func<UnitConfigView, double?>? _fixedPeriodSeconds;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger _logger;

    private readonly object _gate = new();
    private readonly Dictionary<UnitKey, Entry> _units = new();
    private readonly Dictionary<UnitKey, int> _nextEpoch = new();   // epoch cho unit tạo lại với cùng khoá
    private readonly ConcurrentDictionary<Task, byte> _disposing = new();
    private bool _disposed;

    /// <param name="seed">Seed đã được <see cref="SimulationSeed.Resolve"/> (khác 0).</param>
    /// <param name="fixedPeriodSeconds">Chu kỳ cố định của rule density (FR-SIM-13); null hoặc trả null/≤ 0 thì profile quyết định nhịp.</param>
    public UnitRegistry(
        long seed,
        SimulationProfile profile,
        TimeSpan armDelay,
        TimeProvider time,
        UnitEmitterFactory emitterFactory,
        Func<UnitConfigView, double?>? fixedPeriodSeconds = null,
        ILoggerFactory? loggerFactory = null)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(emitterFactory);
        ArgumentOutOfRangeException.ThrowIfLessThan(armDelay, TimeSpan.Zero);
        var errors = profile.Validate();
        if (errors.Count > 0)
        {
            throw new ArgumentException(string.Join("; ", errors), nameof(profile));
        }

        Seed = seed;
        _profile = profile;
        _armDelay = armDelay;
        _time = time;
        _emitterFactory = emitterFactory;
        _fixedPeriodSeconds = fixedPeriodSeconds;
        _loggerFactory = loggerFactory ?? NullLoggerFactory.Instance;
        _logger = _loggerFactory.CreateLogger<UnitRegistry>();
        _logger.LogInformation("Simulation seed {Seed}, profile {Profile}, arm delay {ArmDelay}", seed, profile.Name, armDelay);
    }

    public long Seed { get; }

    public void TaskApplied(string appId, TaskApplyResult result)
    {
        ArgumentNullException.ThrowIfNull(appId);
        ArgumentNullException.ThrowIfNull(result);

        try
        {
            Reconcile(appId, result.Current);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Identity {AppId}: đồng bộ unit của task {TaskId} lỗi", appId, result.Current.TaskId);
        }
    }

    public void TasksRemoved(string appId, IReadOnlyList<ParsedTask> removed)
    {
        ArgumentNullException.ThrowIfNull(appId);
        ArgumentNullException.ThrowIfNull(removed);

        var stale = new List<UnitRunner>();
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            foreach (var task in removed)
            {
                foreach (var key in KeysOf(appId, task.TaskId))
                {
                    stale.Add(Detach(key));
                }
            }
        }

        foreach (var runner in stale)
        {
            DisposeInBackground(runner);
        }
    }

    public void ForgetIdentity(string appId)
    {
        ArgumentNullException.ThrowIfNull(appId);

        var stale = new List<UnitRunner>();
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            foreach (var key in KeysOf(appId))
            {
                stale.Add(Detach(key));   // thường rỗng: kho đã Clear trước đó
            }

            foreach (var key in _nextEpoch.Keys.Where(k => k.AppId == appId).ToList())
            {
                _nextEpoch.Remove(key);
            }
        }

        foreach (var runner in stale)
        {
            DisposeInBackground(runner);
        }
    }

    public IReadOnlyList<UnitSnapshot> Snapshots(string? appId = null)
    {
        lock (_gate)
        {
            return _units
                .Where(kv => appId is null || kv.Key.AppId == appId)
                .Select(kv => kv.Value.Runner.Snapshot())
                .OrderBy(s => s.Key.AppId, StringComparer.Ordinal)
                .ThenBy(s => s.Key.TaskId, StringComparer.Ordinal)
                .ThenBy(s => s.Key.RoiId)
                .ThenBy(s => s.Key.RuleName, StringComparer.Ordinal)
                .ToList();
        }
    }
    
    public UnitTriggerResult Trigger(string appId, string taskId, int roiId, string? ruleName = null)
    {
        ArgumentNullException.ThrowIfNull(appId);
        ArgumentNullException.ThrowIfNull(taskId);

        UnitKey key;
        UnitRunner runner;
        lock (_gate)
        {
            var matches = _units
                .Where(kv => kv.Key.AppId == appId
                             && kv.Key.TaskId == taskId
                             && kv.Key.RoiId == roiId
                             && (ruleName is null || kv.Key.RuleName == ruleName))
                .OrderBy(kv => kv.Key.RuleName, StringComparer.Ordinal)
                .ToList();

            if (matches.Count == 0)
            {
                return new UnitTriggerResult(UnitTriggerOutcome.NotFound, []);
            }

            if (matches.Count > 1)
            {
                return new UnitTriggerResult(UnitTriggerOutcome.Ambiguous, matches.Select(m => m.Key).ToList());
            }

            key = matches[0].Key;
            runner = matches[0].Value.Runner;
        }

        // Gọi ngoài khoá của registry: runner có khoá riêng.
        return runner.Trigger() switch
        {
            TriggerResult.Accepted => new UnitTriggerResult(UnitTriggerOutcome.Accepted, [key]),
            TriggerResult.RejectedBusy => new UnitTriggerResult(UnitTriggerOutcome.Busy, [key]),
            _ => new UnitTriggerResult(UnitTriggerOutcome.NotFound, []),   // unit vừa bị gỡ giữa hai bước
        };
    }

    /// <summary>Chờ các unit đã gỡ dừng hẳn. Dùng trong test và khi tắt.</summary>
    public Task WhenIdleAsync() => Task.WhenAll(_disposing.Keys.ToArray());

    public async ValueTask DisposeAsync()
    {
        List<UnitRunner> all;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            all = _units.Values.Select(e => e.Runner).ToList();
            _units.Clear();
        }

        foreach (var runner in all)
        {
            DisposeInBackground(runner);
        }

        await WhenIdleAsync().ConfigureAwait(false);
    }

    // ---------------------------------------------------------------- nội bộ

    private void Reconcile(string appId, ParsedTask task)
    {
        var taskEnabled = task.Enable != false;   // FR-CMD-14: vắng mặt hoặc true là bật
        var stale = new List<UnitRunner>();

        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            var wanted = new HashSet<UnitKey>();
            foreach (var rule in task.Rules)
            {
                var key = new UnitKey(appId, task.TaskId, rule.RoiId, rule.Name);
                wanted.Add(key);

                var view = new UnitConfigView(task, rule);
                var effective = taskEnabled && rule.Enable != false;
                var resetTimer = Normalize(_fixedPeriodSeconds?.Invoke(view));

                if (_units.TryGetValue(key, out var entry))
                {
                    entry.Config.Replace(view);
                    entry.Runner.SetResetTimer(resetTimer);
                    entry.Runner.SetAuto(effective);   // không đổi thì không làm gì (và không tăng epoch)
                }
                else
                {
                    var epoch = _nextEpoch.GetValueOrDefault(key);
                    var config = new UnitConfig(view);
                    var runner = new UnitRunner(
                        key, Seed, _profile, _armDelay, _time, _emitterFactory(key, config),
                        resetTimer, epoch, _loggerFactory.CreateLogger<UnitRunner>());
                    _units[key] = new Entry(runner, config);
                    runner.Start(effective);
                    _logger.LogInformation("Unit {Unit} được tạo (epoch {Epoch}, auto {Auto})", key, epoch, effective);
                }
            }

            foreach (var key in KeysOf(appId, task.TaskId).Where(k => !wanted.Contains(k)))
            {
                stale.Add(Detach(key));
            }
        }

        foreach (var runner in stale)
        {
            DisposeInBackground(runner);
        }
    }

    // Gọi trong khoá. Ghi nhớ epoch để unit tạo lại với cùng khoá tiếp tục dãy (add lại → phiên mới).
    private UnitRunner Detach(UnitKey key)
    {
        var entry = _units[key];
        _units.Remove(key);
        _nextEpoch[key] = entry.Runner.Snapshot().Epoch + 1;
        _logger.LogInformation("Unit {Unit} bị gỡ", key);
        return entry.Runner;
    }

    private List<UnitKey> KeysOf(string appId, string? taskId = null) =>
        _units.Keys.Where(k => k.AppId == appId && (taskId is null || k.TaskId == taskId)).ToList();

    private void DisposeInBackground(UnitRunner runner)
    {
        var task = Task.Run(async () =>
        {
            try
            {
                await runner.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Dừng unit {Unit} lỗi", runner.Key);
            }
        });

        _disposing[task] = 0;
        _ = task.ContinueWith(t => _disposing.TryRemove(t, out _), TaskScheduler.Default);
    }

    private static double? Normalize(double? seconds) => seconds is > 0 ? seconds : null;
}