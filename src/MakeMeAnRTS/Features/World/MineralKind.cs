namespace MakeMeAnRTS.Features.World;

/// <summary>
/// The minerals a mine can extract. Each has its own abundance map across the world, which is what
/// the prospector surveys and the mineral heat map overlay draws.
/// </summary>
public enum MineralKind : byte
{
    Stone,
    Iron,
    Gold,
}

public static class Minerals
{
    /// <summary>All minerals in declaration order. Index matches <see cref="MineralKind"/>'s numeric value.</summary>
    public static readonly MineralKind[] All = Enum.GetValues<MineralKind>();

    public static string DisplayName(this MineralKind mineral) => mineral switch
    {
        MineralKind.Stone => "Stone",
        MineralKind.Iron => "Iron",
        MineralKind.Gold => "Gold",
        _ => throw new ArgumentOutOfRangeException(nameof(mineral), mineral, "Unhandled mineral kind."),
    };
}
