namespace MakeMeAnRTS.Features.World.Generation;

/// <summary>
/// Everything that shapes a map. A settings object plus its <see cref="Seed"/> reproduces a map
/// exactly, which is how generation bugs get reported and re-examined.
/// </summary>
public sealed record WorldGenSettings
{
    public int Seed { get; init; } = 1;
    public int Width { get; init; } = 176;
    public int Height { get; init; } = 176;

    /// <summary>Normalised elevation below which land floods into lakes and seas.</summary>
    public float WaterLevel { get; init; } = 0.30f;

    /// <summary>Normalised elevation above which ground turns to bare rock: walkable, never buildable.</summary>
    public float RockLevel { get; init; } = 0.80f;

    /// <summary>How many rivers to try to source from the highlands. Some merge or run dry.</summary>
    public int RiverCount { get; init; } = 10;

    /// <summary>Roughly the fraction of eligible land that grows trees.</summary>
    public float ForestDensity { get; init; } = 0.45f;

    /// <summary>Minimum tiles between two generated river crossings on the same stretch of water.</summary>
    public int BridgeSpacing { get; init; } = 14;

    public int PlayerCount { get; init; } = 2;

    /// <summary>Radius around each start position cleared of trees and levelled so a base always fits.</summary>
    public int StartClearRadius { get; init; } = 4;

    public void Validate()
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(Width, 48);
        ArgumentOutOfRangeException.ThrowIfLessThan(Height, 48);
        ArgumentOutOfRangeException.ThrowIfLessThan(PlayerCount, 2);
        ArgumentOutOfRangeException.ThrowIfNegative(RiverCount);
        ArgumentOutOfRangeException.ThrowIfLessThan(BridgeSpacing, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(StartClearRadius, 2);

        if (WaterLevel <= 0f || WaterLevel >= RockLevel || RockLevel >= 1f)
            throw new ArgumentException($"Need 0 < WaterLevel ({WaterLevel}) < RockLevel ({RockLevel}) < 1.");

        if (ForestDensity is < 0f or > 1f)
            throw new ArgumentOutOfRangeException(nameof(ForestDensity), ForestDensity, "Forest density must be within 0..1.");
    }
}
