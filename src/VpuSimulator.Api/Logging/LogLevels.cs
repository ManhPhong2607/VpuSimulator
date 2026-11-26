using Serilog.Events;

namespace VpuSimulator.Api.Logging;

// Nơi DUY NHẤT chuyển qua lại giữa chữ ("debug|info|warn|error") và mức log của Serilog.
// POST /logs/level, GET /config và cấu hình khởi động đều dùng chung, nên không thể lệch nhau.
public static class LogLevels
{
    // Chỉ chấp nhận đúng 4 giá trị contract, không phân biệt hoa thường. Giá trị khác trả false.
    public static bool TryParse(string? text, out LogEventLevel level)
    {
        switch (text?.Trim().ToLowerInvariant())
        {
            case "debug": level = LogEventLevel.Debug; return true;
            case "info": level = LogEventLevel.Information; return true;
            case "warn": level = LogEventLevel.Warning; return true;
            case "error": level = LogEventLevel.Error; return true;
            default: level = default; return false;
        }
    }

    public static string Name(LogEventLevel level) => level switch
    {
        LogEventLevel.Verbose or LogEventLevel.Debug => "debug",
        LogEventLevel.Information => "info",
        LogEventLevel.Warning => "warn",
        _ => "error",
    };
}