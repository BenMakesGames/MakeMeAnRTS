using MakeMeAnRTS.Core;

namespace MakeMeAnRTS.Features.Pathfinding;

/// <summary>
/// Where a path is trying to get to.
/// </summary>
/// <remarks>
/// <see cref="Adjacent"/> exists because most things worth walking to cannot be stood on: a tree, a
/// mine, an enemy building. Asking for a path *to* them would always fail, so callers ask to get next
/// to them instead.
/// </remarks>
public readonly record struct PathGoal
{
    /// <summary>The tile the goal is centred on; also what the heuristic aims at.</summary>
    public GridPos Target { get; }

    /// <summary>Footprint size of the target, so a path can reach the edge of a multi-tile building.</summary>
    public int TargetSize { get; }

    /// <summary>True to stop next to the target rather than on it.</summary>
    public bool StopAdjacent { get; }

    private PathGoal(GridPos target, int targetSize, bool stopAdjacent)
    {
        Target = target;
        TargetSize = targetSize;
        StopAdjacent = stopAdjacent;
    }

    /// <summary>Walk onto <paramref name="tile"/> itself.</summary>
    public static PathGoal Exactly(GridPos tile) => new(tile, 1, stopAdjacent: false);

    /// <summary>Walk to any tile touching a <paramref name="size"/>-square footprint at <paramref name="origin"/>.</summary>
    public static PathGoal Adjacent(GridPos origin, int size = 1)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(size, 1);
        return new PathGoal(origin, size, stopAdjacent: true);
    }

    /// <summary>Whether <paramref name="tile"/> satisfies this goal.</summary>
    public bool IsSatisfiedBy(GridPos tile)
    {
        if (!StopAdjacent)
            return tile == Target;

        // Touching means within one step of the footprint on both axes, but not inside it.
        var insideX = tile.X >= Target.X && tile.X < Target.X + TargetSize;
        var insideY = tile.Y >= Target.Y && tile.Y < Target.Y + TargetSize;
        if (insideX && insideY)
            return false;

        var dx = tile.X < Target.X ? Target.X - tile.X : Math.Max(0, tile.X - (Target.X + TargetSize - 1));
        var dy = tile.Y < Target.Y ? Target.Y - tile.Y : Math.Max(0, tile.Y - (Target.Y + TargetSize - 1));

        return dx <= 1 && dy <= 1;
    }

    /// <summary>The point paths should aim at: the centre of the footprint.</summary>
    public Vec2 AimPoint => new(Target.X + TargetSize / 2f, Target.Y + TargetSize / 2f);
}
