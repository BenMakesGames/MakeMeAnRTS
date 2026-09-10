using MakeMeAnRTS.Core;
using MakeMeAnRTS.Features.Buildings;
using MakeMeAnRTS.Features.Units;
using MakeMeAnRTS.Features.World;
using MakeMeAnRTS.Features.World.Generation;

namespace MakeMeAnRTS.Features.Match;

/// <summary>Builds a fresh match: the map, the players, and each side's opening pieces.</summary>
public static class MatchSetup
{
    /// <summary>The opening loadout, identical for every player: there are no factions.</summary>
    public const int StartingCitizens = 4;

    private static readonly ResourceAmounts StartingResources = ResourceAmounts.Of(wood: 220, stone: 120, iron: 40);

    public static MatchState Create(WorldGenSettings worldSettings, string humanName = "You", string cpuName = "CPU")
    {
        ArgumentNullException.ThrowIfNull(worldSettings);

        var map = WorldGenerator.Generate(worldSettings);
        var players = new List<Player>();

        for (var index = 0; index < worldSettings.PlayerCount; index++)
        {
            // Player 0 is the human; every other seat is a CPU opponent.
            var isHuman = index == 0;
            var name = isHuman ? humanName : worldSettings.PlayerCount > 2 ? $"{cpuName} {index}" : cpuName;

            players.Add(new Player(index, name, Player.Colors[index % Player.Colors.Length], isHuman, map.Width, map.Height));
        }

        var state = new MatchState(map, players);

        for (var index = 0; index < players.Count; index++)
        {
            players[index].Resources.Add(StartingResources);
            PlaceStartingPieces(state, index, map.StartPositions[index]);
        }

        return state;
    }

    /// <summary>
    /// Plants a home base at a start position, with citizens, a scout and a prospector around it.
    /// </summary>
    /// <remarks>
    /// The base is nudged so the start position sits at its centre, and the generator has already
    /// cleared and levelled a radius around that point, so the footprint always fits.
    /// </remarks>
    private static void PlaceStartingPieces(MatchState state, int playerIndex, GridPos start)
    {
        var baseStats = BuildingCatalog.For(BuildingKind.HomeBase);
        var origin = new GridPos(start.X - baseStats.Size / 2, start.Y - baseStats.Size / 2);

        if (!state.Map.IsFootprintBuildable(origin, baseStats.Size))
            throw new WorldGenerationException($"Start position {start} for player {playerIndex} cannot fit a home base.");

        var homeBase = state.SpawnBuilding(BuildingKind.HomeBase, playerIndex, origin, minedMineral: null, startCompleted: true);

        // Spread the opening units around the base instead of stacking them all on one tile.
        var spawnTiles = state.Map.FootprintApproaches(homeBase.Origin, homeBase.Stats.Size).ToList();
        if (spawnTiles.Count == 0)
            throw new WorldGenerationException($"Player {playerIndex}'s home base at {origin} has no free ground around it.");

        // Each citizen takes its own stand of trees, so they do not all queue at one tile.
        var claimedTrees = new HashSet<GridPos>();
        var spawnIndex = 0;

        foreach (var kind in StartingUnits())
        {
            var tile = spawnTiles[spawnIndex++ % spawnTiles.Count];
            var unit = state.SpawnUnit(kind, playerIndex, tile.Center);

            if (kind != UnitKind.Citizen)
                continue;

            // Citizens open on wood, which is what every first building costs.
            if (state.FindNearestTrees(tile, skip: claimedTrees) is { } trees)
            {
                claimedTrees.Add(trees);
                unit.GiveOrder(new UnitOrder.GatherWood(trees));
            }
        }
    }

    private static IEnumerable<UnitKind> StartingUnits()
    {
        for (var i = 0; i < StartingCitizens; i++)
            yield return UnitKind.Citizen;

        yield return UnitKind.Scout;
        yield return UnitKind.Prospector;
    }
}
