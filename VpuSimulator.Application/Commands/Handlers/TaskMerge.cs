using System.Text.Json;

namespace VpuSimulator.Application.Commands;

/// <summary>
/// Trộn thông tin bổ sung vào một task đang chạy (cntrl-task, update-task).
/// Trường vắng mặt trong bản vá giữ nguyên giá trị cũ (FR-CMD-8); trường có mặt thay thế giá trị cũ NGUYÊN CẢ khối.
/// Hệ quả: bản vá không có <c>rule_conf</c> thì tập rule giữ nguyên; có <c>rule_conf</c> thì tập rule bằng đúng <c>rule_conf</c> đó.
/// JSON <c>null</c> trong bản vá coi như vắng mặt.
/// </summary>
public static class TaskMerge
{
    /// <summary>Trả về kết quả parse của task sau khi trộn; bị từ chối nếu bản trộn có rule sai kiểu (khi đó bên gọi giữ task cũ).</summary>
    public static TaskParseResult Apply(ParsedTask existing, JsonElement patch, Func<string, bool>? isSupportedRule = null)
    {
        ArgumentNullException.ThrowIfNull(existing);

        var merged = Overlay(existing.Raw, patch);
        return VmsTaskParser.ParseTask(merged, isSupportedRule);
    }

    private static JsonElement Overlay(JsonElement baseTask, JsonElement patch)
    {
        // Không phân biệt hoa/thường (IF-CMD-1): bản vá "URL" phải thay trường "url" cũ, không được sinh thêm trường thứ hai.
        var properties = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in baseTask.EnumerateObject())
        {
            properties[property.Name] = property.Value;
        }

        foreach (var property in patch.EnumerateObject())
        {
            if (property.Value.ValueKind != JsonValueKind.Null)
            {
                properties[property.Name] = property.Value;
            }
        }

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            foreach (var (name, value) in properties)
            {
                writer.WritePropertyName(name);
                value.WriteTo(writer);
            }

            writer.WriteEndObject();
        }

        using var document = JsonDocument.Parse(stream.ToArray());
        return document.RootElement.Clone();
    }
}
