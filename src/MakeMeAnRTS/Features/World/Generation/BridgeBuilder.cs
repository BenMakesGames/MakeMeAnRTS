using MakeMeAnRTS.Core;

namespace MakeMeAnRTS.Features.World.Generation;

/// <summary>Places river crossings: a scattering for flavour, then as many as it takes to make the map playable.</summary>
internal static class BridgeBuilder
{
    /// <summary>
    /// A crossing candidate: a river tile with dry land directly opposite on both sides.
    /// </summary>
    private readonly record struct Crossing(GridPos Position, GridPos BankA, GridPos BankB);

    public static void Build(Grid2D<Tile> tiles, WorldGenSettings settings, Rng rng)
    {
        var crossings = FindCrossings(tiles);
        rng.Shuffle(crossings);

        var placed = new List<GridPos>();

        // Pass one: spread crossings out so a river is a real obstacle, not a colander.
        foreach (var crossing in crossings)
        {
            if (placed.Any(existing => GridPos.StepDistance(existing, crossing.Position) < settings.BridgeSpacing))
                continue;

            tiles.At(crossing.Position).Terrain = TerrainKind.Bridge;
            placed.Add(crossing.Position);
        }
    }

    /// <summary>
    /// Adds crossings until <paramref name="required"/> tiles all share one landmass.
    /// </summary>
    /// <returns>True if every required position ended up mutually reachable.</returns>
    public static bool ConnectRegions(Grid2D<Tile> tiles, IReadOnlyList<GridPos> required)
    {
        if (required.Count < 2)
            return true;

        var crossings = FindCrossings(tiles);

        // Each added bridge merges two components, so this terminates well inside the candidate count.
        for (var attempt = 0; attempt <= crossings.Count; attempt++)
        {
            var components = LandComponents.Build(tiles);

            if (required.All(pos => components.AreConnected(required[0], pos)))
                return true;

            Crossing? joining = crossings
                .Where(crossing =>
                    tiles[crossing.Position].Terrain == TerrainKind.River &&
                    components.LabelAt(crossing.BankA) != 0 &&
                    components.LabelAt(crossing.BankB) != 0 &&
                    components.LabelAt(crossing.BankA) != components.LabelAt(crossing.BankB))
                .Select(crossing => (Crossing?)crossing)
                .FirstOrDefault();

            if (joining is null)
                return false;

            tiles.At(joining.Value.Position).Terrain = TerrainKind.Bridge;
        }

        return false;
    }

    private static List<Crossing> FindCrossings(Grid2D<Tile> tiles)
    {
        var crossings = new List<Crossing>();

        foreach (var pos in tiles.Positions())
        {
            if (tiles[pos].Terrain != TerrainKind.River)
                continue;

            TryAdd(tiles, crossings, pos, new GridPos(-1, 0), new GridPos(1, 0));
            TryAdd(tiles, crossings, pos, new GridPos(0, -1), new GridPos(0, 1));
        }

        return crossings;
    }

    private static void TryAdd(Grid2D<Tile> tiles, List<Crossing> crossings, GridPos river, GridPos towardA, GridPos towardB)
    {
        var bankA = river + towardA;
        var bankB = river + towardB;

        if (!tiles.IsInBounds(bankA) || !tiles.IsInBounds(bankB))
            return;

        // Both banks must be dry, or the "bridge" would just extend the river.
        if (tiles[bankA].Terrain.IsWater() || tiles[bankB].Terrain.IsWater())
            return;

        crossings.Add(new Crossing(river, bankA, bankB));
    }
}
