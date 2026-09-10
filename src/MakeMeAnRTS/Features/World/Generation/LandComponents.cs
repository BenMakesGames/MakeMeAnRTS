using MakeMeAnRTS.Core;

namespace MakeMeAnRTS.Features.World.Generation;

/// <summary>
/// Labels connected regions of land, so generation can prove a map is playable before handing it over.
/// </summary>
/// <remarks>
/// Trees count as passable here even though they block units, because a worker can always cut a way
/// through. Water and rivers are the only real barriers, and bridging them is exactly what this is for.
/// </remarks>
internal sealed class LandComponents
{
    /// <summary>Component id per tile; 0 means water (no component).</summary>
    private readonly Grid2D<int> _labels;

    public int ComponentCount { get; }

    private LandComponents(Grid2D<int> labels, int componentCount)
    {
        _labels = labels;
        ComponentCount = componentCount;
    }

    public static LandComponents Build(Grid2D<Tile> tiles)
    {
        var labels = new Grid2D<int>(tiles.Width, tiles.Height);
        var nextLabel = 0;
        var queue = new Queue<GridPos>();

        foreach (var start in tiles.Positions())
        {
            if (labels[start] != 0 || !IsPassable(tiles, start))
                continue;

            nextLabel++;
            labels[start] = nextLabel;
            queue.Enqueue(start);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();

                foreach (var offset in GridPos.Neighbors4)
                {
                    var candidate = current + offset;
                    if (!tiles.IsInBounds(candidate) || labels[candidate] != 0 || !IsPassable(tiles, candidate))
                        continue;

                    labels[candidate] = nextLabel;
                    queue.Enqueue(candidate);
                }
            }
        }

        return new LandComponents(labels, nextLabel);
    }

    /// <summary>Component id at a tile, or 0 for water and off-map tiles.</summary>
    public int LabelAt(GridPos pos) => _labels.GetOrDefault(pos, 0);

    public bool AreConnected(GridPos a, GridPos b)
    {
        var labelA = LabelAt(a);
        return labelA != 0 && labelA == LabelAt(b);
    }

    /// <summary>Tile count of the component containing <paramref name="pos"/>, or 0 for water.</summary>
    public int SizeOfComponentAt(GridPos pos)
    {
        var label = LabelAt(pos);
        if (label == 0)
            return 0;

        var size = 0;
        foreach (var candidate in _labels.Positions())
            if (_labels[candidate] == label)
                size++;

        return size;
    }

    private static bool IsPassable(Grid2D<Tile> tiles, GridPos pos) => tiles[pos].Terrain.IsWalkable();
}
