namespace MakeMeAnRTS.Features.World;

/// <summary>
/// One map cell. Kept small and blittable: a 192x192 map is ~37k of these and they are walked every
/// frame by rendering and constantly by pathfinding.
/// </summary>
public struct Tile
{
    public TerrainKind Terrain;

    /// <summary>Elevation 0-255. Drives generation (water pools low, rivers run downhill) and shading.</summary>
    public byte Height;

    /// <summary>Wood remaining in the tile's trees. 0 means clear ground; anything above means woods.</summary>
    public byte Wood;

    public readonly bool HasTrees => Wood > 0;

    /// <summary>Trees block movement, so woods are obstacles until they are cut down.</summary>
    public readonly bool IsWalkable => Terrain.IsWalkable() && Wood == 0;

    public readonly bool IsBuildable => Terrain.IsBuildable() && Wood == 0;
}
