using MakeMeAnRTS.Core;

namespace MakeMeAnRTS.Features.Vision;

/// <summary>
/// Fog of war for one player: what has ever been seen, and what is in sight right now.
/// </summary>
/// <remarks>
/// Two layers, because they answer different questions. <see cref="IsExplored"/> is permanent and
/// decides whether terrain is drawn at all. <see cref="IsVisible"/> is rebuilt every update and decides
/// whether enemy units are drawn - so an enemy walking through explored-but-unwatched ground stays
/// hidden, which is what makes a scout worth having.
/// </remarks>
public sealed class PlayerVision
{
    private readonly Grid2D<bool> _explored;
    private readonly Grid2D<bool> _visible;

    public int Width { get; }
    public int Height { get; }

    public PlayerVision(int width, int height)
    {
        Width = width;
        Height = height;
        _explored = new Grid2D<bool>(width, height);
        _visible = new Grid2D<bool>(width, height);
    }

    public bool IsExplored(GridPos pos) => _explored.GetOrDefault(pos, false);
    public bool IsVisible(GridPos pos) => _visible.GetOrDefault(pos, false);

    /// <summary>Clears the live-sight layer. Call once per update before adding this update's watchers.</summary>
    public void BeginUpdate() => _visible.Fill(false);

    /// <summary>Lights up a circle around a unit or building, and marks it explored for good.</summary>
    public void Reveal(GridPos center, int radius)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(radius);

        var radiusSquared = radius * radius;

        for (var dy = -radius; dy <= radius; dy++)
        {
            for (var dx = -radius; dx <= radius; dx++)
            {
                if (dx * dx + dy * dy > radiusSquared)
                    continue;

                var pos = new GridPos(center.X + dx, center.Y + dy);
                if (!_visible.IsInBounds(pos))
                    continue;

                _visible[pos] = true;
                _explored[pos] = true;
            }
        }
    }

    /// <summary>Reveals the whole map, for the debug overlay and for reviewing a finished match.</summary>
    public void RevealAll()
    {
        _explored.Fill(true);
        _visible.Fill(true);
    }
}
