using SDL3;
using MakeMeAnRTS.Engine;

namespace MakeMeAnRTS.Features.World;

/// <summary>The map's palette. Kept in one place so terrain, the minimap and overlays never disagree.</summary>
public static class WorldColors
{
    public static SDL.Color Terrain(TerrainKind terrain) => terrain switch
    {
        TerrainKind.Grass => Palette.Rgb(86, 128, 62),
        TerrainKind.Dirt => Palette.Rgb(126, 106, 74),
        TerrainKind.Sand => Palette.Rgb(198, 180, 122),
        TerrainKind.Rock => Palette.Rgb(132, 126, 120),
        TerrainKind.Water => Palette.Rgb(46, 82, 132),
        TerrainKind.River => Palette.Rgb(58, 108, 168),
        TerrainKind.Bridge => Palette.Rgb(140, 100, 58),
        _ => throw new ArgumentOutOfRangeException(nameof(terrain), terrain, "Unhandled terrain kind."),
    };

    public static readonly SDL.Color TreeCanopy = Palette.Rgb(38, 84, 44);
    public static readonly SDL.Color TreeTrunk = Palette.Rgb(72, 52, 34);

    /// <summary>Heat map colour for a mineral, brightened by abundance at the tile.</summary>
    public static SDL.Color Mineral(MineralKind mineral) => mineral switch
    {
        MineralKind.Stone => Palette.Rgb(198, 198, 208),
        MineralKind.Iron => Palette.Rgb(206, 122, 88),
        MineralKind.Gold => Palette.Rgb(240, 200, 70),
        _ => throw new ArgumentOutOfRangeException(nameof(mineral), mineral, "Unhandled mineral kind."),
    };

    /// <summary>
    /// Shades a tile by elevation, plus a fixed speckle so large flat areas have some texture.
    /// </summary>
    /// <remarks>
    /// Without the speckle, a plateau or a wide meadow renders as one dead sheet of colour and the map
    /// looks like a diagram. The speckle comes from the tile's own coordinates, so it never shimmers
    /// as the camera moves, and it is small enough not to read as terrain the player could act on.
    ///
    /// Water keeps its flat colour: shading it by the drowned terrain underneath makes lakes look
    /// blotchy rather than deep.
    /// </remarks>
    public static SDL.Color Shaded(TerrainKind terrain, byte height, int tileX, int tileY)
    {
        var baseColor = Terrain(terrain);

        if (terrain.IsWater())
            return baseColor;

        // +/-18% brightness across the elevation range: enough to read relief without muddying the palette.
        var shade = 0.82f + height / 255f * 0.36f;

        return Palette.Scale(baseColor, shade + Speckle(tileX, tileY));
    }

    /// <summary>A stable +/-3% brightness wobble derived from the tile's coordinates.</summary>
    private static float Speckle(int tileX, int tileY)
    {
        var hash = unchecked((uint)(tileX * 73856093 ^ tileY * 19349663));
        hash ^= hash >> 13;
        hash *= 2246822519u;
        hash ^= hash >> 16;

        return (hash & 0xFF) / 255f * 0.06f - 0.03f;
    }
}
