namespace VpuSimulator.Application.Identity;

/// <summary>Ngược của <see cref="DurationParser"/>: "30s", "500ms". Kết quả luôn parse lại được.</summary>
public static class DurationFormat
{
    public static string Format(TimeSpan value)
    {
        var ms = (long)value.TotalMilliseconds;
        return ms % 1000 == 0
            ? $"{ms / 1000}s"
            : $"{ms}ms";
    }
}