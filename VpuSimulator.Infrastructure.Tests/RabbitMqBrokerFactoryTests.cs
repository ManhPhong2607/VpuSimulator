using VpuSimulator.Application.Options;
using VpuSimulator.Infrastructure.Messaging;

namespace VpuSimulator.Infrastructure.Tests;

/// <summary>Không cần RabbitMQ: chỉ kiểm tra tra cứu target và hợp đồng "không ném lỗi khi broker tắt".</summary>
public class RabbitMqBrokerFactoryTests
{
    private static RabbitMqBrokerFactory Create() => new(
        new Dictionary<string, BrokerTargetOptions>
        {
            ["default"] = new() { Host = "127.0.0.1", Port = 1 },   // cổng 1: không ai nghe → kết nối bị từ chối
            ["Site-B"] = new() { Host = "127.0.0.1", Port = 1, Exchange = "x" },
        },
        TimeProvider.System);

    [Fact]
    public void Exists_ignores_case_and_rejects_unknown_or_blank()
    {
        var f = Create();
        Assert.True(f.Exists("default"));
        Assert.True(f.Exists("site-b"));
        Assert.False(f.Exists("nope"));
        Assert.False(f.Exists(""));
    }

    [Fact]
    public void CreateSession_for_unknown_target_throws_ArgumentException()
    {
        Assert.Throws<ArgumentException>(() => Create().CreateSession("nope", "vpu"));
    }

    [Fact]
    public async Task CreateSession_does_not_connect_and_can_be_disposed_without_start()
    {
        var session = Create().CreateSession("default", "vpu");

        Assert.False(session.IsConnected);
        await session.DisposeAsync();
    }

    [Fact]
    public async Task StartAsync_returns_immediately_when_broker_is_down_and_publish_is_rejected()
    {
        var session = Create().CreateSession("default", "vpu");
        var started = Environment.TickCount64;

        await session.StartAsync();

        Assert.True(Environment.TickCount64 - started < 2000, "StartAsync không được chờ kết nối");
        await Task.Delay(300);   // cho vòng nền thử kết nối ít nhất một lần
        Assert.False(session.IsConnected);
        await Assert.ThrowsAsync<InvalidOperationException>(() => session.PublishAsync("inbound.status.vpu.vpu", new { a = 1 }));

        var stopped = Environment.TickCount64;
        await session.DisposeAsync();
        Assert.True(Environment.TickCount64 - stopped < 3000, "Dispose không được bị treo bởi vòng reconnect");
    }

    [Fact]
    public async Task Start_twice_is_rejected()
    {
        var session = Create().CreateSession("default", "vpu");
        await session.StartAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => session.StartAsync());
        await session.DisposeAsync();
    }
}
