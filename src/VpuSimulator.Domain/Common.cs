using System;
using System.Collections.Generic;
using System.Text;

namespace VpuSimulator.Domain
{
    public readonly record struct Bbox(int X, int Y, int W, int H)
    {
        // Dung cho follow-up deactivate/discard (SRS 6.6/6.7): bbox co dinh {0,0,0,0}
        public static readonly Bbox Zero = new(0, 0, 0, 0);
    }

    public sealed record AttributeItem(string Value, double Prob, Bbox? BBox);

    public readonly record struct TargetDims(int Width, int Height);

    public sealed record RegionConfig(
        string Path,
        string? Head,
        TargetDims? TargetDims,
        string? Anchor);

    public sealed record AffectObjectConfig(
        string Label,
        double OverlapRatio,
        IReadOnlyList<double> DimRatio,
        int MinArea,
        IReadOnlyList<string> Movement,
        int Direction);
}
 