using System.Text.Json.Serialization;
using VpuSimulator.Domain;
using VpuSimulator.Domain.Events;

namespace VpuSimulator.Application.Messaging;

/// <summary>
/// Envelope dữ liệu sự kiện AI gửi ra RabbitMQ (routing key: inbound.data.vpu.<task_id>).
/// Tuân thủ nghiêm ngặt SRS §3.1.6, Contract v4 §2 và AI_ModelPayloads §4.1 - 4.5.
/// </summary>
public sealed record DataEventEnvelope(
    [property: JsonPropertyName("appid")] string AppId,
    [property: JsonPropertyName("scope")] string Scope,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("timestamp")] long Timestamp,
    [property: JsonPropertyName("uuid")] string Uuid,
    [property: JsonPropertyName("detail")] DataEventDetail Detail);

public sealed record DataEventDetail(
    [property: JsonPropertyName("data")] IReadOnlyList<DataEventItem> Data);

public sealed record DataEventItem(
    [property: JsonPropertyName("class")] int Class,
    [property: JsonPropertyName("type")] int Type,
    [property: JsonPropertyName("version")] int Version,
    [property: JsonPropertyName("roi_id")] int RoiId,
    [property: JsonPropertyName("event_key")] string EventKey,
    [property: JsonPropertyName("event_state")] int EventState,
    [property: JsonPropertyName("source_id")] string SourceId,
    [property: JsonPropertyName("source_name")] string SourceName,
    [property: JsonPropertyName("task")] string Task,
    [property: JsonPropertyName("start_time")] long StartTime,
    [property: JsonPropertyName("end_time")] long EndTime,
    [property: JsonPropertyName("details")] object Details);

/// <summary>
/// Phần details dành riêng cho nhóm Nhận diện (Recognition - Group 3, ví dụ Rule #1 License Plate).
/// </summary>
public sealed record RecognitionEventDetails(
    [property: JsonPropertyName("event_type")] int EventType,
    [property: JsonPropertyName("object_type")] int ObjectType,
    [property: JsonPropertyName("object_label")] string ObjectLabel,
    [property: JsonPropertyName("tracker")] int Tracker,
    [property: JsonPropertyName("bbox")] Bbox Bbox,
    [property: JsonPropertyName("image_url")] string? ImageUrl,
    [property: JsonPropertyName("object_image_url")] string? ObjectImageUrl,
    [property: JsonPropertyName("object_crop_url")] string? ObjectCropUrl,
    [property: JsonPropertyName("video_url")] string? VideoUrl,
    [property: JsonPropertyName("accuracy_ratio")] double? AccuracyRatio,
    [property: JsonPropertyName("ai_flow_group_type")] int? AiFlowGroupType,
    [property: JsonPropertyName("event_type_string")] string? EventTypeString,
    [property: JsonPropertyName("profile_object_id")] int ProfileObjectId,
    [property: JsonPropertyName("groups")] IReadOnlyList<object>? Groups,
    [property: JsonPropertyName("attributes")] RecognitionWireAttributes Attributes);

public sealed record RecognitionWireAttributes(
    [property: JsonPropertyName("identity")] WireAttributeItem Identity);

public sealed record WireAttributeItem(
    [property: JsonPropertyName("prob")] double Prob,
    [property: JsonPropertyName("value")] string Value,
    [property: JsonPropertyName("category")] string? Category,
    [property: JsonPropertyName("bbox")] Bbox? Bbox);

/// <summary>
/// Phần details dành riêng cho nhóm Vi phạm (Violation - Group 2, ví dụ Rule #6 Traffic Signal Violation).
/// </summary>
public sealed record ViolationEventDetails(
    [property: JsonPropertyName("event_type")] int EventType,
    [property: JsonPropertyName("type")] int Type,
    [property: JsonPropertyName("object_type")] int ObjectType,
    [property: JsonPropertyName("object_label")] string ObjectLabel,
    [property: JsonPropertyName("tracker")] int Tracker,
    [property: JsonPropertyName("bbox")] Bbox Bbox,
    [property: JsonPropertyName("image_url")] string? ImageUrl,
    [property: JsonPropertyName("video_url")] string? VideoUrl,
    [property: JsonPropertyName("image_ctxt_url")] IReadOnlyList<string>? ImageCtxtUrl,
    [property: JsonPropertyName("attributes")] object? Attributes,
    [property: JsonPropertyName("ref_id")] string? RefId,
    [property: JsonPropertyName("state")] int? State,
    [property: JsonPropertyName("vio_type_str")] string? VioTypeStr);

/// <summary>
/// Phần details dành riêng cho nhóm Mật độ (Density - Group 1).
/// </summary>
public sealed record DensityEventDetails(
    [property: JsonPropertyName("event_type")] int EventType,
    [property: JsonPropertyName("total")] int Total,
    [property: JsonPropertyName("avg_occupancy")] double AvgOccupancy,
    [property: JsonPropertyName("avg_speed")] double AvgSpeed,
    [property: JsonPropertyName("data")] IReadOnlyList<DensityDataPoint> Data,
    [property: JsonPropertyName("partial_data")] IReadOnlyList<object> PartialData);

/// <summary>
/// Phần details dành riêng cho nhóm Bất thường (Abnormal - Group 4).
/// </summary>
public sealed record AbnormalEventDetails(
    [property: JsonPropertyName("event_type")] int EventType,
    [property: JsonPropertyName("object_type")] int ObjectType,
    [property: JsonPropertyName("object_label")] string ObjectLabel,
    [property: JsonPropertyName("object_size")] int? ObjectSize,
    [property: JsonPropertyName("bbox")] Bbox Bbox,
    [property: JsonPropertyName("prob")] double? Prob,
    [property: JsonPropertyName("image_url")] string? ImageUrl,
    [property: JsonPropertyName("video_url")] string? VideoUrl,
    [property: JsonPropertyName("incident_type")] int? IncidentType,
    [property: JsonPropertyName("ref_id")] int? RefId,
    [property: JsonPropertyName("state")] int? State,
    [property: JsonPropertyName("details")] object? Details);
