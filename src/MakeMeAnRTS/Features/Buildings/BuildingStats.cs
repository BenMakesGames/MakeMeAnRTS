using MakeMeAnRTS.Features.Match;

namespace MakeMeAnRTS.Features.Buildings;

/// <summary>Everything that differs between one kind of building and another.</summary>
public sealed record BuildingStats
{
    public required string DisplayName { get; init; }

    /// <summary>Footprint edge length in tiles. Buildings are always square.</summary>
    public required int Size { get; init; }

    public required float MaxHealth { get; init; }

    public required ResourceAmounts BuildCost { get; init; }

    /// <summary>Worker-seconds of labour to finish. Two builders halve the wall-clock time.</summary>
    public required float BuildSeconds { get; init; }

    public required int VisionRadius { get; init; }

    /// <summary>Resources this building accepts from a hauler. Empty means it is not a drop-off.</summary>
    public IReadOnlyList<ResourceKind> AcceptsDeliveries { get; init; } = [];

    /// <summary>How many workers can be assigned to produce here. Only mines use this.</summary>
    public int MaxWorkers { get; init; }

    /// <summary>Must be placed on ground bearing the mineral it extracts.</summary>
    public bool RequiresMineral { get; init; }

    /// <summary>
    /// May be built on bare rock, which nothing else can. Mines only, and they need it: minerals favour
    /// high ground and high ground is rock.
    /// </summary>
    public bool AllowsRockyGround { get; init; }

    /// <summary>Hotkey shown in the build menu and accepted while a builder is selected.</summary>
    public required char BuildHotkey { get; init; }

    public bool IsDropOff => AcceptsDeliveries.Count > 0;

    public bool Accepts(ResourceKind resource) => AcceptsDeliveries.Contains(resource);
}
