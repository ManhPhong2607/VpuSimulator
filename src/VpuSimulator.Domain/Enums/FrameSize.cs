namespace VpuSimulator.Domain.Enums
{
    // Kích thước khung hình để sinh bbox 
    public static class FrameSize
    {
        public const int FallbackWidth = 1920;
        public const int FallbackHeight = 1080;

        public static (int Width, int Height) Resolve(int? width, int? height) =>
            width is > 0 && height is > 0 ? (width.Value, height.Value) : (FallbackWidth, FallbackHeight);
    }
}
