using VpuSimulator.Application.Identity;
using VpuSimulator.Application.Options;

namespace VpuSimulator.Api.Hosting;

/// <summary>
/// Cầu nối giữa vòng đời ứng dụng và IdentityLifecycleService:
/// khởi động → nạp từng identity trong cấu hình; tắt → StopAllAsync (mỗi identity phát stoprồi đóng session)
/// </summary>
public sealed class IdentityHostedService(
    IdentityLifecycleService identities,
    MessagingOptions options,
    ILogger<IdentityHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // Lỗi cấu hình → dừng ngay với thông báo đầy đủ. Broker tắt thì KHÔNG phải lỗi ở đây (session tự reconnect)
        var errors = MessagingConfigValidator.Validate(options);
        if (errors.Count > 0)
        {
            throw new InvalidOperationException("Cấu hình Messaging không hợp lệ:" + Environment.NewLine + string.Join(Environment.NewLine, errors));
        }

        foreach (var identity in options.Identities)
        {
            var result = await identities.AddAsync(identity, cancellationToken);
            if (result == IdentityOpResult.Added)
            {
                logger.LogInformation("Identity {AppId} started", identity.AppId);
            }
            else
            {
                logger.LogError("Identity {AppId} was not started: {Result}", identity.AppId, result);   // lỗi 1 identity không chặn identity khác
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => identities.StopAllAsync(cancellationToken);
}
