using System.Collections.Concurrent;
using System.Text.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using VpuSimulator.Application.Identity;
using VpuSimulator.Application.Options;
using VpuSimulator.Infrastructure.Messaging;
using VpuSimulator.Infrastructure.Tests.Support;

namespace VpuSimulator.Infrastructure.Tests;

/// <summary>
/// Chạy với RabbitMQ thật. Đóng vai VMS: tự bind một hàng đợi vào <c>inbound.status.vpu.#</c> rồi quan sát đúng những gì
/// simulator gửi ra. Mặc định bị BỎ QUA (xem <see cref="FactIfRabbitMqAttribute"/>).
/// </summary>
public class RabbitMqHeartbeatIntegrationTests
{
    [FactIfRabbitMq]
    public async Task Identity_publishes_running_then_heartbeat_then_stop_on_a_real_broker()
    {
        var host = Environment.GetEnvironmentVariable(FactIfRabbitMqAttribute.HostVariable)!;
        var target = new BrokerTargetOptions { Host = host };   // guest/guest, vhost "/", exchange amq.topic
        var appId = "it-" + Guid.NewGuid().ToString("N")[..8];
        var received = new ConcurrentQueue<JsonElement>();

        // "VMS": nghe bản tin status của đúng identity này.
        var vmsFactory = new ConnectionFactory { HostName = host };
        await using var vmsConnection = await vmsFactory.CreateConnectionAsync();
        await using var vmsChannel = await vmsConnection.CreateChannelAsync();
        var queue = (await vmsChannel.QueueDeclareAsync()).QueueName;
        await vmsChannel.QueueBindAsync(queue, target.Exchange, $"inbound.status.vpu.{appId}");
        var consumer = new AsyncEventingBasicConsumer(vmsChannel);
        consumer.ReceivedAsync += (_, a) =>
        {
            received.Enqueue(JsonSerializer.Deserialize<JsonElement>(a.Body.Span));
            return Task.CompletedTask;
        };
        await vmsChannel.BasicConsumeAsync(queue, autoAck: true, consumer);

        var factory = new RabbitMqBrokerFactory(new Dictionary<string, BrokerTargetOptions> { ["default"] = target }, TimeProvider.System);
        await using var service = new IdentityLifecycleService(factory, TimeProvider.System);

        Assert.Equal(IdentityOpResult.Added, await service.AddAsync(new IdentityOptions { AppId = appId, Heartbeat = "500ms" }));

        await WaitFor(() => received.Any(m => Kind(m) == "running"), "running");
        await WaitFor(() => received.Count(m => Kind(m) == "heartbeat") >= 2, "2 heartbeat");

        var hb = received.First(m => Kind(m) == "heartbeat");
        Assert.Equal("", hb.GetProperty("appid").GetString());
        Assert.Equal("process-unit", hb.GetProperty("scope").GetString());
        Assert.Equal(appId, hb.GetProperty("uuid").GetString());
        Assert.Equal(JsonValueKind.Null, hb.GetProperty("detail").ValueKind);

        Assert.True(await service.RemoveAsync(appId));
        await WaitFor(() => received.Any(m => Kind(m) == "status" && m.GetProperty("detail").GetProperty("status").GetString() == "stop"), "stop");
    }

    private static string Kind(JsonElement m)
    {
        var type = m.GetProperty("type").GetString()!;
        if (type == "status" && m.GetProperty("detail").GetProperty("status").GetString() == "running") return "running";
        return type;
    }

    private static async Task WaitFor(Func<bool> condition, string what)
    {
        var deadline = Environment.TickCount64 + 10_000;
        while (!condition())
        {
            if (Environment.TickCount64 > deadline) throw new Xunit.Sdk.XunitException($"Timed out waiting for {what}");
            await Task.Delay(50);
        }
    }
}
