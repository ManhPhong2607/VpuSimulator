namespace VpuSimulator.Application.Options;

/// <summary>
/// 1 target broker = 1 bộ cấu hình kết nối
/// (broker.host/port/virtual_host/username/password/exchange/prefetch/auto_ack/reconnect_*).
/// </summary>
public sealed class BrokerTargetOptions
{
    /// <summary>Tên target mặc định — dùng khi identity không chỉ định broker.</summary>
    public const string DefaultName = "default";

    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string VirtualHost { get; set; } = "/";
    public string Username { get; set; } = "guest";
    public string Password { get; set; } = "guest";   // secret — không log
    public string Exchange { get; set; } = "amq.topic";
    public int Prefetch { get; set; } = 0;
    public bool AutoAck { get; set; } = true;
    public TimeSpan ReconnectMin { get; set; } = TimeSpan.FromSeconds(1);
    public TimeSpan ReconnectMax { get; set; } = TimeSpan.FromSeconds(30);

    public override string ToString() => $"{Host}:{Port}{VirtualHost} exchange={Exchange}";   // chỉ host:port, không có credential
}
