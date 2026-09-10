using MakeMeAnRTS.Features.Match;

namespace MakeMeAnRTS.Features.Buildings;

/// <summary>The stat block for every building kind. Same table for both sides; there are no factions.</summary>
public static class BuildingCatalog
{
    private static readonly ResourceKind[] EverySort = Resources.All;
    private static readonly ResourceKind[] WoodOnly = [ResourceKind.Wood];

    private static readonly Dictionary<BuildingKind, BuildingStats> Stats = new()
    {
        [BuildingKind.HomeBase] = new BuildingStats
        {
            DisplayName = "Home Base",
            Size = 3,
            MaxHealth = 900f,
            // Expensive enough that a second base is a real decision, not an opening move.
            BuildCost = ResourceAmounts.Of(wood: 300, stone: 150),
            BuildSeconds = 90f,
            VisionRadius = 11,
            AcceptsDeliveries = EverySort,
            BuildHotkey = 'H',
        },
        [BuildingKind.Lumberjack] = new BuildingStats
        {
            DisplayName = "Lumberjack",
            Size = 2,
            MaxHealth = 250f,
            // Cheap on purpose: shortening the walk to the woods should always be worth doing.
            BuildCost = ResourceAmounts.Of(wood: 80),
            BuildSeconds = 22f,
            VisionRadius = 6,
            AcceptsDeliveries = WoodOnly,
            BuildHotkey = 'L',
        },
        [BuildingKind.Mine] = new BuildingStats
        {
            DisplayName = "Mine",
            Size = 2,
            MaxHealth = 300f,
            BuildCost = ResourceAmounts.Of(wood: 100, stone: 40),
            BuildSeconds = 30f,
            VisionRadius = 5,
            MaxWorkers = 4,
            RequiresMineral = true,
            BuildHotkey = 'M',
        },
        [BuildingKind.Barracks] = new BuildingStats
        {
            DisplayName = "Barracks",
            Size = 3,
            MaxHealth = 520f,
            BuildCost = ResourceAmounts.Of(wood: 150, stone: 80),
            BuildSeconds = 45f,
            VisionRadius = 7,
            BuildHotkey = 'B',
        },
        [BuildingKind.ArcheryRange] = new BuildingStats
        {
            DisplayName = "Archery Range",
            Size = 3,
            MaxHealth = 450f,
            BuildCost = ResourceAmounts.Of(wood: 150, stone: 50, iron: 40),
            BuildSeconds = 45f,
            VisionRadius = 8,
            BuildHotkey = 'R',
        },
    };

    public static readonly BuildingKind[] All = Enum.GetValues<BuildingKind>();

    /// <summary>What a player can order built, in build-menu order. The starting base is not on the list.</summary>
    public static readonly BuildingKind[] Buildable =
        [BuildingKind.Lumberjack, BuildingKind.Mine, BuildingKind.Barracks, BuildingKind.ArcheryRange, BuildingKind.HomeBase];

    public static BuildingStats For(BuildingKind kind) =>
        Stats.TryGetValue(kind, out var stats)
            ? stats
            : throw new ArgumentOutOfRangeException(nameof(kind), kind, "No stats defined for this building kind.");

    /// <summary>Looks up a buildable kind by its hotkey, for the build menu.</summary>
    public static BuildingKind? ByHotkey(char hotkey)
    {
        var upper = char.ToUpperInvariant(hotkey);

        foreach (var kind in Buildable)
            if (For(kind).BuildHotkey == upper)
                return kind;

        return null;
    }
}
