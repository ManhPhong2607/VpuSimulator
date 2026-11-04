using System.Text.Json.Serialization;

namespace VpuSimulator.Domain;

public readonly record struct Bbox(
    [property: JsonPropertyName("x")] int X,
    [property: JsonPropertyName("y")] int Y,
    [property: JsonPropertyName("w")] int W,
    [property: JsonPropertyName("h")] int H)
{
    public static readonly Bbox Zero = new(0, 0, 0, 0);
}