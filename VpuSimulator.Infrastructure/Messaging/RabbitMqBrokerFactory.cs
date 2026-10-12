using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using VpuSimulator.Application.Interfaces;
using VpuSimulator.Application.Options;

namespace VpuSimulator.Infrastructure.Messaging;

/// <summary>
/// Tạo <see cref="RabbitMqBrokerSession"/> theo tên target trong cấu hình. Đây là chỗ DUY NHẤT biết
/// "tên target → host/vhost/exchange nào", nên đổi đích bắn heartbeat chỉ là đổi cấu hình.
/// Tạo session KHÔNG kết nối; chỉ <c>StartAsync</c> mới bắt đầu.
/// </summary>
public sealed class RabbitMqBrokerFactory : IMessageBrokerFactory
{
    private readonly IReadOnlyDictionary<string, BrokerTargetOptions> _targets;
    private readonly TimeProvider _time;
    private readonly ILoggerFactory _loggerFactory;

    public RabbitMqBrokerFactory(
        IEnumerable<KeyValuePair<string, BrokerTargetOptions>> targets,
        TimeProvider time,
        ILoggerFactory? loggerFactory = null)
    {
        ArgumentNullException.ThrowIfNull(targets);
        ArgumentNullException.ThrowIfNull(time);

        // Sao chép sang từ điển không phân biệt hoa thường: đổi cấu hình lúc chạy không làm hỏng factory.
        _targets = new Dictionary<string, BrokerTargetOptions>(targets, StringComparer.OrdinalIgnoreCase);
        _time = time;
        _loggerFactory = loggerFactory ?? NullLoggerFactory.Instance;
    }

    public bool Exists(string brokerName) => !string.IsNullOrWhiteSpace(brokerName) && _targets.ContainsKey(brokerName);

    public IBrokerSession CreateSession(string brokerName, string appId)
    {
        if (!Exists(brokerName))
        {
            throw new ArgumentException($"Broker target '{brokerName}' không có trong cấu hình.", nameof(brokerName));
        }

        return new RabbitMqBrokerSession(
            _targets[brokerName],
            brokerName,
            appId,
            _time,
            _loggerFactory.CreateLogger<RabbitMqBrokerSession>());
    }
}
