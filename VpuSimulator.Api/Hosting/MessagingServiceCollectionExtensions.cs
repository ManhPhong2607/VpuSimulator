using VpuSimulator.Application.Commands;
using VpuSimulator.Application.Identity;
using VpuSimulator.Application.Interfaces;
using VpuSimulator.Application.Options;
using VpuSimulator.Application.State;
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
        
        // Xử lý lệnh VMS: kho task trong bộ nhớ, bộ đếm, 5 handler, và dispatcher làm IInboundMessageHandler.
        // Tham số isSupportedRule của handler để mặc định (null = chấp nhận mọi tên rule) cho tới khi có registry rule type.
        services.AddSingleton<CommandStats>();
        
        services.AddSingleton<ITaskStore, InMemoryTaskStore>();
        
        services.AddSingleton<ICommandHandler, AddTaskCommandHandler>();
        
        services.AddSingleton<ICommandHandler, EndTaskCommandHandler>();
        
        services.AddSingleton<ICommandHandler, StopAllCommandHandler>();
        
        services.AddSingleton<ICommandHandler, ControlTaskCommandHandler>();
        
        services.AddSingleton<ICommandHandler, UpdateTaskCommandHandler>();
        
        services.AddSingleton<IInboundMessageHandler, VmsCommandDispatcher>();
        
        services.AddSingleton(sp => new IdentityLifecycleService(
            sp.GetRequiredService<IMessageBrokerFactory>(),
            sp.GetRequiredService<TimeProvider>(),
            sp.GetRequiredService<ILoggerFactory>(),
            heartbeatJitter: interval => TimeSpan.FromMilliseconds(Random.Shared.NextDouble() * Math.Min(3000, interval.TotalMilliseconds * 0.1)),
            inboundHandler: sp.GetService<IInboundMessageHandler>(),
            taskStore: sp.GetService<ITaskStore>()));
        
        services.AddHostedService<IdentityHostedService>();
        return services;
    }
}
