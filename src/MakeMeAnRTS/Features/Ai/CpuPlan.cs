using MakeMeAnRTS.Features.Buildings;
using MakeMeAnRTS.Features.Units;

namespace MakeMeAnRTS.Features.Ai;

/// <summary>
/// The CPU's opening: what to build, in what order, and how many citizens to support it.
/// </summary>
/// <remarks>
/// Kept as data rather than buried in branches so the opponent's behaviour can be read and retuned in
/// one place. The CPU works down this list, skipping anything it cannot afford or place yet, so a
/// blocked step delays the plan instead of stopping it.
/// </remarks>
public sealed record CpuPlan
{
    /// <summary>Working mines to keep. Mines exhaust their footprint, so this is a running target.</summary>
    public required int TargetMines { get; init; }

    /// <summary>Lumberjacks to keep, to shorten the walk from the woods.</summary>
    public required int TargetLumberjacks { get; init; }

    /// <summary>Barracks to keep once the economy can support them.</summary>
    public required int TargetBarracks { get; init; }

    /// <summary>Archery ranges to keep. Built after the first barracks.</summary>
    public required int TargetArcheryRanges { get; init; }

    /// <summary>Citizens to keep alive. Above this, production switches to soldiers.</summary>
    public required int TargetCitizens { get; init; }

    /// <summary>Military units to gather before attacking.</summary>
    public required int AttackArmySize { get; init; }

    /// <summary>How many citizens to keep cutting wood rather than mining.</summary>
    public required int WoodcuttersWanted { get; init; }

    /// <summary>Seconds between the CPU reconsidering its situation.</summary>
    public float ThinkInterval { get; init; } = 1.0f;

    /// <summary>Military kinds to train, cycled through so the army stays mixed.</summary>
    public IReadOnlyList<UnitKind> MilitaryMix { get; init; } = [UnitKind.Soldier, UnitKind.Soldier, UnitKind.Archer];

    /// <summary>A steady opening: wood, then mining, then an army.</summary>
    public static readonly CpuPlan Standard = new()
    {
        TargetMines = 3,
        TargetLumberjacks = 2,
        TargetBarracks = 2,
        TargetArcheryRanges = 1,
        TargetCitizens = 12,
        AttackArmySize = 8,
        WoodcuttersWanted = 5,
    };
}
