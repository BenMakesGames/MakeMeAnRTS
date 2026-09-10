using SDL3;
using MakeMeAnRTS.Core;
using MakeMeAnRTS.Engine;
using MakeMeAnRTS.Features.Buildings;
using MakeMeAnRTS.Features.Camera;
using MakeMeAnRTS.Features.Match;
using MakeMeAnRTS.Features.PlayerControl;
using MakeMeAnRTS.Features.Units;
using MakeMeAnRTS.Features.World;

namespace MakeMeAnRTS.Features.Hud;

/// <summary>
/// Draws the interface: stockpiles, minimap, what is selected, and what can be done with it.
/// </summary>
/// <remarks>
/// The command panel is context-sensitive and always lists its own hotkeys, because a game with this
/// many verbs and no tutorial has to teach itself. If a key does something, it is written on screen
/// next to the thing it does.
/// </remarks>
public sealed class HudRenderer
{
    /// <summary>How long a message stays on screen before fading out.</summary>
    private const float MessageSeconds = 4f;

    private readonly MatchState _state;
    private readonly WorldRenderer _world;
    private readonly int _playerIndex;

    public HudRenderer(MatchState state, WorldRenderer world, int playerIndex)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(world);

        _state = state;
        _world = world;
        _playerIndex = playerIndex;
    }

    private Player Player => _state.PlayerAt(_playerIndex);

    public void Draw(Renderer2D renderer, Camera2D camera, PlayerController controller, HudLayout layout)
    {
        ArgumentNullException.ThrowIfNull(controller);

        DrawTopBar(renderer, layout);
        DrawBottomBar(renderer, layout);
        DrawMinimap(renderer, camera, layout);
        DrawSelectionPanel(renderer, controller, layout);
        DrawCommandPanel(renderer, controller, layout);
        DrawMessage(renderer, controller, layout);

        if (_state.IsOver)
            DrawOutcomeBanner(renderer, layout);
    }

    private void DrawTopBar(Renderer2D renderer, HudLayout layout)
    {
        renderer.FillRect(layout.TopBar.X, layout.TopBar.Y, layout.TopBar.W, layout.TopBar.H, HudColors.PanelBackground);
        renderer.FillRect(layout.TopBar.X, layout.TopBar.Y + layout.TopBar.H - 1, layout.TopBar.W, 1, HudColors.PanelBorder);

        var x = (float)HudLayout.Padding;

        foreach (var resource in Resources.All)
        {
            renderer.FillRect(x, layout.TopBar.Y + 8f, 9f, 9f, HudColors.Resource(resource));
            x += 13f;

            // Named, not just colour-coded: a swatch alone makes the player learn a legend for no reason.
            var label = $"{resource.DisplayName()} {Player.Resources[resource]:N0}";
            renderer.DrawText(label, x, layout.TopBar.Y + 9f, HudColors.Text);
            x += renderer.Font.MeasureWidth(label) + 16f;
        }

        var units = _state.UnitsOf(_playerIndex).Count();
        var workers = _state.UnitsOf(_playerIndex).Count(unit => unit.Stats.CanWork);
        var idle = _state.UnitsOf(_playerIndex).Count(unit => unit.Stats.CanWork && unit.Order is UnitOrder.Idle);

        var summary = $"Units {units}   Workers {workers}   Idle workers {idle}";
        renderer.DrawText(summary, x + 10f, layout.TopBar.Y + 9f, idle > 0 ? HudColors.Warning : HudColors.DimText);

        var clock = $"{(int)(_state.ElapsedSeconds / 60f):00}:{(int)(_state.ElapsedSeconds % 60f):00}";
        renderer.DrawText(clock, layout.TopBar.W - renderer.Font.MeasureWidth(clock) - HudLayout.Padding, layout.TopBar.Y + 9f, HudColors.DimText);
    }

    private static void DrawBottomBar(Renderer2D renderer, HudLayout layout)
    {
        renderer.FillRect(layout.BottomBar.X, layout.BottomBar.Y, layout.BottomBar.W, layout.BottomBar.H, HudColors.PanelBackground);
        renderer.FillRect(layout.BottomBar.X, layout.BottomBar.Y, layout.BottomBar.W, 1, HudColors.PanelBorder);
    }

    private void DrawMinimap(Renderer2D renderer, Camera2D camera, HudLayout layout)
    {
        var bounds = layout.Minimap;

        renderer.SetClip(bounds);
        _world.DrawMinimap(renderer, bounds);

        var scaleX = bounds.W / (float)_state.Map.Width;
        var scaleY = bounds.H / (float)_state.Map.Height;

        // Unexplored ground is blanked out, so the minimap shows what this player knows, not the truth.
        for (var y = 0; y < _state.Map.Height; y++)
        {
            for (var x = 0; x < _state.Map.Width; x++)
            {
                if (Player.Vision.IsExplored(new GridPos(x, y)))
                    continue;

                renderer.FillRect(bounds.X + x * scaleX, bounds.Y + y * scaleY, MathF.Ceiling(scaleX), MathF.Ceiling(scaleY), Palette.Rgb(10, 11, 15));
            }
        }

        foreach (var building in _state.Buildings)
        {
            if (building.OwnerIndex != _playerIndex && !Player.Vision.IsExplored(building.Center.ToTile()))
                continue;

            var color = _state.PlayerAt(building.OwnerIndex).Color;
            var size = MathF.Max(3f, building.Stats.Size * scaleX);

            renderer.FillRect(bounds.X + building.Origin.X * scaleX, bounds.Y + building.Origin.Y * scaleY, size, size, color);
        }

        foreach (var unit in _state.Units)
        {
            if (unit.OwnerIndex != _playerIndex && !Player.Vision.IsVisible(unit.Tile))
                continue;

            var color = _state.PlayerAt(unit.OwnerIndex).Color;
            renderer.FillRect(bounds.X + unit.Position.X * scaleX, bounds.Y + unit.Position.Y * scaleY, 2f, 2f, color);
        }

        // The camera box, so the minimap says where you are as well as what is out there.
        var (min, max) = camera.VisibleTileRange();
        renderer.DrawRect(
            bounds.X + min.X * scaleX,
            bounds.Y + min.Y * scaleY,
            (max.X - min.X) * scaleX,
            (max.Y - min.Y) * scaleY,
            Palette.Rgba(255, 255, 255, 190));

        renderer.SetClip(null);
        renderer.DrawRect(bounds.X, bounds.Y, bounds.W, bounds.H, HudColors.PanelBorder);
    }

    private void DrawSelectionPanel(Renderer2D renderer, PlayerController controller, HudLayout layout)
    {
        var bounds = layout.SelectionPanel;
        renderer.DrawRect(bounds.X, bounds.Y, bounds.W, bounds.H, HudColors.PanelBorder);

        var x = bounds.X + 8f;
        var y = bounds.Y + 7f;

        if (controller.Selection.ResolveBuilding(_state) is { } building)
        {
            DrawBuildingDetails(renderer, building, x, y);
            return;
        }

        var units = controller.Selection.ResolveUnits(_state);
        if (units.Count == 0)
        {
            renderer.DrawText("Nothing selected", x, y, HudColors.DimText);
            renderer.DrawText("Drag a box or click a unit. Right click gives orders.", x, y + 14f, HudColors.DimText);
            return;
        }

        DrawUnitDetails(renderer, units, x, y, bounds);
    }

    private void DrawBuildingDetails(Renderer2D renderer, Building building, float x, float y)
    {
        var owner = _state.PlayerAt(building.OwnerIndex);

        renderer.DrawText(building.Stats.DisplayName, x, y, owner.Color, 2);
        y += 18f;

        if (!building.IsComplete)
        {
            renderer.DrawText($"Under construction {building.BuildFraction * 100f:0}%", x, y, HudColors.Warning);
            return;
        }

        renderer.DrawText($"Health {building.Health:0} / {building.Stats.MaxHealth:0}", x, y, HudColors.DimText);
        y += 12f;

        if (building.MinedMineral is { } mineral)
        {
            var remaining = building.FootprintTiles().Sum(tile => (int)_state.Map.MineralAt(mineral, tile));
            var exhausted = _state.IsMineExhausted(building);

            renderer.DrawText(
                exhausted ? $"{mineral.DisplayName()} seam worked out" : $"{mineral.DisplayName()} remaining {remaining}",
                x,
                y,
                exhausted ? HudColors.Warning : WorldColors.Mineral(mineral));

            y += 12f;
            renderer.DrawText($"Workers {building.AssignedWorkerIds.Count} / {building.Stats.MaxWorkers}   (right click with citizens to assign)", x, y, HudColors.DimText);
            return;
        }

        if (building.Stats.IsDropOff)
        {
            renderer.DrawText($"Accepts {string.Join(", ", building.Stats.AcceptsDeliveries.Select(resource => resource.DisplayName()))}", x, y, HudColors.DimText);
            y += 12f;
        }

        if (building.TrainingQueue.Count > 0)
            renderer.DrawText($"Training {building.TrainingQueue.Count} queued   (Backspace cancels)", x, y, HudColors.Accent);
    }

    private void DrawUnitDetails(Renderer2D renderer, List<Unit> units, float x, float y, SDL.Rect bounds)
    {
        var byKind = units.GroupBy(unit => unit.Kind).OrderBy(group => group.Key).ToList();
        var heading = units.Count == 1 ? units[0].Stats.DisplayName : $"{units.Count} units selected";

        renderer.DrawText(heading, x, y, _state.PlayerAt(units[0].OwnerIndex).Color, 2);
        y += 18f;

        if (units.Count == 1)
        {
            var unit = units[0];

            renderer.DrawText($"Health {unit.Health:0} / {unit.Stats.MaxHealth:0}", x, y, HudColors.DimText);
            y += 12f;
            renderer.DrawText($"Doing: {DescribeOrder(unit)}", x, y, HudColors.Text);
            y += 12f;

            if (unit.CarriedAmount > 0 && unit.CarriedResource is { } carried)
                renderer.DrawText($"Carrying {unit.CarriedAmount} {carried.DisplayName()}", x, y, HudColors.Resource(carried));

            return;
        }

        foreach (var group in byKind)
        {
            if (y > bounds.Y + bounds.H - 14f)
                break;

            var idle = group.Count(unit => unit.Order is UnitOrder.Idle);
            var note = idle > 0 ? $"  ({idle} idle)" : "";

            renderer.DrawText($"{group.Count()}x {UnitCatalog.For(group.Key).DisplayName}{note}", x, y, idle > 0 ? HudColors.Warning : HudColors.Text);
            y += 12f;
        }
    }

    /// <summary>Plain English for what a unit is up to, for the selection panel.</summary>
    private string DescribeOrder(Unit unit) => unit.Order switch
    {
        UnitOrder.Idle => "nothing",
        UnitOrder.Move move => $"walking to {move.Destination}",
        UnitOrder.GatherWood gather => $"cutting wood at {gather.Tree}",
        UnitOrder.WorkMine mine => $"working the {DescribeBuilding(mine.MineId)}",
        UnitOrder.BuildStructure build => $"building the {DescribeBuilding(build.BuildingId)}",
        UnitOrder.Survey survey => $"surveying {survey.Target}",
        UnitOrder.AttackUnit => "attacking",
        UnitOrder.AttackBuilding attack => $"attacking the {DescribeBuilding(attack.TargetBuildingId)}",
        _ => throw new ArgumentOutOfRangeException(nameof(unit), unit.Order, "Unhandled unit order."),
    };

    private string DescribeBuilding(int id) => _state.FindBuilding(id)?.Stats.DisplayName.ToLowerInvariant() ?? "building";

    private void DrawCommandPanel(Renderer2D renderer, PlayerController controller, HudLayout layout)
    {
        var bounds = layout.CommandPanel;
        renderer.DrawRect(bounds.X, bounds.Y, bounds.W, bounds.H, HudColors.PanelBorder);

        var x = bounds.X + 8f;
        var y = bounds.Y + 7f;

        if (controller.Placement is { } placement)
        {
            var stats = BuildingCatalog.For(placement.Kind);

            renderer.DrawText($"Placing {stats.DisplayName}", x, y, HudColors.Accent);
            y += 13f;
            renderer.DrawText(placement.Problem ?? "Click to place. Right click cancels.", x, y, placement.IsValid ? HudColors.Good : HudColors.Warning);
            return;
        }

        if (controller.Selection.ResolveBuilding(_state) is { } building && building.OwnerIndex == _playerIndex && building.IsComplete)
        {
            var trainable = UnitCatalog.TrainedAt(building.Kind).ToList();

            if (trainable.Count > 0)
            {
                renderer.DrawText("Train", x, y, HudColors.Text);
                y += 13f;

                for (var slot = 0; slot < trainable.Count; slot++)
                {
                    var stats = UnitCatalog.For(trainable[slot]);
                    var affordable = Player.Resources.CanAfford(stats.TrainCost);

                    renderer.DrawText($"[{slot + 1}] {stats.DisplayName}  {stats.TrainCost}", x, y, affordable ? HudColors.Text : HudColors.DimText);
                    y += 12f;
                }

                return;
            }
        }

        var builders = controller.Selection.ResolveUnits(_state).Where(unit => unit.Stats.CanBuild).ToList();

        if (builders.Count > 0)
        {
            renderer.DrawText("Build", x, y, HudColors.Text);
            y += 13f;

            foreach (var kind in BuildingCatalog.Buildable)
            {
                if (y > bounds.Y + bounds.H - 12f)
                    break;

                var stats = BuildingCatalog.For(kind);
                var affordable = Player.Resources.CanAfford(stats.BuildCost);

                renderer.DrawText($"[{stats.BuildHotkey}] {stats.DisplayName}  {stats.BuildCost}", x, y, affordable ? HudColors.Text : HudColors.DimText);
                y += 12f;
            }

            return;
        }

        renderer.DrawText("Keys", x, y, HudColors.Text);
        y += 13f;

        foreach (var line in new[]
        {
            "WASD / arrows  pan     wheel  zoom",
            "Tab  mineral heat map  Space  home base",
            ".  next idle worker    F1  debug overlay",
        })
        {
            renderer.DrawText(line, x, y, HudColors.DimText);
            y += 12f;
        }
    }

    private void DrawMessage(Renderer2D renderer, PlayerController controller, HudLayout layout)
    {
        if (controller.Message is not { } message || controller.MessageAgeSeconds > MessageSeconds)
            return;

        // Fades over its last second, so it goes away without the player having to notice it going.
        var fade = Math.Clamp(MessageSeconds - controller.MessageAgeSeconds, 0f, 1f);
        var alpha = MathHelpers.ToByte(fade * 255f);

        var width = renderer.Font.MeasureWidth(message, 2) + 20f;
        var x = layout.Viewport.X + (layout.Viewport.W - width) / 2f;
        var y = (float)layout.Viewport.Y + 14f;

        renderer.FillRect(x, y, width, 22f, Palette.Rgba(18, 20, 26, MathHelpers.ToByte(fade * 220f)));
        renderer.DrawRect(x, y, width, 22f, HudColors.PanelBorder.WithAlpha(alpha));
        renderer.DrawText(message, x + 10f, y + 6f, HudColors.Text.WithAlpha(alpha), 2);
    }

    private void DrawOutcomeBanner(Renderer2D renderer, HudLayout layout)
    {
        var won = _state.WinnerIndex == _playerIndex;
        var text = won ? "VICTORY" : "DEFEAT";
        var color = won ? HudColors.Good : Palette.Rgb(226, 88, 78);

        var centerX = layout.Viewport.X + layout.Viewport.W / 2f;
        var y = layout.Viewport.Y + layout.Viewport.H / 2f - 30f;

        renderer.FillRect(layout.Viewport.X, y - 12f, layout.Viewport.W, 64f, Palette.Rgba(10, 12, 16, 200));
        renderer.DrawTextCentered(text, centerX, y, color, 5);
        renderer.DrawTextCentered("Esc to quit", centerX, y + 40f, HudColors.DimText, 2);
    }
}
