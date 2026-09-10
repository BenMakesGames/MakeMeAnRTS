namespace MakeMeAnRTS.Features.Match;

/// <summary>
/// A bundle of resources: a stockpile, a price, or a payout.
/// </summary>
/// <remarks>
/// Immutable so a price can be a shared static without any risk of a caller spending from the price
/// list itself. Stockpiles are held in a <see cref="ResourceStore"/>, which wraps one of these.
/// </remarks>
public readonly struct ResourceAmounts
{
    private readonly int[]? _amounts;

    public static readonly ResourceAmounts None = default;

    private ResourceAmounts(int[] amounts) => _amounts = amounts;

    public static ResourceAmounts Of(int wood = 0, int stone = 0, int iron = 0, int gold = 0)
    {
        var amounts = new int[Resources.All.Length];
        amounts[(int)ResourceKind.Wood] = wood;
        amounts[(int)ResourceKind.Stone] = stone;
        amounts[(int)ResourceKind.Iron] = iron;
        amounts[(int)ResourceKind.Gold] = gold;

        return new ResourceAmounts(amounts);
    }

    public static ResourceAmounts Single(ResourceKind resource, int amount)
    {
        var amounts = new int[Resources.All.Length];
        amounts[(int)resource] = amount;

        return new ResourceAmounts(amounts);
    }

    /// <summary>Amount of one resource. A default-constructed bundle reads as zero everywhere.</summary>
    public int this[ResourceKind resource] => _amounts?[(int)resource] ?? 0;

    public bool IsEmpty
    {
        get
        {
            foreach (var resource in Resources.All)
                if (this[resource] != 0)
                    return false;

            return true;
        }
    }

    /// <summary>The non-zero entries, for display. Ordered by <see cref="ResourceKind"/>.</summary>
    public IEnumerable<(ResourceKind Resource, int Amount)> NonZero()
    {
        // Materialised rather than deferred: a struct's members cannot be captured by an iterator.
        var entries = new List<(ResourceKind, int)>();

        foreach (var resource in Resources.All)
            if (this[resource] != 0)
                entries.Add((resource, this[resource]));

        return entries;
    }

    public override string ToString()
        => IsEmpty ? "free" : string.Join(" ", NonZero().Select(entry => $"{entry.Amount} {entry.Resource.DisplayName()}"));
}
