using System.Text;
using MakeMeAnRTS.Core;

namespace MakeMeAnRTS.Features.World;

/// <summary>
/// Counts up what a generated map actually contains.
/// </summary>
/// <remarks>
/// A picture shows whether a map looks right; these numbers show whether it is right. "0 bridges" or
/// "2% wood" is obvious in a line of text and easy to miss in a thumbnail.
/// </remarks>
public static class MapStatistics
{
    public static string Summarize(WorldMap map)
    {
        ArgumentNullException.ThrowIfNull(map);

        var counts = new Dictionary<TerrainKind, int>();
        var woodTiles = 0;
        var totalWood = 0L;

        foreach (var pos in map.Tiles.Positions())
        {
            var tile = map.TileAt(pos);
            counts[tile.Terrain] = counts.GetValueOrDefault(tile.Terrain) + 1;

            if (tile.HasTrees)
            {
                woodTiles++;
                totalWood += tile.Wood;
            }
        }

        var area = map.Width * map.Height;
        var summary = new StringBuilder();

        summary.Append("terrain:");
        foreach (var terrain in Enum.GetValues<TerrainKind>())
            summary.Append($" {terrain}={Percent(counts.GetValueOrDefault(terrain), area)}");

        summary.AppendLine();
        summary.AppendLine($"woods: {Percent(woodTiles, area)} of tiles, {totalWood:N0} wood total");

        foreach (var mineral in Minerals.All)
        {
            var map1 = map.MineralMap(mineral);
            var tiles = 0;
            var rich = 0;
            long total = 0;

            foreach (var pos in map1.Positions())
            {
                var abundance = map1[pos];
                if (abundance == 0)
                    continue;

                tiles++;
                total += abundance;

                if (abundance >= 160)
                    rich++;
            }

            summary.AppendLine($"{mineral.DisplayName(),-6}: {Percent(tiles, area)} of tiles bearing, {Percent(rich, area)} rich, {total:N0} total");
        }

        summary.Append($"starts: {string.Join(", ", map.StartPositions)}");

        return summary.ToString();
    }

    private static string Percent(int count, int total) => $"{count * 100f / total:0.0}%";
}
