using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using VpuSimulator.Application.Interfaces;
using VpuSimulator.Application.Interfaces.Messaging;
using VpuSimulator.Application.Messaging;

namespace VpuSimulator.Application.Identity;

/// <summary>
/// Vòng heartbeat của 1 identity: mỗi chu kỳ, nếu session đang kết nối thì
/// publish 1 bản tin heartbeat lên inbound.status.vpu.app_id.
/// Phạm vi cố ý hẹp: vòng này KHÔNG gửi running/stop (do IdentityLifecycleService và
/// IBrokerSession.OnConnectedAsync lo), KHÔNG biết exchange/host nào (do session lo), và KHÔNG tự
/// reconnect. Nhờ vậy test được bằng broker giả và đồng hồ giả
/// </summary>
public sealed class HeartbeatLoop
{
    private readonly IBrokerSession _session;
    private readonly string _appId;
    private readonly TimeSpan _interval;
    private readonly TimeProvider _time;
    private readonly ILogger _logger;
    private long _publishFailures;
    private readonly TimeSpan _initialDelay;
    private long _lastSuccessTicks;
    
    public HeartbeatLoop(
        IBrokerSession session,
        string appId,
        TimeSpan interval,
        TimeProvider time,
        ILogger? logger = null,
        TimeSpan initialDelay = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentException.ThrowIfNullOrWhiteSpace(appId);
        ArgumentNullException.ThrowIfNull(time);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(interval, TimeSpan.Zero);
        _initialDelay = initialDelay < TimeSpan.Zero ? TimeSpan.Zero : initialDelay;

        _session = session;
        _appId = appId;
        _interval = interval;
        _time = time;
        _logger = logger ?? NullLogger.Instance;
    }

    // Số lần publish heartbeat thất bại (khi đang kết nối). Bản tin lỗi bị bỏ, không gửi bù
    public long PublishFailureCount => Interlocked.Read(ref _publishFailures);
    public DateTimeOffset? LastHeartbeatAt
    {
        get { var t = Interlocked.Read(ref _lastSuccessTicks); return t == 0 ? null : new DateTimeOffset(t, TimeSpan.Zero); }
    }
    /// <summary>
    /// Chạy tới khi cancellationToken bị huỷ. Timer được tạo ĐỒNG BỘ ở lệnh đầu tiên của hàm
    /// (trước await đầu tiên) — test dựa vào điều này để biết khi RunAsync trả Task về thì timer đã sẵn sàng
    /// </summary>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        if (_initialDelay > TimeSpan.Zero)
        {
            try { await Task.Delay(_initialDelay, _time, cancellationToken).ConfigureAwait(false); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return; }
        }
        using var timer = new PeriodicTimer(_interval, _time);

        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken)) 
            {
                // chỉ heartbeat khi đang kết nối. Mất kết nối thì bỏ qua tick này (không đệm, không gửi bù)
                if (!_session.IsConnected)
                {
                    continue;
                }

                await PublishHeartbeatAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Dừng có chủ đích (gỡ identity / tắt tiến trình) — không phải lỗi
        }
    }

    private async Task PublishHeartbeatAsync(CancellationToken cancellationToken)
    {
        // Mỗi lần publish có hạn chót = 1 chu kỳ: publish treo (chờ khoá, mạng nghẽn) không chặn được các tick sau.
        using var publishCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        publishCts.CancelAfter(_interval);

        try
        {
            var message = StatusMessages.Heartbeat(_appId, _time.GetUtcNow());
            await _session.PublishAsync(RoutingKeys.Status(_appId), message, publishCts.Token);
            Interlocked.Exchange(ref _lastSuccessTicks, _time.GetUtcNow().UtcTicks);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            // Lỗi publish (kể cả quá hạn) của identity này không được làm sập vòng lặp, không ảnh hưởng identity khác.
            // Huỷ có chủ đích (gỡ identity/tắt tiến trình) thì cancellationToken đã bị huỷ nên không rơi vào đây.
            Interlocked.Increment(ref _publishFailures);
            _logger.LogWarning(ex, "Heartbeat publish failed for identity {AppId}", _appId);
        }
    }
    
    public void RecordHeartbeatSuccess()
    {
        Interlocked.Exchange(ref _lastSuccessTicks, _time.GetUtcNow().UtcTicks);
    }

}
