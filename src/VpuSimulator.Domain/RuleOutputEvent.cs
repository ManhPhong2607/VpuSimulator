using System;
using System.Collections.Generic;
using System.Text;

namespace VpuSimulator.Domain
{
    public enum EventState { Idle = 0, Early = 1, Lately = 2, Confirmed = 3, Discarded = 4, Deactivate = 5, Suspect = 6, Unknown = 7 }

    // SourceId/SourceName/Task — moi RuleHandler/Builder phai tu COPY gia tri tu context.Unit vao day khi dung su kien, khong con tu dong "doc qua Unit"
    // nhu truoc. Checklist cho tung handler: SourceId = context.Unit.SourceId, SourceName = context.Unit.SourceName, Task = context.Unit.TaskId.
    public abstract record RuleOutputEvent
    {
        public required string EventKey { get; init; }
        public required EventState EventState { get; init; }
        public required string SourceId { get; init; }
        public required string SourceName { get; init; }
        public required string Task { get; init; }
        public required DateTimeOffset StartTime { get; init; }
        public DateTimeOffset? EndTime { get; init; }
        public required int RoiId { get; init; }
        public int Class { get; init; } = 1;
        public int Version { get; init; } = 2;
    }

    public readonly record struct DensityDataPoint(double AvgSpeed, int ClassId, string Label, int Volume);

    public sealed record DensityEvent : RuleOutputEvent
    {
        public int EventType { get; init; } = 1;
        public required int Total { get; init; }
        public required double AvgSpeed { get; init; }             // PHAI Math.Round(x, 1) — bug tim thay khi verify
        public required double AvgOccupancy { get; init; }          // PHAI Math.Round(x, 1)
        public required IReadOnlyList<DensityDataPoint> Data { get; init; }
        public IReadOnlyList<object> PartialData { get; init; } = Array.Empty<object>(); 
    }

    public sealed record RecognitionEvent : RuleOutputEvent
    {
        public int EventType { get; init; } = 3;
        public required int ObjectType { get; init; }
        public required string ObjectLabel { get; init; }
        public required int Tracker { get; init; }
        public required Bbox Bbox { get; init; }
        public string? ImageUrl { get; init; }
        public string? ObjectImageUrl { get; init; }
        public string? ObjectCropUrl { get; init; }
        public string? VideoUrl { get; init; }                       // luon null o nhom recognition 
        public AttributeItem? Attributes { get; init; }             

        // 5 field co dinh 
        public double AccuracyRatio { get; init; } = 0;
        public int AiFlowGroupType { get; init; } = 0;
        public int ProfileObjectId { get; init; } = 0;
        public string? EventTypeString { get; init; } = null;
        public IReadOnlyList<object>? Groups { get; init; } = null;
    }

    public enum FollowUpKind { Deactivate, Discard }
    public sealed record ViolationEvent : RuleOutputEvent
    {
        public int EventType { get; init; } = 2;

        /// details.type (KHAC EventType header o tren — xem Data Conflict #3, SRS_nghiep_vu muc 8): 4 (su kien moi) hoac 2/4 tuy rule khi follow-up (#6 doi thanh 2, #7 giu nguyen 4).
        public required int DetailsType { get; init; }
        public required int ObjectType { get; init; }
        public required string ObjectLabel { get; init; }
        public required int Tracker { get; init; }
        public required Bbox Bbox { get; init; }
        public string? ImageUrl { get; init; }
        public string? VideoUrl { get; init; }                       // null o su kien moi, "" o follow-up (SRS.md 3.1.6)
        public IReadOnlyList<string>? ImageCtxtUrl { get; init; }      
        public AttributeItem? Attributes { get; init; }               
        public string? RefId { get; init; }                           
        public int? State { get; init; }                                

        public required string VioTypeStr { get; init; }

        // null = su kien MOI. Co gia tri = day la ban tin follow-up thay cho su kien moi.
        public FollowUpKind? FollowUp { get; init; }
    }

    public sealed record AbnormalEvent : RuleOutputEvent
    {
        public int EventType { get; init; } = 4;
        public required int ObjectType { get; init; }                 // 1=nguoi, 8=khuon mat (virtual fence)
        public required string ObjectLabel { get; init; }
        public int? ObjectSize { get; init; }
        public Bbox Bbox { get; init; } = Bbox.Zero;                   // LUON co mat, mac dinh {0,0,0,0} tru #19
        public double? Prob { get; init; }
        public string? ImageUrl { get; init; }
        public string? VideoUrl { get; init; }
        public int? IncidentType { get; init; }                        // null = virtual fence #19; co gia tri = #15/#20
        public int? RefId { get; init; }                              
        public int? State { get; init; }
        public object? CrowdDetails { get; init; }                        // { crowd_id, crowd_size } — bien the tu tap dong nguoi
    }
}
