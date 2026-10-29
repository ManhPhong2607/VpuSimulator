using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using VpuSimulator.Domain;

namespace VpuSimulator.Application.Scheduling;

/// <summary>
/// Vòng lập lịch của MỘT emission unit. Một vòng lặp duy nhất, tuần tự: chờ tới hạn (hoặc được đánh thức), phát một lần,
/// tính lần kế tiếp từ lúc lần phát kết thúc. Vì tuần tự nên các lần phát của cùng unit không bao giờ chồng nhau
///
/// Mọi trạng thái nằm sau một khoá (_gate). Hàm phát (_emit) luôn được gọi NGOÀI khoá
/// chỉ gọi hàm "phát một lần" do bên ngoài đưa vào
/// </summary>
public sealed class UnitRunner : IAsyncDisposable
{
    private enum WaitResult
    {
        Due,
        Woken,
        Stopped,
    }

    private readonly UnitKey _key;
    private readonly long _seed;
    private readonly TimeSpan _armDelay;
    private readonly TimeProvider _time;
    private readonly UnitEmitDelegate _emit;
    private readonly ILogger _logger;
    private readonly CancellationTokenSource _stop = new();
    private readonly object _gate = new();

    // Mọi trường dưới đây chỉ được đọc/ghi trong lock (_gate).
    private SimulationProfile _profile;
    private double? _resetTimerSeconds;
    private bool _started;
    private bool _stopped;
    private bool _auto;
    private int _epoch;
    private static long s_nextRunToken;
    private long _runToken;
    private Random _payloadRandom;
    private Random _scheduleRandom;
    private bool _isEmitting;
    private bool _manualRequested;
    private bool _recomputeRequested;
    private DateTimeOffset? _nextDue;
    private DateTimeOffset? _lastEmitAt;       // lần phát THÀNH CÔNG (Emitted) cuối, dùng cho min_gap
    private DateTimeOffset? _lastCompletion;   // lúc lần phát gần nhất kết thúc, dù kết quả gì
    private TaskCompletionSource _wake = NewWake();
    private Task? _loop;
    private long _emitted;
    private long _noMessage;
    private long _precondition;
    private long _errors;
    private long _ticksDropped;
    private long _ticksDeferred;
    private long _triggersRejected;

    /// epoch: Số phiên khởi đầu. Nơi tạo unit giữ bộ đếm theo từng khoá và truyền vào
    public UnitRunner(
        UnitKey key,
        long seed,
        SimulationProfile profile,
        TimeSpan armDelay,
        TimeProvider time,
        UnitEmitDelegate emit,
        double? resetTimerSeconds = null,
        int epoch = 0,
        ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(emit);
        ArgumentOutOfRangeException.ThrowIfLessThan(armDelay, TimeSpan.Zero);
        ThrowIfInvalid(profile);

        _key = key;
        _seed = seed;
        _profile = profile;
        _armDelay = armDelay;
        _time = time;
        _emit = emit;
        _resetTimerSeconds = resetTimerSeconds;
        _epoch = epoch;
        _runToken = NextRunToken();
        _logger = logger ?? NullLogger.Instance;
        (_payloadRandom, _scheduleRandom) = CreateRandoms();
    }

    public UnitKey Key => _key;

    /// <summary>
    /// Bắt đầu chạy. auto = true: lần phát đầu sau arm delay (FR-SIM-5). auto = false: đứng yên cho tới khi bật auto hoặc trigger
    /// Hàm này chạy đồng bộ tới await đầu tiên của vòng lặp, nên khi trả về thì timer đã được tạo (test dựa vào điều này)
    /// </summary>
    public void Start(bool auto = true)
    {
        lock (_gate)
        {
            if (_started || _stopped)
            {
                throw new InvalidOperationException("Unit đã được khởi động hoặc đã dừng");
            }

            _started = true;
            _auto = auto;
            _nextDue = auto ? _time.GetUtcNow() + _armDelay : null;
        }

        _loop = RunAsync();
    }

    /// <summary>
    /// FR-SIM-3. Bật: lên lịch phát NGAY (không chờ arm delay), sang phiên mới: epoch tăng, Random dựng lại từ seed
    /// Tắt: ngừng phát nhưng giữ unit; lần phát đang chạy dở vẫn được hoàn tất. Trả về true nếu trạng thái thật sự đổi
    /// </summary>
    public bool SetAuto(bool auto)
    {
        lock (_gate)
        {
            if (_stopped || _auto == auto)
            {
                return false;
            }

            _auto = auto;
            if (auto)
            {
                _epoch++;
                _runToken = NextRunToken();
                (_payloadRandom, _scheduleRandom) = CreateRandoms();
                //Bật: bắt đầu lịch ngay, lần phát đầu sau arm delay
                var result = TickScheduleCalculator.ApplyMinGap(_time.GetUtcNow() + _armDelay, _lastEmitAt, _profile.MinGap);
                if (result.Deferred)
                {
                    _ticksDeferred++;
                }

                _nextDue = result.Due;
            }
            else
            {
                _nextDue = null;
            }

            WakeLocked();
            return true;
        }
    }

    /// <summary>FR-SIM-4. Phát một lần ngay, bất kể auto và bất kể min_gap. Bị từ chối khi unit đang phát</summary>
    public TriggerResult Trigger()
    {
        lock (_gate)
        {
            if (!_started || _stopped)
            {
                return TriggerResult.RejectedStopped;
            }

            if (_isEmitting || _manualRequested)
            {
                _triggersRejected++;
                return TriggerResult.RejectedBusy;
            }

            _manualRequested = true;
            WakeLocked();
            return TriggerResult.Accepted;
        }
    }

    /// <summary>FR-SIM-9. Đổi profile lúc runtime: lần phát kế tiếp được tính lại từ lúc lần phát gần nhất kết thúc</summary>
    public void SetProfile(SimulationProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ThrowIfInvalid(profile);

        lock (_gate)
        {
            _profile = profile;
            _recomputeRequested = true;
            WakeLocked();
        }
    }

    /// <summary>FR-SIM-13. reset_timer (giây) của rule density; null hoặc ≤ 0 thì profile quyết định nhịp. Nhịp được tính lại ngay</summary>
    public void SetResetTimer(double? seconds)
    {
        lock (_gate)
        {
            if (_resetTimerSeconds == seconds)
            {
                return;
            }

            _resetTimerSeconds = seconds;
            _recomputeRequested = true;
            WakeLocked();
        }
    }

    public bool TryCommit(long runToken, Action commit)
    {
        ArgumentNullException.ThrowIfNull(commit);
        lock (_gate)
        {
            if (_stopped || runToken != _runToken)
            {
                return false;
            }

            commit();
            return true;
        }
    }

    private static long NextRunToken() => Interlocked.Increment(ref s_nextRunToken);
    public UnitSnapshot Snapshot()
    {
        lock (_gate)
        {
            return new UnitSnapshot(
                _key, _auto, _isEmitting, _epoch, _auto ? _nextDue : null, _lastEmitAt,
                _emitted, _noMessage, _precondition, _errors, _ticksDropped, _ticksDeferred, _triggersRejected);
        }
    }

    /// <summary>FR-SIM-15. Dừng: huỷ lần phát đang chạy (nếu có), không lên lịch lại. Gọi nhiều lần an toàn</summary>
    public async ValueTask DisposeAsync()
    {
        lock (_gate)
        {
            if (_stopped)
            {
                return;
            }

            _stopped = true;
        }

        await _stop.CancelAsync().ConfigureAwait(false);
        if (_loop is not null)
        {
            await _loop.ConfigureAwait(false);
        }

        _stop.Dispose();
    }

    // ---------------------------------------------------------------- vòng lặp

    private async Task RunAsync()
    {
        var stop = _stop.Token;
        try
        {
            while (!stop.IsCancellationRequested)
            {
                bool manual;
                DateTimeOffset? due;
                Task wakeTask;
                lock (_gate)
                {
                    if (_wake.Task.IsCompleted)
                    {
                        _wake = NewWake();   // tín hiệu cũ đã được xử lý bằng cách đọc lại trạng thái ngay dưới đây
                    }

                    if (_recomputeRequested)
                    {
                        _recomputeRequested = false;
                        RecomputeNextDueLocked();
                    }

                    manual = _manualRequested;
                    due = _auto ? _nextDue : null;
                    wakeTask = _wake.Task;
                }

                if (!manual)
                {
                    var waited = await WaitAsync(due, wakeTask, stop).ConfigureAwait(false);
                    if (waited == WaitResult.Stopped)
                    {
                        return;
                    }

                    if (waited == WaitResult.Woken)
                    {
                        continue;   // trạng thái đã đổi: đọc lại
                    }
                }

                UnitEmitContext context;
                lock (_gate)
                {
                    var now = _time.GetUtcNow();
                    if (manual)
                    {
                        _manualRequested = false;
                    }
                    else if (!_auto || _nextDue is null || now < _nextDue.Value)
                    {
                        continue;   // trạng thái đổi giữa lúc hết hạn và lúc lấy khoá
                    }

                    _isEmitting = true;
                    if (!manual)
                    {
                        _nextDue = null;   // đang phát: chưa có lần kế tiếp, tính lại khi kết thúc
                    }

                    context = new UnitEmitContext(_key, _epoch, _runToken, _payloadRandom, manual, now);
                }

                RuleOutcome outcome;
                try
                {
                    outcome = await _emit(context, stop).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (stop.IsCancellationRequested)
                {
                    lock (_gate)
                    {
                        _isEmitting = false;
                    }

                    return;
                }
                catch (Exception ex)
                {
                    // FR-SIM-11: lần phát lỗi chỉ ghi log, unit vẫn tiếp tục ở lần sau.
                    _logger.LogError(ex, "Unit {Unit}: lần phát lỗi, vẫn tiếp tục ở lần sau", _key);
                    outcome = RuleOutcome.Error;
                }

                lock (_gate)
                {
                    CompleteLocked(context, outcome);
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogCritical(ex, "Unit {Unit}: vòng lập lịch dừng vì lỗi không mong đợi", _key);
        }
    }

    private async Task<WaitResult> WaitAsync(DateTimeOffset? due, Task wakeTask, CancellationToken stop)
    {
        using var delayCts = CancellationTokenSource.CreateLinkedTokenSource(stop);
        var delay = Timeout.InfiniteTimeSpan;
        if (due is not null)
        {
            var remaining = due.Value - _time.GetUtcNow();
            delay = remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
        }

        var delayTask = Task.Delay(delay, _time, delayCts.Token);

        // Đồng hồ có thể nhảy giữa lúc tính delay và lúc tạo timer. Kiểm tra lại, để không chờ thêm một khoảng nữa.
        if (due is not null && _time.GetUtcNow() >= due.Value)
        {
            await delayCts.CancelAsync().ConfigureAwait(false);
            return stop.IsCancellationRequested ? WaitResult.Stopped : WaitResult.Due;
        }

        var finished = await Task.WhenAny(delayTask, wakeTask).ConfigureAwait(false);
        await delayCts.CancelAsync().ConfigureAwait(false);

        if (stop.IsCancellationRequested)
        {
            return WaitResult.Stopped;
        }

        return finished == wakeTask || due is null ? WaitResult.Woken : WaitResult.Due;
    }

    // ---------------------------------------------------------------- tính lịch (luôn gọi trong khoá)

    private void CompleteLocked(UnitEmitContext context, RuleOutcome outcome)
    {
        _isEmitting = false;
        var now = _time.GetUtcNow();
        _lastCompletion = now;

        switch (outcome)
        {
            case RuleOutcome.Emitted:
                _emitted++;
                break;
            case RuleOutcome.NoMessage:
                _noMessage++;
                break;
            case RuleOutcome.Precondition:
                _precondition++;
                break;
            default:
                _errors++;
                break;
        }

        // Lần phát thuộc phiên cũ (đã Off→On trong lúc phát): lịch đã được đặt lại khi đổi phiên, kết quả này không được ghi đè.
        if (context.RunToken != _runToken)
        {
            return;
        }

        if (outcome == RuleOutcome.Emitted)
        {
            _lastEmitAt = now;
        }

        if (!_auto)
        {
            _nextDue = null;
            return;
        }

        if (context.IsManual)
        {
            // Lịch tự động vẫn chạy theo hạn cũ. Nếu hạn đó rơi vào lúc đang phát thủ công thì tick bị bỏ và được đếm (FR-SIM-12).
            if (_nextDue is { } pending && now >= pending)
            {
                _ticksDropped++;
                _nextDue = ComputeNextDueLocked(now);
            }

            return;
        }

        _nextDue = ComputeNextDueLocked(now);
    }

    private DateTimeOffset ComputeNextDueLocked(DateTimeOffset completion)
    {
        var delay = TickScheduleCalculator.NextDelay(_profile, _resetTimerSeconds, _scheduleRandom);
        var result = TickScheduleCalculator.ApplyMinGap(completion + delay, _lastEmitAt, _profile.MinGap);
        if (result.Deferred)
        {
            _ticksDeferred++;
        }

        return result.Due;
    }

    private void RecomputeNextDueLocked()
    {
        // Chưa phát lần nào thì giữ arm delay. Đang tắt auto thì chưa có lịch.
        if (!_auto || _lastCompletion is null || _isEmitting)
        {
            return;
        }

        _nextDue = ComputeNextDueLocked(_lastCompletion.Value);
    }

    // ---------------------------------------------------------------- tiện ích

    private (Random Payload, Random Schedule) CreateRandoms() =>
    (
        SimulationSeed.CreateRandom(_seed, _key.AppId, _key.TaskId, _key.RoiId, _key.RuleName, _epoch, SimulationSeed.PayloadStream),
        SimulationSeed.CreateRandom(_seed, _key.AppId, _key.TaskId, _key.RoiId, _key.RuleName, _epoch, SimulationSeed.ScheduleStream)
    );

    private void WakeLocked() => _wake.TrySetResult();

    private static TaskCompletionSource NewWake() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static void ThrowIfInvalid(SimulationProfile profile)
    {
        var errors = profile.Validate();
        if (errors.Count > 0)
        {
            throw new ArgumentException(string.Join("; ", errors), nameof(profile));
        }
    }
}