using MakeMeAnRTS.Core;
using MakeMeAnRTS.Features.Buildings;
using MakeMeAnRTS.Features.Pathfinding;
using MakeMeAnRTS.Features.Prospecting;
using MakeMeAnRTS.Features.Units;
using MakeMeAnRTS.Features.World;

namespace MakeMeAnRTS.Features.Match;

/// <summary>
/// Everything in a running match: the map, the players, and every unit, building and sign on it.
/// </summary>
/// <remarks>
/// This is state plus the operations that keep that state consistent - spawning, removal, tile
/// occupancy. The rules that advance it live in the per-feature updaters, and the orders that drive it
/// come through <see cref="MatchCommands"/>. Splitting it that way keeps this type readable as the game
/// grows, and means the CPU opponent and the player go through exactly the same door.
/// </remarks>
public sealed class MatchState
{
    private readonly Dictionary<int, Unit> _unitsById = [];
    private readonly Dictionary<int, Building> _buildingsById = [];
    private readonly List<Unit> _units = [];
    private readonly List<Building> _buildings = [];
    private readonly List<MapSign> _signs = [];

    private int _nextEntityId = 1;

    public WorldMap Map { get; }
    public Pathfinder Pathfinder { get; }
    public IReadOnlyList<Player> Players { get; }

    public IReadOnlyList<Unit> Units => _units;
    public IReadOnlyList<Building> Buildings => _buildings;
    public IReadOnlyList<MapSign> Signs => _signs;

    /// <summary>Seconds of match time elapsed. Drives training, construction and sign timestamps.</summary>
    public float ElapsedSeconds { get; set; }

    /// <summary>The winner's index once the match is over, or null while it is still being played.</summary>
    public int? WinnerIndex { get; set; }

    public bool IsOver => WinnerIndex is not null;

    public MatchState(WorldMap map, IReadOnlyList<Player> players)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(players);

        if (players.Count < 2)
            throw new ArgumentException("A match needs at least two players.", nameof(players));

        Map = map;
        Players = players;
        Pathfinder = new Pathfinder(map);
    }

    public Player PlayerAt(int index) =>
        index >= 0 && index < Players.Count
            ? Players[index]
            : throw new ArgumentOutOfRangeException(nameof(index), index, $"No player {index} in this match.");

    public Unit? FindUnit(int id) => _unitsById.GetValueOrDefault(id);
    public Building? FindBuilding(int id) => _buildingsById.GetValueOrDefault(id);

    /// <summary>The building covering a tile, or null. Used for click targeting and drop-off lookups.</summary>
    public Building? BuildingAtTile(GridPos pos)
    {
        foreach (var building in _buildings)
            if (building.CoversTile(pos))
                return building;

        return null;
    }

    public IEnumerable<Unit> UnitsOf(int playerIndex) => _units.Where(unit => unit.OwnerIndex == playerIndex);
    public IEnumerable<Building> BuildingsOf(int playerIndex) => _buildings.Where(building => building.OwnerIndex == playerIndex);

    public Unit SpawnUnit(UnitKind kind, int ownerIndex, Vec2 position)
    {
        var unit = new Unit(_nextEntityId++, kind, ownerIndex, position);

        _units.Add(unit);
        _unitsById[unit.Id] = unit;

        return unit;
    }

    /// <summary>
    /// Places a building and marks its tiles as occupied.
    /// </summary>
    /// <remarks>
    /// Occupancy is claimed here rather than by the caller, and released in <see cref="RemoveBuilding"/>,
    /// so the map can never be left with phantom obstacles where a building used to be.
    /// </remarks>
    public Building SpawnBuilding(BuildingKind kind, int ownerIndex, GridPos origin, MineralKind? minedMineral, bool startCompleted)
    {
        var building = new Building(_nextEntityId++, kind, ownerIndex, origin, minedMineral, startCompleted);

        foreach (var tile in building.FootprintTiles())
            Map.SetOccupied(tile, true);

        _buildings.Add(building);
        _buildingsById[building.Id] = building;

        return building;
    }

    public MapSign AddSign(int ownerIndex, GridPos position, IReadOnlyDictionary<MineralKind, int> readings)
    {
        var sign = new MapSign(_nextEntityId++, ownerIndex, position, readings, ElapsedSeconds);
        _signs.Add(sign);

        return sign;
    }

    public void RemoveUnit(Unit unit)
    {
        ArgumentNullException.ThrowIfNull(unit);

        // A dead worker must not keep holding a slot in the mine it was assigned to.
        foreach (var building in _buildings)
            building.RemoveWorker(unit.Id);

        _units.Remove(unit);
        _unitsById.Remove(unit.Id);
    }

    public void RemoveBuilding(Building building)
    {
        ArgumentNullException.ThrowIfNull(building);

        foreach (var tile in building.FootprintTiles())
            Map.SetOccupied(tile, false);

        _buildings.Remove(building);
        _buildingsById.Remove(building.Id);
    }

    /// <summary>
    /// Whether a mine has taken everything its footprint had.
    /// </summary>
    /// <remarks>
    /// A mine only draws from the tiles it stands on, so deposits are finite and mines genuinely run
    /// out. Anything deciding where to send workers or what to build next has to know the difference
    /// between a mine and a monument to one.
    /// </remarks>
    public bool IsMineExhausted(Building mine)
    {
        ArgumentNullException.ThrowIfNull(mine);

        if (mine.MinedMineral is not { } mineral)
            return false;

        foreach (var tile in mine.FootprintTiles())
            if (Map.MineralAt(mineral, tile) > 0)
                return false;

        return true;
    }

    /// <summary>
    /// The nearest completed building of <paramref name="ownerIndex"/> that accepts <paramref name="resource"/>.
    /// </summary>
    /// <remarks>
    /// This is what makes a lumberjack worth building: it enters the same search as the home base, so a
    /// closer one simply wins and woodcutters retarget themselves with no further orders.
    /// </remarks>
    public Building? FindNearestDropOff(int ownerIndex, ResourceKind resource, Vec2 from)
    {
        Building? best = null;
        var bestDistance = float.MaxValue;

        foreach (var building in _buildings)
        {
            if (building.OwnerIndex != ownerIndex || !building.IsComplete || !building.Stats.Accepts(resource))
                continue;

            var distance = Vec2.DistanceSquared(from, building.Center);
            if (distance >= bestDistance)
                continue;

            best = building;
            bestDistance = distance;
        }

        return best;
    }

    /// <summary>
    /// The nearest tile of trees that can actually be cut, searched outwards in rings.
    /// </summary>
    /// <param name="skip">Tiles already claimed by other workers, so a group spreads over a wood.</param>
    /// <remarks>
    /// Only <see cref="WorldMap.IsCuttable"/> tiles count. Returning a tree buried inside a wood would
    /// send a worker to stand on a tile that no route can reach, and it would wait there forever.
    /// </remarks>
    public GridPos? FindNearestTrees(GridPos near, int maxRadius = 30, IReadOnlySet<GridPos>? skip = null)
    {
        for (var radius = 0; radius <= maxRadius; radius++)
        {
            for (var dy = -radius; dy <= radius; dy++)
            {
                for (var dx = -radius; dx <= radius; dx++)
                {
                    if (radius > 0 && Math.Abs(dx) != radius && Math.Abs(dy) != radius)
                        continue;

                    var candidate = new GridPos(near.X + dx, near.Y + dy);
                    if (Map.IsCuttable(candidate) && skip?.Contains(candidate) != true)
                        return candidate;
                }
            }
        }

        return null;
    }
}
