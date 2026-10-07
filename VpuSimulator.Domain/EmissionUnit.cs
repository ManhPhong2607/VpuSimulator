using System;
using System.Collections.Generic;
using System.Text;

namespace VpuSimulator.Domain
{
    public sealed class EmissionUnit
    {
        public required string AppId { get; set; }
        public required string TaskId { get; set; }
        public required string SourceId { get; set; }
        public required string SourceName { get; set; }
        public required int RoiId { get; set; }
        public bool IsEmitting { get; set; }
        public DateTimeOffset? LastEmitAt { get; set; }
        public ActiveFollowUpState? ActiveFollowUp { get; set; }
    }

    public sealed class ActiveFollowUpState
    {   
        public required string EventKey { get; set; }
        public required string Label { get; set; }
        public required int ObjectType { get; set; }
        public required int Tracker { get; set; }
        public string? RefId { get; set; }
    }
}
