using VpuSimulator.Domain.Events;

namespace VpuSimulator.Application.Messaging;

/// <summary>
/// Chuyển đổi từ RuleOutputEvent (Domain) sang DataEventEnvelope (chuẩn wire gửi lên RabbitMQ).
/// </summary>
public static class EnvelopeMapper
{
    private const string TaskScope = "task-unit";
    private const string DataType = "data";

    public static DataEventEnvelope Map(string appId, string taskId, DateTimeOffset now, RuleOutputEvent ev)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appId);
        ArgumentException.ThrowIfNullOrWhiteSpace(taskId);
        ArgumentNullException.ThrowIfNull(ev);

        var details = MapDetails(ev);

        var item = new DataEventItem(
            Class: ev.Class,
            Type: ev.Type,
            Version: ev.Version,
            RoiId: ev.RoiId,
            EventKey: ev.EventKey,
            EventState: (int)ev.EventState,
            SourceId: ev.SourceId,
            SourceName: ev.SourceName,
            Task: ev.Task,
            StartTime: ev.StartTime.ToUnixTimeMilliseconds(),
            EndTime: (ev.EndTime ?? ev.StartTime).ToUnixTimeMilliseconds(),
            Details: details
        );

        return new DataEventEnvelope(
            AppId: appId,
            Scope: TaskScope,
            Type: DataType,
            Timestamp: now.ToUnixTimeMilliseconds(),
            Uuid: taskId,
            Detail: new DataEventDetail(new[] { item })
        );
    }

    private static object MapDetails(RuleOutputEvent ev) => ev switch
    {
        RecognitionEvent rec => new RecognitionEventDetails(
            EventType: rec.EventType,
            ObjectType: rec.ObjectType,
            ObjectLabel: rec.ObjectLabel,
            Tracker: rec.Tracker,
            Bbox: rec.Bbox,
            ImageUrl: rec.ImageUrl,
            ObjectImageUrl: rec.ObjectImageUrl,
            ObjectCropUrl: rec.ObjectCropUrl,
            VideoUrl: rec.VideoUrl,
            AccuracyRatio: rec.AccuracyRatio,
            AiFlowGroupType: rec.AiFlowGroupType,
            EventTypeString: rec.EventTypeString,
            ProfileObjectId: rec.ProfileObjectId,
            Groups: rec.Groups,
            Attributes: new RecognitionWireAttributes(
                rec.Attributes != null
                    ? new WireAttributeItem(rec.Attributes.Prob, rec.Attributes.Value, null, rec.Attributes.BBox)
                    : new WireAttributeItem(0.0, string.Empty, null, null)
            )
        ),

        ViolationEvent vio => new ViolationEventDetails(
            EventType: vio.EventType,
            Type: vio.DetailsType,
            ObjectType: vio.ObjectType,
            ObjectLabel: vio.ObjectLabel,
            Tracker: vio.Tracker,
            Bbox: vio.Bbox,
            ImageUrl: vio.ImageUrl,
            VideoUrl: vio.VideoUrl,
            ImageCtxtUrl: vio.ImageCtxtUrl,
            Attributes: vio.Attributes != null
                ? new { identity = new WireAttributeItem(vio.Attributes.Prob, vio.Attributes.Value, null, vio.Attributes.BBox) }
                : null,
            RefId: vio.RefId,
            State: vio.State,
            VioTypeStr: vio.VioTypeStr
        ),

        DensityEvent den => new DensityEventDetails(
            EventType: den.EventType,
            Total: den.Total,
            AvgOccupancy: den.AvgOccupancy,
            AvgSpeed: den.AvgSpeed,
            Data: den.Data,
            PartialData: den.PartialData
        ),

        AbnormalEvent abn => new AbnormalEventDetails(
            EventType: abn.EventType,
            ObjectType: abn.ObjectType,
            ObjectLabel: abn.ObjectLabel,
            ObjectSize: abn.ObjectSize,
            Bbox: abn.Bbox,
            Prob: abn.Prob,
            ImageUrl: abn.ImageUrl,
            VideoUrl: abn.VideoUrl,
            IncidentType: abn.IncidentType,
            RefId: abn.RefId,
            State: abn.State,
            Details: abn.CrowdDetails
        ),

        _ => throw new NotSupportedException($"Không hỗ trợ loại sự kiện {ev.GetType().Name}")
    };
}
