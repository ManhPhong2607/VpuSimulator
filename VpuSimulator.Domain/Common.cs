using System;
using System.Collections.Generic;
using System.Text;

namespace VpuSimulator.Domain
{
    public readonly record struct BoundingBox(int X, int Y, int W, int H)
    {
        public static readonly BoundingBox Zero = new(0, 0, 0, 0);
    }

    public sealed record AttributeItem(string Value, double Prob, BoundingBox? BBox);

    public sealed record RegionConfig(
        string Path,
        string? Head,
        int TargetWidth,
        int TargetHeight,
        string Anchor);

    public sealed record AffectObjectConfig(
        string label,
        double OverlapRatio,
        IReadOnlyList<double> DimRatio,
        int MinArea,
        IReadOnlyList<string> Movement,
        int Direction
        );
}
 