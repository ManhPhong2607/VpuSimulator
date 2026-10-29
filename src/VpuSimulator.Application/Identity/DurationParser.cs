using System.Globalization;
using System.Text.RegularExpressions;

namespace VpuSimulator.Application.Identity;

/// <summary>
/// Parse duration theo cú pháp: 500ms, 30s, 5m, 6h (cho phép số thập phân và dấu trừ,
/// vì cấu hình heartbeat "≤ 0 nghĩa là 30 s" nên giá trị âm/0 phải parse được để áp dụng quy tắc đó).
/// Scenario cũng cần hàm này để kiểm tra "heartbeat parse được"
/// </summary>
public static partial class DurationParser
{
    [GeneratedRegex(@"^\s*(-?\d+(?:\.\d+)?)\s*(ms|s|m|h)\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex Pattern();

    public static bool TryParse(string? text, out TimeSpan value)
    {
        value = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var match = Pattern().Match(text);
        if (!match.Success)
        {
            return false;
        }

        var number = double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        try
        {
            value = match.Groups[2].Value switch
            {
                "ms" => TimeSpan.FromMilliseconds(number),
                "s" => TimeSpan.FromSeconds(number),
                "m" => TimeSpan.FromMinutes(number),
                "h" => TimeSpan.FromHours(number),
                _ => throw new InvalidOperationException("unreachable"),
            };
            return true;
        }
        catch (OverflowException)
        {
            return false;
        }
    }
}
