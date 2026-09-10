using MakeMeAnRTS.Core;
using MakeMeAnRTS.Features.World;

namespace MakeMeAnRTS.Features.Prospecting;

/// <summary>
/// A survey marker a prospector leaves behind, recording what it found and where.
/// </summary>
/// <remarks>
/// The sign is the point of prospecting: a player should be able to send a prospector out, forget about
/// it, and later find a note on the map saying "iron 180 here". The readings are a snapshot from when
/// the survey happened, so a sign next to a worked-out mine is stale on purpose.
/// </remarks>
public sealed class MapSign
{
    public int Id { get; }
    public int OwnerIndex { get; }
    public GridPos Position { get; }

    /// <summary>Average abundance of each mineral over the surveyed circle, at the time of the survey.</summary>
    public IReadOnlyDictionary<MineralKind, int> Readings { get; }

    /// <summary>Match time the survey was taken, in seconds.</summary>
    public float SurveyedAtSeconds { get; }

    public MapSign(int id, int ownerIndex, GridPos position, IReadOnlyDictionary<MineralKind, int> readings, float surveyedAtSeconds)
    {
        ArgumentNullException.ThrowIfNull(readings);

        Id = id;
        OwnerIndex = ownerIndex;
        Position = position;
        Readings = readings;
        SurveyedAtSeconds = surveyedAtSeconds;
    }

    /// <summary>The richest mineral found, which is what the sign shows when there is no room for detail.</summary>
    public (MineralKind Mineral, int Abundance) Best
    {
        get
        {
            var best = MineralKind.Stone;
            var bestAbundance = -1;

            foreach (var mineral in Minerals.All)
            {
                if (Readings.TryGetValue(mineral, out var abundance) && abundance > bestAbundance)
                {
                    best = mineral;
                    bestAbundance = abundance;
                }
            }

            return (best, Math.Max(0, bestAbundance));
        }
    }

    /// <summary>Whether the survey found anything worth mining at all.</summary>
    public bool IsBarren => Best.Abundance < BarrenThreshold;

    /// <summary>Below this average abundance a deposit is not worth the cost of a mine.</summary>
    public const int BarrenThreshold = 12;

    /// <summary>One line for the sign on the map, e.g. "Iron 184".</summary>
    public string ShortLabel()
    {
        var (mineral, abundance) = Best;
        return IsBarren ? "barren" : $"{mineral.DisplayName()} {abundance}";
    }

    /// <summary>The full reading, for the selection panel.</summary>
    public IEnumerable<string> DetailLines()
        => Minerals.All.Select(mineral => $"{mineral.DisplayName(),-6} {Readings.GetValueOrDefault(mineral)}");
}
