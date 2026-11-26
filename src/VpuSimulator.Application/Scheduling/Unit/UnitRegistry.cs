using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using VpuSimulator.Application.Commands;
using VpuSimulator.Application.State;
using VpuSimulator.Application.Events;

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
    Busy, // unit đang phát 
    NotFound, // không có unit này (hoặc vừa bị gỡ)
    Ambiguous, // nhiều rule cùng roi mà không nói rõ rule nào
}

/// Matches Accepted/Busy: đúng một khoá. Ambiguous: các khoá trùng. NotFound: rỗng
public sealed record UnitTriggerResult(UnitTriggerOutcome Outcome, IReadOnlyList<UnitKey> Matches);

public interface IUnitRegistry : ITaskObserver
{
    long Seed { get; }

    IReadOnlyList<UnitSnapshot> Snapshots(string? appId = null);

    //FR-SIM-4: phát một lần ngay, bất kể auto. ruleName null: chỉ hợp lệ khi (appId, task, roi) khớp đúng một unit
    UnitTriggerResult Trigger(string appId, string taskId, int roiId, string? ruleName = null);

    //FR-SIM-3: bật/tắt phát tự động của một unit. Lựa chọn thủ công được nhớ, VMS update-task không ghi đè.
    UnitAutoResult SetAuto(string appId, string taskId, int roiId, bool auto, string? ruleName = null);
    
    //FR-SIM-9 / W-10: đổi profile của các unit ĐANG TỒN TẠI theo phạm vi. Trả số unit đã đổi.
    //appId null = toàn bộ; taskId chỉ hợp lệ khi có appId. Unit tạo sau vẫn dùng profile mặc định.
    int ApplyProfile(SimulationProfile profile, string? appId = null, string? taskId = null);

    // Công tắc tắt thủ công cấp task (VPS Off). Độc lập với `enable` do VMS gửi: update-task không ghi đè.
    // Mất khi task bị gỡ (end-task, stop-all, DELETE) hoặc identity bị gỡ.
    void SetTaskManuallyOff(string appId, string taskId, bool off);

    bool IsTaskManuallyOff(string appId, string taskId);

    void SetIdentityOff(string appId, bool off);

    bool IsIdentityOff(string appId);

    //Gỡ identity: quên bộ đếm epoch của identity đó. Gọi SAU khi kho task đã Clear
    void ForgetIdentity(string appId);

    //FR-ID-9: profile riêng của identity, dùng cho unit TẠO SAU. Không đổi unit đang có (W-10)
    void SetIdentityProfile(string appId, SimulationProfile profile);

    //Tên profile mà unit mới của identity sẽ dùng
    string IdentityProfileName(string appId);
    
    int RefreshProfile(SimulationProfile profile);

    // Profile này đang được dùng bởi bao nhiêu unit và bao nhiêu identity (profile riêng). Không tính profile mặc định.
    ProfileUsage UsageOfProfile(string name);
}

public enum UnitAutoOutcome
{
    Applied,
    NotFound,
    Ambiguous, // nhiều rule cùng roi mà không nói rõ rule nào
}

/// Auto: giá trị HIỆU LỰC sau khi áp dụng. Có thể là false dù yêu cầu true, khi task/rule đang bị VMS tắt.
public sealed record UnitAutoResult(UnitAutoOutcome Outcome, IReadOnlyList<UnitKey> Matches, bool Auto);

public sealed record ProfileUsage(int Units, int Identities);

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

    private SimulationProfile _profile;
    private readonly TimeSpan _armDelay;
    private readonly TimeProvider _time;
    private readonly UnitEmitterFactory _emitterFactory;
    private readonly Func<UnitConfigView, double?>? _fixedPeriodSeconds;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger _logger;
    private readonly SseEventBus? _sse;

    private readonly object _gate = new();
    private readonly Dictionary<UnitKey, Entry> _units = new();
    private readonly Dictionary<UnitKey, int> _nextEpoch = new(); // epoch cho unit tạo lại với cùng khoá
    private readonly Dictionary<UnitKey, bool> _manualAuto = new();
    private readonly HashSet<(string AppId, string TaskId)> _manualTaskOff = new();
    private readonly HashSet<string> _identityOff = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SimulationProfile> _identityProfiles = new(StringComparer.Ordinal);
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
        ILoggerFactory? loggerFactory = null,
        SseEventBus? sse = null)
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
        _sse = sse;
        _logger.LogInformation("Simulation seed {Seed}, profile {Profile}, arm delay {ArmDelay}", seed, profile.Name,
            armDelay);
    }

    public long Seed { get; }

    public void TaskApplied(string appId, TaskApplyResult result)
    {
        ArgumentNullException.ThrowIfNull(appId);
        ArgumentNullException.ThrowIfNull(result);

        try
        {
            Reconcile(appId, result.Current);
            if (result.Created)
            {
                _sse?.Publish("task_added", new { type = "task_added", app_id = appId, task = result.Current.TaskId });
            }
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
                _manualTaskOff.Remove((appId, task.TaskId));
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

        foreach (var task in removed)
        {
            _sse?.Publish("task_removed", new { type = "task_removed", app_id = appId, task = task.TaskId });
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
                stale.Add(Detach(key)); // thường rỗng: kho đã Clear trước đó
            }

            foreach (var key in _nextEpoch.Keys.Where(k => k.AppId == appId).ToList())
            {
                _nextEpoch.Remove(key);
            }

            _identityProfiles.Remove(appId);
            _manualTaskOff.RemoveWhere(p => p.AppId == appId);
            _identityOff.Remove(appId);
        }

        foreach (var runner in stale)
        {
            DisposeInBackground(runner);
        }
    }

    public void SetIdentityProfile(string appId, SimulationProfile profile)
    {
        ArgumentNullException.ThrowIfNull(appId);
        ArgumentNullException.ThrowIfNull(profile);

        lock (_gate)
        {
            _identityProfiles[appId] = profile;
        }
    }

    public string IdentityProfileName(string appId)
    {
        ArgumentNullException.ThrowIfNull(appId);

        lock (_gate)
        {
            return _identityProfiles.GetValueOrDefault(appId, _profile).Name;
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
            _ => new UnitTriggerResult(UnitTriggerOutcome.NotFound, []), // unit vừa bị gỡ giữa hai bước
        };
    }

    public UnitAutoResult SetAuto(string appId, string taskId, int roiId, bool auto,
        string? ruleName = null)
    {
        ArgumentNullException.ThrowIfNull(appId);
        ArgumentNullException.ThrowIfNull(taskId);

        // Trong khoá của registry (giống Reconcile) để không chạy đua với update-task.
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
                return new UnitAutoResult(UnitAutoOutcome.NotFound, [], false);
            }

            if (matches.Count > 1)
            {
                return new UnitAutoResult(UnitAutoOutcome.Ambiguous,
                    matches.Select(m => m.Key).ToList(), false);
            }

            var key = matches[0].Key;
            var entry = matches[0].Value;
            _manualAuto[key] = auto;

            var view = entry.Config.Current;
            var effective = EffectiveAuto(view.Task, view.Rule, key); // _manualAuto[key] đã được ghi ở trên
            entry.Runner.SetAuto(effective); // không đổi thì không làm gì (và không tăng epoch)
            return new UnitAutoResult(UnitAutoOutcome.Applied, [key], effective);
        }
    }

    public void SetTaskManuallyOff(string appId, string taskId, bool off)
    {
        ArgumentNullException.ThrowIfNull(appId);
        ArgumentNullException.ThrowIfNull(taskId);

        // Trong khoá của registry (giống Reconcile) để không chạy đua với update-task.
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            if (off)
            {
                _manualTaskOff.Add((appId, taskId));
            }
            else
            {
                _manualTaskOff.Remove((appId, taskId));
            }

            foreach (var key in KeysOf(appId, taskId))
            {
                var entry = _units[key];
                var view = entry.Config.Current;
                entry.Runner.SetAuto(EffectiveAuto(view.Task, view.Rule, key)); // không đổi thì không làm gì
            }
        }
    }

    public bool IsTaskManuallyOff(string appId, string taskId)
    {
        ArgumentNullException.ThrowIfNull(appId);
        ArgumentNullException.ThrowIfNull(taskId);

        lock (_gate)
        {
            return _manualTaskOff.Contains((appId, taskId));
        }
    }

    public void SetIdentityOff(string appId, bool off)
    {
        ArgumentNullException.ThrowIfNull(appId);

        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            if (off)
            {
                _identityOff.Add(appId);
            }
            else
            {
                _identityOff.Remove(appId);
            }

            foreach (var key in KeysOf(appId))
            {
                var entry = _units[key];
                var view = entry.Config.Current;
                entry.Runner.SetAuto(EffectiveAuto(view.Task, view.Rule, key)); // không đổi thì không làm gì
            }
        }
    }

    public bool IsIdentityOff(string appId)
    {
        ArgumentNullException.ThrowIfNull(appId);

        lock (_gate)
        {
            return _identityOff.Contains(appId);
        }
    }

    public int ApplyProfile(SimulationProfile profile, string? appId = null, string? taskId = null)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (taskId is not null && appId is null)
        {
            throw new ArgumentException("task cần đi kèm app_id", nameof(taskId));
        }

        var errors = profile.Validate();
        if (errors.Count > 0)
        {
            throw new ArgumentException(string.Join("; ", errors), nameof(profile));
        }

        List<UnitRunner> targets;
        lock (_gate)
        {
            targets = _units
                .Where(kv => (appId is null || kv.Key.AppId == appId)
                             && (taskId is null || kv.Key.TaskId == taskId))
                .Select(kv => kv.Value.Runner)
                .ToList();
        }

        // Gọi ngoài khoá của registry: runner có khoá riêng.
        foreach (var runner in targets)
        {
            runner.SetProfile(profile);
        }

        return targets.Count;
    }

    public int RefreshProfile(SimulationProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var errors = profile.Validate();
        if (errors.Count > 0)
        {
            throw new ArgumentException(string.Join("; ", errors), nameof(profile));
        }

        List<UnitRunner> targets;
        lock (_gate)
        {
            // Profile mặc định và profile riêng của identity: các unit TẠO SAU sẽ dùng định nghĩa mới
            if (_profile.Name == profile.Name)
            {
                _profile = profile;
            }

            foreach (var appId in _identityProfiles.Where(kv => kv.Value.Name == profile.Name)
                         .Select(kv => kv.Key).ToList())
            {
                _identityProfiles[appId] = profile;
            }

            // Các unit ĐANG chạy với profile này
            targets = _units.Values
                .Where(e => e.Runner.Snapshot().Profile == profile.Name)
                .Select(e => e.Runner)
                .ToList();
        }

        // Gọi ngoài khoá của registry (giống ApplyProfile): runner có khoá riêng
        foreach (var runner in targets)
        {
            runner.SetProfile(profile);
        }

        return targets.Count;
    }

    public ProfileUsage UsageOfProfile(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        lock (_gate)
        {
            return new ProfileUsage(
                _units.Values.Count(e => e.Runner.Snapshot().Profile == name),
                _identityProfiles.Values.Count(p => p.Name == name));
        }
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
    // Gọi trong khoá. FR-CMD-14: enable vắng mặt hoặc true là bật. Cả bốn điều kiện phải thoả.
    private bool EffectiveAuto(ParsedTask task, ParsedRule rule, UnitKey key) =>
        !_identityOff.Contains(key.AppId)
        && task.Enable != false
        && !_manualTaskOff.Contains((key.AppId, key.TaskId))
        && rule.Enable != false
        && _manualAuto.GetValueOrDefault(key, true);

    private void Reconcile(string appId, ParsedTask task)
    {
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
                var effective = EffectiveAuto(task, rule, key);
                var resetTimer = Normalize(_fixedPeriodSeconds?.Invoke(view));

                if (_units.TryGetValue(key, out var entry))
                {
                    entry.Config.Replace(view);
                    entry.Runner.SetResetTimer(resetTimer);
                    entry.Runner.SetAuto(effective); // không đổi thì không làm gì (và không tăng epoch)
                }
                else
                {
                    var epoch = _nextEpoch.GetValueOrDefault(key);
                    var config = new UnitConfig(view);
                    var runner = new UnitRunner(
                        key, Seed, _identityProfiles.GetValueOrDefault(key.AppId, _profile), _armDelay, _time,
                        _emitterFactory(key, config), resetTimer, epoch, _loggerFactory.CreateLogger<UnitRunner>());
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
        _manualAuto.Remove(key);
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