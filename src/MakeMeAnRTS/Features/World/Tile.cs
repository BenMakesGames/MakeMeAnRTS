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

    /// <summary>
    /// Whether a land unit can be here. Woods slow movement rather than blocking it.
    /// </summary>
    /// <remarks>
    /// Trees blocked movement at first, which made woods proper obstacles and read well on paper. In
    /// practice it walled bases in: a start in a forest clearing left scouts and prospectors unable to
    /// leave until citizens had chopped an exit, and both sides spent whole matches wandering their own
    /// back garden. Slowing movement keeps woods meaningful for routing without ever trapping anyone.
    /// </remarks>
    public readonly bool IsWalkable => Terrain.IsWalkable();

    /// <summary>Trees still have to be cleared before anything can be built on the ground beneath them.</summary>
    public readonly bool IsBuildable => Terrain.IsBuildable() && Wood == 0;

    /// <summary>Extra movement cost for pushing through woods, on top of the terrain's own cost.</summary>
    private const float WoodedPenalty = 2.2f;

    /// <summary>
    /// What it costs to cross this tile. Used by pathfinding and by movement itself, so the route a unit
    /// picks is the route that is actually fastest for it.
    /// </summary>
    public readonly float MoveCost => Terrain.MoveCost() * (HasTrees ? WoodedPenalty : 1f);
}
