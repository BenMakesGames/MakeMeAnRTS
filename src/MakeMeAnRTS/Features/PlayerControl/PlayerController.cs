using SDL3;
using MakeMeAnRTS.Core;
using MakeMeAnRTS.Engine;
using MakeMeAnRTS.Features.Buildings;
using MakeMeAnRTS.Features.Camera;
using MakeMeAnRTS.Features.Match;
using MakeMeAnRTS.Features.Units;
using MakeMeAnRTS.Features.World;

namespace MakeMeAnRTS.Features.PlayerControl;

/// <summary>
/// Turns keyboard and mouse into selections and orders.
/// </summary>
/// <remarks>
/// Deliberately conventional: left click selects, drag box-selects, right click does the obvious thing
/// to whatever was clicked, and a building's letter starts placing it. A player who has played an RTS
/// before should not have to be taught any of this, so the surprises are kept to the things this game
/// actually does differently - surveying, and assigning workers to mines.
/// </remarks>
public sealed class PlayerController
{
    /// <summary>Drag distance in pixels below which a press-and-release counts as a click, not a box.</summary>
    private const float ClickSlop = 5f;

    private readonly MatchState _state;
    private readonly MatchCommands _commands;
    private readonly Camera2D _camera;
    private readonly int _playerIndex;

    private Vec2? _dragStart;

    public Selection Selection { get; } = new();

    /// <summary>The building being positioned, or null when not placing anything.</summary>
    public PlacementPreview? Placement { get; private set; }

    /// <summary>Which mineral's heat map is drawn over the world, or null for none.</summary>
    public MineralKind? MineralOverlay { get; private set; }

    /// <summary>Whether the debug overlay is on.</summary>
    public bool ShowDebugOverlay { get; private set; }

    /// <summary>The box currently being dragged, in screen pixels, for the renderer to draw.</summary>
    public (Vec2 Start, Vec2 End)? DragBox { get; private set; }

    /// <summary>The last thing the player needs telling, with the time it was raised.</summary>
    public string? Message { get; private set; }

    public float MessageAgeSeconds { get; private set; }

    /// <summary>Whether the current message reports a refusal rather than just confirming something.</summary>
    public bool MessageIsProblem { get; private set; }

    /// <summary>Bumped whenever a new message is raised, so anything watching can tell them apart.</summary>
    public int MessageRevision { get; private set; }

    public PlayerController(MatchState state, Camera2D camera, int playerIndex)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(camera);

        _state = state;
        _camera = camera;
        _playerIndex = playerIndex;
        _commands = new MatchCommands(state);
    }

    private Player Player => _state.PlayerAt(_playerIndex);

    public void Update(InputState input, float deltaSeconds, SDL.Rect minimapBounds)
    {
        ArgumentNullException.ThrowIfNull(input);

        MessageAgeSeconds += deltaSeconds;
        Selection.Prune(_state);

        HandleHotkeys(input);

        if (HandleMinimapClick(input, minimapBounds))
            return;

        if (!_camera.ViewportContains(input.MousePosition))
            return;

        var hovered = _camera.ScreenToTile(input.MousePosition);
        Placement?.MoveTo(hovered, _state, Player);

        HandleLeftMouse(input, hovered);
        HandleRightMouse(input, hovered);
    }

    private void HandleHotkeys(InputState input)
    {
        if (input.WasPressed(SDL.Scancode.Escape))
        {
            // Escape backs out one step at a time: first the placement, then the selection.
            if (Placement is not null)
                Placement = null;
            else
                Selection.Clear();
        }

        if (input.WasPressed(SDL.Scancode.Tab))
            CycleMineralOverlay();

        if (input.WasPressed(SDL.Scancode.F1))
            ShowDebugOverlay = !ShowDebugOverlay;

        if (input.WasPressed(SDL.Scancode.Space))
            CenterOnHome();

        if (input.WasPressed(SDL.Scancode.Period))
            SelectIdleWorker();

        HandleBuildHotkeys(input);
        HandleTrainHotkeys(input);
    }

    /// <summary>A building's letter starts placing it, as long as somebody selected could build it.</summary>
    private void HandleBuildHotkeys(InputState input)
    {
        foreach (var kind in BuildingCatalog.Buildable)
        {
            var stats = BuildingCatalog.For(kind);
            var scancode = ScancodeForLetter(stats.BuildHotkey);

            if (scancode is null || !input.WasPressed(scancode.Value))
                continue;

            if (!Selection.ResolveUnits(_state).Any(unit => unit.Stats.CanBuild))
            {
                Raise("Select a citizen first.");
                return;
            }

            Placement = new PlacementPreview(kind);
            return;
        }
    }

    /// <summary>Number keys train the selected building's units, in menu order.</summary>
    private void HandleTrainHotkeys(InputState input)
    {
        if (Selection.ResolveBuilding(_state) is not { } building || building.OwnerIndex != _playerIndex)
            return;

        var trainable = UnitCatalog.TrainedAt(building.Kind).ToList();

        for (var slot = 0; slot < trainable.Count && slot < 4; slot++)
        {
            if (!input.WasPressed(SDL.Scancode.Alpha1 + slot))
                continue;

            if (!_commands.TryQueueTraining(building, trainable[slot], out var reason))
                Raise(reason ?? "Cannot train that.");
        }

        // Backspace cancels the last one queued, which is the usual undo for a misclick.
        if (input.WasPressed(SDL.Scancode.Backspace))
            _commands.TryCancelLastTraining(building);
    }

    private void HandleLeftMouse(InputState input, GridPos hovered)
    {
        if (input.WasMousePressed(MouseButton.Left))
        {
            if (Placement is not null)
            {
                PlaceBuilding();
                return;
            }

            _dragStart = input.MousePosition;
        }

        if (_dragStart is { } start && input.IsMouseDown(MouseButton.Left))
            DragBox = (start, input.MousePosition);

        if (!input.WasMouseReleased(MouseButton.Left) || _dragStart is not { } pressedAt)
            return;

        DragBox = null;
        _dragStart = null;

        var dragged = Vec2.Distance(pressedAt, input.MousePosition);

        if (dragged <= ClickSlop)
            SelectAt(hovered, input.IsShiftDown);
        else
            SelectInBox(pressedAt, input.MousePosition, input.IsShiftDown);
    }

    private void HandleRightMouse(InputState input, GridPos hovered)
    {
        if (!input.WasMousePressed(MouseButton.Right))
            return;

        // Right click during placement cancels it, which is what the button means everywhere else.
        if (Placement is not null)
        {
            Placement = null;
            return;
        }

        var units = Selection.ResolveUnits(_state).Where(unit => unit.OwnerIndex == _playerIndex).ToList();
        if (units.Count == 0)
            return;

        _commands.OrderInteract(units, hovered);
    }

    private void SelectAt(GridPos tile, bool add)
    {
        var unit = FindUnitAt(tile);
        if (unit is not null)
        {
            Selection.SelectUnits([unit], add);
            return;
        }

        if (_state.BuildingAtTile(tile) is { } building && Player.Vision.IsExplored(tile))
        {
            Selection.SelectBuilding(building);
            return;
        }

        if (!add)
            Selection.Clear();
    }

    private void SelectInBox(Vec2 corner, Vec2 opposite, bool add)
    {
        var minX = MathF.Min(corner.X, opposite.X);
        var maxX = MathF.Max(corner.X, opposite.X);
        var minY = MathF.Min(corner.Y, opposite.Y);
        var maxY = MathF.Max(corner.Y, opposite.Y);

        // Own units only: dragging a box over a battle should select your side, not everyone in it.
        var inBox = _state.UnitsOf(_playerIndex)
            .Where(unit =>
            {
                var screen = _camera.WorldToScreen(unit.Position);
                return screen.X >= minX && screen.X <= maxX && screen.Y >= minY && screen.Y <= maxY;
            })
            .ToList();

        if (inBox.Count > 0)
            Selection.SelectUnits(inBox, add);
        else if (!add)
            Selection.Clear();
    }

    private void PlaceBuilding()
    {
        if (Placement is not { } placement)
            return;

        if (!placement.IsValid)
        {
            Raise(placement.Problem ?? "Cannot build there.");
            return;
        }

        var builders = Selection.ResolveUnits(_state).Where(unit => unit.Stats.CanBuild).ToList();

        if (!_commands.TryPlaceBuilding(_playerIndex, placement.Kind, placement.Origin, builders, out var reason, out _))
        {
            Raise(reason ?? "Cannot build there.");
            return;
        }

        Placement = null;
    }

    /// <summary>Clicking the minimap jumps the camera there, which is what it is for.</summary>
    private bool HandleMinimapClick(InputState input, SDL.Rect bounds)
    {
        var inside =
            input.MousePosition.X >= bounds.X && input.MousePosition.X < bounds.X + bounds.W &&
            input.MousePosition.Y >= bounds.Y && input.MousePosition.Y < bounds.Y + bounds.H;

        if (!inside)
            return false;

        if (input.IsMouseDown(MouseButton.Left) || input.WasMousePressed(MouseButton.Left))
        {
            var fractionX = (input.MousePosition.X - bounds.X) / bounds.W;
            var fractionY = (input.MousePosition.Y - bounds.Y) / bounds.H;

            _camera.CenterOn(new Vec2(fractionX * _state.Map.Width, fractionY * _state.Map.Height));
        }

        return true;
    }

    private void CycleMineralOverlay()
    {
        MineralOverlay = MineralOverlay switch
        {
            null => MineralKind.Stone,
            MineralKind.Stone => MineralKind.Iron,
            MineralKind.Iron => MineralKind.Gold,
            _ => null,
        };

        Raise(
            MineralOverlay is { } mineral ? $"{mineral.DisplayName()} heat map (surveyed ground only)" : "Heat map off",
            isProblem: false);
    }

    private void CenterOnHome()
    {
        var home = _state.BuildingsOf(_playerIndex).FirstOrDefault(building => building.Kind == BuildingKind.HomeBase)
            ?? _state.BuildingsOf(_playerIndex).FirstOrDefault();

        if (home is not null)
            _camera.CenterOn(home.Center);
    }

    /// <summary>Selects and jumps to an idle worker, so wasted citizens are easy to find.</summary>
    private void SelectIdleWorker()
    {
        var idle = _state.UnitsOf(_playerIndex)
            .FirstOrDefault(unit => unit.Stats.CanWork && unit.Order is UnitOrder.Idle);

        if (idle is null)
        {
            Raise("No idle workers.");
            return;
        }

        Selection.SelectUnits([idle], add: false);
        _camera.CenterOn(idle.Position);
    }

    private Unit? FindUnitAt(GridPos tile)
    {
        // Own units win ties, so clicking into a melee picks the one you can actually order.
        return _state.Units
            .Where(unit => unit.IsAlive && unit.Tile == tile)
            .Where(unit => unit.OwnerIndex == _playerIndex || Player.Vision.IsVisible(unit.Tile))
            .MinBy(unit => unit.OwnerIndex == _playerIndex ? 0 : 1);
    }

    /// <param name="isProblem">
    /// False for a message that merely confirms something, so a toggle does not sound like a refusal.
    /// </param>
    public void Raise(string message, bool isProblem = true)
    {
        Message = message;
        MessageAgeSeconds = 0f;
        MessageIsProblem = isProblem;
        MessageRevision++;
    }

    /// <summary>Maps a build hotkey letter to its scancode. Only A-Z are used.</summary>
    private static SDL.Scancode? ScancodeForLetter(char letter)
    {
        var upper = char.ToUpperInvariant(letter);

        return upper is >= 'A' and <= 'Z' ? SDL.Scancode.A + (upper - 'A') : null;
    }
}
