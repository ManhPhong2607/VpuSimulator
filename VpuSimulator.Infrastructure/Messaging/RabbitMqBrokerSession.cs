using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using VpuSimulator.Application.Interfaces;
using VpuSimulator.Application.Options;

namespace VpuSimulator.Infrastructure.Messaging;

/// <summary>
/// 1 identity = 1 kết nối + 1 channel RabbitMQ riêng (SRS IF-MQ-2), tới đúng target (host/vhost/exchange) được chọn.
///
/// Vòng đời: <see cref="StartAsync"/> chỉ khởi động tác vụ nền rồi trả về NGAY. Tác vụ nền lặp:
/// kết nối → subscribe → báo <see cref="OnConnectedAsync"/> → chờ tới khi mất kết nối → đợi backoff → lặp lại.
/// Broker tắt thì không ném exception ra ngoài, chỉ ghi log rồi thử lại (FR-ID-2, FR-ID-7).
///
/// Cố ý TẮT auto-recovery của thư viện và tự viết vòng lặp, vì mỗi lần kết nối lại phải chạy callback
/// (để gửi lại <c>running</c>) và backoff phải đúng 1s→30s theo cấu hình.
/// </summary>
public sealed class RabbitMqBrokerSession : IBrokerSession
{
    private readonly BrokerTargetOptions _options;
    private readonly string _brokerName;
    private readonly string _appId;
    private readonly TimeProvider _time;
    private readonly ILogger _logger;
    private readonly JsonSerializerOptions _json;
    private readonly CancellationTokenSource _stop = new();
    private readonly SemaphoreSlim _publishLock = new(1, 1);

    private Task? _runTask;
    private volatile IChannel? _channel;
    private volatile bool _connected;
    private int _disposed;

    public RabbitMqBrokerSession(
        BrokerTargetOptions options,
        string brokerName,
        string appId,
        TimeProvider time,
        ILogger? logger = null,
        JsonSerializerOptions? json = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(brokerName);
        ArgumentException.ThrowIfNullOrWhiteSpace(appId);
        ArgumentNullException.ThrowIfNull(time);

        _options = options;
        _brokerName = brokerName;
        _appId = appId;
        _time = time;
        _logger = logger ?? NullLogger.Instance;
        // Mặc định của System.Text.Json KHÔNG bỏ key null → "detail": null vẫn có mặt (SRS CON-2). Đừng bật DefaultIgnoreCondition.
        _json = json ?? new JsonSerializerOptions();
    }

    public bool IsConnected => _connected;

    public Func<CancellationToken, Task>? OnConnectedAsync { get; set; }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) == 1, this);
        if (_runTask is not null)
        {
            throw new InvalidOperationException("Session đã được Start.");
        }

        // Task.Run: toàn bộ kết nối nằm ngoài luồng gọi, StartAsync không bao giờ chờ broker.
        _runTask = Task.Run(() => RunAsync(_stop.Token), CancellationToken.None);
        return Task.CompletedTask;
    }

    public async Task PublishAsync(string routingKey, object envelope, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(routingKey);
        ArgumentNullException.ThrowIfNull(envelope);

        var channel = _channel;
        if (!_connected || channel is null)
        {
            // Mất kết nối thì bỏ, không đệm (SRS FR-ID-10). Bên gọi (HeartbeatLoop) đã tự kiểm tra IsConnected.
            throw new InvalidOperationException($"Identity '{_appId}' chưa kết nối tới broker '{_brokerName}'.");
        }

        var body = JsonSerializer.SerializeToUtf8Bytes(envelope, envelope.GetType(), _json);
        var properties = new BasicProperties { ContentType = "application/json" };   // SRS IF-MQ-6

        // Serialize các lần publish trên cùng channel (không giả định channel an toàn khi publish đồng thời).
        await _publishLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await channel.BasicPublishAsync(_options.Exchange, routingKey, mandatory: false, properties, body, cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            _publishLock.Release();
        }
    }

    private async Task RunAsync(CancellationToken ct)
    {
        var delay = _options.ReconnectMin;

        while (!ct.IsCancellationRequested)
        {
            IConnection? connection = null;
            IChannel? channel = null;
            try
            {
                var factory = new ConnectionFactory
                {
                    HostName = _options.Host,
                    Port = _options.Port,
                    VirtualHost = _options.VirtualHost,
                    UserName = _options.Username,
                    Password = _options.Password,
                    ClientProvidedName = $"vpu-simulator:{_appId}",
                    AutomaticRecoveryEnabled = false,
                    TopologyRecoveryEnabled = false,
                    RequestedConnectionTimeout = TimeSpan.FromSeconds(10),
                };

                connection = await factory.CreateConnectionAsync(ct).ConfigureAwait(false);
                var lost = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                connection.ConnectionShutdownAsync += (_, _) => { lost.TrySetResult(); return Task.CompletedTask; };

                var ch = await connection.CreateChannelAsync(cancellationToken: ct).ConfigureAwait(false);
                channel = ch;
                ch.ChannelShutdownAsync += (_, _) => { lost.TrySetResult(); return Task.CompletedTask; };

                if (_options.Prefetch > 0)
                {
                    await ch.BasicQosAsync(0, (ushort)Math.Min(_options.Prefetch, ushort.MaxValue), false, ct).ConfigureAwait(false);
                }

                // Hàng đợi riêng của identity: tên do server đặt, exclusive + auto-delete → biến mất khi kết nối đóng.
                // 🟠 Giả định (SRS không quy định tên/độ bền hàng đợi): đủ cho simulator, xem lại nếu VMS cần lệnh đã gửi lúc VPU đang tắt.
                var queue = (await ch.QueueDeclareAsync(queue: string.Empty, durable: false, exclusive: true, autoDelete: true,
                    cancellationToken: ct).ConfigureAwait(false)).QueueName;

                // SRS IF-MQ-3 / FR-CMD-1: outbound.vpu.<app_id>.#  (xem DATA CONFLICT #8 về .# và .*, chưa xác nhận với VMS)
                await ch.QueueBindAsync(queue, _options.Exchange, $"outbound.vpu.{_appId}.#", cancellationToken: ct).ConfigureAwait(false);

                var consumer = new AsyncEventingBasicConsumer(ch);
                consumer.ReceivedAsync += (_, args) => OnReceivedAsync(ch, args);
                await ch.BasicConsumeAsync(queue, _options.AutoAck, consumer, ct).ConfigureAwait(false);

                _channel = ch;
                _connected = true;
                delay = _options.ReconnectMin;   // kết nối thành công → backoff về đầu
                _logger.LogInformation("Identity {AppId} connected to broker '{Broker}' ({Target})", _appId, _brokerName, _options);

                await InvokeOnConnectedAsync(ct).ConfigureAwait(false);

                await lost.Task.WaitAsync(ct).ConfigureAwait(false);   // chặn tới khi mất kết nối hoặc bị huỷ
                _logger.LogWarning("Identity {AppId} lost connection to broker '{Broker}'", _appId, _brokerName);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Identity {AppId} cannot use broker '{Broker}' ({Target}); retry in {Delay}",
                    _appId, _brokerName, _options, delay);
            }
            finally
            {
                _connected = false;
                _channel = null;
                await CloseQuietlyAsync(channel, connection).ConfigureAwait(false);
            }

            try
            {
                await Task.Delay(delay, _time, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            delay = TimeSpan.FromTicks(Math.Min(delay.Ticks * 2, _options.ReconnectMax.Ticks));
        }
    }

    private async Task InvokeOnConnectedAsync(CancellationToken ct)
    {
        var callback = OnConnectedAsync;
        if (callback is null) return;

        try
        {
            await callback(ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Hợp đồng IBrokerSession: callback lỗi không được làm sập vòng reconnect.
            _logger.LogError(ex, "OnConnected callback of identity {AppId} failed", _appId);
        }
    }

    private async Task OnReceivedAsync(IChannel channel, BasicDeliverEventArgs args)
    {
        // CHƯA có bộ xử lý lệnh ở bước heartbeat: chỉ ghi log để thấy lệnh VMS tới đúng identity.
        // Khi làm Giai đoạn 2 (add-task...), thêm callback nhận lệnh vào IBrokerSession rồi gọi ở đây.
        _logger.LogDebug("Identity {AppId} received message on '{RoutingKey}' ({Bytes} bytes), no handler yet",
            _appId, args.RoutingKey, args.Body.Length);

        if (!_options.AutoAck)
        {
            await channel.BasicAckAsync(args.DeliveryTag, multiple: false).ConfigureAwait(false);
        }
    }

    private async Task CloseQuietlyAsync(IChannel? channel, IConnection? connection)
    {
        // DisposeAsync của IChannel/IConnection (RabbitMQ.Client 7.x) đóng kết nối nếu còn mở.
        try
        {
            if (channel is not null) await channel.DisposeAsync().ConfigureAwait(false);
            if (connection is not null) await connection.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error while closing connection of identity {AppId}", _appId);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1) return;

        _connected = false;
        await _stop.CancelAsync().ConfigureAwait(false);

        if (_runTask is not null)
        {
            try
            {
                // Vòng nền tự đóng channel/connection trong finally. Giới hạn thời gian để tắt app không bị treo.
                await _runTask.WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Session of identity {AppId} did not stop cleanly", _appId);
            }
        }

        _stop.Dispose();
        _publishLock.Dispose();
    }
}
