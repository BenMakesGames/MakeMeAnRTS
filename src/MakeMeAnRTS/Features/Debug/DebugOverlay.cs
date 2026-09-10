using MakeMeAnRTS.Core;
using MakeMeAnRTS.Engine;
using MakeMeAnRTS.Features.Camera;
using MakeMeAnRTS.Features.Hud;
using MakeMeAnRTS.Features.Match;
using MakeMeAnRTS.Features.Units;

namespace MakeMeAnRTS.Features.Debug;

/// <summary>
/// Draws what the simulation is thinking: routes, order targets, and where the opponent's units are.
/// </summary>
/// <remarks>
/// Toggled with F1. Most bugs in a game like this are invisible in a normal frame - a unit standing
/// still looks the same whether it is idle, blocked, or walking a route to somewhere unreachable. Being
/// able to see the route and the target answers that in one glance instead of a debugging session.
/// </remarks>
public sealed class DebugOverlay
{
    private readonly MatchState _state;

    public DebugOverlay(MatchState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        _state = state;
    }

    public void Draw(Renderer2D renderer, Camera2D camera, int viewerIndex)
    {
        foreach (var unit in _state.Units)
        {
            var screen = camera.WorldToScreen(unit.Position);
            if (!camera.ViewportContains(screen))
                continue;

            var isOwn = unit.OwnerIndex == viewerIndex;
            var routeColor = isOwn ? Palette.Rgba(120, 220, 255, 170) : Palette.Rgba(255, 150, 130, 140);

            DrawRoute(renderer, camera, unit, screen, routeColor);
            DrawOrderTarget(renderer, camera, unit, screen);

            // A unit that has been going nowhere for a while is the thing worth spotting.
            if (unit.BlockedSeconds > 0.5f)
                renderer.DrawTextShadowed($"stuck {unit.BlockedSeconds:0.0}s", screen.X + 6f, screen.Y - 6f, HudColors.Warning);
        }

        DrawLegend(renderer, camera);
    }

    private static void DrawRoute(Renderer2D renderer, Camera2D camera, Unit unit, Vec2 from, SDL3.SDL.Color color)
    {
        var previous = from;

        foreach (var step in unit.Path)
        {
            var point = camera.WorldToScreen(step.Center);
            renderer.DrawLine(previous.X, previous.Y, point.X, point.Y, color);
            previous = point;
        }
    }

    private void DrawOrderTarget(Renderer2D renderer, Camera2D camera, Unit unit, Vec2 from)
    {
        var target = OrderTarget(unit);
        if (target is not { } tile)
            return;

        var screen = camera.WorldToScreen(tile.Center);
        var size = MathF.Max(4f, camera.Zoom * 0.3f);

        renderer.DrawRect(screen.X - size / 2f, screen.Y - size / 2f, size, size, Palette.Rgba(255, 230, 120, 200));
        renderer.DrawLine(from.X, from.Y, screen.X, screen.Y, Palette.Rgba(255, 230, 120, 70));
    }

    /// <summary>The tile a unit's order is aimed at, or null for orders with no fixed place.</summary>
    private GridPos? OrderTarget(Unit unit) => unit.Order switch
    {
        UnitOrder.Move move => move.Destination,
        UnitOrder.GatherWood gather => gather.Tree,
        UnitOrder.Survey survey => survey.Target,
        UnitOrder.WorkMine mine => _state.FindBuilding(mine.MineId)?.Origin,
        UnitOrder.BuildStructure build => _state.FindBuilding(build.BuildingId)?.Origin,
        UnitOrder.AttackBuilding attack => _state.FindBuilding(attack.TargetBuildingId)?.Origin,
        UnitOrder.AttackUnit attack => _state.FindUnit(attack.TargetUnitId)?.Tile,
        _ => null,
    };

    private void DrawLegend(Renderer2D renderer, Camera2D camera)
    {
        var lines = new[]
        {
            "F1 debug overlay",
            $"units {_state.Units.Count}  buildings {_state.Buildings.Count}  signs {_state.Signs.Count}",
            $"blue lines: your routes   red: theirs   yellow box: order target",
        };

        var y = camera.Viewport.Y + camera.Viewport.H - lines.Length * 12f - 6f;

        foreach (var line in lines)
        {
            renderer.DrawTextShadowed(line, camera.Viewport.X + 8f, y, HudColors.Text);
            y += 12f;
        }
    }
}
