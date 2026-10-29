using System.Text.Json;
using System.Text.Json.Nodes;
using VpuSimulator.Domain;

namespace VpuSimulator.Infrastructure.Messaging;


// File THU NGHIEM RIENG cua B de tu kiem tra RuleOutputEvent khop shape JSON that — KHONG phai
// bai nop (EnvelopeMapper thuoc A theo bang phan cong). Khi A dua ban that, thay the truc tiep.

public static class EnvelopeMapper
{
    private static int GroupCodeOf(RuleOutputEvent ev) => ev switch
    {
        DensityEvent => 1,
        ViolationEvent => 2,
        RecognitionEvent => 3,
        AbnormalEvent => 4,
        _ => throw new NotSupportedException($"Unknown RuleOutputEvent type: {ev.GetType().Name}")
    };

    public static string ToDataEnvelopeJson(RuleOutputEvent ev)
    {
        var details = BuildDetails(ev);

        var item = new JsonObject
        {
            ["class"] = ev.Class,
            ["type"] = GroupCodeOf(ev),
            ["version"] = ev.Version,
            ["roi_id"] = ev.RoiId,
            ["event_key"] = ev.EventKey,
            ["event_state"] = (int)ev.EventState,
            ["source_id"] = ev.SourceId,
            ["source_name"] = ev.SourceName,
            ["task"] = ev.Task,
            ["start_time"] = ev.StartTime.ToUnixTimeMilliseconds(),
            ["end_time"] = ev.EndTime?.ToUnixTimeMilliseconds(),
            ["details"] = details,
        };

        var envelope = new JsonObject
        {
            ["appid"] = "",
            ["scope"] = "task-unit",
            ["type"] = "data",
            ["timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            ["uuid"] = ev.Task,
            ["detail"] = new JsonObject { ["data"] = new JsonArray(item) },
        };

        return envelope.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
    }

    private static JsonObject BuildDetails(RuleOutputEvent ev) => ev switch
    {
        DensityEvent d => new JsonObject
        {
            ["event_type"] = d.EventType,
            ["avg_occupancy"] = Math.Round(d.AvgOccupancy, 1),
            ["avg_speed"] = Math.Round(d.AvgSpeed, 1),
            ["total"] = d.Total,
            ["data"] = new JsonArray(d.Data.Select(p => (JsonNode)new JsonObject
            {
                ["avg_speed"] = p.AvgSpeed,
                ["class_id"] = p.ClassId,
                ["label"] = p.Label,
                ["volume"] = p.Volume,
            }).ToArray()),
            ["partial_data"] = new JsonArray(),
        },

        RecognitionEvent r => new JsonObject
        {
            ["event_type"] = r.EventType,
            ["object_type"] = r.ObjectType,
            ["object_label"] = r.ObjectLabel,
            ["tracker"] = r.Tracker,
            ["bbox"] = BboxToJson(r.Bbox),
            ["image_url"] = r.ImageUrl,
            ["object_image_url"] = r.ObjectImageUrl,
            ["object_crop_url"] = r.ObjectCropUrl,
            ["video_url"] = r.VideoUrl,
            ["accuracy_ratio"] = r.AccuracyRatio,
            ["ai_flow_group_type"] = r.AiFlowGroupType,
            ["profile_object_id"] = r.ProfileObjectId,
            ["event_type_string"] = r.EventTypeString,
            ["groups"] = r.Groups is { } g ? new JsonArray(g.Select(x => JsonSerializer.SerializeToNode(x)).ToArray()) : null,
            ["attributes"] = r.Attributes is { } id
                ? new JsonObject { ["identity"] = AttributeToJson(id) }
                : null,
        },

        ViolationEvent v => new JsonObject
        {
            ["event_type"] = v.DetailsType,
            ["object_type"] = v.ObjectType,
            ["object_label"] = v.ObjectLabel,
            ["tracker"] = v.Tracker,
            ["bbox"] = BboxToJson(v.Bbox),
            ["image_url"] = v.ImageUrl,
            ["video_url"] = v.VideoUrl,
            ["image_ctxt_url"] = v.ImageCtxtUrl is { } urls ? new JsonArray(urls.Select(u => (JsonNode)u!).ToArray()) : null,
            ["attributes"] = v.Attributes is { } id
                ? new JsonObject { ["identity"] = AttributeToJson(id) }
                : null,
            ["ref_id"] = v.RefId,
            ["state"] = v.State,
            ["vio_type_str"] = v.VioTypeStr,
        },

        AbnormalEvent a => new JsonObject
        {
            ["event_type"] = a.EventType,
            ["object_type"] = a.ObjectType,
            ["object_label"] = a.ObjectLabel,
            ["object_size"] = a.ObjectSize,
            ["bbox"] = BboxToJson(a.Bbox),
            ["prob"] = a.Prob,
            ["image_url"] = a.ImageUrl,
            ["video_url"] = a.VideoUrl,
            ["incident_type"] = a.IncidentType,
            ["ref_id"] = a.RefId,
            ["state"] = a.State,
            ["details"] = a.CrowdDetails is not null ? JsonSerializer.SerializeToNode(a.CrowdDetails) : null,
        },

        _ => throw new NotSupportedException($"Unknown RuleOutputEvent type: {ev.GetType().Name}")
    };

    private static JsonObject BboxToJson(Bbox b) =>
        new() { ["x"] = b.X, ["y"] = b.Y, ["w"] = b.W, ["h"] = b.H };

    private static JsonObject AttributeToJson(AttributeItem a) => new()
    {
        ["value"] = a.Value,
        ["prob"] = a.Prob,
        ["bbox"] = a.BBox is { } b ? BboxToJson(b) : null,
    };
}
