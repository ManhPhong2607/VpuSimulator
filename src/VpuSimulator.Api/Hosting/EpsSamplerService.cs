using Microsoft.Extensions.Hosting;
using VpuSimulator.Application.Scheduling;

namespace VpuSimulator.Api.Hosting;

public sealed class EpsSamplerService(IUnitRegistry units, EpsMeter meter, TimeProvider time) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1), time);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
            {
                meter.Sample(units.Snapshots());
            }
        }
        catch (OperationCanceledException)
        {
            // dừng ứng dụng
        }
    }
}