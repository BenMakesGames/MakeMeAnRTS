using MakeMeAnRTS.Features.Buildings;
using MakeMeAnRTS.Features.Match;

namespace MakeMeAnRTS.Features.Units;

/// <summary>
/// The stat block for every unit kind - the game's balance, in one readable place.
/// </summary>
/// <remarks>
/// Both sides draw from this table: there are no faction differences, so a change here changes the game
/// for the player and the CPU equally.
/// </remarks>
public static class UnitCatalog
{
    private static readonly Dictionary<UnitKind, UnitStats> Stats = new()
    {
        [UnitKind.Citizen] = new UnitStats
        {
            DisplayName = "Citizen",
            Glyph = 'c',
            MaxHealth = 45f,
            MoveSpeed = 2.7f,
            VisionRadius = 6,
            // Citizens fight badly on purpose: enough to bother a lone scout, not enough to be an army.
            AttackDamage = 3f,
            AttackInterval = 1.4f,
            CarryCapacity = 10,
            GatherRate = 1.1f,
            CanBuild = true,
            TrainCost = ResourceAmounts.Of(wood: 50),
            TrainSeconds = 11f,
            TrainedAt = BuildingKind.HomeBase,
        },
        [UnitKind.Scout] = new UnitStats
        {
            DisplayName = "Scout",
            Glyph = 's',
            MaxHealth = 50f,
            // Fast and far-sighted, and deliberately useless for anything but looking.
            MoveSpeed = 5.2f,
            VisionRadius = 13,
            AttackDamage = 2f,
            AttackInterval = 1.5f,
            TrainCost = ResourceAmounts.Of(wood: 40, stone: 10),
            TrainSeconds = 10f,
            TrainedAt = BuildingKind.HomeBase,
        },
        [UnitKind.Prospector] = new UnitStats
        {
            DisplayName = "Prospector",
            Glyph = 'p',
            MaxHealth = 40f,
            MoveSpeed = 3.0f,
            VisionRadius = 7,
            CanSurvey = true,
            TrainCost = ResourceAmounts.Of(wood: 60, stone: 20),
            TrainSeconds = 14f,
            TrainedAt = BuildingKind.HomeBase,
        },
        [UnitKind.Soldier] = new UnitStats
        {
            DisplayName = "Soldier",
            Glyph = 'S',
            MaxHealth = 130f,
            MoveSpeed = 2.8f,
            VisionRadius = 8,
            AttackDamage = 13f,
            AttackRange = 1.1f,
            AttackInterval = 1.0f,
            TrainCost = ResourceAmounts.Of(wood: 40, iron: 30),
            TrainSeconds = 17f,
            TrainedAt = BuildingKind.Barracks,
        },
        [UnitKind.Archer] = new UnitStats
        {
            DisplayName = "Archer",
            Glyph = 'A',
            MaxHealth = 70f,
            MoveSpeed = 2.9f,
            VisionRadius = 10,
            // Hits softer than a soldier but from five tiles away, so archers need soldiers in front.
            AttackDamage = 9f,
            AttackRange = 5f,
            AttackInterval = 1.3f,
            TrainCost = ResourceAmounts.Of(wood: 50, iron: 15, gold: 10),
            TrainSeconds = 19f,
            TrainedAt = BuildingKind.ArcheryRange,
        },
    };

    public static readonly UnitKind[] All = Enum.GetValues<UnitKind>();

    public static UnitStats For(UnitKind kind) =>
        Stats.TryGetValue(kind, out var stats)
            ? stats
            : throw new ArgumentOutOfRangeException(nameof(kind), kind, "No stats defined for this unit kind.");

    /// <summary>The unit kinds a given building can train, in menu order.</summary>
    public static IEnumerable<UnitKind> TrainedAt(BuildingKind building) => All.Where(kind => For(kind).TrainedAt == building);
}
