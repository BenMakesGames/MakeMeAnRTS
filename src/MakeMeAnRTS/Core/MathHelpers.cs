namespace MakeMeAnRTS.Core;

public static class MathHelpers
{
    public static float Clamp01(float value) => Math.Clamp(value, 0f, 1f);

    public static float Lerp(float a, float b, float t) => a + (b - a) * t;

    /// <summary>Where <paramref name="value"/> sits between the edges, as 0..1. Returns 0 for a degenerate range.</summary>
    public static float InverseLerp(float edge0, float edge1, float value)
    {
        var span = edge1 - edge0;
        return MathF.Abs(span) < 1e-6f ? 0f : Clamp01((value - edge0) / span);
    }

    /// <summary>Clamps to a byte, so noise and accumulator maths can never wrap around to 0 or 255.</summary>
    public static byte ToByte(float value) => (byte)Math.Clamp(MathF.Round(value), 0f, 255f);

    public static byte ToByte(int value) => (byte)Math.Clamp(value, 0, 255);
}
