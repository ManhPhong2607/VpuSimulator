using System.Text.RegularExpressions;
using VpuSimulator.Domain;
using VpuSimulator.Infrastructure;
using VpuSimulator.Rules.VehicleRecognition;
using Xunit;
using static VpuSimulator.Rules.Tests.TestKit;

namespace VpuSimulator.Rules.Tests.SchemaDerivedTests;

public sealed class VehicleRecognitionTests
{
    private static readonly AffectObjectConfig[] Objs = { Obj("car"), Obj("motorbike") };
    private readonly VehicleRecognitionRuleHandler _handler = new();
    private readonly VehicleRecognitionEventBuilder _builder = new();

    private async Task<RecognitionEvent?> EmitAsync(TickContext tick, string raw = "{}", AffectObjectConfig[]? objs = null) =>
        (RecognitionEvent?)await new FakeEmitter(Unit()).TickAsync(_handler, _builder, Rule("License Plate", objs ?? Objs, raw), tick);

    private static bool Inside(Bbox b, Bbox o) => b.X >= o.X && b.Y >= o.Y && b.X + b.W <= o.X + o.W && b.Y + b.H <= o.Y + o.H;

    [Fact]
    public void Name_IsTheWireRuleName() => Assert.Equal("License Plate", _handler.Name);

    [Fact]
    public async Task EmptyAffectObject_IsPrecondition_AndEmitsNothing()
    {
        var decision = await _handler.ExecuteAsync(Unit(), Rule("License Plate", Array.Empty<AffectObjectConfig>()), Tick(), null, default);
        Assert.Equal(RuleOutcome.Precondition, decision.Outcome);
        Assert.Null(await EmitAsync(Tick(), objs: Array.Empty<AffectObjectConfig>()));
    }

    [Fact]
    public async Task FixedFields_MatchSchema()
    {
        var rng = new Random(1);
        for (int i = 0; i < 200; i++)
        {
            var ev = (await EmitAsync(Tick(rng: rng)))!;
            Assert.Equal(0, ev.ObjectType);
            Assert.Contains(ev.ObjectLabel, new[] { "car", "motorbike" });      // loại phương tiện
            Assert.Equal(EventState.Lately, ev.EventState);
            Assert.Equal("cam-1", ev.SourceId);
            Assert.Equal(100, (ev.EndTime!.Value - ev.StartTime).TotalMilliseconds);
            Assert.InRange(Math.Max(ev.Bbox.W, ev.Bbox.H), 300, 499);
        }
    }

    [Fact]
    public async Task OcrEnabled_EmptyPlateRatio_IsAbout35Percent()
    {
        var rng = new Random(12345);
        int total = 4000, empty = 0;
        for (int i = 0; i < total; i++)
            if ((await EmitAsync(Tick(rng: rng)))!.Attributes!.Value == "") empty++;
        Assert.InRange((double)empty / total, 0.32, 0.38);
    }

    [Fact]
    public async Task NonEmptyPlate_MatchesFormat_AndBboxIsInsideVehicleBox()
    {
        var carFormat = @"\d{2}[A-Z]\d{5}";
        var motorbikeFormat = @"\d{2}[A-Z]\d \d{5}";
        var format = new Regex($@"^({carFormat}|{motorbikeFormat})$");
        var rng = new Random(3);
        int nonEmpty = 0;
        for (int i = 0; i < 300; i++)
        {
            var ev = (await EmitAsync(Tick(rng: rng)))!;
            if (ev.Attributes!.Value == "") continue;
            nonEmpty++;
            Assert.Matches(format, ev.Attributes.Value);
            Assert.True(Inside(ev.Attributes.BBox!.Value, ev.Bbox));
        }
        Assert.True(nonEmpty > 0);
    }

    [Fact]
    public async Task OcrDisabled_IdentityIsAlwaysEmpty_ButVehicleIsStillEmitted()
    {
        var rng = new Random(4);
        for (int i = 0; i < 200; i++)
        {
            var ev = (await EmitAsync(Tick(rng: rng), raw: """{"object_identify": false}"""))!;
            Assert.Equal("", ev.Attributes!.Value);
            Assert.Equal(0.0, ev.Attributes.Prob);
            Assert.Null(ev.Attributes.BBox);
        }
    }

    [Theory]
    [InlineData("""{"object_identify": true}""")]
    [InlineData("{}")]
    public async Task OcrEnabledOrMissing_ReadsPlateAbout65Percent(string raw)
    {
        var rng = new Random(11);
        int total = 3000, withPlate = 0;
        for (int i = 0; i < total; i++)
            if ((await EmitAsync(Tick(rng: rng), raw))!.Attributes!.Value != "") withPlate++;
        Assert.InRange((double)withPlate / total, 0.61, 0.69);
    }

    [Fact]
    public async Task ReidFields_AreIgnored()
    {
        var ev = (await EmitAsync(Tick(), """{"enable_reid": true, "reid_filters": ["car"]}""", new[] { Obj("bus") }))!;
        Assert.Equal("bus", ev.ObjectLabel);
    }

    [Fact]
    public async Task FixedValues_FixLabelAndPlate_ButNotWhenOcrIsOff()
    {
        var fixedOn = new FixedValues(Enabled: true, ObjectLabel: "truck", PlateText: "29A12345");
        var rng = new Random(5);
        for (int i = 0; i < 100; i++)
        {
            var ev = (await EmitAsync(Tick(rng: rng, fixedValues: fixedOn)))!;
            Assert.Equal("truck", ev.ObjectLabel);
            Assert.Equal("29A12345", ev.Attributes!.Value);
        }
        var off = (await EmitAsync(Tick(fixedValues: fixedOn), """{"object_identify": false}"""))!;
        Assert.Equal("", off.Attributes!.Value);
    }

    [Fact]
    public async Task Correlation_IsOnlyProposedByTheBuilder_AndWrittenAfterPublish()
    {
        var store = new InMemoryCorrelationStore();
        var tick = Tick(store: store);
        var rule = Rule("License Plate", Objs);

        var built = _builder.Build(Unit(), rule, tick, new RuleDecision(RuleOutcome.Emitted), null);
        Assert.Contains(built.Proposals, p => p is WriteCorrelation);
        Assert.Empty(store.GetAll("vpu-gate-a"));                         // builder không ghi trực tiếp

        await new FakeEmitter(Unit()).TickAsync(_handler, _builder, rule, tick, publishFails: true);
        Assert.Empty(store.GetAll("vpu-gate-a"));                         // publish lỗi: không để lại correlation giả

        var ev = (await new FakeEmitter(Unit()).TickAsync(_handler, _builder, rule, tick))!;
        var written = Assert.IsType<LicensePlateCorrelationValue>(Assert.Single(store.GetAll("vpu-gate-a")).Value);
        Assert.Equal(ev.EventKey, written.EventKey);
    }

    [Fact]
    public async Task SameSeed_GivesSameValues()
    {
        static (string, int, Bbox, string?) Key(RecognitionEvent e) => (e.ObjectLabel, e.Tracker, e.Bbox, e.Attributes?.Value);
        Assert.Equal(Key((await EmitAsync(Tick(seed: 7)))!), Key((await EmitAsync(Tick(seed: 7)))!));
    }
}