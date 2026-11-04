namespace VpuSimulator.Application.Interfaces.Messaging;

public interface IMessageBroker
{
    bool IsConnected { get; }
    Task PublishAsync(string routingKey, object envelope, CancellationToken cancellationToken = default);
}
