namespace MakeMeAnRTS.Core;

/// <summary>World-space position in tile units. (1.0, 1.0) is the centre of tile (1, 1).</summary>
public readonly record struct Vec2(float X, float Y)
{
    public static readonly Vec2 Zero = new(0f, 0f);

    public float LengthSquared => X * X + Y * Y;
    public float Length => MathF.Sqrt(LengthSquared);

    /// <summary>Unit-length copy, or <see cref="Zero"/> for a zero-length vector (never NaN).</summary>
    public Vec2 Normalized()
    {
        var length = Length;
        return length > 1e-6f ? new Vec2(X / length, Y / length) : Zero;
    }

    public GridPos ToTile() => new((int)MathF.Floor(X), (int)MathF.Floor(Y));

    public static Vec2 operator +(Vec2 a, Vec2 b) => new(a.X + b.X, a.Y + b.Y);
    public static Vec2 operator -(Vec2 a, Vec2 b) => new(a.X - b.X, a.Y - b.Y);
    public static Vec2 operator *(Vec2 a, float s) => new(a.X * s, a.Y * s);

    public static float Distance(Vec2 a, Vec2 b) => (a - b).Length;
    public static float DistanceSquared(Vec2 a, Vec2 b) => (a - b).LengthSquared;
}
