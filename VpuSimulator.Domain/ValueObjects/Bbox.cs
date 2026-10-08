namespace VpuSimulator.Domain;

public readonly record struct Bbox(int X, int Y, int W, int H)
{
    public static readonly Bbox Zero = new(0, 0, 0, 0);
}