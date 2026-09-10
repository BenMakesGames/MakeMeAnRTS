using SDL3;
using MakeMeAnRTS.Core;
using MakeMeAnRTS.Engine;
using MakeMeAnRTS.Features.Camera;
using MakeMeAnRTS.Features.Hud;
using MakeMeAnRTS.Features.Match;
using MakeMeAnRTS.Features.Vision;

namespace MakeMeAnRTS.Features.Units;

/// <summary>
/// Draws units as coloured discs with a letter for their kind.
/// </summary>
/// <remarks>
/// There are no sprites, so a unit has to be readable from its colour (whose it is), its letter (what
/// it is) and a couple of small marks (hurt, carrying, selected). Anything more decorative would just
/// make a screen full of them harder to read.
/// </remarks>
public sealed class UnitRenderer
{
    /// <summary>Unit radius as a fraction of a tile.</summary>
    private const float RadiusInTiles = 0.34f;

    /// <summary>Below this zoom a unit is a few pixels across and its letter is unreadable, so it is dropped.</summary>
    private const float MinZoomForGlyph = 13f;

    private readonly MatchState _state;

    public UnitRenderer(MatchState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        _state = state;
    }

    public void Draw(Renderer2D renderer, Camera2D camera, PlayerVision viewer, int viewerIndex, IReadOnlySet<int> selectedUnitIds)
    {
        ArgumentNullException.ThrowIfNull(viewer);
        ArgumentNullException.ThrowIfNull(selectedUnitIds);

        var radius = camera.Zoom * RadiusInTiles;

        foreach (var unit in _state.Units)
        {
            // Enemies are only drawn where this player can currently see; explored ground is not enough.
            if (unit.OwnerIndex != viewerIndex && !viewer.IsVisible(unit.Tile))
                continue;

            var screen = camera.WorldToScreen(unit.Position);
            if (!camera.ViewportContains(screen))
                continue;

            var color = _state.PlayerAt(unit.OwnerIndex).Color;

            if (selectedUnitIds.Contains(unit.Id))
                renderer.FillCircle(screen.X, screen.Y, radius + 2.5f, Palette.Rgba(255, 255, 255, 170));

            // A dark rim keeps units legible against grass, sand and rock alike.
            renderer.FillCircle(screen.X, screen.Y, radius, Palette.Scale(color, 0.45f));
            renderer.FillCircle(screen.X, screen.Y, radius * 0.78f, color);

            if (camera.Zoom >= MinZoomForGlyph)
                DrawGlyph(renderer, unit, screen, radius);

            if (unit.CarriedAmount > 0)
                DrawCarriedLoad(renderer, unit, screen, radius);

            if (unit.Health < unit.Stats.MaxHealth)
                DrawHealthBar(renderer, unit, screen, radius);
        }
    }

    private static void DrawGlyph(Renderer2D renderer, Unit unit, Vec2 screen, float radius)
    {
        var scale = radius >= 11f ? 2 : 1;
        var glyph = unit.Stats.Glyph.ToString();

        renderer.DrawTextCentered(glyph, screen.X, screen.Y - BitmapFont.GlyphHeight * scale / 2f, Palette.Rgb(20, 22, 28), scale);
    }

    /// <summary>A small tab in the resource's own colour, so a full hauler is obvious at a glance.</summary>
    private static void DrawCarriedLoad(Renderer2D renderer, Unit unit, Vec2 screen, float radius)
    {
        if (unit.CarriedResource is not { } resource)
            return;

        var size = MathF.Max(3f, radius * 0.5f);
        renderer.FillRect(screen.X + radius * 0.5f, screen.Y - radius - size * 0.5f, size, size, HudColors.Resource(resource));
    }

    private static void DrawHealthBar(Renderer2D renderer, Unit unit, Vec2 screen, float radius)
    {
        var width = radius * 2.2f;
        var height = MathF.Max(2f, radius * 0.22f);
        var x = screen.X - width / 2f;
        var y = screen.Y - radius - height - 2f;
        var fraction = Math.Clamp(unit.Health / unit.Stats.MaxHealth, 0f, 1f);

        renderer.FillRect(x, y, width, height, Palette.Rgba(0, 0, 0, 190));
        renderer.FillRect(x, y, width * fraction, height, HudColors.HealthBar(fraction));
    }
}
