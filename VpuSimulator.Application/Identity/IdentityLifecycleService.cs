using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using VpuSimulator.Application.Interfaces;
using VpuSimulator.Application.Messaging;
using VpuSimulator.Application.Options;

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

    public IdentityLifecycleService(
        IMessageBrokerFactory factory,
        TimeProvider time,
        ILoggerFactory? loggerFactory = null)
    {
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(time);
        _factory = factory;
        _time = time;
        _loggerFactory = loggerFactory ?? NullLoggerFactory.Instance;
        _logger = _loggerFactory.CreateLogger<IdentityLifecycleService>();
    }

    /// <summary>Danh sách app_id đang chạy.</summary>
    public IReadOnlyCollection<string> AppIds => _identities.Keys.ToArray();

    public async Task<IdentityOpResult> AddAsync(IdentityOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        var appId = options.AppId;
        if (string.IsNullOrWhiteSpace(appId))
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
                // Mỗi lần (re)subscribe → phát status running cấp process (FR-ID-7).
                session.OnConnectedAsync = token => PublishProcessStatusAsync(session, appId, StatusMessages.ProcessRunning, token);
                await session.StartAsync(cancellationToken).ConfigureAwait(false);

                var cts = new CancellationTokenSource();
                var loop = new HeartbeatLoop(session, appId, interval, _time, _loggerFactory.CreateLogger<HeartbeatLoop>());
                var loopTask = loop.RunAsync(cts.Token);   // chạy tới await đầu tiên rồi trả Task về

                _identities[appId] = new RunningIdentity(session, cts, loopTask);
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
    /// Gỡ identity (FR-ID-6): dừng heartbeat → phát status stop (nếu đang kết nối) → ĐÓNG session broker.
    /// Đóng session thật sự là điểm bản tham chiếu của SRS đang làm sai (identity đã gỡ vẫn heartbeat và nhận lệnh) — đừng lặp lại.
    /// </summary>
    /// <returns>false nếu không có identity này.</returns>
    public async Task<bool> RemoveAsync(string appId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(appId);

        RunningIdentity? running;
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!_identities.TryRemove(appId, out running))
            {
                return false;
            }
        }
        finally
        {
            _gate.Release();
        }

        await StopIdentityAsync(appId, running, cancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <summary>Tắt có trật tự: mỗi identity phát stop cấp process rồi đóng session (FR-ID-3).</summary>
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

        // 2) Phát stop (best-effort, có giới hạn thời gian). Mất kết nối thì không gửi được — FR-ID-10: không đệm.
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
            return false;   // không parse được → từ chối (khớp FR-CTL-7 "heartbeat parse được")
        }

        interval = parsed > TimeSpan.Zero ? parsed : DefaultHeartbeat;   // ≤ 0 nghĩa là 30s
        return true;
    }

    private sealed record RunningIdentity(IBrokerSession Session, CancellationTokenSource Cts, Task LoopTask);
}
