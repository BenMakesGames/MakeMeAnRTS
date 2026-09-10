using MakeMeAnRTS.Core;

namespace MakeMeAnRTS.Features.World.Generation;

/// <summary>Grows woods on damp lowland grass, in clumps rather than an even sprinkle.</summary>
internal static class ForestPlanter
{
    /// <summary>Wood in a fully grown tile. Tiles at the edge of a wood hold proportionally less.</summary>
    public const int MaxWoodPerTile = 60;

    private const int MinWoodPerTile = 12;

    public static void Plant(Grid2D<Tile> tiles, Grid2D<float> moisture, WorldGenSettings settings, Rng rng)
    {
        // A higher density setting means a lower moisture bar to clear, so more of the map qualifies.
        var threshold = MathHelpers.Lerp(0.95f, 0.15f, settings.ForestDensity);

        foreach (var pos in tiles.Positions())
        {
            ref var tile = ref tiles.At(pos);

            if (tile.Terrain != TerrainKind.Grass)
                continue;

            var damp = moisture[pos];
            if (damp < threshold)
                continue;

            // Density ramps from the threshold up, so woods thin out at their edges instead of ending abruptly.
            var thickness = MathHelpers.InverseLerp(threshold, 1f, damp);
            if (!rng.Chance(0.35f + thickness * 0.65f))
                continue;

            var wood = MathHelpers.Lerp(MinWoodPerTile, MaxWoodPerTile, thickness * rng.NextFloat(0.7f, 1f));
            tile.Wood = MathHelpers.ToByte(wood);
        }
    }
}
