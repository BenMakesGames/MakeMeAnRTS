using MakeMeAnRTS.Features.World;

namespace MakeMeAnRTS.Features.Match;

/// <summary>What players stockpile. Wood comes from trees; the rest come out of mines.</summary>
public enum ResourceKind : byte
{
    Wood,
    Stone,
    Iron,
    Gold,
}

public static class Resources
{
    public static readonly ResourceKind[] All = Enum.GetValues<ResourceKind>();

    public static string DisplayName(this ResourceKind resource) => resource switch
    {
        ResourceKind.Wood => "Wood",
        ResourceKind.Stone => "Stone",
        ResourceKind.Iron => "Iron",
        ResourceKind.Gold => "Gold",
        _ => throw new ArgumentOutOfRangeException(nameof(resource), resource, "Unhandled resource kind."),
    };

    /// <summary>The stockpile a mined mineral lands in.</summary>
    public static ResourceKind ToResource(this MineralKind mineral) => mineral switch
    {
        MineralKind.Stone => ResourceKind.Stone,
        MineralKind.Iron => ResourceKind.Iron,
        MineralKind.Gold => ResourceKind.Gold,
        _ => throw new ArgumentOutOfRangeException(nameof(mineral), mineral, "Unhandled mineral kind."),
    };
}
