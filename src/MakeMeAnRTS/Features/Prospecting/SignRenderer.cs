using MakeMeAnRTS.Core;
using MakeMeAnRTS.Engine;
using MakeMeAnRTS.Features.Camera;
using MakeMeAnRTS.Features.Hud;
using MakeMeAnRTS.Features.World;

namespace MakeMeAnRTS.Features.Prospecting;

/// <summary>
/// Draws survey signs: a post with the reading written on it.
/// </summary>
/// <remarks>
/// The label is the whole point - a sign that just marked a spot would leave the player clicking each
/// one to remember what was there. It is drawn at a fixed pixel size rather than scaled with the
/// camera, so it stays readable when zoomed out to look at the map as a whole.
/// </remarks>
public sealed class SignRenderer
{
    /// <summary>Below this zoom the map is being read as a whole and labels would just be clutter.</summary>
    private const float MinZoomForLabel = 9f;

    public void Draw(Renderer2D renderer, Camera2D camera, IEnumerable<MapSign> signs, int viewerIndex)
    {
        ArgumentNullException.ThrowIfNull(signs);

        foreach (var sign in signs)
        {
            // A player's own surveys only: signs are private notes, not public noticeboards.
            if (sign.OwnerIndex != viewerIndex)
                continue;

            var screen = camera.WorldToScreen(sign.Position.Center);
            if (!camera.ViewportContains(screen))
                continue;

            var postHeight = MathF.Max(6f, camera.Zoom * 0.7f);
            var postWidth = MathF.Max(2f, camera.Zoom * 0.12f);

            renderer.FillRect(screen.X - postWidth / 2f, screen.Y - postHeight, postWidth, postHeight, Palette.Rgb(96, 70, 44));

            // Barren readings still matter up close, but a map dotted with them is unreadable.
            var labelThreshold = sign.IsBarren ? MinZoomForLabel * 1.6f : MinZoomForLabel;

            if (camera.Zoom < labelThreshold)
                continue;

            var label = sign.ShortLabel();
            var width = renderer.Font.MeasureWidth(label) + 6f;
            var height = BitmapFont.GlyphHeight + 5f;
            var boardX = screen.X - width / 2f;
            var boardY = screen.Y - postHeight - height + 1f;

            var tint = sign.IsBarren ? HudColors.DimText : WorldColors.Mineral(sign.Best.Mineral);

            renderer.FillRect(boardX, boardY, width, height, Palette.Rgba(24, 20, 16, 225));
            renderer.DrawRect(boardX, boardY, width, height, Palette.Rgb(120, 92, 58));
            renderer.DrawText(label, boardX + 3f, boardY + 3f, tint);
        }
    }
}
