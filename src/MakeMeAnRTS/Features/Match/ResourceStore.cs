namespace MakeMeAnRTS.Features.Match;

/// <summary>
/// A player's stockpile.
/// </summary>
/// <remarks>
/// Spending is all-or-nothing through <see cref="TrySpend"/>: there is no way to partially pay for
/// something and no way to go negative, so callers cannot leave a player owing resources.
/// </remarks>
public sealed class ResourceStore
{
    private readonly int[] _amounts = new int[Resources.All.Length];

    public int this[ResourceKind resource] => _amounts[(int)resource];

    public void Add(ResourceKind resource, int amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        _amounts[(int)resource] += amount;
    }

    public void Add(ResourceAmounts amounts)
    {
        foreach (var resource in Resources.All)
            _amounts[(int)resource] += amounts[resource];
    }

    public bool CanAfford(ResourceAmounts price) => Resources.All.All(resource => _amounts[(int)resource] >= price[resource]);

    /// <summary>Pays <paramref name="price"/> if it is affordable. Nothing is deducted if it is not.</summary>
    public bool TrySpend(ResourceAmounts price)
    {
        if (!CanAfford(price))
            return false;

        foreach (var resource in Resources.All)
            _amounts[(int)resource] -= price[resource];

        return true;
    }

    /// <summary>Returns a price to the stockpile, for a cancelled order or a demolished foundation.</summary>
    public void Refund(ResourceAmounts price) => Add(price);
}
