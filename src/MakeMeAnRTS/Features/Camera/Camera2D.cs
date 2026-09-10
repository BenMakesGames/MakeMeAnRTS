using SDL3;
using MakeMeAnRTS.Core;
using MakeMeAnRTS.Engine;

namespace MakeMeAnRTS.Features.Camera;

/// <summary>
/// Maps between world tiles and screen pixels, and handles panning and zooming.
/// </summary>
/// <remarks>
/// The camera is always clamped so the map fills the viewport where it can. Letting the view drift off
/// into blank space is disorienting and makes minimap clicks land somewhere other than where the player
/// aimed.
/// </remarks>
public sealed class Camera2D
{
    public const float MinZoom = 6f;
    public const float MaxZoom = 48f;

    private const float KeyboardPanTilesPerSecond = 22f;
    private const float EdgePanTilesPerSecond = 18f;

    /// <summary>How close to the window edge the pointer must be to start an edge pan, in pixels.</summary>
    private const int EdgePanMargin = 12;

    private readonly int _mapWidth;
    private readonly int _mapHeight;

    /// <summary>World position at the centre of the viewport, in tiles.</summary>
    public Vec2 Center { get; private set; }

    /// <summary>Screen pixels per world tile.</summary>
    public float Zoom { get; private set; } = 20f;

    /// <summary>The screen rectangle the world is drawn into, excluding HUD panels.</summary>
    public SDL.Rect Viewport { get; set; }

    /// <summary>Whether edge-of-screen panning is active. Off by default so it can't fight a windowed player.</summary>
    public bool EdgePanEnabled { get; set; }

    public Camera2D(int mapWidth, int mapHeight, SDL.Rect viewport)
    {
        _mapWidth = mapWidth;
        _mapHeight = mapHeight;
        Viewport = viewport;
        Center = new Vec2(mapWidth / 2f, mapHeight / 2f);
    }

    /// <summary>Zooms and centres so the whole map is visible, for the minimap and map previews.</summary>
    public void FitToMap()
    {
        var scale = MathF.Min(Viewport.W / (float)_mapWidth, Viewport.H / (float)_mapHeight);

        Zoom = Math.Clamp(scale, MinZoom * 0.05f, MaxZoom);
        Center = new Vec2(_mapWidth / 2f, _mapHeight / 2f);
    }

    public Vec2 WorldToScreen(Vec2 world) => new(
        Viewport.X + Viewport.W / 2f + (world.X - Center.X) * Zoom,
        Viewport.Y + Viewport.H / 2f + (world.Y - Center.Y) * Zoom);

    public Vec2 ScreenToWorld(Vec2 screen) => new(
        Center.X + (screen.X - Viewport.X - Viewport.W / 2f) / Zoom,
        Center.Y + (screen.Y - Viewport.Y - Viewport.H / 2f) / Zoom);

    public GridPos ScreenToTile(Vec2 screen) => ScreenToWorld(screen).ToTile();

    public bool ViewportContains(Vec2 screen) =>
        screen.X >= Viewport.X && screen.Y >= Viewport.Y &&
        screen.X < Viewport.X + Viewport.W && screen.Y < Viewport.Y + Viewport.H;

    /// <summary>The inclusive tile range currently visible, padded by one tile so partial tiles still draw.</summary>
    public (GridPos Min, GridPos Max) VisibleTileRange()
    {
        var topLeft = ScreenToWorld(new Vec2(Viewport.X, Viewport.Y));
        var bottomRight = ScreenToWorld(new Vec2(Viewport.X + Viewport.W, Viewport.Y + Viewport.H));

        var min = new GridPos(
            Math.Max(0, (int)MathF.Floor(topLeft.X) - 1),
            Math.Max(0, (int)MathF.Floor(topLeft.Y) - 1));
        var max = new GridPos(
            Math.Min(_mapWidth - 1, (int)MathF.Ceiling(bottomRight.X) + 1),
            Math.Min(_mapHeight - 1, (int)MathF.Ceiling(bottomRight.Y) + 1));

        return (min, max);
    }

    /// <summary>Sets the zoom directly, for the minimap, map previews and jumping to a fixed scale.</summary>
    public void SetZoom(float pixelsPerTile)
    {
        Zoom = Math.Clamp(pixelsPerTile, MinZoom * 0.05f, MaxZoom);
        ClampToMap();
    }

    public void CenterOn(Vec2 world)
    {
        Center = world;
        ClampToMap();
    }

    public void Update(InputState input, float deltaSeconds)
    {
        ArgumentNullException.ThrowIfNull(input);

        ApplyZoom(input);
        ApplyPan(input, deltaSeconds);
        ClampToMap();
    }

    private void ApplyZoom(InputState input)
    {
        if (MathF.Abs(input.WheelDelta) < 0.01f)
            return;

        var anchorWorld = ScreenToWorld(input.MousePosition);
        var previousZoom = Zoom;

        // Multiplicative so each notch feels the same at every zoom level.
        Zoom = Math.Clamp(Zoom * MathF.Pow(1.15f, input.WheelDelta), MinZoom, MaxZoom);

        if (MathF.Abs(Zoom - previousZoom) < 0.001f)
            return;

        // Keep the tile under the cursor pinned, so zooming feels like it targets what you point at.
        if (ViewportContains(input.MousePosition))
            Center += anchorWorld - ScreenToWorld(input.MousePosition);
    }

    private void ApplyPan(InputState input, float deltaSeconds)
    {
        var direction = Vec2.Zero;

        if (input.IsDown(SDL.Scancode.W) || input.IsDown(SDL.Scancode.Up))
            direction += new Vec2(0f, -1f);
        if (input.IsDown(SDL.Scancode.S) || input.IsDown(SDL.Scancode.Down))
            direction += new Vec2(0f, 1f);
        if (input.IsDown(SDL.Scancode.A) || input.IsDown(SDL.Scancode.Left))
            direction += new Vec2(-1f, 0f);
        if (input.IsDown(SDL.Scancode.D) || input.IsDown(SDL.Scancode.Right))
            direction += new Vec2(1f, 0f);

        var speed = KeyboardPanTilesPerSecond;

        if (direction == Vec2.Zero && EdgePanEnabled && ViewportContains(input.MousePosition))
        {
            direction = EdgePanDirection(input.MousePosition);
            speed = EdgePanTilesPerSecond;
        }

        if (direction == Vec2.Zero)
            return;

        // Panning in tiles per second keeps the felt speed constant as the player zooms in and out.
        Center += direction.Normalized() * (speed * deltaSeconds);
    }

    private Vec2 EdgePanDirection(Vec2 mouse)
    {
        var direction = Vec2.Zero;

        if (mouse.X - Viewport.X < EdgePanMargin)
            direction += new Vec2(-1f, 0f);
        if (Viewport.X + Viewport.W - mouse.X < EdgePanMargin)
            direction += new Vec2(1f, 0f);
        if (mouse.Y - Viewport.Y < EdgePanMargin)
            direction += new Vec2(0f, -1f);
        if (Viewport.Y + Viewport.H - mouse.Y < EdgePanMargin)
            direction += new Vec2(0f, 1f);

        return direction;
    }

    private void ClampToMap()
    {
        var halfWidth = Viewport.W / 2f / Zoom;
        var halfHeight = Viewport.H / 2f / Zoom;

        // When the map is narrower than the view, centre it rather than clamping to a negative range.
        var x = halfWidth * 2f >= _mapWidth ? _mapWidth / 2f : Math.Clamp(Center.X, halfWidth, _mapWidth - halfWidth);
        var y = halfHeight * 2f >= _mapHeight ? _mapHeight / 2f : Math.Clamp(Center.Y, halfHeight, _mapHeight - halfHeight);

        Center = new Vec2(x, y);
    }
}
