using MakeMeAnRTS.Features.Buildings;
using MakeMeAnRTS.Features.Match;

namespace MakeMeAnRTS.Features.Units;

/// <summary>Everything that differs between one kind of unit and another.</summary>
public sealed record UnitStats
{
    public required string DisplayName { get; init; }

    /// <summary>Single letter drawn on the unit, so kinds are told apart without art.</summary>
    public required char Glyph { get; init; }

    public required float MaxHealth { get; init; }

    /// <summary>Tiles per second on open grass. Rough terrain slows this by the tile's movement cost.</summary>
    public required float MoveSpeed { get; init; }

    public required int VisionRadius { get; init; }

    /// <summary>Damage per hit. Zero marks a unit that cannot fight at all.</summary>
    public float AttackDamage { get; init; }

    /// <summary>Reach in tiles. 1 is melee.</summary>
    public float AttackRange { get; init; } = 1f;

    public float AttackInterval { get; init; } = 1f;

    /// <summary>How much wood or ore the unit can carry per trip. Zero marks a unit that cannot haul.</summary>
    public int CarryCapacity { get; init; }

    /// <summary>Resource units gathered per second while working.</summary>
    public float GatherRate { get; init; }

    public bool CanBuild { get; init; }
    public bool CanSurvey { get; init; }

    public required ResourceAmounts TrainCost { get; init; }
    public required float TrainSeconds { get; init; }

    /// <summary>Which building trains this unit.</summary>
    public required BuildingKind TrainedAt { get; init; }

    public bool CanFight => AttackDamage > 0f;
    public bool CanWork => CarryCapacity > 0 && GatherRate > 0f;
}
