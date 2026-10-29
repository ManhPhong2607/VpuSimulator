using System.Collections.Generic;
using System.Text.Json;

namespace VpuSimulator.Domain;

public sealed class RuleContext
{
    public required int RoiId { get; init; }
    public required string Name { get; init; }
    public required AiFlowCode AiFlowCode { get; init; }
    public int ZoneType { get; init; }
    public IReadOnlyList<int>? RelatedRoi { get; init; }
    public bool Enable { get; init; } = true;
    public IReadOnlyList<AffectObjectConfig>? AffectObject { get; init; }
    public RegionConfig? Region { get; init; }
    public required JsonElement RawParams { get; init; }  //tham số riêng từng rule
}