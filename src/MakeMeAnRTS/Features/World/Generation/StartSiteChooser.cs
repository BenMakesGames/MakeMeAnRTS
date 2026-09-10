using MakeMeAnRTS.Core;

namespace MakeMeAnRTS.Features.World.Generation;

/// <summary>Finds fair, workable places for players to start, then levels them so a base always fits.</summary>
internal static class StartSiteChooser
{
    /// <summary>Tiles kept clear of the map edge, so a base is never jammed into a corner.</summary>
    private const int EdgeMargin = 10;

    /// <summary>Radius that must be completely dry - no lake or river may cut through a starting camp.</summary>
    private const int DryRadius = 3;

    /// <summary>How far out to look for the woods a starting economy needs.</summary>
    private const int WoodSearchRadius = 14;

    private readonly record struct Site(GridPos Position, float Score);

    /// <summary>
    /// How far apart starts should be, as a fraction of the map's short side, in order of preference.
    /// </summary>
    /// <remarks>
    /// Tried in turn before giving up on a map. Throwing away an otherwise good map because its two
    /// best sites are 70 tiles apart instead of 79 is a bad trade: the player waits on another
    /// generation pass and gets a map no better than the one just discarded. A slightly closer pair of
    /// starts is a far smaller cost, and the last rung only has to beat "no map at all".
    /// </remarks>
    private static readonly float[] SeparationLadder = [0.45f, 0.36f, 0.28f];

    /// <summary>
    /// Chooses <paramref name="playerCount"/> sites that are viable, mutually reachable, and as far
    /// apart as the map allows.
    /// </summary>
    /// <returns>The chosen positions, or null if this map simply has no room for them.</returns>
    public static IReadOnlyList<GridPos>? Choose(Grid2D<Tile> tiles, WorldGenSettings settings)
    {
        var components = LandComponents.Build(tiles);
        var candidates = ScoreCandidates(tiles, settings);

        if (candidates.Count < settings.PlayerCount)
            return null;

        // Keep starts on the largest landmass; the bridge pass has already joined what it could.
        var bestComponent = candidates
            .GroupBy(site => components.LabelAt(site.Position))
            .OrderByDescending(group => group.Count())
            .First();

        // Filter by quality rather than taking the top N: the highest-scoring sites tend to cluster in
        // one lush corner, and a pool with no spread cannot produce well-separated starts.
        var bestScore = bestComponent.Max(site => site.Score);
        var pool = bestComponent.Where(site => site.Score >= bestScore * 0.7f).ToList();

        if (pool.Count < settings.PlayerCount)
            return null;

        var shortSide = Math.Min(tiles.Width, tiles.Height);
        var chosen = SelectSpreadSites(pool, settings.PlayerCount);

        if (chosen is null)
            return null;

        var closestPair = chosen.SelectMany(a => chosen.Where(b => b != a).Select(b => GridPos.StepDistance(a, b))).Min();

        // The best spread this pool can manage is what it is; the ladder decides whether that is enough.
        return closestPair >= (int)(shortSide * SeparationLadder[^1]) ? chosen : null;
    }

    /// <summary>Farthest-point selection: each new start is pushed as far from the existing ones as possible.</summary>
    private static List<GridPos>? SelectSpreadSites(List<Site> pool, int count)
    {
        var chosen = new List<GridPos> { pool.MaxBy(site => site.Score).Position };

        while (chosen.Count < count)
        {
            var next = pool
                .Where(site => !chosen.Contains(site.Position))
                .OrderByDescending(site => chosen.Min(existing => GridPos.StepDistance(existing, site.Position)))
                .ThenByDescending(site => site.Score)
                .Select(site => (GridPos?)site.Position)
                .FirstOrDefault();

            if (next is null)
                return null;

            chosen.Add(next.Value);
        }

        return chosen;
    }

    /// <summary>How well a chosen set of starts scores against the separation ladder, for diagnostics.</summary>
    public static int SeparationRungFor(int shortSide, int closestPair)
    {
        for (var rung = 0; rung < SeparationLadder.Length; rung++)
            if (closestPair >= (int)(shortSide * SeparationLadder[rung]))
                return rung;

        return SeparationLadder.Length;
    }

    /// <summary>Clears trees and breaks up rock around a start, so the opening minutes are never a lottery.</summary>
    public static void PrepareSite(Grid2D<Tile> tiles, GridPos site, WorldGenSettings settings)
    {
        for (var dy = -settings.StartClearRadius; dy <= settings.StartClearRadius; dy++)
        {
            for (var dx = -settings.StartClearRadius; dx <= settings.StartClearRadius; dx++)
            {
                var pos = new GridPos(site.X + dx, site.Y + dy);
                if (!tiles.IsInBounds(pos))
                    continue;

                ref var tile = ref tiles.At(pos);
                if (tile.Terrain.IsWater())
                    continue;

                tile.Wood = 0;

                if (tile.Terrain == TerrainKind.Rock)
                    tile.Terrain = TerrainKind.Dirt;
            }
        }
    }

    private static List<Site> ScoreCandidates(Grid2D<Tile> tiles, WorldGenSettings settings)
    {
        var sites = new List<Site>();
        var radius = settings.StartClearRadius;

        // Sampling every third tile: adjacent candidates are near-identical, and this keeps scoring cheap.
        for (var y = EdgeMargin; y < tiles.Height - EdgeMargin; y += 3)
        {
            for (var x = EdgeMargin; x < tiles.Width - EdgeMargin; x += 3)
            {
                var pos = new GridPos(x, y);

                if (!IsDry(tiles, pos, DryRadius))
                    continue;

                var openLand = CountBuildable(tiles, pos, radius);
                var area = (radius * 2 + 1) * (radius * 2 + 1);

                // Demand most of the camp be usable ground rather than rock or lake shore.
                if (openLand < area * 0.7f)
                    continue;

                sites.Add(new Site(pos, openLand + CountNearbyWood(tiles, pos) * 0.02f));
            }
        }

        return sites;
    }

    private static bool IsDry(Grid2D<Tile> tiles, GridPos center, int radius)
    {
        for (var dy = -radius; dy <= radius; dy++)
            for (var dx = -radius; dx <= radius; dx++)
                if (tiles.GetOrDefault(center + new GridPos(dx, dy), default).Terrain.IsWater())
                    return false;

        return true;
    }

    /// <summary>Counts tiles that are - or would be after clearing - fit to build on.</summary>
    private static int CountBuildable(Grid2D<Tile> tiles, GridPos center, int radius)
    {
        var count = 0;

        for (var dy = -radius; dy <= radius; dy++)
        {
            for (var dx = -radius; dx <= radius; dx++)
            {
                var pos = center + new GridPos(dx, dy);
                if (!tiles.IsInBounds(pos))
                    continue;

                // Rock is levelled to dirt by PrepareSite, so it counts as usable here.
                var terrain = tiles[pos].Terrain;
                if (terrain.IsBuildable() || terrain == TerrainKind.Rock)
                    count++;
            }
        }

        return count;
    }

    private static int CountNearbyWood(Grid2D<Tile> tiles, GridPos center)
    {
        var total = 0;

        for (var dy = -WoodSearchRadius; dy <= WoodSearchRadius; dy += 2)
            for (var dx = -WoodSearchRadius; dx <= WoodSearchRadius; dx += 2)
                total += tiles.GetOrDefault(center + new GridPos(dx, dy), default).Wood;

        return total;
    }
}
