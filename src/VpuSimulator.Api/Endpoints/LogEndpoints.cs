using Serilog.Core;
using VpuSimulator.Api.Logging;
using VpuSimulator.Application.Logging;

namespace VpuSimulator.Api.Endpoints;

public static partial class ControlApiEndpoints
{
    // GET /api/v1/logs?n=100: n dòng log gần nhất, mới nhất trước (contract 9.9, FR-OBS-4).
    private static IResult GetLogs(LogBuffer buffer, int n = 100)
    {
        if (n < 1) return Results.BadRequest(new ErrorDto("n phải ≥ 1."));
        return Results.Ok(buffer.Latest(n).Select(LogLineDto.From).ToList());
    }

    // POST /api/v1/logs/level: đổi mức log lúc runtime (FR-OBS-2, contract 4.1).
    private static IResult SetLogLevel(
        LoggingLevelSwitch levelSwitch,
        ILoggerFactory loggerFactory,
        SetLogLevelRequest? body)
    {
        if (body is null || string.IsNullOrWhiteSpace(body.Level))
            return Results.BadRequest(new ErrorDto("Thiếu trường bắt buộc: level."));

        if (!LogLevels.TryParse(body.Level, out var target))
            return Results.BadRequest(new ErrorDto(
                $"Mức log '{body.Level}' không hợp lệ. Chấp nhận: debug, info, warn, error."));

        levelSwitch.MinimumLevel = target;
        var canonical = LogLevels.Name(target);
        loggerFactory.CreateLogger("ControlApi")
            .LogInformation("Control API: POST /logs/level đổi mức log sang {Level}", canonical);

        return Results.Ok(new { level = canonical });
    }

    // POST /api/v1/logs/verbose: bật/tắt ghi payload bản tin vào log (FR-OBS-3, contract 4.1).
    private static IResult SetLogVerbose(
        RuntimeLogSettings runtimeLog,
        ILoggerFactory loggerFactory,
        SetLogVerboseRequest? body)
    {
        if (body?.Enabled is null)
            return Results.BadRequest(new ErrorDto("Thiếu trường bắt buộc: enabled."));

        runtimeLog.Verbose = body.Enabled.Value;
        loggerFactory.CreateLogger("ControlApi")
            .LogInformation("Control API: POST /logs/verbose đặt verbose={Verbose}", runtimeLog.Verbose);

        return Results.Ok(new { verbose = runtimeLog.Verbose });
    }
}

