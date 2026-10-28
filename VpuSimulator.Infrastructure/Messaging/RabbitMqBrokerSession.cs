using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using VpuSimulator.Application.Interfaces;
using VpuSimulator.Application.Interfaces.Messaging;
using VpuSimulator.Application.Messaging;
using VpuSimulator.Application.Options;

namespace VpuSimulator.Infrastructure.Messaging;

/// <summary>
/// 1 identity = 1 kết nối + 1 channel RabbitMQ riêng, tới đúng target (host/vhost/exchange) được chọn.
///
/// Vòng đời: StartAsync chỉ khởi động tác vụ nền rồi trả về NGAY. Tác vụ nền lặp:
/// kết nối → subscribe → báo OnConnectedAsync → chờ tới khi mất kết nối → đợi backoff → lặp lại.
/// Broker tắt thì không ném exception ra ngoài, chỉ ghi log rồi thử lại.
///
/// Cố ý TẮT auto-recovery của thư viện và tự viết vòng lặp, vì mỗi lần kết nối lại phải chạy callback
/// (để gửi lại running) và backoff phải đúng 1s→30s theo cấu hình.
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
    private static readonly TimeSpan StableConnection = TimeSpan.FromSeconds(30);

    private Task? _runTask;
    private volatile IChannel? _channel;
    private volatile bool _channelReady;
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
        // Mặc định của System.Text.Json KHÔNG bỏ key null → "detail": null vẫn có mặt. Đừng bật DefaultIgnoreCondition.
        _json = json ?? new JsonSerializerOptions();
    }

    public bool IsConnected => _connected;

    public Func<CancellationToken, Task>? OnConnectedAsync { get; set; }

    public Func<InboundMessage, CancellationToken, Task>? OnMessageAsync { get; set; }

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
        
        var body = JsonSerializer.SerializeToUtf8Bytes(envelope, envelope.GetType(), _json);
        var properties = new BasicProperties { ContentType = "application/json" };
        
        await _publishLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Đọc trạng thái SAU khi giữ khoá, để không dùng channel đã bị đóng ngay trước đó.
            var channel = _channel;
            if (!_channelReady || channel is null)
            {
                throw new InvalidOperationException($"Identity '{_appId}' chưa kết nối tới broker '{_brokerName}'.");
            }

            try
            {
                await channel.BasicPublishAsync(_options.Exchange, routingKey, mandatory: false, properties, body, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is RabbitMQ.Client.Exceptions.AlreadyClosedException
                                           or RabbitMQ.Client.Exceptions.OperationInterruptedException
                                           or ObjectDisposedException)
            {
                // Mất kết nối giữa lúc publish: coi như "chưa kết nối" (không đệm).
                throw new InvalidOperationException($"Identity '{_appId}' mất kết nối khi đang gửi tới broker '{_brokerName}'.", ex);
            }
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
                    RequestedHeartbeat = TimeSpan.FromSeconds(10)
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
                
                try
                {
                    await ch.ExchangeDeclarePassiveAsync(_options.Exchange, ct).ConfigureAwait(false);
                }
                catch (RabbitMQ.Client.Exceptions.OperationInterruptedException ex) when (ex.ShutdownReason?.ReplyCode == 404)
                {
                    throw new ExchangeMissingException(
                        $"Exchange '{_options.Exchange}' chưa tồn tại trên broker '{_brokerName}' (vhost '{_options.VirtualHost}'). " +
                        "VMS phải tạo trước, hoặc tạo tay kiểu topic.", ex);
                }
                
                // Hàng đợi riêng của identity: tên do server đặt, exclusive + auto-delete → biến mất khi kết nối đóng.
                // Giả định: đủ cho simulator, xem lại nếu VMS cần lệnh đã gửi lúc VPU đang tắt.
                var queue = (await ch.QueueDeclareAsync(queue: string.Empty, durable: false, exclusive: true, autoDelete: true,
                    cancellationToken: ct).ConfigureAwait(false)).QueueName;
                
                await ch.QueueBindAsync(queue, _options.Exchange, $"outbound.vpu.{_appId}.#", cancellationToken: ct).ConfigureAwait(false);

                var consumer = new AsyncEventingBasicConsumer(ch);
                consumer.ReceivedAsync += (_, args) => OnReceivedAsync(ch, args, ct);
                await ch.BasicConsumeAsync(queue, _options.AutoAck, consumer, ct).ConfigureAwait(false);

                _channel = ch;
                _channelReady = true;
                var connectedAt = _time.GetTimestamp(); // backoff chỉ về đầu khi kết nối sống đủ lâu
                _logger.LogInformation("Identity {AppId} connected to broker '{Broker}' ({Target})", _appId, _brokerName, _options);

                await InvokeOnConnectedAsync(ct).ConfigureAwait(false);
                _connected = true;
                
                await lost.Task.WaitAsync(ct).ConfigureAwait(false);   // chặn tới khi mất kết nối hoặc bị huỷ

                // Chỉ về backoff đầu nếu kết nối đã sống đủ lâu; kết nối chập chờn thì backoff tiếp tục tăng.
                if (_time.GetElapsedTime(connectedAt) >= StableConnection)
                {
                    delay = _options.ReconnectMin;
                }
                _logger.LogWarning("Identity {AppId} lost connection to broker '{Broker}'", _appId, _brokerName);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (ExchangeMissingException ex)
            {
                // Không truyền ex vào LogWarning để không in stack trace mỗi lần thử lại.
                _logger.LogWarning("Identity {AppId}: {Message} Thử lại sau {Delay}.", _appId, ex.Message, delay);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Identity {AppId} cannot use broker '{Broker}' ({Target}); retry in {Delay}",
                    _appId, _brokerName, _options, delay);
            }
            finally
            {
                _connected = false;
                _channelReady = false;
                _channel = null;
                await CloseQuietlyAsync(channel, connection).ConfigureAwait(false);
            }

            try
            {
                // Cộng thêm 0 đến 20% ngẫu nhiên: nhiều identity cùng rớt sẽ không nối lại cùng một lúc.
                var wait = delay + TimeSpan.FromMilliseconds(Random.Shared.NextDouble() * 0.2 * delay.TotalMilliseconds);
                await Task.Delay(wait, _time, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            delay = TimeSpan.FromTicks(Math.Min(delay.Ticks * 2, _options.ReconnectMax.Ticks));
        }
    }

    private Task InvokeOnConnectedAsync(CancellationToken ct)
        => OnConnectedAsync?.Invoke(ct) ?? Task.CompletedTask;

    private async Task OnReceivedAsync(IChannel channel, BasicDeliverEventArgs args, CancellationToken ct)
    {
        // Body của thư viện chỉ hợp lệ trong lúc xử lý sự kiện này (bộ nhớ được tái sử dụng) → sao chép trước khi giao đi.
        var message = new InboundMessage(args.RoutingKey, args.Body.ToArray());
        var handler = OnMessageAsync;

        if (handler is null)
        {
            _logger.LogDebug("Identity {AppId} received message on '{RoutingKey}' ({Bytes} bytes), no handler set",
                _appId, message.RoutingKey, message.Body.Length);
        }
        else
        {
            try
            {
                await handler(message, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // Đang tắt session: bỏ qua.
            }
            catch (Exception ex)
            {
                // Nuốt lỗi: một bản tin hỏng không được làm đứt consumer hay vòng reconnect. Không log body.
                _logger.LogError(ex, "Identity {AppId}: handler failed on '{RoutingKey}' ({Bytes} bytes); message dropped",
                    _appId, message.RoutingKey, message.Body.Length);
            }
        }

        if (!_options.AutoAck)
        {
            try
            {
                await channel.BasicAckAsync(args.DeliveryTag, multiple: false, ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is RabbitMQ.Client.Exceptions.AlreadyClosedException
                                           or RabbitMQ.Client.Exceptions.OperationInterruptedException
                                           or ObjectDisposedException
                                           or OperationCanceledException)
            {
                // Kênh đã đóng giữa chừng: broker sẽ tự gửi lại bản tin chưa ack cho kết nối kế tiếp. Không phải lỗi.
                _logger.LogDebug(ex, "Identity {AppId}: cannot ack message on '{RoutingKey}', channel closed", _appId, message.RoutingKey);
            }
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

        if (_runTask is null || _runTask.IsCompleted) _stop.Dispose();
    }
    // Exchange chưa tồn tại là lỗi cấu hình/môi trường đã biết rõ nguyên nhân: log một dòng, không in stack trace.
    private sealed class ExchangeMissingException(string message, Exception inner) : Exception(message, inner);
}
