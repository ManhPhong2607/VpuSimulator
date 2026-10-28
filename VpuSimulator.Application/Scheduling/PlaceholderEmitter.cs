using Microsoft.Extensions.Logging;
using VpuSimulator.Domain;

namespace VpuSimulator.Application.Scheduling;

/// <summary>
/// Hàm phát tạm cho tới khi handler sẵn sàng: không publish gì, chỉ trả NoMessage để scheduler chạy thật được
/// Thay bằng emitter thật trong DI (xem Program/MessagingServiceCollectionExtensions) khi có
/// </summary>
public static class PlaceholderEmitter
{
    public static UnitEmitterFactory Create(ILogger? logger = null) =>
        (key, _) => (context, _) =>
        {
            logger?.LogDebug("Unit {Unit} đến hạn (epoch {Epoch}, manual {Manual}), chưa có handler thật", key, context.Epoch, context.IsManual);
            return Task.FromResult(RuleOutcome.NoMessage);
        };
}