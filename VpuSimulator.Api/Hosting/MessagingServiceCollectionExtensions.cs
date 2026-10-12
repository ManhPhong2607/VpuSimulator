using VpuSimulator.Application.Identity;
using VpuSimulator.Application.Interfaces;
using VpuSimulator.Application.Options;
using VpuSimulator.Infrastructure.Messaging;

namespace VpuSimulator.Api.Hosting;

public static class MessagingServiceCollectionExtensions
{
    // Đăng ký toàn bộ phần heartbeat/identity. Gọi 1 dòng trong Program.cs: builder.Services.AddVpuMessaging(builder.Configuration)
    public static IServiceCollection AddVpuMessaging(this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection("Messaging").Get<MessagingOptions>() ?? new MessagingOptions();

        services.AddSingleton(options);
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IMessageBrokerFactory>(sp => new RabbitMqBrokerFactory(
            options.Brokers,
            sp.GetRequiredService<TimeProvider>(),
            sp.GetRequiredService<ILoggerFactory>()));
        services.AddSingleton<IdentityLifecycleService>();
        services.AddHostedService<IdentityHostedService>();
        return services;
    }
}
