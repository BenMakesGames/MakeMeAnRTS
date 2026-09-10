using MakeMeAnRTS.Core;
using MakeMeAnRTS.Engine;
using MakeMeAnRTS.Features.Camera;

namespace MakeMeAnRTS.Features.Vision;

/// <summary>
/// Draws the fog of war over everything else: solid for unseen ground, a shadow for remembered ground.
/// </summary>
/// <remarks>
/// Drawn last, so it covers terrain and buildings alike. Two shades rather than one because the two
/// states mean different things to a player: black is "no idea", shadow is "I have been here, but I
/// cannot see it now".
/// </remarks>
public sealed class FogRenderer
{
    private static readonly SDL3.SDL.Color Unexplored = Palette.Rgba(6, 7, 10, 255);
    private static readonly SDL3.SDL.Color Remembered = Palette.Rgba(8, 10, 16, 110);

    public void Draw(Renderer2D renderer, Camera2D camera, PlayerVision vision)
    {
        ArgumentNullException.ThrowIfNull(vision);

        var (min, max) = camera.VisibleTileRange();
        var size = MathF.Ceiling(camera.Zoom);

        for (var y = min.Y; y <= max.Y; y++)
        {
            for (var x = min.X; x <= max.X; x++)
            {
                var pos = new GridPos(x, y);

                if (vision.IsVisible(pos))
                    continue;

                var screen = camera.WorldToScreen(new Vec2(x, y));
                var shade = vision.IsExplored(pos) ? Remembered : Unexplored;

                renderer.FillRect(MathF.Floor(screen.X), MathF.Floor(screen.Y), size, size, shade);
            }
        }
    }
}
