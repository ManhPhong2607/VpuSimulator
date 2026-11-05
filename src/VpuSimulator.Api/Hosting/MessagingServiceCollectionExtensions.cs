using VpuSimulator.Application.Commands;
using VpuSimulator.Application.Identity;
using VpuSimulator.Application.Interfaces;
using VpuSimulator.Application.Interfaces.Messaging;
using VpuSimulator.Application.Interfaces.Simulation;
using VpuSimulator.Application.Options;
using VpuSimulator.Application.Scheduling;
using VpuSimulator.Application.State;
using VpuSimulator.Domain.Enums;
using VpuSimulator.Domain.FollowUp;
using VpuSimulator.Infrastructure.Messaging;
using VpuSimulator.Infrastructure.Persistence;
using VpuSimulator.Infrastructure.Rules;
using VpuSimulator.Rules;
using VpuSimulator.Rules.FollowUp;

namespace VpuSimulator.Api.Hosting;

public static class MessagingServiceCollectionExtensions
{
    // Đăng ký toàn bộ phần heartbeat/identity. Gọi 1 dòng trong Program.cs: builder.Services.AddVpuMessaging(builder.Configuration)
    public static IServiceCollection AddVpuMessaging(this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection("Messaging").Get<MessagingOptions>() ?? new MessagingOptions();

        services.AddSingleton(options);

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(new AppClock(TimeProvider.System.GetUtcNow())); // truyền instance: tạo ngay lúc khởi động

        services.AddSingleton<IMessageBrokerFactory>(sp => new RabbitMqBrokerFactory(
            options.Brokers,
            sp.GetRequiredService<TimeProvider>(),
            sp.GetRequiredService<ILoggerFactory>()));

        // Kho lưu trữ correlation liên kết dữ liệu giữa các bài toán (#1 và #6)
        services.AddSingleton<ICorrelationStore, InMemoryCorrelationStore>();

        // Chính sách follow-up cho các rule có chu kỳ tiếp diễn (Illegal Stop, Traffic Signal Violation)
        services.AddSingleton<IFollowUpPolicy, DefaultFollowUpPolicy>();

        // Registry tra cứu định nghĩa rule (kết hợp Handler + Builder + EmittedGroup)
        services.AddSingleton<IRuleHandlerRegistry>(sp => new RuleHandlerRegistry(RuleCatalog.Create(sp.GetRequiredService<IFollowUpPolicy>())));

        // Xử lý lệnh VMS: kho task trong bộ nhớ, bộ đếm, 5 handler, và dispatcher làm IInboundMessageHandler.
        services.AddSingleton<CommandStats>();

        // Scheduler: mỗi rule của mỗi task đang chạy là một emission unit. Sử dụng emitter thật.
        services.AddSingleton(sp => new UnitRegistry(
            SimulationSeed.Resolve(0),
            SimulationProfiles.Normal,
            TimeSpan.FromSeconds(5),
            sp.GetRequiredService<TimeProvider>(),
            UnitEmitter.CreateFactory(
                sp.GetRequiredService<IRuleHandlerRegistry>(),
                appId => sp.GetRequiredService<IdentityLifecycleService>().TryGetBroker(appId),
                sp.GetRequiredService<ICorrelationStore>(),
                sp.GetService<ILogger<UnitRunner>>()),
            fixedPeriodSeconds: view => DensityPeriod.Resolve(view, sp.GetRequiredService<IRuleHandlerRegistry>()),
            loggerFactory: sp.GetRequiredService<ILoggerFactory>()));
        services.AddSingleton<IUnitRegistry>(sp => sp.GetRequiredService<UnitRegistry>());

        services.AddSingleton<ITaskStore>(sp => new InMemoryTaskStore(sp.GetRequiredService<UnitRegistry>()));

        services.AddSingleton<ICommandHandler>(sp => new AddTaskCommandHandler(
            sp.GetRequiredService<ITaskStore>(),
            sp.GetRequiredService<TimeProvider>(),
            sp.GetRequiredService<CommandStats>(),
            isSupportedRule: (name, aiFlowCode) =>
                sp.GetRequiredService<IRuleHandlerRegistry>().Resolve(name, ToAiFlowCode(aiFlowCode)) is not null,
            logger: sp.GetService<ILogger<AddTaskCommandHandler>>()));
        
        services.AddSingleton<ICommandHandler, EndTaskCommandHandler>();

        services.AddSingleton<ICommandHandler, StopAllCommandHandler>();

        services.AddSingleton<ICommandHandler, ControlTaskCommandHandler>();

        services.AddSingleton<ICommandHandler, UpdateTaskCommandHandler>();

        services.AddSingleton<IInboundMessageHandler, VmsCommandDispatcher>();

        services.AddSingleton(sp => new IdentityLifecycleService(
            sp.GetRequiredService<IMessageBrokerFactory>(),
            sp.GetRequiredService<TimeProvider>(),
            sp.GetRequiredService<ILoggerFactory>(),
            heartbeatJitter: interval =>
                TimeSpan.FromMilliseconds(Random.Shared.NextDouble() *
                                          Math.Min(3000, interval.TotalMilliseconds * 0.1)),
            inboundHandler: sp.GetService<IInboundMessageHandler>(),
            taskStore: sp.GetService<ITaskStore>(),
            units: sp.GetService<IUnitRegistry>()));

        services.AddHostedService<IdentityHostedService>();
        return services;
    }
    // Giống cách UnitEmitter ánh xạ: mã ngoài enum coi như vắng mặt, rơi về định nghĩa không ràng buộc
    private static AiFlowCode? ToAiFlowCode(int? code) =>
        code is { } c && Enum.IsDefined(typeof(AiFlowCode), c) ? (AiFlowCode)c : null;
}