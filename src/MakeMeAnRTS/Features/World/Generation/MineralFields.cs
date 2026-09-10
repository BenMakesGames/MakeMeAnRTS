using MakeMeAnRTS.Core;

namespace MakeMeAnRTS.Features.World.Generation;

/// <summary>
/// Scatters each mineral as smooth, overlapping deposits - the heat maps the prospector uncovers.
/// </summary>
/// <remarks>
/// Every mineral uses its own noise seed and its own rarity, so no two heat maps line up. Terrain
/// nudges the result (stone favours high ground, gold hides deep) which gives prospecting somewhere
/// sensible to start looking without making it a foregone conclusion.
/// </remarks>
internal static class MineralFields
{
    private readonly record struct Recipe(float FeatureSize, float Threshold, float Richness, float HighGroundBias);

    private static Recipe RecipeFor(MineralKind mineral) => mineral switch
    {
        // Common, broad, and noticeably richer where the ground turns rocky.
        MineralKind.Stone => new Recipe(FeatureSize: 26f, Threshold: 0.52f, Richness: 1.0f, HighGroundBias: 0.30f),

        // The workhorse: mid-sized deposits spread fairly evenly across the map.
        MineralKind.Iron => new Recipe(FeatureSize: 18f, Threshold: 0.62f, Richness: 1.0f, HighGroundBias: 0.10f),

        // Rare, tight, and worth crossing the map for.
        MineralKind.Gold => new Recipe(FeatureSize: 10f, Threshold: 0.80f, Richness: 1.0f, HighGroundBias: -0.15f),

        _ => throw new ArgumentOutOfRangeException(nameof(mineral), mineral, "Unhandled mineral kind."),
    };

    public static Grid2D<byte>[] Generate(Grid2D<Tile> tiles, Grid2D<float> heights, WorldGenSettings settings)
    {
        var maps = new Grid2D<byte>[Minerals.All.Length];

        foreach (var mineral in Minerals.All)
        {
            var recipe = RecipeFor(mineral);
            var map = new Grid2D<byte>(tiles.Width, tiles.Height);

            // Normalised, so a threshold means the same fraction of the map on every seed.
            var field = NoiseField.Generate(
                tiles.Width,
                tiles.Height,
                unchecked(settings.Seed * 7919 + (int)mineral * 104729),
                octaves: 3,
                featureSize: recipe.FeatureSize);

            foreach (var pos in tiles.Positions())
            {
                // Nothing to mine under a lake or a river.
                if (tiles[pos].Terrain.IsWater())
                    continue;

                var value = field[pos] + (heights[pos] - 0.5f) * recipe.HighGroundBias;

                if (value <= recipe.Threshold)
                    continue;

                var strength = MathHelpers.InverseLerp(recipe.Threshold, 1f, value);
                map[pos] = MathHelpers.ToByte(strength * recipe.Richness * 255f);
            }

            maps[(int)mineral] = map;
        }

        return maps;
    }
}
