using MakeMeAnRTS.Core;
using MakeMeAnRTS.Features.Buildings;
using MakeMeAnRTS.Features.Match;
using MakeMeAnRTS.Features.Pathfinding;
using MakeMeAnRTS.Features.World;

namespace MakeMeAnRTS.Features.Units;

/// <summary>
/// Advances every unit one step: walking, working, hauling, building, surveying and fighting.
/// </summary>
/// <remarks>
/// Each update re-derives what a unit should be doing from its order plus what it is carrying, rather
/// than tracking a separate "phase". A woodcutter with a full load walks to a drop-off; the same unit
/// with empty hands walks back to the trees. That means there is no phase to get out of step with
/// reality when a tree runs out, a drop-off is destroyed, or the player interrupts with a new order.
/// </remarks>
public sealed class UnitUpdater
{
    /// <summary>Seconds between path recomputations for a unit that cannot make progress.</summary>
    private const float RepathInterval = 0.8f;

    /// <summary>How close to a waypoint's centre counts as having reached it, in tiles.</summary>
    private const float WaypointReachedDistance = 0.08f;

    /// <summary>How long a unit tries to reach somewhere it cannot get to before abandoning the order.</summary>
    private const float GiveUpSeconds = 2.5f;

    private readonly MatchState _state;

    public UnitUpdater(MatchState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        _state = state;
    }

    public void Update(float deltaSeconds)
    {
        // A copy, because combat and completed buildings can remove units mid-update.
        foreach (var unit in _state.Units.ToList())
        {
            if (!unit.IsAlive)
                continue;

            unit.AttackCooldown = MathF.Max(0f, unit.AttackCooldown - deltaSeconds);
            unit.RepathCooldown = MathF.Max(0f, unit.RepathCooldown - deltaSeconds);

            UpdateOne(unit, deltaSeconds);
        }
    }

    private void UpdateOne(Unit unit, float deltaSeconds)
    {
        // A full load outranks everything except an explicit move or attack order: haul it home first.
        if (unit.IsCarryingFullLoad && OrderAllowsHauling(unit.Order))
        {
            DeliverLoad(unit, deltaSeconds);
            return;
        }

        switch (unit.Order)
        {
            case UnitOrder.Idle:
                UpdateIdle(unit, deltaSeconds);
                break;

            case UnitOrder.Move move:
                // Arriving and giving up both end the order; the unit stops either way.
                if (StepTowards(unit, PathGoal.Exactly(move.Destination), deltaSeconds) != MoveOutcome.Moving)
                    unit.GiveOrder(UnitOrder.Idle.Instance);
                break;

            case UnitOrder.GatherWood gather:
                UpdateGatherWood(unit, gather, deltaSeconds);
                break;

            case UnitOrder.WorkMine mine:
                UpdateWorkMine(unit, mine, deltaSeconds);
                break;

            case UnitOrder.BuildStructure build:
                UpdateBuild(unit, build, deltaSeconds);
                break;

            case UnitOrder.Survey survey:
                UpdateSurvey(unit, survey, deltaSeconds);
                break;

            case UnitOrder.AttackUnit attack:
                UpdateAttackUnit(unit, attack, deltaSeconds);
                break;

            case UnitOrder.AttackBuilding attack:
                UpdateAttackBuilding(unit, attack, deltaSeconds);
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(unit), unit.Order, "Unhandled unit order.");
        }
    }

    /// <summary>Move and attack orders are the player overriding the unit, so hauling waits.</summary>
    private static bool OrderAllowsHauling(UnitOrder order) =>
        order is UnitOrder.Idle or UnitOrder.GatherWood or UnitOrder.WorkMine;

    private void UpdateIdle(Unit unit, float deltaSeconds)
    {
        // Anything still holding a partial load walks it home rather than standing around with it.
        if (unit.CarriedAmount > 0)
        {
            DeliverLoad(unit, deltaSeconds);
            return;
        }

        if (!unit.Stats.CanFight)
            return;

        var target = FindNearestEnemyUnitInVision(unit);
        if (target is not null)
            unit.GiveOrder(new UnitOrder.AttackUnit(target.Id));
    }

    private void UpdateGatherWood(Unit unit, UnitOrder.GatherWood order, float deltaSeconds)
    {
        if (!unit.Stats.CanWork)
        {
            unit.GiveOrder(UnitOrder.Idle.Instance);
            return;
        }

        // Logged out: move on to the nearest remaining trees rather than waiting on a bare tile forever.
        if (!_state.Map.TileAt(order.Tree).HasTrees)
        {
            var nextStand = _state.FindNearestTrees(order.Tree);

            unit.GiveOrder(nextStand is null ? UnitOrder.Idle.Instance : new UnitOrder.GatherWood(nextStand.Value));
            return;
        }

        // Trees block movement, so the goal is a tile beside them, not the tile itself.
        switch (StepTowards(unit, PathGoal.Adjacent(order.Tree), deltaSeconds))
        {
            case MoveOutcome.Moving:
                return;

            case MoveOutcome.GaveUp:
                // Cannot get to this stand - try the nearest other one rather than standing here.
                RetargetWood(unit, order.Tree);
                return;
        }

        unit.GatherProgress += unit.Stats.GatherRate * deltaSeconds;

        var whole = (int)unit.GatherProgress;
        if (whole <= 0)
            return;

        unit.GatherProgress -= whole;

        var room = unit.Stats.CarryCapacity - unit.CarriedAmount;
        var cut = _state.Map.CutWood(order.Tree, Math.Min(whole, room));
        if (cut <= 0)
            return;

        unit.CarriedResource = ResourceKind.Wood;
        unit.CarriedAmount += cut;
    }

    private void UpdateWorkMine(Unit unit, UnitOrder.WorkMine order, float deltaSeconds)
    {
        var mine = _state.FindBuilding(order.MineId);

        if (mine is null || !mine.IsAlive || mine.OwnerIndex != unit.OwnerIndex || mine.MinedMineral is null)
        {
            unit.GiveOrder(UnitOrder.Idle.Instance);
            return;
        }

        // An unfinished mine needs building before it needs working; do the obvious thing.
        if (!mine.IsComplete)
        {
            if (unit.Stats.CanBuild)
                unit.GiveOrder(new UnitOrder.BuildStructure(mine.Id));
            else
                unit.GiveOrder(UnitOrder.Idle.Instance);

            return;
        }

        if (!unit.Stats.CanWork || !mine.TryAssignWorker(unit.Id))
        {
            unit.GiveOrder(UnitOrder.Idle.Instance);
            return;
        }

        var approach = StepTowards(unit, PathGoal.Adjacent(mine.Origin, mine.Stats.Size), deltaSeconds);

        if (approach == MoveOutcome.GaveUp)
        {
            mine.RemoveWorker(unit.Id);
            unit.GiveOrder(UnitOrder.Idle.Instance);
            return;
        }

        if (approach == MoveOutcome.Moving)
            return;

        unit.GatherProgress += unit.Stats.GatherRate * deltaSeconds;

        var whole = (int)unit.GatherProgress;
        if (whole <= 0)
            return;

        unit.GatherProgress -= whole;

        var mineral = mine.MinedMineral.Value;
        var richest = RichestFootprintTile(mine, mineral);

        if (richest is null)
        {
            // Worked out. Free the slot so the player's citizens do not idle inside a dead mine.
            mine.RemoveWorker(unit.Id);
            unit.GiveOrder(UnitOrder.Idle.Instance);
            return;
        }

        var room = unit.Stats.CarryCapacity - unit.CarriedAmount;
        var extracted = _state.Map.ExtractMineral(mineral, richest.Value, Math.Min(whole, room));
        if (extracted <= 0)
            return;

        unit.CarriedResource = mineral.ToResource();
        unit.CarriedAmount += extracted;

        // Mining is its own survey: what the ground actually yielded updates the owner's heat map.
        _state.PlayerAt(unit.OwnerIndex).Knowledge.RecordObservation(mineral, richest.Value, _state.Map.MineralAt(mineral, richest.Value));
    }

    private void UpdateBuild(Unit unit, UnitOrder.BuildStructure order, float deltaSeconds)
    {
        var site = _state.FindBuilding(order.BuildingId);

        if (site is null || !site.IsAlive || site.OwnerIndex != unit.OwnerIndex || !unit.Stats.CanBuild)
        {
            unit.GiveOrder(UnitOrder.Idle.Instance);
            return;
        }

        if (site.IsComplete)
        {
            OnSiteFinished(unit, site);
            return;
        }

        switch (StepTowards(unit, PathGoal.Adjacent(site.Origin, site.Stats.Size), deltaSeconds))
        {
            case MoveOutcome.Moving:
                return;

            case MoveOutcome.GaveUp:
                unit.GiveOrder(UnitOrder.Idle.Instance);
                return;
        }

        if (site.AddBuildProgress(deltaSeconds))
            OnSiteFinished(unit, site);
    }

    /// <summary>
    /// Decides what a builder does once its building is finished.
    /// </summary>
    /// <remarks>
    /// Whoever builds a mine starts working it. Building a mine and then finding it idle because nobody
    /// was assigned is a small, repeated annoyance, and "the people who built it work it" is what a
    /// player expects anyway.
    /// </remarks>
    private static void OnSiteFinished(Unit unit, Building site)
    {
        var shouldStaffIt = site.Kind == BuildingKind.Mine && unit.Stats.CanWork && site.HasWorkerSlot;

        unit.GiveOrder(shouldStaffIt ? new UnitOrder.WorkMine(site.Id) : UnitOrder.Idle.Instance);
    }

    private void UpdateSurvey(Unit unit, UnitOrder.Survey order, float deltaSeconds)
    {
        if (!unit.Stats.CanSurvey)
        {
            unit.GiveOrder(UnitOrder.Idle.Instance);
            return;
        }

        // Survey from beside the spot if it cannot be stood on, so a reading over water still works.
        var goal = _state.Map.IsWalkable(order.Target) ? PathGoal.Exactly(order.Target) : PathGoal.Adjacent(order.Target);

        switch (StepTowards(unit, goal, deltaSeconds))
        {
            case MoveOutcome.Moving:
                return;

            case MoveOutcome.GaveUp:
                // Unreachable ground stays unsurveyed; whoever gave the order can choose somewhere else.
                unit.GiveOrder(UnitOrder.Idle.Instance);
                return;
        }

        var player = _state.PlayerAt(unit.OwnerIndex);
        var readings = player.Knowledge.Survey(_state.Map, order.Target, SurveyRadius);

        _state.AddSign(unit.OwnerIndex, order.Target, readings);
        unit.GiveOrder(UnitOrder.Idle.Instance);
    }

    /// <summary>
    /// Radius of ground a single survey reads.
    /// </summary>
    /// <remarks>
    /// Sized so that surveying a map is a real task but not a career: at radius 7 a lone prospector read
    /// about 400 tiles of a 30,000-tile map in twenty-five minutes, which left both sides unable to find
    /// anything worth mining.
    /// </remarks>
    public const int SurveyRadius = 10;

    private void UpdateAttackUnit(Unit unit, UnitOrder.AttackUnit order, float deltaSeconds)
    {
        var target = _state.FindUnit(order.TargetUnitId);

        if (target is null || !target.IsAlive || target.OwnerIndex == unit.OwnerIndex)
        {
            unit.GiveOrder(UnitOrder.Idle.Instance);
            return;
        }

        if (!unit.Stats.CanFight)
        {
            unit.GiveOrder(UnitOrder.Idle.Instance);
            return;
        }

        var distance = Vec2.Distance(unit.Position, target.Position);

        if (distance > unit.Stats.AttackRange)
        {
            StepTowards(unit, PathGoal.Adjacent(target.Tile), deltaSeconds);
            return;
        }

        unit.ClearPath();

        if (unit.AttackCooldown > 0f)
            return;

        target.TakeDamage(unit.Stats.AttackDamage);
        unit.AttackCooldown = unit.Stats.AttackInterval;
    }

    private void UpdateAttackBuilding(Unit unit, UnitOrder.AttackBuilding order, float deltaSeconds)
    {
        var target = _state.FindBuilding(order.TargetBuildingId);

        if (target is null || !target.IsAlive || target.OwnerIndex == unit.OwnerIndex || !unit.Stats.CanFight)
        {
            unit.GiveOrder(UnitOrder.Idle.Instance);
            return;
        }

        // Measured to the footprint's edge, or long buildings would be unreachable by melee units.
        var distance = DistanceToFootprint(unit.Position, target);

        if (distance > unit.Stats.AttackRange)
        {
            StepTowards(unit, PathGoal.Adjacent(target.Origin, target.Stats.Size), deltaSeconds);
            return;
        }

        unit.ClearPath();

        if (unit.AttackCooldown > 0f)
            return;

        target.TakeDamage(unit.Stats.AttackDamage);
        unit.AttackCooldown = unit.Stats.AttackInterval;
    }

    private void DeliverLoad(Unit unit, float deltaSeconds)
    {
        if (unit.CarriedResource is not { } resource || unit.CarriedAmount <= 0)
        {
            unit.DeliveryTargetId = null;
            return;
        }

        var dropOff = unit.DeliveryTargetId is { } id ? _state.FindBuilding(id) : null;

        // Re-pick the drop-off whenever the remembered one is gone, so a destroyed depot is not fatal.
        if (dropOff is null || !dropOff.IsAlive || !dropOff.IsComplete || !dropOff.Stats.Accepts(resource))
        {
            dropOff = _state.FindNearestDropOff(unit.OwnerIndex, resource, unit.Position);
            unit.DeliveryTargetId = dropOff?.Id;

            if (dropOff is null)
                return;

            unit.ClearPath();
        }

        switch (StepTowards(unit, PathGoal.Adjacent(dropOff.Origin, dropOff.Stats.Size), deltaSeconds))
        {
            case MoveOutcome.Moving:
                return;

            case MoveOutcome.GaveUp:
                // This depot is cut off; forget it so the next update picks a different one.
                unit.DeliveryTargetId = null;
                unit.BlockedSeconds = 0f;
                return;
        }

        var (delivered, amount) = unit.TakeCarriedLoad();
        _state.PlayerAt(unit.OwnerIndex).Resources.Add(delivered, amount);
    }

    /// <summary>What one step of movement achieved.</summary>
    private enum MoveOutcome
    {
        /// <summary>Still on its way.</summary>
        Moving,

        /// <summary>Standing somewhere that satisfies the goal.</summary>
        Arrived,

        /// <summary>Spent long enough failing to get closer that the goal should be treated as unreachable.</summary>
        GaveUp,
    }

    /// <summary>
    /// Walks the unit towards <paramref name="goal"/>, pathing as needed.
    /// </summary>
    private MoveOutcome StepTowards(Unit unit, PathGoal goal, float deltaSeconds)
    {
        if (goal.IsSatisfiedBy(unit.Tile))
        {
            unit.ClearPath();
            unit.BlockedSeconds = 0f;

            return MoveOutcome.Arrived;
        }

        if (unit.Path.Count == 0)
        {
            if (unit.RepathCooldown > 0f)
                return Stall(unit, deltaSeconds);

            unit.RepathCooldown = RepathInterval;
            unit.SetPath(_state.Pathfinder.FindPath(unit.Tile, goal));

            // Nowhere to go: hold position rather than jittering against the obstruction.
            if (unit.Path.Count == 0)
                return Stall(unit, deltaSeconds);
        }

        var waypoint = unit.Path[0];

        // The route was computed against a map that has since changed - build it again.
        if (!_state.Map.IsWalkable(waypoint) && waypoint != unit.Tile)
        {
            unit.ClearPath();
            return Stall(unit, deltaSeconds);
        }

        var target = waypoint.Center;
        var toTarget = target - unit.Position;
        var distance = toTarget.Length;

        // Rough ground costs more to cross, using the same numbers pathfinding used to choose this route.
        var step = unit.Stats.MoveSpeed / MathF.Max(1f, _state.Map.TileAt(waypoint).MoveCost) * deltaSeconds;

        // Real movement happened, so whatever was blocking the unit no longer is.
        unit.BlockedSeconds = 0f;

        if (distance <= MathF.Max(step, WaypointReachedDistance))
        {
            unit.Position = target;
            unit.AdvancePath();

            return goal.IsSatisfiedBy(unit.Tile) ? MoveOutcome.Arrived : MoveOutcome.Moving;
        }

        unit.Position += toTarget.Normalized() * step;
        return MoveOutcome.Moving;
    }

    /// <summary>
    /// Counts time spent going nowhere, and reports failure once it has gone on long enough.
    /// </summary>
    /// <remarks>
    /// A moment of this is normal - a doorway is briefly crowded, a route needs rebuilding after a
    /// building went up. Seconds of it means the goal cannot be reached at all, and the order has to end
    /// or the unit stands there for the rest of the match.
    /// </remarks>
    private static MoveOutcome Stall(Unit unit, float deltaSeconds)
    {
        unit.BlockedSeconds += deltaSeconds;

        return unit.BlockedSeconds >= GiveUpSeconds ? MoveOutcome.GaveUp : MoveOutcome.Moving;
    }

    /// <summary>Sends a woodcutter to a different stand, avoiding the one it could not get to.</summary>
    private void RetargetWood(Unit unit, GridPos unreachable)
    {
        var replacement = _state.FindNearestTrees(unit.Tile, skip: new HashSet<GridPos> { unreachable });

        unit.GiveOrder(replacement is null ? UnitOrder.Idle.Instance : new UnitOrder.GatherWood(replacement.Value));
    }

    private Unit? FindNearestEnemyUnitInVision(Unit unit)
    {
        Unit? best = null;
        var bestDistance = (float)(unit.Stats.VisionRadius * unit.Stats.VisionRadius);

        foreach (var candidate in _state.Units)
        {
            if (candidate.OwnerIndex == unit.OwnerIndex || !candidate.IsAlive)
                continue;

            var distance = Vec2.DistanceSquared(unit.Position, candidate.Position);
            if (distance >= bestDistance)
                continue;

            best = candidate;
            bestDistance = distance;
        }

        return best;
    }

    /// <summary>The tile of a mine's footprint holding the most of its mineral, or null once it is worked out.</summary>
    private GridPos? RichestFootprintTile(Building mine, MineralKind mineral)
    {
        GridPos? best = null;
        byte bestAbundance = 0;

        foreach (var tile in mine.FootprintTiles())
        {
            var abundance = _state.Map.MineralAt(mineral, tile);
            if (abundance <= bestAbundance)
                continue;

            best = tile;
            bestAbundance = abundance;
        }

        return best;
    }

    /// <summary>Distance from a point to the nearest edge of a building's footprint, in tiles.</summary>
    private static float DistanceToFootprint(Vec2 from, Building building)
    {
        var minX = building.Origin.X;
        var minY = building.Origin.Y;
        var maxX = building.Origin.X + building.Stats.Size;
        var maxY = building.Origin.Y + building.Stats.Size;

        var dx = MathF.Max(0f, MathF.Max(minX - from.X, from.X - maxX));
        var dy = MathF.Max(0f, MathF.Max(minY - from.Y, from.Y - maxY));

        return MathF.Sqrt(dx * dx + dy * dy);
    }
}
