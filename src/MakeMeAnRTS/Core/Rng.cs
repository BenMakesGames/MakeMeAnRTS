namespace MakeMeAnRTS.Core;

/// <summary>
/// Deterministic PCG-XSH-RR generator. The same seed always yields the same sequence, so a map seed
/// fully describes a map and a bug in generation can be reproduced from its seed alone.
/// </summary>
public sealed class Rng
{
    private const ulong Multiplier = 6364136223846793005ul;
    private const ulong Increment = 1442695040888963407ul;

    private ulong _state;

    public int Seed { get; }

    public Rng(int seed)
    {
        Seed = seed;
        _state = unchecked((ulong)seed * Multiplier + Increment);
        NextUInt();
    }

    public uint NextUInt()
    {
        var previous = _state;
        _state = unchecked(previous * Multiplier + Increment);

        var xorShifted = (uint)(((previous >> 18) ^ previous) >> 27);
        var rotation = (int)(previous >> 59);

        return (xorShifted >> rotation) | (xorShifted << ((-rotation) & 31));
    }

    /// <summary>Uniform float in [0, 1).</summary>
    public float NextFloat() => (NextUInt() >> 8) * (1f / (1 << 24));

    /// <summary>Uniform float in [min, max).</summary>
    public float NextFloat(float min, float max) => min + NextFloat() * (max - min);

    /// <summary>Uniform int in [min, maxExclusive). Returns <paramref name="min"/> for an empty range.</summary>
    public int NextInt(int min, int maxExclusive)
    {
        if (maxExclusive <= min)
            return min;

        return min + (int)(NextUInt() % (uint)(maxExclusive - min));
    }

    public int NextInt(int maxExclusive) => NextInt(0, maxExclusive);

    public bool Chance(float probability) => NextFloat() < probability;

    /// <summary>Fisher-Yates shuffle, so callers can randomise iteration order without bias.</summary>
    public void Shuffle<T>(IList<T> items)
    {
        for (var i = items.Count - 1; i > 0; i--)
        {
            var j = NextInt(i + 1);
            (items[i], items[j]) = (items[j], items[i]);
        }
    }

    public T Pick<T>(IReadOnlyList<T> items)
    {
        if (items.Count == 0)
            throw new ArgumentException("Cannot pick from an empty list.", nameof(items));

        return items[NextInt(items.Count)];
    }

    /// <summary>A derived generator, so one subsystem consuming extra numbers cannot shift another's output.</summary>
    public Rng Fork(int salt) => new(unchecked(Seed * 31 + salt * (int)2654435761u));
}
