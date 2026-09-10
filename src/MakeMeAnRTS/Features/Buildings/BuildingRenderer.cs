using SDL3;
using MakeMeAnRTS.Core;
using MakeMeAnRTS.Engine;
using MakeMeAnRTS.Features.Camera;
using MakeMeAnRTS.Features.Hud;
using MakeMeAnRTS.Features.Match;
using MakeMeAnRTS.Features.Vision;
using MakeMeAnRTS.Features.World;

namespace MakeMeAnRTS.Features.Buildings;

/// <summary>
/// Draws buildings as team-coloured blocks with a letter, plus progress for anything in flight.
/// </summary>
/// <remarks>
/// Buildings are drawn on explored ground even when nobody is watching, because a base does not walk
/// away. Units are not, which is the difference between remembering where something is and seeing it.
/// </remarks>
public sealed class BuildingRenderer
{
    private readonly MatchState _state;

    public BuildingRenderer(MatchState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        _state = state;
    }

    public void Draw(Renderer2D renderer, Camera2D camera, PlayerVision viewer, int viewerIndex, IReadOnlySet<int> selectedBuildingIds)
    {
        ArgumentNullException.ThrowIfNull(viewer);
        ArgumentNullException.ThrowIfNull(selectedBuildingIds);

        foreach (var building in _state.Buildings)
        {
            if (building.OwnerIndex != viewerIndex && !viewer.IsExplored(building.Center.ToTile()))
                continue;

            var topLeft = camera.WorldToScreen(new Vec2(building.Origin.X, building.Origin.Y));
            var size = building.Stats.Size * camera.Zoom;

            if (!camera.ViewportContains(new Vec2(topLeft.X + size / 2f, topLeft.Y + size / 2f)))
                continue;

            var color = _state.PlayerAt(building.OwnerIndex).Color;

            // A foundation is drawn as an outline on bare ground, so it reads as "not finished yet".
            if (building.IsComplete)
            {
                renderer.FillRect(topLeft.X, topLeft.Y, size, size, Palette.Scale(color, 0.55f));
                renderer.FillRect(topLeft.X + size * 0.14f, topLeft.Y + size * 0.14f, size * 0.72f, size * 0.72f, color);
            }
            else
            {
                renderer.FillRect(topLeft.X, topLeft.Y, size, size, Palette.Rgba(60, 56, 48, 200));
                renderer.FillRect(topLeft.X, topLeft.Y + size * (1f - building.BuildFraction), size, size * building.BuildFraction, Palette.Scale(color, 0.5f));
            }

            renderer.DrawRectThick(topLeft.X, topLeft.Y, size, size, Palette.Scale(color, 0.35f), MathF.Max(1f, camera.Zoom * 0.06f));

            if (selectedBuildingIds.Contains(building.Id))
                renderer.DrawRectThick(topLeft.X - 2f, topLeft.Y - 2f, size + 4f, size + 4f, Palette.White, 2f);

            DrawLabel(renderer, camera, building, topLeft, size);

            if (building.Health < building.Stats.MaxHealth)
                DrawHealthBar(renderer, building, topLeft, size);

            if (building.IsComplete && building.PeekTraining() is not null)
                DrawTrainingBar(renderer, building, topLeft, size);
        }
    }

    private void DrawLabel(Renderer2D renderer, Camera2D camera, Building building, Vec2 topLeft, float size)
    {
        if (camera.Zoom < 10f)
            return;

        var scale = camera.Zoom >= 18f ? 2 : 1;
        var glyph = LabelFor(building);

        renderer.DrawTextCentered(
            glyph,
            topLeft.X + size / 2f,
            topLeft.Y + size / 2f - BitmapFont.GlyphHeight * scale / 2f,
            Palette.Rgb(16, 18, 24),
            scale);
    }

    /// <summary>
    /// A mine shows its mineral's initial rather than "M", and goes dim once it is worked out.
    /// </summary>
    private string LabelFor(Building building)
    {
        if (building.Kind != BuildingKind.Mine || building.MinedMineral is not { } mineral)
            return building.Stats.BuildHotkey.ToString();

        return _state.IsMineExhausted(building) ? "-" : mineral.DisplayName()[..1];
    }

    private static void DrawHealthBar(Renderer2D renderer, Building building, Vec2 topLeft, float size)
    {
        var height = MathF.Max(2f, size * 0.08f);
        var fraction = Math.Clamp(building.Health / building.Stats.MaxHealth, 0f, 1f);

        renderer.FillRect(topLeft.X, topLeft.Y - height - 2f, size, height, Palette.Rgba(0, 0, 0, 190));
        renderer.FillRect(topLeft.X, topLeft.Y - height - 2f, size * fraction, height, HudColors.HealthBar(fraction));
    }

    private static void DrawTrainingBar(Renderer2D renderer, Building building, Vec2 topLeft, float size)
    {
        if (building.PeekTraining() is not { } kind)
            return;

        var seconds = Units.UnitCatalog.For(kind).TrainSeconds;
        var fraction = seconds <= 0f ? 1f : Math.Clamp(building.TrainingElapsed / seconds, 0f, 1f);
        var height = MathF.Max(2f, size * 0.08f);
        var y = topLeft.Y + size + 2f;

        renderer.FillRect(topLeft.X, y, size, height, Palette.Rgba(0, 0, 0, 190));
        renderer.FillRect(topLeft.X, y, size * fraction, height, HudColors.Accent);
    }
}
