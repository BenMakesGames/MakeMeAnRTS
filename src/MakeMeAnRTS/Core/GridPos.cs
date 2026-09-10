namespace MakeMeAnRTS.Core;

/// <summary>Integer tile coordinate. May be off-map; callers check with <see cref="Grid2D{T}.IsInBounds"/>.</summary>
public readonly record struct GridPos(int X, int Y)
{
    /// <summary>Centre of the tile in world space, which is where units stand.</summary>
    public Vec2 Center => new(X + 0.5f, Y + 0.5f);

    public static GridPos operator +(GridPos a, GridPos b) => new(a.X + b.X, a.Y + b.Y);

    /// <summary>Chebyshev distance: the number of 8-directional steps between two tiles.</summary>
    public static int StepDistance(GridPos a, GridPos b) => Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));

    /// <summary>Manhattan distance, used where diagonal moves should not be free.</summary>
    public static int GridDistance(GridPos a, GridPos b) => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);

    /// <summary>The four orthogonal neighbour offsets, then the four diagonals.</summary>
    public static readonly GridPos[] Neighbors8 =
    [
        new(1, 0), new(-1, 0), new(0, 1), new(0, -1),
        new(1, 1), new(1, -1), new(-1, 1), new(-1, -1),
    ];

    public static readonly GridPos[] Neighbors4 = [new(1, 0), new(-1, 0), new(0, 1), new(0, -1)];

    public override string ToString() => $"({X},{Y})";
}
