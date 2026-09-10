using MakeMeAnRTS.Core;

namespace MakeMeAnRTS.Features.World.Generation;

/// <summary>
/// Raises closed depressions until every land tile has a strictly downhill path to a lake or the map edge.
/// </summary>
/// <remarks>
/// Without this, a river walking downhill stops at the first pit in the noise - which is everywhere -
/// so rivers came out as stubs and the pit-flooding fallback left diamond-shaped ponds. This is the
/// standard priority-flood: process tiles outward from the outlets, lowest first, lifting anything that
/// would sit below the level it has to drain over.
/// </remarks>
internal static class SinkFiller
{
    /// <summary>The slope left behind in a filled basin: enough to pick a direction, too little to see.</summary>
    private const float DrainSlope = 1e-4f;

    /// <summary>
    /// Fills sinks in <paramref name="heights"/> in place. Existing water tiles are outlets alongside the
    /// map edge, so lakes stay put and rivers run into them.
    /// </summary>
    public static void Fill(Grid2D<float> heights, Grid2D<Tile> tiles)
    {
        var resolved = new Grid2D<bool>(heights.Width, heights.Height);
        var pending = new PriorityQueue<GridPos, float>();

        foreach (var pos in heights.Positions())
        {
            var isEdge = pos.X == 0 || pos.Y == 0 || pos.X == heights.Width - 1 || pos.Y == heights.Height - 1;

            if (!isEdge && !tiles[pos].Terrain.IsWater())
                continue;

            resolved[pos] = true;
            pending.Enqueue(pos, heights[pos]);
        }

        while (pending.TryDequeue(out var current, out var drainLevel))
        {
            foreach (var offset in GridPos.Neighbors4)
            {
                var neighbor = current + offset;
                if (!heights.IsInBounds(neighbor) || resolved[neighbor])
                    continue;

                resolved[neighbor] = true;

                // Anything at or below the level it drains over gets lifted just above it.
                if (heights[neighbor] <= drainLevel)
                    heights[neighbor] = drainLevel + DrainSlope;

                pending.Enqueue(neighbor, heights[neighbor]);
            }
        }
    }
}
