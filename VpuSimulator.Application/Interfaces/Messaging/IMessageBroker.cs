using System.Threading;
using System.Threading.Tasks;

namespace VpuSimulator.Application.Interfaces;

public interface IMessageBroker
{
    Task PublishAsync(string routingKey, object envelope, CancellationToken cancellationToken = default);
}
