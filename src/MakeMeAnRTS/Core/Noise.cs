namespace MakeMeAnRTS.Core;

/// <summary>
/// Seeded value noise with fractal (fBm) octaves. Value noise rather than Perlin because terrain
/// only needs smooth, repeatable blobs and this has no gradient tables to get wrong.
/// </summary>
public sealed class Noise
{
    private readonly int _seed;

    public Noise(int seed) => _seed = seed;

    /// <summary>Smooth noise in [0, 1] sampled at an arbitrary point.</summary>
    public float Sample(float x, float y)
    {
        var x0 = (int)MathF.Floor(x);
        var y0 = (int)MathF.Floor(y);
        var fx = Smoothstep(x - x0);
        var fy = Smoothstep(y - y0);

        var top = Lerp(Corner(x0, y0), Corner(x0 + 1, y0), fx);
        var bottom = Lerp(Corner(x0, y0 + 1), Corner(x0 + 1, y0 + 1), fx);

        return Lerp(top, bottom, fy);
    }

    /// <summary>
    /// Sum of <paramref name="octaves"/> doublings of frequency at halving amplitude, normalised to
    /// [0, 1]. More octaves means more fine detail on top of the same large-scale shape.
    /// </summary>
    public float SampleFractal(float x, float y, int octaves, float frequency, float persistence = 0.5f)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(octaves, 1);

        var total = 0f;
        var amplitude = 1f;
        var maxAmplitude = 0f;

        for (var octave = 0; octave < octaves; octave++)
        {
            total += Sample(x * frequency, y * frequency) * amplitude;
            maxAmplitude += amplitude;
            amplitude *= persistence;
            frequency *= 2f;
        }

        return total / maxAmplitude;
    }

    private float Corner(int x, int y)
    {
        var hash = unchecked((uint)(x * 374761393 + y * 668265263 + _seed * 1274126177));
        hash = (hash ^ (hash >> 13)) * 1274126177u;
        hash ^= hash >> 16;

        return (hash >> 8) * (1f / (1 << 24));
    }

    private static float Smoothstep(float t) => t * t * (3f - 2f * t);
    private static float Lerp(float a, float b, float t) => a + (b - a) * t;
}
