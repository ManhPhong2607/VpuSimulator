using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using VpuSimulator.Application.Interfaces;
using VpuSimulator.Application.Messaging;
using VpuSimulator.Application.Options;
using VpuSimulator.Application.State;

namespace VpuSimulator.Application.Identity;

/// <summary>Kết quả thêm identity — Control API ánh xạ: Duplicate → 409, Invalid*/UnknownBroker → 400</summary>
public enum IdentityOpResult
{
    Added,
    Duplicate,
    InvalidAppId,
    InvalidHeartbeat,
    UnknownBroker,
    Failed,
}

public sealed record IdentityStatus(string AppId, bool IsConnected, long HeartbeatFailures, DateTimeOffset? LastHeartbeatAt);

/// <summary>
/// Quản lý vòng đời các identity: thêm lúc khởi động hoặc runtime, gỡ, tắt có trật tự
/// Mỗi identity có session broker riêng + vòng heartbeat riêng, chạy độc lập
///
/// Class thường, KHÔNG phải BackgroundService, để Application không phụ thuộc gói Hosting. Lớp host (Api) bọc nó trong
/// IHostedService: StartAsync → AddAsync từng identity trong cấu hình; StopAsync → StopAllAsync 
/// </summary>
public sealed class IdentityLifecycleService : IAsyncDisposable
{
    private static readonly TimeSpan DefaultHeartbeat = TimeSpan.FromSeconds(30);   // cấu hình: "≤ 0 nghĩa là 30s"
    private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(5);         // giới hạn thời gian gửi status stop

    private readonly IMessageBrokerFactory _factory;
    private readonly TimeProvider _time;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<string, RunningIdentity> _identities = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _gate = new(1, 1);   // thêm/gỡ identity hiếm, tuần tự hoá cho đơn giản
    private readonly Func<TimeSpan, TimeSpan>? _heartbeatJitter;
    private readonly IInboundMessageHandler? _inboundHandler;
    private readonly ITaskStore? _taskStore;

    public IdentityLifecycleService(
        IMessageBrokerFactory factory,
        TimeProvider time,
        ILoggerFactory? loggerFactory = null,
        Func<TimeSpan, TimeSpan>? heartbeatJitter = null,
        IInboundMessageHandler? inboundHandler = null,
        ITaskStore? taskStore = null)
    {
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(time);
        _factory = factory;
        _time = time;
        _loggerFactory = loggerFactory ?? NullLoggerFactory.Instance;
        _logger = _loggerFactory.CreateLogger<IdentityLifecycleService>();
        _heartbeatJitter = heartbeatJitter;
        _inboundHandler = inboundHandler;
        _taskStore = taskStore;
    }

    //Danh sách app_id đang chạy
    public IReadOnlyCollection<string> AppIds => _identities.Keys.ToArray();

    public async Task<IdentityOpResult> AddAsync(IdentityOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        var appId = options.AppId;
        if (!AppIdRules.IsValid(appId))
        {
            return IdentityOpResult.InvalidAppId;
        }

        if (!TryResolveInterval(options.Heartbeat, out var interval))
        {
            return IdentityOpResult.InvalidHeartbeat;
        }

        var brokerName = string.IsNullOrWhiteSpace(options.Broker) ? BrokerTargetOptions.DefaultName : options.Broker;
        if (!_factory.Exists(brokerName))
        {
            return IdentityOpResult.UnknownBroker;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_identities.ContainsKey(appId))
            {
                return IdentityOpResult.Duplicate;
            }

            IBrokerSession session;
            try
            {
                session = _factory.CreateSession(brokerName, appId);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Cannot create broker session for identity {AppId} on target {Broker}", appId, brokerName);
                return IdentityOpResult.Failed;
            }

            var registered = false;
            try
            {
                var cts = new CancellationTokenSource();
                var loop = new HeartbeatLoop
                    (session, 
                    appId, 
                    interval, 
                    _time, _loggerFactory.CreateLogger<HeartbeatLoop>(),
                    _heartbeatJitter?.Invoke(interval) ?? TimeSpan.Zero);
                // Mỗi lần (re)subscribe → phát status running cấp process
                session.OnConnectedAsync = async token =>
                {
                    // Thứ tự bắt buộc: running trước, rồi heartbeat ngay, để VMS không phải chờ thêm một chu kỳ sau khi nối lại.
                    await PublishProcessStatusAsync(session, appId, StatusMessages.ProcessRunning, token).ConfigureAwait(false);
                    await session.PublishAsync(RoutingKeys.Status(appId),
                        StatusMessages.Heartbeat(appId, _time.GetUtcNow()), token).ConfigureAwait(false);
                    loop.RecordHeartbeatSuccess();
                };
                // Mọi lệnh VMS gửi xuống identity này đi vào một handler chung, kèm appId để handler biết thuộc identity nào.
                // Phải đặt TRƯỚC StartAsync, để không mất bản tin nào ngay sau khi subscribe.
                if (_inboundHandler is not null)
                {
                    session.OnMessageAsync = (message, token) => _inboundHandler.HandleAsync(appId, session, message, token);
                }
                await session.StartAsync(cancellationToken).ConfigureAwait(false);

                var loopTask = loop.RunAsync(cts.Token);   // chạy tới await đầu tiên rồi trả Task về
                    
                _identities[appId] = new RunningIdentity(session, cts, loop, loopTask);
                registered = true;
                return IdentityOpResult.Added;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Cannot start identity {AppId}", appId);
                return IdentityOpResult.Failed;
            }
            finally
            {
                if (!registered)
                {
                    await DisposeSessionAsync(session, appId).ConfigureAwait(false);
                }
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Gỡ identity: dừng heartbeat → phát status stop (nếu đang kết nối) → ĐÓNG session broker.
    /// False nếu không có identity này
    /// </summary>
    public async Task<bool> RemoveAsync(string appId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(appId);

        if (!_identities.TryGetValue(appId, out var running)) return false;
        if (Interlocked.Exchange(ref running.Stopping, 1) == 1) return false;   // đã có người đang gỡ

        try
        {
            await StopIdentityAsync(appId, running, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _identities.TryRemove(new KeyValuePair<string, RunningIdentity>(appId, running));
            ClearTasks(appId);
        }
        return true;
    }

    //Tắt có trật tự: mỗi identity phát stop cấp process rồi đóng session
    public async Task StopAllAsync(CancellationToken cancellationToken = default)
    {
        var appIds = _identities.Keys.ToArray();
        await Task.WhenAll(appIds.Select(id => RemoveAsync(id, cancellationToken))).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        await StopAllAsync().ConfigureAwait(false);
        _gate.Dispose();
    }

    private async Task StopIdentityAsync(string appId, RunningIdentity running, CancellationToken cancellationToken)
    {
        // 1) Dừng heartbeat trước, để heartbeat không chen vào sau status stop.
        running.Cts.Cancel();
        try
        {
            await running.LoopTask.ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Heartbeat loop of identity {AppId} ended with error", appId);
        }

        // 2) Phát stop (best-effort, có giới hạn thời gian). Mất kết nối thì không gửi được không đệm.
        if (running.Session.IsConnected)
        {
            using var stopCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            stopCts.CancelAfter(StopTimeout);
            try
            {
                await PublishProcessStatusAsync(running.Session, appId, StatusMessages.ProcessStop, stopCts.Token)
                    .ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Cannot publish stop status for identity {AppId}", appId);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Publishing stop status for identity {AppId} was cancelled or timed out", appId);
            }
        }

        // 3) Đóng session: ngừng nhận lệnh, giải phóng tài nguyên.
        await DisposeSessionAsync(running.Session, appId).ConfigureAwait(false);
        running.Cts.Dispose();
    }

    private async Task PublishProcessStatusAsync(
        IBrokerSession session,
        string appId,
        Func<string, DateTimeOffset, StatusMessage> build,
        CancellationToken cancellationToken)
    {
        var message = build(appId, _time.GetUtcNow());
        await session.PublishAsync(RoutingKeys.Status(appId), message, cancellationToken).ConfigureAwait(false);
    }

    private void ClearTasks(string appId)
    {
        if (_taskStore is null) return;
        var removed = _taskStore.Clear(appId);
        if (removed.Count > 0)
        {
            _logger.LogInformation("Identity {AppId}: đã xóa {Count} task", appId,  removed.Count);
        }
    }
    
    private async Task DisposeSessionAsync(IBrokerSession session, string appId)
    {
        try
        {
            await session.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error while closing broker session of identity {AppId}", appId);
        }
    }

    private static bool TryResolveInterval(string? text, out TimeSpan interval)
    {
        interval = DefaultHeartbeat;
        if (string.IsNullOrWhiteSpace(text))
        {
            return true;   // không cấu hình → mặc định 30s
        }

        if (!DurationParser.TryParse(text, out var parsed))
        {
            return false;   // không parse được → từ chối ("heartbeat parse được")
        }

        interval = parsed > TimeSpan.Zero ? parsed : DefaultHeartbeat;   // ≤ 0 nghĩa là 30s
        return true;
    }

    private sealed class RunningIdentity(IBrokerSession session, CancellationTokenSource cts, HeartbeatLoop loop, Task loopTask)
    {
        public IBrokerSession Session { get; } = session;
        public CancellationTokenSource Cts { get; } = cts;
        public HeartbeatLoop Loop { get; } = loop;
        public Task LoopTask { get; } = loopTask;
        public int Stopping; 
    }
    
    public IReadOnlyList<IdentityStatus> GetStatuses() =>
        _identities.Select(kv => new IdentityStatus(kv.Key, kv.Value.Session.IsConnected,
                kv.Value.Loop.PublishFailureCount, kv.Value.Loop.LastHeartbeatAt))
            .OrderBy(s => s.AppId, StringComparer.Ordinal).ToList();
}
