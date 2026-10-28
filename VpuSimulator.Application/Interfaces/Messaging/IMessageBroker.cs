namespace VpuSimulator.Application.Interfaces.Messaging;

public interface IMessageBroker
{
    Task PublishAsync(string routingKey, object envelope, CancellationToken cancellationToken = default);
}
