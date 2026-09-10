using MakeMeAnRTS.Core;

namespace MakeMeAnRTS.Features.World.Generation;

/// <summary>
/// Turns a seed into a playable map: elevation, lakes, rivers, crossings, woods, mineral heat maps and
/// start positions.
/// </summary>
/// <remarks>
/// Generation can legitimately fail - some seeds produce a map with nowhere fair to start, or two
/// halves no bridge can join. Rather than shipping a broken map, <see cref="Generate"/> retries with a
/// derived seed and throws if even that runs out. Failing loudly here is much cheaper than debugging a
/// game that started on an unplayable map.
/// </remarks>
public static class WorldGenerator
{
    private const int MaxAttempts = 12;

    /// <summary>How the most recent generation's rivers ended. Diagnostics only; see <see cref="RiverOutcome"/>.</summary>
    public static IReadOnlyList<RiverOutcome> LastRiverOutcomes { get; private set; } = [];

    public static WorldMap Generate(WorldGenSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.Validate();

        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            // A derived seed, so a retry produces a genuinely different map but stays reproducible.
            var attemptSettings = attempt == 0 ? settings : settings with { Seed = unchecked(settings.Seed * 31 + attempt) };

            if (TryGenerate(attemptSettings, out var map))
                return map;
        }

        throw new WorldGenerationException(
            $"Could not generate a playable {settings.Width}x{settings.Height} map for {settings.PlayerCount} players " +
            $"from seed {settings.Seed} after {MaxAttempts} attempts.");
    }

    private static bool TryGenerate(WorldGenSettings settings, out WorldMap map)
    {
        var rng = new Rng(settings.Seed);

        var heights = NoiseField.Generate(settings.Width, settings.Height, settings.Seed, octaves: 6, featureSize: settings.Width / 3f);
        // A distinct seed offset, so moisture never mirrors elevation and forests do not just ring the hills.
        var moisture = NoiseField.Generate(settings.Width, settings.Height, unchecked(settings.Seed + 1013904223), octaves: 4, featureSize: settings.Width / 4f);

        var tiles = PaintTerrain(heights, moisture, settings);

        // Rivers can only run downhill if every tile has somewhere downhill to go.
        SinkFiller.Fill(heights, tiles);

        LastRiverOutcomes = RiverCarver.Carve(tiles, heights, settings, rng.Fork(1));
        PaintShoreline(tiles);
        BridgeBuilder.Build(tiles, settings, rng.Fork(2));

        ForestPlanter.Plant(tiles, moisture, settings, rng.Fork(3));

        var startPositions = StartSiteChooser.Choose(tiles, settings);
        if (startPositions is null)
        {
            map = null!;
            return false;
        }

        // Only now is it worth guaranteeing crossings: the bridges needed depend on where players start.
        if (!BridgeBuilder.ConnectRegions(tiles, startPositions))
        {
            map = null!;
            return false;
        }

        foreach (var start in startPositions)
            StartSiteChooser.PrepareSite(tiles, start, settings);

        var minerals = MineralFields.Generate(tiles, heights, settings);
        StoreHeights(tiles, heights);

        map = new WorldMap(settings.Width, settings.Height, settings.Seed, tiles, minerals, startPositions);
        return true;
    }

    private static Grid2D<Tile> PaintTerrain(Grid2D<float> heights, Grid2D<float> moisture, WorldGenSettings settings)
    {
        var tiles = new Grid2D<Tile>(heights.Width, heights.Height);

        foreach (var pos in tiles.Positions())
        {
            var elevation = heights[pos];

            tiles.At(pos).Terrain = elevation switch
            {
                _ when elevation < settings.WaterLevel => TerrainKind.Water,
                _ when elevation > settings.RockLevel => TerrainKind.Rock,
                // Dry ground where there is little moisture; grass everywhere else.
                _ when moisture[pos] < 0.38f => TerrainKind.Dirt,
                _ => TerrainKind.Grass,
            };
        }

        return tiles;
    }

    /// <summary>Turns land that touches standing water into sand, which is what makes lakes read as lakes.</summary>
    private static void PaintShoreline(Grid2D<Tile> tiles)
    {
        var shoreline = new List<GridPos>();

        foreach (var pos in tiles.Positions())
        {
            if (tiles[pos].Terrain is not (TerrainKind.Grass or TerrainKind.Dirt))
                continue;

            foreach (var offset in GridPos.Neighbors8)
            {
                if (tiles.GetOrDefault(pos + offset, default).Terrain == TerrainKind.Water)
                {
                    shoreline.Add(pos);
                    break;
                }
            }
        }

        // Collected first, then applied: painting in place would let sand seed more sand.
        foreach (var pos in shoreline)
            tiles.At(pos).Terrain = TerrainKind.Sand;
    }

    private static void StoreHeights(Grid2D<Tile> tiles, Grid2D<float> heights)
    {
        foreach (var pos in tiles.Positions())
            tiles.At(pos).Height = MathHelpers.ToByte(heights[pos] * 255f);
    }
}

public sealed class WorldGenerationException(string message) : Exception(message);
