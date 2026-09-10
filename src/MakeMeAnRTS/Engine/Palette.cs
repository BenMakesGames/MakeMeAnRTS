using SDL3;

namespace MakeMeAnRTS.Engine;

/// <summary>Colour construction helpers. Feature-specific palettes live with their feature.</summary>
public static class Palette
{
    public static SDL.Color Rgb(byte r, byte g, byte b) => new() { R = r, G = g, B = b, A = 255 };

    public static SDL.Color Rgba(byte r, byte g, byte b, byte a) => new() { R = r, G = g, B = b, A = a };

    public static SDL.Color Gray(byte level) => Rgb(level, level, level);

    public static SDL.Color WithAlpha(this SDL.Color color, byte alpha) => new() { R = color.R, G = color.G, B = color.B, A = alpha };

    public static SDL.Color Lerp(SDL.Color from, SDL.Color to, float t)
    {
        t = Math.Clamp(t, 0f, 1f);

        return new SDL.Color
        {
            R = (byte)(from.R + (to.R - from.R) * t),
            G = (byte)(from.G + (to.G - from.G) * t),
            B = (byte)(from.B + (to.B - from.B) * t),
            A = (byte)(from.A + (to.A - from.A) * t),
        };
    }

    /// <summary>Multiplies RGB by <paramref name="factor"/>, for cheap shading of a base colour.</summary>
    public static SDL.Color Scale(SDL.Color color, float factor)
    {
        static byte Apply(byte channel, float factor) => (byte)Math.Clamp(MathF.Round(channel * factor), 0f, 255f);

        return new SDL.Color { R = Apply(color.R, factor), G = Apply(color.G, factor), B = Apply(color.B, factor), A = color.A };
    }

    public static readonly SDL.Color White = Rgb(255, 255, 255);
    public static readonly SDL.Color Black = Rgb(0, 0, 0);
}
