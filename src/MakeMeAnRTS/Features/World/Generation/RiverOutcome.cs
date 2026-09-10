namespace MakeMeAnRTS.Features.World.Generation;

/// <summary>
/// How one river's course ended. Anything but <see cref="Stalled"/> is a proper outlet.
/// </summary>
/// <remarks>
/// Purely diagnostic: the map-preview tool prints a tally so river tuning can be judged from numbers
/// instead of by squinting at a thumbnail. A rising <see cref="Stalled"/> count means the sink filler
/// is no longer doing its job.
/// </remarks>
public enum RiverOutcome
{
    ReachedWater,
    LeftTheMap,
    Merged,

    /// <summary>Ran out of downhill without reaching water.</summary>
    Stalled,

    /// <summary>Too short to keep, so it was erased again.</summary>
    Discarded,
}
