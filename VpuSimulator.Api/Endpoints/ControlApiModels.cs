using System.Text.Json.Serialization;

namespace VpuSimulator.Api.Endpoints;

public sealed record IdentityDto(
    [property: JsonPropertyName("app_id")] string AppId,
    [property: JsonPropertyName("connected")] bool Connected,
    [property: JsonPropertyName("heartbeat_failures")] long HeartbeatFailures,
    [property: JsonPropertyName("last_heartbeat_at")] DateTimeOffset? LastHeartbeatAt,
    [property: JsonPropertyName("tasks")] int Tasks,
    [property: JsonPropertyName("units")] int Units);

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
    [property: JsonPropertyName("task_type")] string? TaskType,
    [property: JsonPropertyName("mode")] string? Mode,
    [property: JsonPropertyName("rules")] IReadOnlyList<RuleDto> Rules,
    [property: JsonPropertyName("skipped_rules")] int SkippedRules,
    [property: JsonPropertyName("warnings")] int Warnings);

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
    [property: JsonPropertyName("errors")] long Errors,
    [property: JsonPropertyName("ticks_dropped")] long TicksDropped,
    [property: JsonPropertyName("ticks_deferred_by_min_gap")] long TicksDeferredByMinGap,
    [property: JsonPropertyName("triggers_rejected")] long TriggersRejected);

public sealed record StatusDto(
    [property: JsonPropertyName("identities")] int Identities,
    [property: JsonPropertyName("identities_connected")] int IdentitiesConnected,
    [property: JsonPropertyName("units")] int Units,
    [property: JsonPropertyName("units_auto")] int UnitsAuto,
    [property: JsonPropertyName("seed")] string Seed,   // chuỗi: số 64-bit vượt 2^53 sẽ bị làm tròn
    [property: JsonPropertyName("uptime_seconds")] long UptimeSeconds,
    [property: JsonPropertyName("emitted_total")] long EmittedTotal,
    [property: JsonPropertyName("no_message_total")] long NoMessageTotal,
    [property: JsonPropertyName("precondition_total")] long PreconditionTotal,
    [property: JsonPropertyName("errors_total")] long ErrorsTotal,
    [property: JsonPropertyName("commands")] IReadOnlyDictionary<string, long> Commands);