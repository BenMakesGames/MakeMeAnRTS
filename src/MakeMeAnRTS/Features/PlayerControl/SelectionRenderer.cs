using MakeMeAnRTS.Core;
using MakeMeAnRTS.Engine;
using MakeMeAnRTS.Features.Buildings;
using MakeMeAnRTS.Features.Camera;
using MakeMeAnRTS.Features.Hud;
using MakeMeAnRTS.Features.Match;
using MakeMeAnRTS.Features.World;

namespace MakeMeAnRTS.Features.PlayerControl;

/// <summary>Draws the player's own interface marks over the world: the drag box and the building preview.</summary>
public sealed class SelectionRenderer
{
    private readonly MatchState _state;

    public SelectionRenderer(MatchState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        _state = state;
    }

    public void Draw(Renderer2D renderer, Camera2D camera, PlayerController controller, Player player)
    {
        ArgumentNullException.ThrowIfNull(controller);
        ArgumentNullException.ThrowIfNull(player);

        DrawPlacementPreview(renderer, camera, controller, player);
        DrawDragBox(renderer, controller);
    }

    private void DrawPlacementPreview(Renderer2D renderer, Camera2D camera, PlayerController controller, Player player)
    {
        if (controller.Placement is not { } placement)
            return;

        var topLeft = camera.WorldToScreen(new Vec2(placement.Origin.X, placement.Origin.Y));
        var size = placement.Size * camera.Zoom;

        var tint = placement.IsValid ? Palette.Rgba(120, 220, 130, 90) : Palette.Rgba(230, 90, 80, 90);
        var edge = placement.IsValid ? HudColors.Good : Palette.Rgb(230, 90, 80);

        renderer.FillRect(topLeft.X, topLeft.Y, size, size, tint);
        renderer.DrawRectThick(topLeft.X, topLeft.Y, size, size, edge, 2f);

        // A mine shows which mineral this ground would actually yield, since that is the whole decision.
        if (BuildingCatalog.For(placement.Kind).RequiresMineral &&
            new MatchCommands(_state).BestKnownMineral(player, placement.Origin, placement.Size) is { } mineral)
        {
            renderer.DrawTextShadowed(mineral.DisplayName(), topLeft.X, topLeft.Y - 14f, WorldColors.Mineral(mineral));
        }
    }

    private static void DrawDragBox(Renderer2D renderer, PlayerController controller)
    {
        if (controller.DragBox is not { } box)
            return;

        var x = MathF.Min(box.Start.X, box.End.X);
        var y = MathF.Min(box.Start.Y, box.End.Y);
        var width = MathF.Abs(box.End.X - box.Start.X);
        var height = MathF.Abs(box.End.Y - box.Start.Y);

        renderer.FillRect(x, y, width, height, Palette.Rgba(120, 200, 255, 40));
        renderer.DrawRect(x, y, width, height, Palette.Rgba(160, 220, 255, 220));
    }
}
