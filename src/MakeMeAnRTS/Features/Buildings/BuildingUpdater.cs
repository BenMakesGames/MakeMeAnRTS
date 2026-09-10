using MakeMeAnRTS.Core;
using MakeMeAnRTS.Features.Match;
using MakeMeAnRTS.Features.Units;

namespace MakeMeAnRTS.Features.Buildings;

/// <summary>Advances training queues and turns finished training into units on the ground.</summary>
/// <remarks>
/// Construction progress is not handled here - it is driven by the builders standing next to the site,
/// in <see cref="UnitUpdater"/>, so a foundation with nobody working on it genuinely does not progress.
/// </remarks>
public sealed class BuildingUpdater
{
    private readonly MatchState _state;

    public BuildingUpdater(MatchState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        _state = state;
    }

    public void Update(float deltaSeconds)
    {
        foreach (var building in _state.Buildings.ToList())
        {
            if (!building.IsAlive || !building.IsComplete)
                continue;

            AdvanceTraining(building, deltaSeconds);
        }
    }

    private void AdvanceTraining(Building building, float deltaSeconds)
    {
        if (building.PeekTraining() is not { } kind)
            return;

        building.TrainingElapsed += deltaSeconds;

        var stats = UnitCatalog.For(kind);
        if (building.TrainingElapsed < stats.TrainSeconds)
            return;

        var spawnTile = FindSpawnTile(building);

        // Nowhere to put the unit: hold it in the queue rather than dropping it inside a wall.
        if (spawnTile is null)
            return;

        building.DequeueTraining();

        var unit = _state.SpawnUnit(kind, building.OwnerIndex, spawnTile.Value.Center);

        // New citizens look for work, so a base left training does not pile up idle villagers.
        if (kind == UnitKind.Citizen)
            SendToNearestWork(unit);
    }

    /// <summary>A walkable tile just outside the building's footprint for a new unit to appear on.</summary>
    private GridPos? FindSpawnTile(Building building)
    {
        foreach (var approach in _state.Map.FootprintApproaches(building.Origin, building.Stats.Size))
            return approach;

        // Footprint fully hemmed in: fall back to a wider search rather than refusing to train at all.
        return _state.Pathfinder.FindNearestWalkable(building.Origin, maxRadius: 8);
    }

    private void SendToNearestWork(Unit unit)
    {
        if (_state.FindNearestTrees(unit.Tile) is { } trees)
            unit.GiveOrder(new UnitOrder.GatherWood(trees));
    }
}
