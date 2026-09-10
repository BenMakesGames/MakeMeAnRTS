using SDL3;
using MakeMeAnRTS.Core;
using MakeMeAnRTS.Engine;
using MakeMeAnRTS.Features.Camera;

namespace MakeMeAnRTS.Features.World;

/// <summary>Draws the terrain, the woods, and the mineral heat map overlay.</summary>
/// <remarks>
/// Only the visible tile range is drawn, so map size costs memory but not frame time. Tiles are plain
/// filled rectangles: at RTS zoom levels a tile is a handful of pixels, and detail beyond a shade of
/// colour is wasted.
/// </remarks>
public sealed class WorldRenderer
{
    private readonly WorldMap _map;

    public WorldRenderer(WorldMap map)
    {
        ArgumentNullException.ThrowIfNull(map);
        _map = map;
    }

    public void DrawTerrain(Renderer2D renderer, Camera2D camera)
    {
        var (min, max) = camera.VisibleTileRange();

        // Rounding out to whole pixels stops hairline seams appearing between tiles at fractional zooms.
        var size = MathF.Ceiling(camera.Zoom);

        for (var y = min.Y; y <= max.Y; y++)
        {
            for (var x = min.X; x <= max.X; x++)
            {
                var pos = new GridPos(x, y);
                var tile = _map.TileAt(pos);
                var screen = camera.WorldToScreen(new Vec2(x, y));

                renderer.FillRect(MathF.Floor(screen.X), MathF.Floor(screen.Y), size, size, WorldColors.Shaded(tile.Terrain, tile.Height, x, y));

                if (tile.HasTrees)
                    DrawTree(renderer, screen, camera.Zoom, tile.Wood);
            }
        }
    }

    /// <summary>
    /// Tints tiles by how much of <paramref name="mineral"/> they hold - the heat map the prospector fills in.
    /// </summary>
    /// <param name="known">
    /// Abundance the viewing player has surveyed. Pass the true map only for debug overlays; passing it
    /// in normal play would hand the player the answers the prospector exists to find.
    /// </param>
    public void DrawMineralHeatMap(Renderer2D renderer, Camera2D camera, MineralKind mineral, Grid2D<byte> known)
    {
        ArgumentNullException.ThrowIfNull(known);

        var (min, max) = camera.VisibleTileRange();
        var size = MathF.Ceiling(camera.Zoom);
        var tint = WorldColors.Mineral(mineral);

        for (var y = min.Y; y <= max.Y; y++)
        {
            for (var x = min.X; x <= max.X; x++)
            {
                var abundance = known[x, y];
                if (abundance == 0)
                    continue;

                var screen = camera.WorldToScreen(new Vec2(x, y));

                // Alpha tracks abundance, so richer ground simply glows brighter.
                var alpha = MathHelpers.ToByte(40 + abundance / 255f * 190f);
                renderer.FillRect(MathF.Floor(screen.X), MathF.Floor(screen.Y), size, size, tint.WithAlpha(alpha));
            }
        }
    }

    /// <summary>Draws a whole-map thumbnail into <paramref name="bounds"/>, for the minimap.</summary>
    public void DrawMinimap(Renderer2D renderer, SDL.Rect bounds)
    {
        var scaleX = bounds.W / (float)_map.Width;
        var scaleY = bounds.H / (float)_map.Height;
        var pixel = MathF.Ceiling(MathF.Max(scaleX, scaleY));

        for (var y = 0; y < _map.Height; y++)
        {
            for (var x = 0; x < _map.Width; x++)
            {
                var tile = _map.TileAt(new GridPos(x, y));
                var color = tile.HasTrees ? WorldColors.TreeCanopy : WorldColors.Shaded(tile.Terrain, tile.Height, x, y);

                renderer.FillRect(bounds.X + x * scaleX, bounds.Y + y * scaleY, pixel, pixel, color);
            }
        }
    }

    private static void DrawTree(Renderer2D renderer, Vec2 screen, float zoom, byte wood)
    {
        // Below a few pixels a tree is a smudge; a flat canopy colour reads better than a shape.
        if (zoom < 5f)
        {
            renderer.FillRect(MathF.Floor(screen.X), MathF.Floor(screen.Y), MathF.Ceiling(zoom), MathF.Ceiling(zoom), WorldColors.TreeCanopy);
            return;
        }

        // Canopy shrinks as the tile is logged out, which is the only cue that a wood is running dry.
        var fullness = 0.55f + wood / (float)ForestFullness * 0.45f;
        var radius = zoom * 0.36f * Math.Clamp(fullness, 0.4f, 1f);
        var centerX = screen.X + zoom / 2f;
        var centerY = screen.Y + zoom / 2f;

        renderer.FillRect(centerX - zoom * 0.06f, centerY, zoom * 0.12f, zoom * 0.4f, WorldColors.TreeTrunk);
        renderer.FillCircle(centerX, centerY - zoom * 0.08f, radius, WorldColors.TreeCanopy);
    }

    /// <summary>Wood in an untouched tile, used only to scale how full a canopy is drawn.</summary>
    private const int ForestFullness = 60;
}
