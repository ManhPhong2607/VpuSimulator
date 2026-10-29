using VpuSimulator.Domain;

namespace VpuSimulator.Rules.Common;

public static class BboxGenerator
{
    private const double MinLong = 300, MaxLong = 500;
    private const double MinRatio = 1.0 / 3.0, MaxRatio = 3.0;
    private const double IdMinLong = 100, IdMaxLong = 200;

    public static Bbox Main(Random rng, int frameW, int frameH)
    {
        for (int attempt = 0; attempt < 20; attempt++)
        {
            var longSide = MinLong + rng.NextDouble() * (MaxLong - MinLong);
            var ratio = Math.Exp(Math.Log(MinRatio) + rng.NextDouble() * (Math.Log(MaxRatio) - Math.Log(MinRatio)));
            int w, h;
            if (ratio >= 1) 
            { w = (int)longSide;
              h = Math.Max(1, (int)(longSide / ratio));
            }
            else { 
                h = (int)longSide;
                w = Math.Max(1, (int)(longSide * ratio)); 
            }
            if (w <= frameW && h <= frameH)
                return new Bbox(rng.Next(0, frameW - w + 1), rng.Next(0, frameH - h + 1), w, h);
        }

        var fw = Math.Max(1, frameW / 2);
        var fh = Math.Max(1, frameH / 2);
        return new Bbox(rng.Next(0, frameW - fw + 1), 
            rng.Next(0, frameH - fh + 1), fw, fh);
    }

    public static Bbox Identity(Random rng, Bbox main)
    {
        var landscape = main.W >= main.H;
        var longAxis = landscape ? main.W : main.H;
        var shortAxis = landscape ? main.H : main.W;

        var longSide = Math.Min((int)(IdMinLong + rng.NextDouble() * (IdMaxLong - IdMinLong)), longAxis);
        var minRatio = Math.Max(1.0, longSide / (double)Math.Max(1, shortAxis));
        var ratio = minRatio >= 3.0 ? 3.0 : minRatio + rng.NextDouble() * (3.0 - minRatio);
        var shortSide = Math.Min(Math.Max(1, (int)(longSide / ratio)), shortAxis);

        var w = landscape ? longSide : shortSide;
        var h = landscape ? shortSide : longSide;
        return new Bbox(main.X + rng.Next(0, main.W - w + 1), 
            main.Y + rng.Next(0, main.H - h + 1), w, h);
    }
}