using MakeMeAnRTS.Core;

namespace MakeMeAnRTS.Features.World.Generation;

/// <summary>Runs rivers from the highlands downhill until they reach standing water or leave the map.</summary>
internal static class RiverCarver
{
    /// <summary>A river that can't find a downhill neighbour has hit a basin; anything shorter is a puddle.</summary>
    private const int MinimumPondLength = 3;

    public static IReadOnlyList<RiverOutcome> Carve(Grid2D<Tile> tiles, Grid2D<float> heights, WorldGenSettings settings, Rng rng)
    {
        var sources = ChooseSources(tiles, heights, settings, rng);

        return sources.Select(source => CarveOne(tiles, heights, source, settings)).ToList();
    }

    /// <summary>
    /// Picks high, dry, well-separated tiles. Spacing matters more than height: several rivers from one
    /// peak just retrace the same valley.
    /// </summary>
    private static List<GridPos> ChooseSources(Grid2D<Tile> tiles, Grid2D<float> heights, WorldGenSettings settings, Rng rng)
    {
        var minimumSourceHeight = MathHelpers.Lerp(settings.RockLevel, 1f, 0.15f);
        var minimumSpacing = Math.Max(8, Math.Min(tiles.Width, tiles.Height) / 6);

        var candidates = new List<GridPos>();
        foreach (var pos in tiles.Positions())
        {
            if (heights[pos] >= minimumSourceHeight && tiles[pos].Terrain != TerrainKind.Water)
                candidates.Add(pos);
        }

        rng.Shuffle(candidates);

        var chosen = new List<GridPos>();
        foreach (var candidate in candidates)
        {
            if (chosen.Count >= settings.RiverCount)
                break;

            if (chosen.All(existing => GridPos.StepDistance(existing, candidate) >= minimumSpacing))
                chosen.Add(candidate);
        }

        return chosen;
    }

    private static RiverOutcome CarveOne(Grid2D<Tile> tiles, Grid2D<float> heights, GridPos source, WorldGenSettings settings)
    {
        var maximumLength = tiles.Width + tiles.Height;
        var carved = new List<GridPos>();
        var visited = new HashSet<GridPos>();
        var current = source;

        for (var step = 0; step < maximumLength; step++)
        {
            if (!visited.Add(current))
                break;

            var terrain = tiles[current].Terrain;

            // Reaching a lake or an existing river is a successful outlet: tributaries merge naturally.
            if (step > 0 && terrain is TerrainKind.Water or TerrainKind.River)
                return terrain == TerrainKind.Water ? RiverOutcome.ReachedWater : RiverOutcome.Merged;

            tiles.At(current).Terrain = TerrainKind.River;
            carved.Add(current);

            if (!TryStepDownhill(heights, visited, current, out var next))
            {
                // A basin with no outlet: pool there instead of leaving the river ending in mid-air.
                FloodBasin(tiles, heights, current, settings);
                return RiverOutcome.Stalled;
            }

            // The map edge is an outlet too - water simply leaves the map.
            if (!tiles.IsInBounds(next))
                return RiverOutcome.LeftTheMap;

            current = next;
        }

        // Ran out of length without finding an outlet; a stranded trickle looks like a bug, so undo it.
        if (carved.Count < MinimumPondLength)
        {
            foreach (var pos in carved)
                tiles.At(pos).Terrain = TerrainKind.Grass;

            return RiverOutcome.Discarded;
        }

        return RiverOutcome.Stalled;
    }

    /// <summary>
    /// Steps to the lowest unvisited neighbour.
    /// </summary>
    /// <remarks>
    /// Ties are allowed rather than requiring a strict drop: the sink filler leaves basins almost flat,
    /// and insisting on a strictly lower neighbour stranded rivers in the middle of them.
    /// </remarks>
    private static bool TryStepDownhill(Grid2D<float> heights, HashSet<GridPos> visited, GridPos from, out GridPos next)
    {
        var current = heights[from];
        var lowest = float.MaxValue;
        var found = false;
        next = from;

        foreach (var offset in GridPos.Neighbors4)
        {
            var candidate = from + offset;

            // Off-map counts as downhill: the river runs off the edge rather than stalling against it.
            if (!heights.IsInBounds(candidate))
            {
                next = candidate;
                return true;
            }

            if (visited.Contains(candidate))
                continue;

            var candidateHeight = heights[candidate];
            if (candidateHeight > current || candidateHeight >= lowest)
                continue;

            lowest = candidateHeight;
            next = candidate;
            found = true;
        }

        return found;
    }

    /// <summary>Fills a closed basin with water up to the lip that drains it, forming a small lake.</summary>
    private static void FloodBasin(Grid2D<Tile> tiles, Grid2D<float> heights, GridPos basin, WorldGenSettings settings)
    {
        var surface = MathF.Min(heights[basin] + 0.012f, settings.RockLevel);
        var queue = new Queue<GridPos>();
        var filled = new HashSet<GridPos>();

        queue.Enqueue(basin);
        filled.Add(basin);

        // Bounded so a shallow, sprawling basin can't swallow half the map.
        const int maximumTiles = 400;

        while (queue.Count > 0 && filled.Count < maximumTiles)
        {
            var current = queue.Dequeue();
            tiles.At(current).Terrain = TerrainKind.Water;

            foreach (var offset in GridPos.Neighbors4)
            {
                var candidate = current + offset;
                if (!tiles.IsInBounds(candidate) || filled.Contains(candidate) || heights[candidate] > surface)
                    continue;

                filled.Add(candidate);
                queue.Enqueue(candidate);
            }
        }
    }
}
