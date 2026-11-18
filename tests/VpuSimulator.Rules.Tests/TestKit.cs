using System.Text.Json;
using VpuSimulator.Domain;
using VpuSimulator.Infrastructure;

namespace VpuSimulator.Rules.Tests;

internal static class TestKit
{
    public static UnitInfo Unit(string appId = "vpu-gate-a") => new(appId, "task-1", "cam-1", "Cam 1", 1, 1280, 720);

    public static AffectObjectConfig Obj(string label) => new(label, 0.5, new[] { 0.3, 4.0 }, 100, new[] { "moving" }, 0);

    public static RuleContext Rule(string name, IReadOnlyList<AffectObjectConfig>? affect = null, string rawJson = "{}",
                                   AiFlowCode code = AiFlowCode.VehicleRecognition) =>
        new() { RoiId = 1, Name = name, AiFlowCode = code, AffectObject = affect, RawParams = JsonDocument.Parse(rawJson).RootElement };

    public static TickContext Tick(int seed = 1, ICorrelationStore? store = null, FixedValues? fixedValues = null,
                                   DateTimeOffset? now = null, Random? rng = null) =>
        new(rng ?? new Random(seed), now ?? DateTimeOffset.UtcNow, store ?? new InMemoryCorrelationStore(), fixedValues ?? FixedValues.Off);
}

internal sealed class TestTime : TimeProvider
{
    public DateTimeOffset Now { get; set; } = new(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow() => Now;
}

// Làm đúng việc của SchedulerService: ExecuteAsync, rồi Build, rồi "publish", rồi áp đề xuất.</summary>
internal sealed class FakeEmitter
{
    public UnitInfo Unit { get; }
    public ActiveFollowUpState? Active { get; private set; }
    public FakeEmitter(UnitInfo unit) => Unit = unit;

    public void ResetRunState() => Active = null;      // Off -> On

    public async Task<RuleOutputEvent?> TickAsync(IRuleHandler handler, IEventPayloadBuilder builder, RuleContext rule,
                                                  TickContext tick, bool publishFails = false)
    {
        var decision = await handler.ExecuteAsync(Unit, rule, tick, Active, CancellationToken.None);
        if (decision.Outcome != RuleOutcome.Emitted) return null;

        var built = builder.Build(Unit, rule, tick, decision, Active);
        if (publishFails) return null;                 // publish lỗi: bỏ đề xuất, trạng thái giữ nguyên

        foreach (var p in built.Proposals)
            switch (p)
            {
                case SetActiveFollowUp s: Active = s.State; break;
                case ClearActiveFollowUp: Active = null; break;
                case WriteCorrelation w: tick.Correlation.Write(Unit.AppId, w.Key, w.Value); break;
            }
        return built.Event;
    }
}