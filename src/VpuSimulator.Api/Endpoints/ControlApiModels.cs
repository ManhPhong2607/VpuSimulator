using System.Text.Json;
using System.Text.Json.Serialization;
using VpuSimulator.Application.Logging;

namespace VpuSimulator.Api.Endpoints;

public sealed record IdentityDto(
    [property: JsonPropertyName("app_id")] string AppId,
    [property: JsonPropertyName("connected")] bool Connected,
    [property: JsonPropertyName("heartbeat_failures")] long HeartbeatFailures,
    [property: JsonPropertyName("last_heartbeat_at")] DateTimeOffset? LastHeartbeatAt,
    [property: JsonPropertyName("tasks")] int Tasks,
    [property: JsonPropertyName("units")] int Units,
    [property: JsonPropertyName("profile")] string Profile,
    [property: JsonPropertyName("eps")] double eps,
    [property: JsonPropertyName("heartbeat")] HeartbeatDto Heartbeat,
    [property: JsonPropertyName("enabled")] bool Enabled,
    [property: JsonPropertyName("status")] string Status);

// Heartbeat gửi dạng chuỗi ("30s"); phản hồi trả cả chuỗi lẫn số giây (contract 10.1)
public sealed record HeartbeatDto(
    [property: JsonPropertyName("enabled")] bool Enabled,
    [property: JsonPropertyName("interval")] string Interval,
    [property: JsonPropertyName("interval_seconds")] double IntervalSeconds,
    [property: JsonPropertyName("last_at")] DateTimeOffset? LastAt)
{
    public static HeartbeatDto From(bool enabled, TimeSpan interval, DateTimeOffset? lastAt) =>
        new(enabled, VpuSimulator.Application.Identity.DurationFormat.Format(interval), interval.TotalSeconds, lastAt);
}

public sealed record PatchHeartbeatRequest(
    [property: JsonPropertyName("interval")] string? Interval,
    [property: JsonPropertyName("enabled")] bool? Enabled);

public sealed record PatchIdentityRequest(
    [property: JsonPropertyName("heartbeat")] PatchHeartbeatRequest? Heartbeat,
    [property: JsonPropertyName("enabled")] bool? Enabled,
    [property: JsonPropertyName("profile")] string? Profile,
    [property: JsonPropertyName("display_name")] string? DisplayName);

public sealed record RuleDto(
    [property: JsonPropertyName("roi_id")] int RoiId,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("type")] int? Type,
    [property: JsonPropertyName("ai_flow_code")] int? AiFlowCode,
    [property: JsonPropertyName("enable")] bool? Enable);

public sealed record TaskDto(
    [property: JsonPropertyName("app_id")] string AppId,
    [property: JsonPropertyName("task")] string Task,
    [property: JsonPropertyName("uuid")] string? Uuid,
    [property: JsonPropertyName("enable")] bool? Enable,
    [property: JsonPropertyName("task_type")]
    string? TaskType,
    [property: JsonPropertyName("mode")] string? Mode,
    [property: JsonPropertyName("rules")] IReadOnlyList<RuleDto> Rules,
    [property: JsonPropertyName("skipped_rules")] int SkippedRules,
    [property: JsonPropertyName("warnings")] int Warnings,
    [property: JsonPropertyName("source_info")] SourceInfoDto? SourceInfo,
    [property: JsonPropertyName("url")] string? Url,
    [property: JsonPropertyName("capability")] CapabilityDto? Capability,
    [property: JsonPropertyName("eps")] double Eps,
    [property: JsonPropertyName("enabled")] bool Enabled,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("off_reason"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? OffReason);

public sealed record UnitDto(
    [property: JsonPropertyName("app_id")] string AppId,
    [property: JsonPropertyName("task")] string Task,
    [property: JsonPropertyName("roi")] int Roi,
    [property: JsonPropertyName("rule")] string Rule,
    [property: JsonPropertyName("auto")] bool Auto,
    [property: JsonPropertyName("emitting")] bool Emitting,
    [property: JsonPropertyName("epoch")] int Epoch,
    [property: JsonPropertyName("next_due_at")] DateTimeOffset? NextDueAt,
    [property: JsonPropertyName("last_emit_at")] DateTimeOffset? LastEmitAt,
    [property: JsonPropertyName("emitted")] long Emitted,
    [property: JsonPropertyName("no_message")] long NoMessage,
    [property: JsonPropertyName("precondition")] long Precondition,
    [property: JsonPropertyName("errors")] long Errors, [property: JsonPropertyName("ticks_dropped")] long TicksDropped,
    [property: JsonPropertyName("ticks_deferred_by_min_gap")] long TicksDeferredByMinGap,
    [property: JsonPropertyName("triggers_rejected")] long TriggersRejected,
    [property: JsonPropertyName("profile")] string Profile,
    [property: JsonPropertyName("rule_type")] int? RuleType,
    [property: JsonPropertyName("ai_flow_code")] int? AiFlowCode,
    [property: JsonPropertyName("reset_timer_sec")] double? ResetTimerSec,
    [property: JsonPropertyName("eps")] double Eps,
    [property: JsonPropertyName("last_result")] string?  LastResult,
    [property: JsonPropertyName("off_reason"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? OffReason,
    [property: JsonPropertyName("rule_conf"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] JsonElement? RuleConf);

public sealed record StatusDto(
    [property: JsonPropertyName("identities")] int Identities,
    [property: JsonPropertyName("identities_connected")] int IdentitiesConnected,
    [property: JsonPropertyName("units")] int Units,
    [property: JsonPropertyName("units_auto")] int UnitsAuto,
    [property: JsonPropertyName("seed")] string Seed, // chuỗi: số 64-bit vượt 2^53 sẽ bị làm tròn
    [property: JsonPropertyName("uptime_seconds")] long UptimeSeconds,
    [property: JsonPropertyName("emitted_total")] long EmittedTotal,
    [property: JsonPropertyName("no_message_total")] long NoMessageTotal,
    [property: JsonPropertyName("precondition_total")] long PreconditionTotal,
    [property: JsonPropertyName("errors_total")] long ErrorsTotal,
    [property: JsonPropertyName("events_total")] long EventsTotal,
    [property: JsonPropertyName("eps_actual")] double EpsActual,
    [property: JsonPropertyName("eps_actual_by_identity")] IReadOnlyDictionary<string, double> EpsActualByIdentity,
    [property: JsonPropertyName("eps_target")] double? EpsTarget,
    [property: JsonPropertyName("commands")] IReadOnlyDictionary<string, long> Commands);

public sealed record StopTasksResultDto(
    [property: JsonPropertyName("identities_affected")] int IdentitiesAffected,
    [property: JsonPropertyName("tasks_stopped")] int TasksStopped);

/// <summary>Lỗi theo SRS 3.1.10: {"error": thông điệp}.</summary>
public sealed record ErrorDto(
    [property: JsonPropertyName("error")] string Error);

/// <summary>Unit được tham chiếu trong phản hồi (vd 202 của trigger).</summary>
public sealed record UnitRefDto(
    [property: JsonPropertyName("app_id")] string AppId,
    [property: JsonPropertyName("task")] string Task,
    [property: JsonPropertyName("roi")] int Roi,
    [property: JsonPropertyName("rule")] string Rule);

/// <summary>Body của POST .../auto. Auto là bool? để thiếu trường thì báo 400, không thì hiểu là false.</summary>
public sealed record SetAutoRequest(
    [property: JsonPropertyName("auto")] bool? Auto);

/// <summary>Body của PATCH /tasks/{app}/{task}. Enabled là bool? để thiếu trường thì báo 400.</summary>
public sealed record PatchTaskRequest(
    [property: JsonPropertyName("enabled")] bool? Enabled);

public sealed record UnitAutoDto(
    [property: JsonPropertyName("app_id")] string AppId,
    [property: JsonPropertyName("task")] string Task,
    [property: JsonPropertyName("roi")] int Roi,
    [property: JsonPropertyName("rule")] string Rule,
    [property: JsonPropertyName("auto")] bool Auto);

public sealed record ApplyProfileRequest(
    [property: JsonPropertyName("profile")]
    string? Profile,
    [property: JsonPropertyName("app_id")] string? AppId,
    [property: JsonPropertyName("task")] string? Task);

public sealed record ApplyProfileResultDto(
    [property: JsonPropertyName("units_updated")]
    int UnitsUpdated);
    
/// <summary>Body của POST /identities (SRS 3.1.10). Tất cả nullable để thiếu trường thì báo 400 chứ không ném lỗi.</summary>
public sealed record CreateIdentityRequest(
    [property: JsonPropertyName("app_id")] string? AppId,
    [property: JsonPropertyName("profile")] string? Profile,
    [property: JsonPropertyName("heartbeat")] string? Heartbeat);

public sealed record ProfileDto(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("is_builtin")] bool IsBuiltin,
    [property: JsonPropertyName("is_system_default")] bool IsSystemDefault,
    [property: JsonPropertyName("interval")] double Interval,
    [property: JsonPropertyName("jitter_min")] double JitterMin,
    [property: JsonPropertyName("jitter_max")] double JitterMax,
    [property: JsonPropertyName("min_gap")] double MinGap,
    [property: JsonPropertyName("bypass_frames")] bool BypassFrames,
    [property: JsonPropertyName("coalesce_timestamps")] bool CoalesceTimestamps,
    [property: JsonPropertyName("ramp_from")] double? RampFrom,
    [property: JsonPropertyName("ramp_to")] double? RampTo,
    [property: JsonPropertyName("ramp_duration")] double? RampDuration);

/// <summary>GET /rules. Chỉ gồm các rule đã đăng ký trong RuleCatalog.</summary>
public sealed record RuleCatalogItemDto(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("group")] string? Group,
    [property: JsonPropertyName("emitted_group")] int? EmittedGroup,
    [property: JsonPropertyName("ai_flow_codes")] IReadOnlyList<int>? AiFlowCodes,
    [property: JsonPropertyName("status")] string Status);

public sealed record SourceInfoDto(
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("address")] string? Address,
    [property: JsonPropertyName("location")] string? Location);

public sealed record CapabilityDto(
    [property: JsonPropertyName("width")] int? Width,
    [property: JsonPropertyName("height")] int? Height,
    [property: JsonPropertyName("fps")] int? Fps);
    
public sealed record ScenarioIssueDto(
    [property: JsonPropertyName("severity")] string Severity,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("detail")] string Detail);

public sealed record ScenarioInfoDto(
    [property: JsonPropertyName("vpu_count")] int VpuCount,
    [property: JsonPropertyName("vps_count")] int VpsCount,
    [property: JsonPropertyName("task_count")] int TaskCount,
    [property: JsonPropertyName("rule_count")] int RuleCount,
    [property: JsonPropertyName("seed")] long? Seed,
    [property: JsonPropertyName("loaded_at")] DateTimeOffset LoadedAt);

public sealed record ScenarioValidationDto(
    [property: JsonPropertyName("issues")] IReadOnlyList<ScenarioIssueDto> Issues,
    [property: JsonPropertyName("ok_identities")] int OkIdentities,
    [property: JsonPropertyName("ok_tasks")] int OkTasks);

public sealed record ScenarioErrorDto(
    [property: JsonPropertyName("error")] string Error,
    [property: JsonPropertyName("issues")] IReadOnlyList<ScenarioIssueDto> Issues);

public sealed record ScenarioAppliedDto(
    [property: JsonPropertyName("identities_added")] int IdentitiesAdded,
    [property: JsonPropertyName("tasks_added")] int TasksAdded,
    [property: JsonPropertyName("rules_added")] int RulesAdded,
    [property: JsonPropertyName("issues")] IReadOnlyList<ScenarioIssueDto> Issues);

public sealed record ScenarioClearedDto(
    [property: JsonPropertyName("identities_removed")] int IdentitiesRemoved,
    [property: JsonPropertyName("tasks_removed")] int TasksRemoved);

public sealed record CorrelationItemDto(
    [property: JsonPropertyName("app_id")] string AppId,
    [property: JsonPropertyName("event_key")] string EventKey,
    [property: JsonPropertyName("label")] string? Label,
    [property: JsonPropertyName("written_at")] DateTimeOffset WrittenAt);

public sealed record CorrelationDto(
    [property: JsonPropertyName("total")] int Total,
    [property: JsonPropertyName("ttl")] string Ttl,
    [property: JsonPropertyName("limit_per_identity")] int LimitPerIdentity,
    [property: JsonPropertyName("items")] IReadOnlyList<CorrelationItemDto> Items);
    
// Khối bản tin của một dòng "message sent" (contract 9.9).
public sealed record LogMessageDto(
    [property: JsonPropertyName("event")] string Event,
    [property: JsonPropertyName("routing_key")] string RoutingKey,
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("size_bytes")] int SizeBytes,
    [property: JsonPropertyName("payload")] object? Payload,
    [property: JsonPropertyName("truncated")] bool Truncated)
{
    public static LogMessageDto From(LogMessageInfo m) =>
        new(m.Event, m.RoutingKey, m.Kind, m.SizeBytes, PayloadOf(m), m.Truncated);

    // Contract 10.1: verbose tắt -> null; bị cắt -> CHUỖI (JSON đã cụt, không còn hợp lệ); còn nguyên -> đối tượng JSON.
    private static object? PayloadOf(LogMessageInfo m)
    {
        if (m.Payload is null) return null;
        if (m.Truncated) return m.Payload;
        try
        {
            using var doc = JsonDocument.Parse(m.Payload);
            return doc.RootElement.Clone();   // Clone vì doc bị giải phóng ngay sau đó
        }
        catch (JsonException)
        {
            return m.Payload;
        }
    }
}

// Một dòng log trả về cho Console. Field vắng (không biết app_id/task/rule, hoặc không phải dòng bản tin) thì bỏ khỏi JSON.
public sealed record LogLineDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("at")] DateTimeOffset At,
    [property: JsonPropertyName("outcome")] string Outcome,
    [property: JsonPropertyName("app_id"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? AppId,
    [property: JsonPropertyName("task"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Task,
    [property: JsonPropertyName("rule"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Rule,
    [property: JsonPropertyName("detail")] string Detail,
    [property: JsonPropertyName("message"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] LogMessageDto? Message)
{
    public static LogLineDto From(LogEntry e) =>
        new(e.Id, e.At, e.Outcome, e.Identity, e.TaskId, e.Rule, e.Detail,
            e.Message is null ? null : LogMessageDto.From(e.Message));
}

public sealed record SetLogLevelRequest([property: JsonPropertyName("level")] string? Level);

public sealed record SetLogVerboseRequest([property: JsonPropertyName("enabled")] bool? Enabled);