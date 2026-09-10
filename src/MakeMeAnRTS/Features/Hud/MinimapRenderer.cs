using SDL3;
using MakeMeAnRTS.Core;
using MakeMeAnRTS.Engine;
using MakeMeAnRTS.Features.Camera;
using MakeMeAnRTS.Features.Match;
using MakeMeAnRTS.Features.World;

namespace MakeMeAnRTS.Features.Hud;

/// <summary>
/// Draws the minimap by keeping a map-sized texture and blitting it once.
/// </summary>
/// <remarks>
/// The obvious version - a filled rectangle per tile for terrain, another per tile for fog, and one per
/// entity - costs a draw call per tile per layer. On a 176x176 map that measured at 93 ms a frame, or
/// eleven frames a second, with the simulation itself taking 0.17 ms. Writing pixels into one texture
/// and blitting it makes the whole minimap a single draw call.
///
/// The texture is rebuilt a few times a second rather than every frame. A minimap that lags a fifth of
/// a second behind is indistinguishable from one that does not, and it takes the cost off the frame.
/// </remarks>
public sealed class MinimapRenderer : IDisposable
{
    /// <summary>Rebuilds per second. Fast enough to feel live, slow enough to be free.</summary>
    private const float RefreshesPerSecond = 6f;

    private readonly MatchState _state;
    private readonly int _playerIndex;
    private readonly IntPtr _texture;
    private readonly byte[] _pixels;
    private readonly int _width;
    private readonly int _height;

    private float _secondsUntilRefresh;
    private bool _disposed;

    public MinimapRenderer(IntPtr renderer, MatchState state, int playerIndex)
    {
        ArgumentNullException.ThrowIfNull(state);

        _state = state;
        _playerIndex = playerIndex;
        _width = state.Map.Width;
        _height = state.Map.Height;
        _pixels = new byte[_width * _height * 4];

        _texture = SDL.CreateTexture(renderer, SDL.PixelFormat.ABGR8888, SDL.TextureAccess.Streaming, _width, _height);
        if (_texture == IntPtr.Zero)
            throw new PlatformException($"Minimap SDL_CreateTexture failed: {SDL.GetError()}");

        SDL.SetTextureScaleMode(_texture, SDL.ScaleMode.Nearest);

        Refresh();
    }

    public void Draw(Renderer2D renderer, Camera2D camera, SDL.Rect bounds, float deltaSeconds)
    {
        _secondsUntilRefresh -= deltaSeconds;

        if (_secondsUntilRefresh <= 0f)
        {
            _secondsUntilRefresh = 1f / RefreshesPerSecond;
            Refresh();

            SDL.UpdateTexture(_texture, IntPtr.Zero, _pixels, _width * 4);
        }

        var destination = new SDL.FRect { X = bounds.X, Y = bounds.Y, W = bounds.W, H = bounds.H };
        SDL.RenderTexture(renderer.Handle, _texture, IntPtr.Zero, in destination);

        DrawCameraBox(renderer, camera, bounds);
        renderer.DrawRect(bounds.X, bounds.Y, bounds.W, bounds.H, HudColors.PanelBorder);
    }

    private void Refresh()
    {
        var vision = _state.PlayerAt(_playerIndex).Vision;

        for (var y = 0; y < _height; y++)
        {
            for (var x = 0; x < _width; x++)
            {
                var pos = new GridPos(x, y);

                if (!vision.IsExplored(pos))
                {
                    WritePixel(x, y, UnexploredColor);
                    continue;
                }

                var tile = _state.Map.TileAt(pos);
                var color = tile.HasTrees ? WorldColors.TreeCanopy : WorldColors.Shaded(tile.Terrain, tile.Height, x, y);

                // Ground that has been seen but is not currently watched is dimmed, matching the world view.
                WritePixel(x, y, vision.IsVisible(pos) ? color : Palette.Scale(color, 0.62f));
            }
        }

        StampEntities(vision);
    }

    private void StampEntities(Vision.PlayerVision vision)
    {
        foreach (var building in _state.Buildings)
        {
            if (building.OwnerIndex != _playerIndex && !vision.IsExplored(building.Center.ToTile()))
                continue;

            var color = _state.PlayerAt(building.OwnerIndex).Color;

            foreach (var tile in building.FootprintTiles())
                WritePixel(tile.X, tile.Y, color);
        }

        foreach (var unit in _state.Units)
        {
            if (unit.OwnerIndex != _playerIndex && !vision.IsVisible(unit.Tile))
                continue;

            // Brightened, because a single pixel in the team colour disappears against similar terrain.
            WritePixel(unit.Tile.X, unit.Tile.Y, Palette.Scale(_state.PlayerAt(unit.OwnerIndex).Color, 1.35f));
        }
    }

    private void DrawCameraBox(Renderer2D renderer, Camera2D camera, SDL.Rect bounds)
    {
        var scaleX = bounds.W / (float)_width;
        var scaleY = bounds.H / (float)_height;
        var (min, max) = camera.VisibleTileRange();

        renderer.DrawRect(
            bounds.X + min.X * scaleX,
            bounds.Y + min.Y * scaleY,
            (max.X - min.X) * scaleX,
            (max.Y - min.Y) * scaleY,
            Palette.Rgba(255, 255, 255, 190));
    }

    private void WritePixel(int x, int y, SDL.Color color)
    {
        if (x < 0 || y < 0 || x >= _width || y >= _height)
            return;

        var offset = (y * _width + x) * 4;

        _pixels[offset + 0] = color.R;
        _pixels[offset + 1] = color.G;
        _pixels[offset + 2] = color.B;
        _pixels[offset + 3] = 255;
    }

    private static readonly SDL.Color UnexploredColor = Palette.Rgb(10, 11, 15);

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        SDL.DestroyTexture(_texture);
    }
}
