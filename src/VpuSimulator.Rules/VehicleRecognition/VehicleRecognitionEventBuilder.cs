using System.Text.Json;
using VpuSimulator.Domain;
using VpuSimulator.Rules.Common;

namespace VpuSimulator.Rules.VehicleRecognition;

public sealed class VehicleRecognitionEventBuilder : IEventPayloadBuilder
{
    private const double EmptyPlateProbability = 0.35;
    private static readonly string ProvinceLetters = "ABCDEFGHKLMNPSTUVXYZ";
    private static readonly int[] ProvinceCodes =
    {
        11, 12, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28,
        29, 30, 31, 32, 33, 34, 35, 36, 37, 38, 43, 47, 49, 50, 51, 52, 53, 54, 55, 56, 57, 58, 59,
        60, 61, 62, 63, 64, 65, 66, 67, 68, 69, 70, 71, 72, 73, 74, 75, 76, 77, 78, 79, 81, 82, 83, 84, 85, 86, 88, 89, 90, 92, 93, 94, 95, 97, 98, 99,
    };

    public BuildResult Build(UnitInfo unit, RuleContext rule, TickContext tick, RuleDecision decision, ActiveFollowUpState? active)
    {
        var rng = tick.Rng;
        var label = LabelPicker.Pick(rule, tick)
            ?? throw new InvalidOperationException("affect_object rỗng: ExecuteAsync lẽ ra đã trả Precondition");
        var mainBox = BboxGenerator.Main(rng, unit.FrameWidth, unit.FrameHeight);
        var identity = BuildIdentity(rule, tick, mainBox, label);
        var eventKey = Guid.NewGuid().ToString();               // nguồn mật mã, không dùng Random của unit

        var ev = new RecognitionEvent
        {
            EventKey = eventKey,
            RoiId = rule.RoiId,
            EventState = EventState.Lately,
            SourceId = unit.SourceId,
            SourceName = unit.SourceName,
            Task = unit.TaskId,
            StartTime = tick.Now.AddMilliseconds(-100),          
            EndTime = tick.Now,
            ObjectType = 0,                                      
            ObjectLabel = label,                                 
            Tracker = rng.Next(1, 5000),
            Bbox = mainBox,
            Attributes = identity,
        };

        // Chỉ ĐỀ XUẤT ghi correlation; emitter ghi sau khi publish thành công.
        return new BuildResult(ev, new StateProposal[]
        {
            new WriteCorrelation(eventKey, new LicensePlateCorrelationValue(eventKey, label, identity.Value, tick.Now)),
        });
    }

    private static AttributeItem BuildIdentity(RuleContext rule, TickContext tick, Bbox mainBox, string label)
    {
        var rng = tick.Rng;
        var empty = new AttributeItem(Value: "", Prob: 0.0, BBox: null);

        if (!IsPlateOcrEnabled(rule.RawParams)) return empty;      // ô OCR tắt trên VMS (object_identify = false)

        string plate;
        if (tick.FixedValues is { Enabled: true, PlateText: { } fixedText }) plate = fixedText;   // FR-GEN-5
        else if (rng.NextDouble() < EmptyPlateProbability) plate = "";                           // 35% không đọc được
        else plate = GeneratePlate(rng, label);

        return plate.Length == 0
            ? empty
            : new AttributeItem(plate, Math.Round(0.7 + rng.NextDouble() * 0.29, 2), BboxGenerator.Identity(rng, mainBox));
    }

    private static bool IsPlateOcrEnabled(JsonElement raw) =>
        !(raw.ValueKind == JsonValueKind.Object
          && raw.TryGetProperty("object_identify", out var v)
          && v.ValueKind == JsonValueKind.False);

    private static string GeneratePlate(Random rng, string label)
    {
        var province = ProvinceCodes[rng.Next(ProvinceCodes.Length)];
        var digits = rng.Next(0, 100_000);

        // Xe máy: 2 số tỉnh + 1 chữ cái + 1 số + 5 chữ số
        if (string.Equals(label, "motorbike", StringComparison.OrdinalIgnoreCase))
        {
            var letter = ProvinceLetters[rng.Next(ProvinceLetters.Length)];
            var seriesNumber = rng.Next(1, 10);
            return $"{province:D2}{letter}{seriesNumber} {digits:D5}";
        }

        // Ô tô: 2 số tỉnh + 1 chữ cái + 5 chữ số
        var carLetter = ProvinceLetters[rng.Next(ProvinceLetters.Length)];
        return $"{province:D2}{carLetter}{digits:D5}";
    }
}