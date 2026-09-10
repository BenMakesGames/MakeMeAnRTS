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

        for (var y = min.Y; y <= max.Y; y++)
        {
            for (var x = min.X; x <= max.X; x++)
            {
                var pos = new GridPos(x, y);
                var tile = _map.TileAt(pos);
                var (rectX, rectY, width, height) = camera.TileRect(x, y);

                renderer.FillRect(rectX, rectY, width, height, WorldColors.Shaded(tile.Terrain, tile.Height, x, y));

                if (tile.HasTrees)
                    DrawTree(renderer, camera.WorldToScreen(new Vec2(x, y)), camera.Zoom, tile.Wood, x, y);
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
        var tint = WorldColors.Mineral(mineral);

        for (var y = min.Y; y <= max.Y; y++)
        {
            for (var x = min.X; x <= max.X; x++)
            {
                var abundance = known[x, y];
                if (abundance == 0)
                    continue;

                var (rectX, rectY, width, height) = camera.TileRect(x, y);

                // Alpha tracks abundance, so richer ground simply glows brighter.
                var alpha = MathHelpers.ToByte(40 + abundance / 255f * 190f);
                renderer.FillRect(rectX, rectY, width, height, tint.WithAlpha(alpha));
            }
        }
    }

    private static void DrawTree(Renderer2D renderer, Vec2 screen, float zoom, byte wood, int tileX, int tileY)
    {
        // Below a few pixels a tree is a smudge; a flat canopy colour reads better than a shape.
        if (zoom < 5f)
        {
            renderer.FillRect(MathF.Floor(screen.X), MathF.Floor(screen.Y), MathF.Ceiling(zoom) + 1f, MathF.Ceiling(zoom) + 1f, WorldColors.TreeCanopy);
            return;
        }

        // Canopy shrinks as the tile is logged out, which is the only cue that a wood is running dry.
        var fullness = 0.55f + wood / (float)ForestFullness * 0.45f;

        // Nudged off-centre and resized per tile, so a wood reads as trees rather than as wallpaper.
        // Derived from the coordinates, so it never shimmers as the camera moves.
        var jitterX = Wobble(tileX, tileY, salt: 1);
        var jitterY = Wobble(tileX, tileY, salt: 2);
        var sizeWobble = 1f + Wobble(tileX, tileY, salt: 3) * 0.5f;

        var radius = zoom * 0.34f * Math.Clamp(fullness, 0.4f, 1f) * sizeWobble;
        var centerX = screen.X + zoom * (0.5f + jitterX * 0.34f);
        var centerY = screen.Y + zoom * (0.5f + jitterY * 0.34f);

        renderer.FillRect(centerX - zoom * 0.06f, centerY, zoom * 0.12f, zoom * 0.4f, WorldColors.TreeTrunk);
        renderer.FillCircle(centerX, centerY - zoom * 0.08f, radius, WorldColors.TreeCanopy);
    }

    /// <summary>Wood in an untouched tile, used only to scale how full a canopy is drawn.</summary>
    private const int ForestFullness = 60;

    /// <summary>A stable value in -0.5..0.5 for a tile, used to vary tree placement and size.</summary>
    private static float Wobble(int tileX, int tileY, int salt)
    {
        var hash = unchecked((uint)(tileX * 73856093 ^ tileY * 19349663 ^ salt * 83492791));
        hash ^= hash >> 13;
        hash *= 2246822519u;
        hash ^= hash >> 16;

        return (hash & 0xFFFF) / 65535f - 0.5f;
    }
}
