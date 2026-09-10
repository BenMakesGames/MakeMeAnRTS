using MakeMeAnRTS.Core;

namespace MakeMeAnRTS.Features.World.Generation;

/// <summary>Builds the smooth, normalised noise fields generation is built on: elevation, moisture, ore.</summary>
internal static class NoiseField
{
    /// <summary>
    /// Fractal noise normalised so the field always spans the full 0..1 range.
    /// </summary>
    /// <remarks>
    /// Raw fBm clusters around its midpoint, so a fixed water level would flood one seed and leave the
    /// next bone dry. Rescaling to the observed min/max makes thresholds like
    /// <see cref="WorldGenSettings.WaterLevel"/> mean the same thing on every seed.
    /// </remarks>
    public static Grid2D<float> Generate(int width, int height, int seed, int octaves, float featureSize)
    {
        var noise = new Noise(seed);
        var field = new Grid2D<float>(width, height);
        var frequency = 1f / featureSize;

        var min = float.MaxValue;
        var max = float.MinValue;

        foreach (var pos in field.Positions())
        {
            var value = noise.SampleFractal(pos.X, pos.Y, octaves, frequency);
            field[pos] = value;

            min = MathF.Min(min, value);
            max = MathF.Max(max, value);
        }

        var span = max - min;
        if (span < 1e-6f)
        {
            field.Fill(0.5f);
            return field;
        }

        foreach (var pos in field.Positions())
            field[pos] = (field[pos] - min) / span;

        return field;
    }
}
