using MakeMeAnRTS.Core;

namespace MakeMeAnRTS.Features.Units;

/// <summary>
/// What a unit is currently trying to do.
/// </summary>
/// <remarks>
/// An order is the player's intent, not a step of a plan. "Cut that wood" survives the walk out, the
/// chopping, the walk back to a drop-off and the walk out again; the unit updater derives each step
/// from the order plus what the unit happens to be carrying. Keeping intent and steps separate is what
/// lets a woodcutter keep working without any further input.
/// </remarks>
public abstract record UnitOrder
{
    /// <summary>Nothing to do. Combat units still defend themselves.</summary>
    public sealed record Idle : UnitOrder
    {
        public static readonly Idle Instance = new();
    }

    /// <summary>Walk to a tile and stop.</summary>
    public sealed record Move(GridPos Destination) : UnitOrder;

    /// <summary>Cut the trees on a tile, hauling each load to the nearest drop-off, until they are gone.</summary>
    public sealed record GatherWood(GridPos Tree) : UnitOrder;

    /// <summary>Work a mine, hauling each load to the nearest drop-off.</summary>
    public sealed record WorkMine(int MineId) : UnitOrder;

    /// <summary>Help raise a building under construction.</summary>
    public sealed record BuildStructure(int BuildingId) : UnitOrder;

    /// <summary>Walk to a spot and survey it for minerals, leaving a sign behind.</summary>
    public sealed record Survey(GridPos Target) : UnitOrder;

    /// <summary>Chase and attack an enemy unit.</summary>
    public sealed record AttackUnit(int TargetUnitId) : UnitOrder;

    /// <summary>Attack an enemy building.</summary>
    public sealed record AttackBuilding(int TargetBuildingId) : UnitOrder;
}
