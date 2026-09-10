using SDL3;

namespace MakeMeAnRTS.Engine;

/// <summary>
/// Immediate-mode primitive drawing in screen pixels: rectangles, lines, circles and triangles.
/// </summary>
/// <remarks>
/// The game draws itself from primitives instead of sprite sheets, so this is the whole art pipeline.
/// Everything takes an explicit colour; nothing here caches renderer state between calls, which keeps
/// draw code order-independent and easy to reason about.
/// </remarks>
public sealed class Renderer2D
{
    /// <summary>Vertices per full circle; 20 is smooth enough at unit scale and cheap enough for hundreds of units.</summary>
    private const int CircleSegments = 20;

    private readonly IntPtr _renderer;
    private readonly SDL.Vertex[] _circleVertices = new SDL.Vertex[CircleSegments * 3];

    public IntPtr Handle => _renderer;
    public BitmapFont Font { get; }

    public Renderer2D(IntPtr renderer, BitmapFont font)
    {
        _renderer = renderer;
        Font = font;
    }

    public void Clear(SDL.Color color)
    {
        SDL.SetRenderDrawColor(_renderer, color.R, color.G, color.B, color.A);
        SDL.RenderClear(_renderer);
    }

    public void FillRect(float x, float y, float width, float height, SDL.Color color)
    {
        SDL.SetRenderDrawColor(_renderer, color.R, color.G, color.B, color.A);
        var rect = new SDL.FRect { X = x, Y = y, W = width, H = height };
        SDL.RenderFillRect(_renderer, in rect);
    }

    public void DrawRect(float x, float y, float width, float height, SDL.Color color)
    {
        SDL.SetRenderDrawColor(_renderer, color.R, color.G, color.B, color.A);
        var rect = new SDL.FRect { X = x, Y = y, W = width, H = height };
        SDL.RenderRect(_renderer, in rect);
    }

    /// <summary>Outlines a rectangle with a border <paramref name="thickness"/> pixels thick, drawn inwards.</summary>
    public void DrawRectThick(float x, float y, float width, float height, SDL.Color color, float thickness)
    {
        if (thickness <= 1f)
        {
            DrawRect(x, y, width, height, color);
            return;
        }

        FillRect(x, y, width, thickness, color);
        FillRect(x, y + height - thickness, width, thickness, color);
        FillRect(x, y + thickness, thickness, height - thickness * 2f, color);
        FillRect(x + width - thickness, y + thickness, thickness, height - thickness * 2f, color);
    }

    public void DrawLine(float x1, float y1, float x2, float y2, SDL.Color color)
    {
        SDL.SetRenderDrawColor(_renderer, color.R, color.G, color.B, color.A);
        SDL.RenderLine(_renderer, x1, y1, x2, y2);
    }

    public void FillCircle(float centerX, float centerY, float radius, SDL.Color color)
    {
        if (radius <= 0.5f)
        {
            FillRect(centerX - 0.5f, centerY - 0.5f, 1f, 1f, color);
            return;
        }

        var fillColor = ToFColor(color);

        for (var segment = 0; segment < CircleSegments; segment++)
        {
            var angle0 = segment * MathF.Tau / CircleSegments;
            var angle1 = (segment + 1) * MathF.Tau / CircleSegments;

            _circleVertices[segment * 3 + 0] = Vertex(centerX, centerY, fillColor);
            _circleVertices[segment * 3 + 1] = Vertex(centerX + MathF.Cos(angle0) * radius, centerY + MathF.Sin(angle0) * radius, fillColor);
            _circleVertices[segment * 3 + 2] = Vertex(centerX + MathF.Cos(angle1) * radius, centerY + MathF.Sin(angle1) * radius, fillColor);
        }

        SDL.RenderGeometry(_renderer, IntPtr.Zero, _circleVertices, _circleVertices.Length, IntPtr.Zero, 0);
    }

    public void FillTriangle(float x1, float y1, float x2, float y2, float x3, float y3, SDL.Color color)
    {
        var fillColor = ToFColor(color);
        Span<SDL.Vertex> vertices =
        [
            Vertex(x1, y1, fillColor),
            Vertex(x2, y2, fillColor),
            Vertex(x3, y3, fillColor),
        ];

        SDL.RenderGeometry(_renderer, IntPtr.Zero, vertices, 3, IntPtr.Zero, 0);
    }

    public void DrawText(string text, float x, float y, SDL.Color color, int scale = 1)
        => Font.Draw(_renderer, text, x, y, color, scale);

    public void DrawTextCentered(string text, float centerX, float y, SDL.Color color, int scale = 1)
        => Font.DrawCentered(_renderer, text, centerX, y, color, scale);

    /// <summary>Text with a one-pixel offset shadow, for labels that must stay readable over any terrain.</summary>
    public void DrawTextShadowed(string text, float x, float y, SDL.Color color, int scale = 1)
    {
        Font.Draw(_renderer, text, x + scale, y + scale, new SDL.Color { R = 0, G = 0, B = 0, A = 190 }, scale);
        Font.Draw(_renderer, text, x, y, color, scale);
    }

    public void Present() => SDL.RenderPresent(_renderer);

    /// <summary>Restricts drawing to a rectangle. Pass <c>null</c> to draw to the whole target again.</summary>
    public void SetClip(SDL.Rect? clip)
    {
        if (clip is null)
        {
            SDL.SetRenderClipRect(_renderer, IntPtr.Zero);
            return;
        }

        var rect = clip.Value;
        SDL.SetRenderClipRect(_renderer, in rect);
    }

    private static SDL.Vertex Vertex(float x, float y, SDL.FColor color) => new()
    {
        Position = new SDL.FPoint { X = x, Y = y },
        Color = color,
        TexCoord = new SDL.FPoint { X = 0f, Y = 0f },
    };

    private static SDL.FColor ToFColor(SDL.Color color) => new()
    {
        R = color.R / 255f,
        G = color.G / 255f,
        B = color.B / 255f,
        A = color.A / 255f,
    };
}
