using MakeMeAnRTS.Core;

namespace MakeMeAnRTS.Features.World;

/// <summary>
/// The generated map: terrain, trees, and one abundance map per mineral, plus the tiles buildings
/// currently occupy.
/// </summary>
/// <remarks>
/// This is ground truth. What a given player has discovered about minerals is deliberately not stored
/// here - see the prospecting feature - because the whole point of the prospector is that abundance is
/// hidden until surveyed.
/// </remarks>
public sealed class WorldMap
{
    private readonly Grid2D<byte>[] _mineralAbundance;

    /// <summary>Tiles covered by a standing building, which block movement without changing the terrain.</summary>
    private readonly Grid2D<bool> _occupied;

    public int Width { get; }
    public int Height { get; }
    public int Seed { get; }
    public Grid2D<Tile> Tiles { get; }

    /// <summary>Where each player's starting base sits, in player order. Always at least two entries.</summary>
    public IReadOnlyList<GridPos> StartPositions { get; }

    public WorldMap(int width, int height, int seed, Grid2D<Tile> tiles, Grid2D<byte>[] mineralAbundance, IReadOnlyList<GridPos> startPositions)
    {
        ArgumentNullException.ThrowIfNull(tiles);
        ArgumentNullException.ThrowIfNull(mineralAbundance);
        ArgumentNullException.ThrowIfNull(startPositions);

        if (mineralAbundance.Length != Minerals.All.Length)
            throw new ArgumentException($"Expected {Minerals.All.Length} mineral maps, got {mineralAbundance.Length}.", nameof(mineralAbundance));

        if (startPositions.Count < 2)
            throw new ArgumentException("A map needs at least two start positions.", nameof(startPositions));

        Width = width;
        Height = height;
        Seed = seed;
        Tiles = tiles;
        StartPositions = startPositions;

        _mineralAbundance = mineralAbundance;
        _occupied = new Grid2D<bool>(width, height);
    }

    public bool IsInBounds(GridPos pos) => Tiles.IsInBounds(pos);

    public Tile TileAt(GridPos pos) => Tiles[pos];

    /// <summary>Abundance 0-255 of <paramref name="mineral"/> at a tile. Off-map tiles read as 0.</summary>
    public byte MineralAt(MineralKind mineral, GridPos pos) => _mineralAbundance[(int)mineral].GetOrDefault(pos, (byte)0);

    public Grid2D<byte> MineralMap(MineralKind mineral) => _mineralAbundance[(int)mineral];

    /// <summary>The richest mineral at a tile, and its abundance. Ties break in <see cref="MineralKind"/> order.</summary>
    public (MineralKind Mineral, byte Abundance) RichestMineralAt(GridPos pos)
    {
        var best = MineralKind.Stone;
        byte bestAbundance = 0;

        foreach (var mineral in Minerals.All)
        {
            var abundance = MineralAt(mineral, pos);
            if (abundance > bestAbundance)
            {
                best = mineral;
                bestAbundance = abundance;
            }
        }

        return (best, bestAbundance);
    }

    /// <summary>Spends up to <paramref name="amount"/> of a deposit, returning what was actually extracted.</summary>
    public int ExtractMineral(MineralKind mineral, GridPos pos, int amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);

        if (!IsInBounds(pos))
            return 0;

        ref var abundance = ref _mineralAbundance[(int)mineral].At(pos);
        var taken = Math.Min(amount, abundance);
        abundance -= (byte)taken;

        return taken;
    }

    /// <summary>Fells up to <paramref name="amount"/> wood from a tile's trees, returning what was cut.</summary>
    public int CutWood(GridPos pos, int amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);

        if (!IsInBounds(pos))
            return 0;

        ref var tile = ref Tiles.At(pos);
        var taken = Math.Min(amount, tile.Wood);
        tile.Wood -= (byte)taken;

        return taken;
    }

    public bool IsOccupied(GridPos pos) => _occupied.GetOrDefault(pos, false);

    /// <summary>Marks or clears the tiles a building stands on. Buildings own the calls in both directions.</summary>
    public void SetOccupied(GridPos pos, bool occupied)
    {
        if (IsInBounds(pos))
            _occupied[pos] = occupied;
    }

    /// <summary>Whether a land unit can stand on the tile right now.</summary>
    public bool IsWalkable(GridPos pos) => IsInBounds(pos) && Tiles[pos].IsWalkable && !_occupied[pos];

    /// <summary>
    /// Whether a building footprint tile is free. Excludes trees, water, rock and other buildings.
    /// </summary>
    /// <param name="allowRockyGround">
    /// True for mines, which are the one thing that belongs on bare rock. Without this, the richest ore
    /// on the map is unusable: minerals favour high ground, high ground is rock, and rock takes no
    /// buildings - so a player could survey a huge seam and have nowhere to put a mine.
    /// </param>
    public bool IsBuildable(GridPos pos, bool allowRockyGround = false)
    {
        if (!IsInBounds(pos) || _occupied[pos])
            return false;

        var tile = Tiles[pos];

        if (tile.HasTrees)
            return false;

        return tile.Terrain.IsBuildable() || (allowRockyGround && tile.Terrain == TerrainKind.Rock);
    }

    /// <summary>Whether an entire <paramref name="size"/>-square footprint with its top-left at <paramref name="origin"/> is free.</summary>
    public bool IsFootprintBuildable(GridPos origin, int size, bool allowRockyGround = false)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(size, 1);

        for (var dy = 0; dy < size; dy++)
            for (var dx = 0; dx < size; dx++)
                if (!IsBuildable(new GridPos(origin.X + dx, origin.Y + dy), allowRockyGround))
                    return false;

        return true;
    }

    /// <summary>Walkable tiles adjacent to a footprint - where a worker stands to use a building.</summary>
    public IEnumerable<GridPos> FootprintApproaches(GridPos origin, int size)
    {
        for (var dy = -1; dy <= size; dy++)
        {
            for (var dx = -1; dx <= size; dx++)
            {
                var isInside = dx >= 0 && dy >= 0 && dx < size && dy < size;
                if (isInside)
                    continue;

                var candidate = new GridPos(origin.X + dx, origin.Y + dy);
                if (IsWalkable(candidate))
                    yield return candidate;
            }
        }
    }
}
