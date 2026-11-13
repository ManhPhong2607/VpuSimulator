using System.Text.Json;
using System.Text.Json.Serialization;

namespace VpuSimulator.Api.Endpoints;

public sealed record IdentityDto(
    [property: JsonPropertyName("app_id")] string AppId,
    [property: JsonPropertyName("connected")] bool Connected,
    [property: JsonPropertyName("heartbeat_failures")] long HeartbeatFailures,
    [property: JsonPropertyName("last_heartbeat_at")] DateTimeOffset? LastHeartbeatAt,
    [property: JsonPropertyName("tasks")] int Tasks,
    [property: JsonPropertyName("units")] int Units,
    [property: JsonPropertyName("profile")] string Profile,
    [property: JsonPropertyName("eps")] double eps);

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
    [property: JsonPropertyName("eps")] double Eps);

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