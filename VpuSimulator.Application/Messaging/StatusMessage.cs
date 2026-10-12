using System.Text.Json.Serialization;

namespace VpuSimulator.Application.Messaging;

/// <summary>
/// Bản tin status cấp process/task gửi ra broker 
/// - appid luôn là "". uuid mới là app id của identity.
/// - timestamp là Unix seconds (UTC).
/// - Key detail phải LUÔN có mặt, kể cả khi null — không bật
///   "ignore null" khi serialize bản tin này. Thứ tự key không quan trọng (SRS A-3).
/// </summary>
public sealed record StatusMessage(
    [property: JsonPropertyName("appid")] string AppId,
    [property: JsonPropertyName("detail")] object? Detail,
    [property: JsonPropertyName("scope")] string Scope,
    [property: JsonPropertyName("timestamp")] long Timestamp,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("uuid")] string Uuid);

/// <summary>Phần detail của status running/stop cấp process: {"status":"running","vps":"&lt;app id&gt;"}.</summary>
public sealed record ProcessStatusDetail(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("vps")] string Vps);

/// <summary>Factory dựng các bản tin status cấp process (scope process-unit, uuid = app id).</summary>
public static class StatusMessages
{
    private const string ProcessScope = "process-unit";

    public static StatusMessage ProcessRunning(string appId, DateTimeOffset now) =>
        Process(appId, now, "status", new ProcessStatusDetail("running", appId));

    public static StatusMessage ProcessStop(string appId, DateTimeOffset now) =>
        Process(appId, now, "status", new ProcessStatusDetail("stop", appId));

    public static StatusMessage Heartbeat(string appId, DateTimeOffset now) =>
        Process(appId, now, "heartbeat", detail: null);

    private static StatusMessage Process(string appId, DateTimeOffset now, string type, object? detail) =>
        new(AppId: string.Empty,
            Detail: detail,
            Scope: ProcessScope,
            Timestamp: now.ToUnixTimeSeconds(),
            Type: type,
            Uuid: appId);
}
