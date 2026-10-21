using System.Text.Json;

namespace VpuSimulator.Application.Commands;

//Kết quả đọc một trường: vắng mặt, đọc được, hay có mặt nhưng sai kiểu
internal enum ReadResult
{
    Absent,
    Ok,
    WrongType,
}

//Đọc trường JSON (phân biệt "vắng mặt" với "có mặt nhưng rỗng hoặc 0"). Giá trị JSON null coi như vắng mặt
internal static class JsonProps
{
    public static bool TryGet(JsonElement obj, string name, out JsonElement value)
    {
        if (obj.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in obj.EnumerateObject())
            {
                if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }
        }

        value = default;
        return false;
    }

    private static bool TryGetNonNull(JsonElement obj, string name, out JsonElement value) =>
        TryGet(obj, name, out value) && value.ValueKind != JsonValueKind.Null;

    public static ReadResult GetString(JsonElement obj, string name, out string? result)
    {
        result = null;
        if (!TryGetNonNull(obj, name, out var e)) return ReadResult.Absent;
        if (e.ValueKind != JsonValueKind.String) return ReadResult.WrongType;
        result = e.GetString();
        return ReadResult.Ok;
    }

    public static ReadResult GetBool(JsonElement obj, string name, out bool? result)
    {
        result = null;
        if (!TryGetNonNull(obj, name, out var e)) return ReadResult.Absent;
        switch (e.ValueKind)
        {
            case JsonValueKind.True: result = true; return ReadResult.Ok;
            case JsonValueKind.False: result = false; return ReadResult.Ok;
            default: return ReadResult.WrongType;
        }
    }

    //Số nguyên. Chấp nhận cả dạng thập phân không có phần lẻ (vd: 131.0)
    public static ReadResult GetInt(JsonElement obj, string name, out int? result)
    {
        result = null;
        if (!TryGetNonNull(obj, name, out var e)) return ReadResult.Absent;
        if (e.ValueKind != JsonValueKind.Number) return ReadResult.WrongType;
        if (e.TryGetInt32(out var i))
        {
            result = i;
            return ReadResult.Ok;
        }

        if (e.TryGetDouble(out var d) && d == Math.Floor(d) && d >= int.MinValue && d <= int.MaxValue)
        {
            result = (int)d;
            return ReadResult.Ok;
        }

        return ReadResult.WrongType;
    }

    public static ReadResult GetDouble(JsonElement obj, string name, out double? result)
    {
        result = null;
        if (!TryGetNonNull(obj, name, out var e)) return ReadResult.Absent;
        if (e.ValueKind != JsonValueKind.Number || !e.TryGetDouble(out var d)) return ReadResult.WrongType;
        result = d;
        return ReadResult.Ok;
    }
}
