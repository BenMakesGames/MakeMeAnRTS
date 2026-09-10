using MakeMeAnRTS.Core;
using MakeMeAnRTS.Features.Buildings;
using MakeMeAnRTS.Features.Units;
using MakeMeAnRTS.Features.World;

namespace MakeMeAnRTS.Features.Match;

/// <summary>
/// Every order a side can give, for the human player and the CPU alike.
/// </summary>
/// <remarks>
/// One door into the match's rules. The CPU cannot cheat by construction: it has no way to place a
/// building it cannot afford or mine ground it has not surveyed, because it asks the same methods the
/// player's mouse does. Failures come back as a reason string rather than an exception, since "you
/// cannot build there" is a normal thing for a player to try.
/// </remarks>
public sealed class MatchCommands
{
    private readonly MatchState _state;

    public MatchCommands(MatchState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        _state = state;
    }

    public void OrderMove(IEnumerable<Unit> units, GridPos destination)
    {
        ArgumentNullException.ThrowIfNull(units);

        foreach (var unit in units)
            unit.GiveOrder(new UnitOrder.Move(destination));
    }

    /// <summary>
    /// The context-sensitive order: works out what clicking a tile means for each selected unit.
    /// </summary>
    /// <remarks>
    /// One click has to serve attacking, chopping, mining, building and walking. Each unit resolves the
    /// click for itself, so a mixed selection of soldiers and citizens sent at a tree does the sensible
    /// thing for both instead of the same wrong thing for one of them.
    /// </remarks>
    public void OrderInteract(IEnumerable<Unit> units, GridPos tile)
    {
        ArgumentNullException.ThrowIfNull(units);

        if (!_state.Map.IsInBounds(tile))
            return;

        foreach (var unit in units)
            unit.GiveOrder(ResolveInteraction(unit, tile));
    }

    private UnitOrder ResolveInteraction(Unit unit, GridPos tile)
    {
        if (FindEnemyUnitAt(unit.OwnerIndex, tile) is { } enemyUnit && unit.Stats.CanFight)
            return new UnitOrder.AttackUnit(enemyUnit.Id);

        if (_state.BuildingAtTile(tile) is { } building)
        {
            if (building.OwnerIndex != unit.OwnerIndex)
                return unit.Stats.CanFight ? new UnitOrder.AttackBuilding(building.Id) : new UnitOrder.Move(tile);

            if (!building.IsComplete && unit.Stats.CanBuild)
                return new UnitOrder.BuildStructure(building.Id);

            if (building.Kind == BuildingKind.Mine && unit.Stats.CanWork && building.HasWorkerSlot)
                return new UnitOrder.WorkMine(building.Id);

            return new UnitOrder.Move(tile);
        }

        if (_state.Map.TileAt(tile).HasTrees && unit.Stats.CanWork)
            return new UnitOrder.GatherWood(tile);

        // A prospector sent to open ground surveys it; that is the only thing it is good for.
        if (unit.Stats.CanSurvey)
            return new UnitOrder.Survey(tile);

        return new UnitOrder.Move(tile);
    }

    /// <summary>Explicitly orders a survey, for the survey hotkey and for the CPU.</summary>
    public void OrderSurvey(IEnumerable<Unit> units, GridPos tile)
    {
        ArgumentNullException.ThrowIfNull(units);

        foreach (var unit in units.Where(unit => unit.Stats.CanSurvey))
            unit.GiveOrder(new UnitOrder.Survey(tile));
    }

    /// <summary>
    /// Places a foundation and puts the given builders to work on it.
    /// </summary>
    /// <param name="failureReason">Why the placement was refused, for the HUD to show. Null on success.</param>
    public bool TryPlaceBuilding(
        int playerIndex,
        BuildingKind kind,
        GridPos origin,
        IEnumerable<Unit> builders,
        out string? failureReason,
        out Building? placed)
    {
        ArgumentNullException.ThrowIfNull(builders);

        placed = null;
        var stats = BuildingCatalog.For(kind);
        var player = _state.PlayerAt(playerIndex);

        if (!_state.Map.IsFootprintBuildable(origin, stats.Size))
        {
            failureReason = "Cannot build there.";
            return false;
        }

        MineralKind? mineral = null;

        if (stats.RequiresMineral)
        {
            mineral = BestKnownMineral(player, origin, stats.Size);

            // The prospector's whole job: unsurveyed ground cannot be mined, even if it is rich.
            if (mineral is null)
            {
                failureReason = "Survey this ground for minerals first.";
                return false;
            }
        }

        if (!player.Resources.TrySpend(stats.BuildCost))
        {
            failureReason = $"Need {stats.BuildCost}.";
            return false;
        }

        placed = _state.SpawnBuilding(kind, playerIndex, origin, mineral, startCompleted: false);

        foreach (var builder in builders.Where(builder => builder.Stats.CanBuild && builder.OwnerIndex == playerIndex))
            builder.GiveOrder(new UnitOrder.BuildStructure(placed.Id));

        failureReason = null;
        return true;
    }

    /// <summary>
    /// The richest mineral the player has actually surveyed under a footprint, or null if none is known.
    /// </summary>
    public MineralKind? BestKnownMineral(Player player, GridPos origin, int size)
    {
        ArgumentNullException.ThrowIfNull(player);

        MineralKind? best = null;
        var bestAbundance = 0;

        for (var dy = 0; dy < size; dy++)
        {
            for (var dx = 0; dx < size; dx++)
            {
                var tile = new GridPos(origin.X + dx, origin.Y + dy);
                if (!player.Knowledge.IsSurveyed(tile))
                    continue;

                foreach (var mineral in Minerals.All)
                {
                    var abundance = player.Knowledge.KnownAbundance(mineral, tile);
                    if (abundance <= bestAbundance)
                        continue;

                    best = mineral;
                    bestAbundance = abundance;
                }
            }
        }

        return best;
    }

    /// <summary>Queues a unit for training, paying for it up front.</summary>
    public bool TryQueueTraining(Building building, UnitKind kind, out string? failureReason)
    {
        ArgumentNullException.ThrowIfNull(building);

        var stats = UnitCatalog.For(kind);

        if (!building.IsComplete)
        {
            failureReason = $"{building.Stats.DisplayName} is still under construction.";
            return false;
        }

        if (stats.TrainedAt != building.Kind)
        {
            failureReason = $"{building.Stats.DisplayName} cannot train a {stats.DisplayName}.";
            return false;
        }

        // Paid at queue time, refunded on cancel, so a long queue cannot be built on money already spent.
        if (!_state.PlayerAt(building.OwnerIndex).Resources.TrySpend(stats.TrainCost))
        {
            failureReason = $"Need {stats.TrainCost}.";
            return false;
        }

        building.EnqueueTraining(kind);
        failureReason = null;

        return true;
    }

    /// <summary>Cancels the last unit queued at a building and refunds it.</summary>
    public bool TryCancelLastTraining(Building building)
    {
        ArgumentNullException.ThrowIfNull(building);

        if (building.CancelLastQueued() is not { } cancelled)
            return false;

        _state.PlayerAt(building.OwnerIndex).Resources.Refund(UnitCatalog.For(cancelled).TrainCost);
        return true;
    }

    private Unit? FindEnemyUnitAt(int viewerIndex, GridPos tile)
    {
        foreach (var unit in _state.Units)
            if (unit.OwnerIndex != viewerIndex && unit.IsAlive && unit.Tile == tile)
                return unit;

        return null;
    }
}
