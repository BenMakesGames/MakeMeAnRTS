namespace MakeMeAnRTS.Features.World;

/// <summary>What a tile is made of. One value per tile; trees and minerals are separate layers.</summary>
public enum TerrainKind : byte
{
    Grass,

    /// <summary>Dry ground on the leeward side of hills. Walkable, buildable, grows no trees.</summary>
    Dirt,

    /// <summary>Shoreline. Walkable and buildable, but bare.</summary>
    Sand,

    /// <summary>High, stony ground. Walkable but nothing can be built on it; where stone is richest.</summary>
    Rock,

    /// <summary>Lake or sea. Impassable and unbuildable.</summary>
    Water,

    /// <summary>Flowing water. Impassable except where a bridge crosses it.</summary>
    River,

    /// <summary>A river crossing. Walkable, but nothing may be built on it.</summary>
    Bridge,
}

public static class TerrainKindExtensions
{
    /// <summary>Whether land units can occupy the tile at all.</summary>
    public static bool IsWalkable(this TerrainKind terrain) => terrain is not (TerrainKind.Water or TerrainKind.River);

    /// <summary>Whether a building's footprint may cover the tile. Stricter than walkability.</summary>
    public static bool IsBuildable(this TerrainKind terrain) => terrain is TerrainKind.Grass or TerrainKind.Dirt or TerrainKind.Sand;

    public static bool IsWater(this TerrainKind terrain) => terrain is TerrainKind.Water or TerrainKind.River;

    /// <summary>
    /// Relative movement cost, used both by pathfinding and by actual movement speed so the route a
    /// unit picks matches the route that is actually fastest.
    /// </summary>
    public static float MoveCost(this TerrainKind terrain) => terrain switch
    {
        TerrainKind.Grass => 1.0f,
        TerrainKind.Dirt => 1.0f,
        TerrainKind.Sand => 1.2f,
        TerrainKind.Rock => 1.5f,
        TerrainKind.Bridge => 1.0f,
        TerrainKind.Water or TerrainKind.River => float.PositiveInfinity,
        _ => throw new ArgumentOutOfRangeException(nameof(terrain), terrain, "Unhandled terrain kind."),
    };
}
