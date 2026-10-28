using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace VpuSimulator.Domain
{
    public sealed record RuleContext
    {
        // field chung rule_conf 
        public required int RoiId { get; init; }                    
        public required string Name { get; init; }
        public required int ZoneType { get; init; }                  // "type" — tham khao, KHONG dung de dispatch
        public IReadOnlyList<int>? RelatedRoi { get; init; }           // Traffic Light
        public required AiFlowCode? AiFlowCode { get; init; }
        public bool Enable { get; init; } = true;
        public IReadOnlyList<AffectObjectConfig>? AffectObject { get; init; }
        public RegionConfig? Region { get; init; }

        // field rieng tung bai, chua parse — moi handler tu doc phan cua no 
        public required JsonElement RawParams { get; init; }
        
    }
}
