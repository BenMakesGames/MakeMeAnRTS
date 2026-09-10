using MakeMeAnRTS.Core;
using MakeMeAnRTS.Features.World;

namespace MakeMeAnRTS.Features.Prospecting;

/// <summary>
/// What one player has learned about the mineral maps, tile by tile.
/// </summary>
/// <remarks>
/// This is the whole reason the prospector exists. The real abundances live on the <see cref="WorldMap"/>
/// and are never shown directly; a player only ever sees what they have surveyed. Handing the true map
/// to the HUD would quietly delete a unit's reason to exist, so the heat map overlay draws from here.
/// </remarks>
public sealed class MineralKnowledge
{
    private readonly Grid2D<byte>[] _known;
    private readonly Grid2D<bool> _surveyed;

    public MineralKnowledge(int width, int height)
    {
        _known = Minerals.All.Select(_ => new Grid2D<byte>(width, height)).ToArray();
        _surveyed = new Grid2D<bool>(width, height);
    }

    /// <summary>Surveyed abundance at a tile: 0 for unsurveyed ground as well as barren ground.</summary>
    public byte KnownAbundance(MineralKind mineral, GridPos pos) => _known[(int)mineral].GetOrDefault(pos, (byte)0);

    public Grid2D<byte> KnownMap(MineralKind mineral) => _known[(int)mineral];

    public bool IsSurveyed(GridPos pos) => _surveyed.GetOrDefault(pos, false);

    /// <summary>
    /// Copies the true abundances within <paramref name="radius"/> of <paramref name="center"/> into
    /// this player's picture of the map.
    /// </summary>
    /// <returns>The average abundance of each mineral over the surveyed area, for the sign to report.</returns>
    public IReadOnlyDictionary<MineralKind, int> Survey(WorldMap map, GridPos center, int radius)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentOutOfRangeException.ThrowIfLessThan(radius, 1);

        var totals = Minerals.All.ToDictionary(mineral => mineral, _ => 0L);
        var tiles = 0;

        for (var dy = -radius; dy <= radius; dy++)
        {
            for (var dx = -radius; dx <= radius; dx++)
            {
                // Circular, not square: a survey should read as a radius on the map.
                if (dx * dx + dy * dy > radius * radius)
                    continue;

                var pos = new GridPos(center.X + dx, center.Y + dy);
                if (!map.IsInBounds(pos))
                    continue;

                _surveyed[pos] = true;
                tiles++;

                foreach (var mineral in Minerals.All)
                {
                    var abundance = map.MineralAt(mineral, pos);
                    _known[(int)mineral][pos] = abundance;
                    totals[mineral] += abundance;
                }
            }
        }

        return tiles == 0
            ? Minerals.All.ToDictionary(mineral => mineral, _ => 0)
            : totals.ToDictionary(entry => entry.Key, entry => (int)(entry.Value / tiles));
    }

    /// <summary>
    /// Records what mining actually turned up at a tile, so a worked deposit stays honest on the heat map.
    /// </summary>
    public void RecordObservation(MineralKind mineral, GridPos pos, byte abundance)
    {
        if (!_surveyed.IsInBounds(pos))
            return;

        _surveyed[pos] = true;
        _known[(int)mineral][pos] = abundance;
    }
}
