namespace VpuSimulator.Infrastructure.Tests.Support;

/// <summary>
/// Test tích hợp cần RabbitMQ thật. Tự BỎ QUA nếu biến môi trường <c>VPU_RABBITMQ_HOST</c> chưa đặt,
/// để <c>dotnet test</c> luôn chạy được trên máy/CI không có broker.
/// Bật bằng:  docker compose -f docker-compose.rabbitmq.yml up -d  rồi  VPU_RABBITMQ_HOST=localhost dotnet test
/// </summary>
public sealed class FactIfRabbitMqAttribute : FactAttribute
{
    public const string HostVariable = "VPU_RABBITMQ_HOST";

    public FactIfRabbitMqAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(HostVariable)))
        {
            Skip = $"Đặt biến môi trường {HostVariable} (và RabbitMQ đang chạy) để chạy test tích hợp.";
        }
    }
}
