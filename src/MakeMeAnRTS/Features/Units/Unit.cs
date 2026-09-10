using MakeMeAnRTS.Core;
using MakeMeAnRTS.Features.Match;

namespace MakeMeAnRTS.Features.Units;

/// <summary>One unit on the map, owned by one player.</summary>
public sealed class Unit
{
    private readonly List<GridPos> _path = [];

    public int Id { get; }
    public UnitKind Kind { get; }
    public int OwnerIndex { get; }
    public UnitStats Stats { get; }

    public Vec2 Position { get; set; }
    public float Health { get; set; }

    public UnitOrder Order { get; private set; } = UnitOrder.Idle.Instance;

    /// <summary>What the unit is hauling. Null when empty-handed.</summary>
    public ResourceKind? CarriedResource { get; set; }

    public int CarriedAmount { get; set; }

    /// <summary>Seconds until this unit may attack again.</summary>
    public float AttackCooldown { get; set; }

    /// <summary>Fractional progress towards the next unit of resource gathered.</summary>
    public float GatherProgress { get; set; }

    /// <summary>Seconds until the unit is willing to recompute its path, so a blocked unit does not thrash A*.</summary>
    public float RepathCooldown { get; set; }

    /// <summary>Where the unit is hauling its load. Null when it is not making a delivery.</summary>
    public int? DeliveryTargetId { get; set; }

    /// <summary>
    /// How long the unit has been unable to make progress towards its goal.
    /// </summary>
    /// <remarks>
    /// Pathfinding answers an unreachable request with the best partial route, which is right for a unit
    /// that should still walk as far as it can - but it means arrival never happens and the order would
    /// never end. This is what lets a unit conclude it cannot get there and give up.
    /// </remarks>
    public float BlockedSeconds { get; set; }

    public bool IsAlive => Health > 0f;
    public bool IsCarryingFullLoad => CarriedAmount >= Stats.CarryCapacity && Stats.CarryCapacity > 0;
    public GridPos Tile => Position.ToTile();

    /// <summary>The route the unit is walking, as remaining tiles. Empty when it has arrived or has no route.</summary>
    public IReadOnlyList<GridPos> Path => _path;

    public Unit(int id, UnitKind kind, int ownerIndex, Vec2 position)
    {
        Id = id;
        Kind = kind;
        OwnerIndex = ownerIndex;
        Stats = UnitCatalog.For(kind);
        Position = position;
        Health = Stats.MaxHealth;
    }

    /// <summary>
    /// Replaces the unit's order and abandons its current route.
    /// </summary>
    /// <remarks>
    /// Any carried load is kept: a woodcutter told to run away should not drop its wood, and it will
    /// deliver the load once it is given something to do again.
    /// </remarks>
    public void GiveOrder(UnitOrder order)
    {
        ArgumentNullException.ThrowIfNull(order);

        Order = order;
        ClearPath();
        RepathCooldown = 0f;
        BlockedSeconds = 0f;
    }

    public void SetPath(IEnumerable<GridPos> path)
    {
        ArgumentNullException.ThrowIfNull(path);

        _path.Clear();
        _path.AddRange(path);
    }

    public void ClearPath() => _path.Clear();

    /// <summary>Drops the first waypoint, once the unit has reached it.</summary>
    public void AdvancePath()
    {
        if (_path.Count > 0)
            _path.RemoveAt(0);
    }

    public void TakeDamage(float amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        Health = MathF.Max(0f, Health - amount);
    }

    /// <summary>Hands over the whole carried load and returns what was handed over.</summary>
    public (ResourceKind Resource, int Amount) TakeCarriedLoad()
    {
        var resource = CarriedResource ?? throw new InvalidOperationException($"Unit {Id} is not carrying anything to unload.");
        var amount = CarriedAmount;

        CarriedResource = null;
        CarriedAmount = 0;
        DeliveryTargetId = null;

        return (resource, amount);
    }
}
