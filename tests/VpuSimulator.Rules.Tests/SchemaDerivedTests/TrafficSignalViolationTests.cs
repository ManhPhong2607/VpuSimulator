using VpuSimulator.Domain;
using VpuSimulator.Infrastructure;
using VpuSimulator.Rules.FollowUp;
using VpuSimulator.Rules.TrafficSignalViolation;
using Xunit;
using static VpuSimulator.Rules.Tests.TestKit;

namespace VpuSimulator.Rules.Tests.SchemaDerivedTests;

public sealed class TrafficSignalViolationTests
{
    private static readonly AffectObjectConfig[] Cars = { Obj("car") };
    private static readonly RuleContext Rule6 = Rule("Traffic Signal Violation", Cars);
    private readonly TrafficSignalViolationRuleHandler _handler = new(new DefaultFollowUpPolicy());
    private readonly TrafficSignalViolationEventBuilder _builder = new();

    private async Task<ViolationEvent?> TickAsync(FakeEmitter emitter, TickContext tick, bool publishFails = false) =>
        (ViolationEvent?)await emitter.TickAsync(_handler, _builder, Rule6, tick, publishFails);

    private static InMemoryCorrelationStore StoreWith(TimeProvider? time = null, params (string Key, string Plate, DateTimeOffset At)[] entries)
    {
        var store = new InMemoryCorrelationStore(time: time);
        foreach (var e in entries) store.Write("vpu-gate-a", e.Key, new LicensePlateCorrelationValue(e.Key, "car", e.Plate, e.At));
        return store;
    }

    [Fact]
    public async Task NewEvent_WithoutCorrelation_HasNullRefId_AndEmptyIdentity()
    {
        var ev = (await TickAsync(new FakeEmitter(Unit()), Tick()))!;
        Assert.Null(ev.RefId);
        Assert.Equal("", ev.Attributes!.Value);
        Assert.Equal("REDLIGHT", ev.VioTypeStr);
        Assert.Equal(4, ev.DetailsType);
        Assert.Null(ev.FollowUp);
    }

    [Fact]
    public async Task NewEvent_ClaimsTheLatestCorrelation_AtomicallyOnce()
    {
        var now = DateTimeOffset.UtcNow;
        var store = StoreWith(null, ("old", "29A11111", now.AddSeconds(-30)), ("new", "29A22222", now.AddSeconds(-2)));

        var ev = (await TickAsync(new FakeEmitter(Unit()), Tick(store: store)))!;

        Assert.Equal("new", ev.RefId);
        Assert.Equal("29A22222", ev.Attributes!.Value);
        Assert.Equal(new[] { "old" }, store.GetAll("vpu-gate-a").Keys);       // "new" đã bị chiếm, "old" còn nguyên
    }

    [Fact]
    public async Task ExpiredCorrelation_IsInvisible_BecauseTheStoreExpiresIt()
    {
        var time = new TestTime();
        var store = new InMemoryCorrelationStore(new CorrelationOptions(TimeSpan.FromMinutes(10), 100), time);
        store.Write("vpu-gate-a", "k1", new LicensePlateCorrelationValue("k1", "car", "30A99999", time.Now));
        time.Now = time.Now.AddMinutes(11);

        var ev = (await TickAsync(new FakeEmitter(Unit()), Tick(store: store, now: time.Now)))!;
        Assert.Null(ev.RefId);
    }

    [Fact]
    public async Task NewEvent_StatesAreInOneTwoThree_AndDrawnIndependently()
    {
        var rng = new Random(2);
        var pairs = new HashSet<(int, int)>();
        for (int i = 0; i < 300; i++)
        {
            var ev = (await TickAsync(new FakeEmitter(Unit()), Tick(rng: rng)))!;
            Assert.InRange((int)ev.EventState, 1, 3);
            Assert.InRange(ev.State!.Value, 1, 3);
            pairs.Add(((int)ev.EventState, ev.State!.Value));
        }
        Assert.True(pairs.Count > 3);                  // header và details.state không bị buộc bằng nhau
    }

    [Fact]
    public async Task Discard_ReusesTheOpenEvent_WithDiscardedStateAndTypeTwo()
    {
        var emitter = new FakeEmitter(Unit());
        var tick = Tick(rng: new Random(9));
        await TickAsync(emitter, tick);                                       // sự kiện mở đầu tiên

        for (int i = 0; i < 200; i++)
        {
            var open = emitter.Active!;
            var ev = (await TickAsync(emitter, tick))!;
            if (ev.FollowUp != FollowUpKind.Discard) continue;

            Assert.Equal(open.EventKey, ev.EventKey);
            Assert.Equal(EventState.Discarded, ev.EventState);                // header = 4
            Assert.Equal(4, ev.State);
            Assert.Equal(2, ev.DetailsType);
            Assert.Equal(Bbox.Zero, ev.Bbox);
            Assert.Null(ev.Attributes);
            Assert.Equal(open.RefId, ev.RefId);
            Assert.Null(emitter.Active);
            return;
        }
        Assert.True(false, "Không gặp discard trong 200 lần phát (xác suất 0,75)");
    }

    [Fact]
    public async Task PublishFailure_DoesNotOpenAFollowUp()
    {
        var emitter = new FakeEmitter(Unit());
        await TickAsync(emitter, Tick(), publishFails: true);
        Assert.Null(emitter.Active);

        var next = (await TickAsync(emitter, Tick(seed: 2)))!;
        Assert.Null(next.FollowUp);                    // không có sự kiện mở nên không thể follow-up
    }

    [Fact]
    public async Task AfterResetRunState_StartsAFreshEvent_NotADiscard()
    {
        var emitter = new FakeEmitter(Unit());
        var first = (await TickAsync(emitter, Tick(seed: 3)))!;
        emitter.ResetRunState();
        var second = (await TickAsync(emitter, Tick(seed: 4)))!;
        Assert.Null(second.FollowUp);
        Assert.NotEqual(first.EventKey, second.EventKey);
    }

    [Fact]
    public async Task FixedValues_OverrideCorrelation_ButStillConsumeIt()
    {
        var store = StoreWith(null, ("k1", "29A11111", DateTimeOffset.UtcNow.AddSeconds(-2)));
        var fx = new FixedValues(Enabled: true, ObjectLabel: "bus", PlateText: "30A99999", RefId: "fixed-ref");

        var emitter = new FakeEmitter(Unit());
        var ev = (await TickAsync(emitter, Tick(store: store, fixedValues: fx)))!;

        Assert.Equal("bus", ev.ObjectLabel);
        Assert.Equal("30A99999", ev.Attributes!.Value);
        Assert.Equal("fixed-ref", ev.RefId);
        Assert.Empty(store.GetAll("vpu-gate-a"));
        Assert.Equal("fixed-ref", emitter.Active!.RefId);       // follow-up giữ ref_id cố định
    }

    [Fact]
    public async Task Decision_DrawsExactlyOneNumber_WhenAFollowUpIsOpen_AndNoneOtherwise()
    {
        var active = new ActiveFollowUpState("k", "car", 0, 1, null);

        var withActive = new Random(5);
        await _handler.ExecuteAsync(Unit(), Rule6, Tick(rng: withActive), active, default);
        var reference = new Random(5);
        reference.NextDouble();
        Assert.Equal(reference.Next(), withActive.Next());       // đúng một lần rút

        var noActive = new Random(5);
        await _handler.ExecuteAsync(Unit(), Rule6, Tick(rng: noActive), null, default);
        Assert.Equal(new Random(5).Next(), noActive.Next());     // không rút lần nào
    }

    [Fact]
    public async Task TrafficLight_NeverEmits()
    {
        var d = await new TrafficLightRuleHandler().ExecuteAsync(Unit(), Rule("Traffic Light", Cars), Tick(), null, default);
        Assert.Equal(RuleOutcome.NoMessage, d.Outcome);
    }
}