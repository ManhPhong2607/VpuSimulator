using Serilog.Core;
using VpuSimulator.Api.Hosting;
using VpuSimulator.Api.Logging;
using VpuSimulator.Application.Commands;
using VpuSimulator.Application.Identity;
using VpuSimulator.Application.Logging;
using VpuSimulator.Application.Options;
using VpuSimulator.Application.Scheduling;
using VpuSimulator.Infrastructure.Persistence;
using VpuSimulator.Rules;

namespace VpuSimulator.Api.Endpoints;

public static partial class ControlApiEndpoints
{
    private static StatusDto GetStatus(
        IdentityLifecycleService identities,
        IUnitRegistry units,
        CommandStats stats,
        TimeProvider time,
        AppClock clock,
        EpsMeter eps)
    {
        var statuses = identities.GetStatuses();
        var snapshots = units.Snapshots();
        var reading = eps.Current;
        // Mỗi identity một khoá: có ramp thì là tổng mục tiêu các unit, không có thì null (contract 9.6)
        var targetByIdentity = statuses.ToDictionary(
            s => s.AppId,
            s => reading.TargetByIdentity is { } map && map.TryGetValue(s.AppId, out var v) ? (double?)v : null,
            StringComparer.Ordinal);

        return new StatusDto(
            Identities: statuses.Count,
            IdentitiesConnected: statuses.Count(s => s.IsConnected),
            Units: snapshots.Count,
            UnitsAuto: snapshots.Count(s => s.Auto),
            Seed: units.Seed.ToString(System.Globalization.CultureInfo.InvariantCulture),
            UptimeSeconds: clock.UptimeSeconds(time),
            EmittedTotal: snapshots.Sum(s => s.Emitted),
            NoMessageTotal: snapshots.Sum(s => s.NoMessage),
            PreconditionTotal: snapshots.Sum(s => s.Precondition),
            ErrorsTotal: snapshots.Sum(s => s.Errors),
            Commands: stats.Snapshot()
                .OrderBy(kv => kv.Key, StringComparer.Ordinal)
                .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal),
            EventsTotal: snapshots.Sum(s => s.Emitted),
            EpsActual: reading.Total,
            EpsActualByIdentity: reading.ByIdentity,
            EpsTarget: reading.Target,
            EpsTargetByIdentity: targetByIdentity);
    }

    // GET /api/v1/config: cấu hình hiệu lực, phản ánh thay đổi runtime, che secret khác rỗng bằng "***" (FR-CFG-1, FR-CFG-6).
    private static IResult GetConfig(
        MessagingOptions messaging,
        SimulationSettings simulation,
        IUnitRegistry units,
        CorrelationOptions correlation,
        LoggingLevelSwitch levelSwitch,
        RuntimeLogSettings runtimeLog,
        LogBuffer logBuffer,
        IConfiguration configuration)
    {
        static string? MaskSecret(string? secret) =>
            string.IsNullOrEmpty(secret) ? secret : "***";

        var brokers = messaging.Brokers.ToDictionary(
            kv => kv.Key,
            kv => new
            {
                host = kv.Value.Host,
                port = kv.Value.Port,
                virtual_host = kv.Value.VirtualHost,
                username = kv.Value.Username,
                password = MaskSecret(kv.Value.Password),
                exchange = kv.Value.Exchange,
                prefetch = kv.Value.Prefetch,
                auto_ack = kv.Value.AutoAck,
                reconnect_min = DurationFormat.Format(kv.Value.ReconnectMin),
                reconnect_max = DurationFormat.Format(kv.Value.ReconnectMax),
                publish_timeout = DurationFormat.Format(kv.Value.PublishTimeout),
            },
            StringComparer.OrdinalIgnoreCase);

        return Results.Ok(new
        {
            api = new
            {
                listen = configuration["Api:Listen"] ?? "http://127.0.0.1:18080",
                token = MaskSecret(configuration["Api:Token"]),
            },
            log = new
            {
                level = LogLevels.Name(levelSwitch.MinimumLevel),
                verbose = runtimeLog.Verbose,
                buffer_size = logBuffer.Capacity,
                max_body_log = runtimeLog.MaxBodyLog,
            },
            scheduler = new
            {
                arm_delay = DurationFormat.Format(simulation.ArmDelay),
                default_profile = simulation.DefaultProfile.Name,
            },
            simulation = new
            {
                seed = units.Seed.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ai_backend = "simulated",
            },
            correlation = new
            {
                ttl = DurationFormat.Format(correlation.Ttl),
                max_per_identity = correlation.MaxPerIdentity,
            },
            brokers,
        });
    }

    // GET /api/v1/rules
    private static IReadOnlyList<RuleCatalogItemDto> GetRules() => RuleItems;

    private static readonly IReadOnlyList<RuleCatalogItemDto> RuleItems = BuildRuleItems();

    private static IReadOnlyList<RuleCatalogItemDto> BuildRuleItems()
    {
        // Nhãn nhóm lấy từ chú thích của RuleDefinition.EmittedGroup
        static string? GroupName(int? g) => g switch
        {
            1 => "density", 2 => "violation", 3 => "recognition", 4 => "abnormal", _ => null,
        };

        return RuleCatalog.All
            .Select(d => new RuleCatalogItemDto(
                d.Name,
                GroupName(d.EmittedGroup),
                d.EmittedGroup,
                d.Handler.AiFlowCodes?.Select(c => (int)c).Order().ToList(),
                "registered"))
            .OrderBy(r => r.Name, StringComparer.Ordinal)
            .ToList();
    }
}

