using System.Text;
using System.Text.Json;

namespace VpuSimulator.Application.Commands;

/// <summary>
/// Bước đầu tiên khi nhận một bản tin VMS: biến byte thô thành VmsEnvelope
/// Hàm thuần, không ghi log và không ném exception với dữ liệu xấu. Bên gọi tự ghi log, tự đếm số bản tin bị loại.
/// </summary>
public static class VmsCommandParser
{
    //Số byte tối đa của bản tin được kèm theo khi báo lỗi
    public const int PreviewBytes = 512;

    public static VmsEnvelopeParseResult Parse(ReadOnlyMemory<byte> body)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(body);
        }
        catch (JsonException)
        {
            return Fail(VmsParseFailureReason.InvalidJson, body);
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return Fail(VmsParseFailureReason.NotAnObject, body);
            }

            var commandRead = JsonProps.GetString(root, "command", out var command);
            if (commandRead != ReadResult.Ok || string.IsNullOrWhiteSpace(command))
            {
                return Fail(VmsParseFailureReason.MissingCommand, body);
            }

            if (!VmsCommandNames.IsKnown(command))
            {
                return Fail(VmsParseFailureReason.UnknownCommand, body);
            }

            // Envelope chỉ để log: đọc lỏng, sai kiểu thì thành null, không từ chối
            JsonProps.GetString(root, "uuid", out var uuid);
            JsonProps.GetString(root, "scope", out var scope);
            long? timestamp = null;
            if (JsonProps.TryGet(root, "timestamp", out var ts) && ts.ValueKind == JsonValueKind.Number && ts.TryGetInt64(out var tsValue))
            {
                timestamp = tsValue;
            }

            // Clone: JsonElement gốc sống cùng JsonDocument sẽ bị giải phóng ngay khi ra khỏi khối using.
            var detail = JsonProps.TryGet(root, "detail", out var d) ? d.Clone() : default;

            return new VmsEnvelopeParseResult(new VmsEnvelope(uuid, scope, timestamp, command, detail), null);
        }
    }

    private static VmsEnvelopeParseResult Fail(VmsParseFailureReason reason, ReadOnlyMemory<byte> body)
    {
        var length = Math.Min(PreviewBytes, body.Length);
        var preview = Encoding.UTF8.GetString(body.Span[..length]);
        return new VmsEnvelopeParseResult(null, new VmsParseFailure(reason, preview));
    }
}
