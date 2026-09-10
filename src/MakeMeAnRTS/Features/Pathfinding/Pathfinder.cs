using MakeMeAnRTS.Core;
using MakeMeAnRTS.Features.World;

namespace MakeMeAnRTS.Features.Pathfinding;

/// <summary>
/// Grid A* over walkable tiles, eight-connected, weighted by terrain movement cost.
/// </summary>
/// <remarks>
/// Not thread-safe and not re-entrant: scratch arrays are reused between calls to keep pathing free of
/// per-request allocation. One instance per match, called from the match update, is the intended use.
///
/// When a goal cannot be reached the search returns the best partial path rather than nothing. A unit
/// that walks as far as it can towards an unreachable order is far less baffling than one that ignores
/// the order entirely, and it is usually what the player meant anyway.
/// </remarks>
public sealed class Pathfinder
{
    /// <summary>Cost of a diagonal step relative to an orthogonal one.</summary>
    private const float DiagonalCost = 1.41421356f;

    /// <summary>
    /// Ceiling on tiles examined per search. A few thousand covers any sane order on these map sizes;
    /// the cap stops one impossible order from stalling a frame.
    /// </summary>
    private const int MaxExpansions = 12_000;

    private readonly WorldMap _map;
    private readonly float[] _costFromStart;
    private readonly int[] _cameFrom;
    private readonly int[] _visitStamp;
    private readonly PriorityQueue<int, float> _frontier = new();

    /// <summary>Bumped per search so the scratch arrays need no clearing between calls.</summary>
    private int _currentSearch;

    public Pathfinder(WorldMap map)
    {
        ArgumentNullException.ThrowIfNull(map);

        _map = map;
        var cells = map.Width * map.Height;
        _costFromStart = new float[cells];
        _cameFrom = new int[cells];
        _visitStamp = new int[cells];
    }

    /// <summary>
    /// Finds a walkable route from <paramref name="start"/> to <paramref name="goal"/>.
    /// </summary>
    /// <param name="ignoreTrees">
    /// True to path through woods as though they were clear, for callers who intend to cut their way in.
    /// </param>
    /// <returns>
    /// Tiles to walk, excluding the start. Empty if the unit is already there, or if nowhere better than
    /// the start could be reached.
    /// </returns>
    public List<GridPos> FindPath(GridPos start, PathGoal goal, bool ignoreTrees = false)
    {
        if (!_map.IsInBounds(start))
            throw new ArgumentOutOfRangeException(nameof(start), start, "Path start is off the map.");

        if (goal.IsSatisfiedBy(start))
            return [];

        _currentSearch++;
        _frontier.Clear();

        var startIndex = Index(start);
        _costFromStart[startIndex] = 0f;
        _cameFrom[startIndex] = -1;
        _visitStamp[startIndex] = _currentSearch;
        _frontier.Enqueue(startIndex, Heuristic(start, goal));

        var closestIndex = startIndex;
        var closestDistance = Heuristic(start, goal);
        var expansions = 0;

        while (_frontier.TryDequeue(out var currentIndex, out _))
        {
            var current = Position(currentIndex);

            if (goal.IsSatisfiedBy(current))
                return Reconstruct(currentIndex);

            if (++expansions > MaxExpansions)
                break;

            foreach (var offset in GridPos.Neighbors8)
            {
                var neighbor = current + offset;
                if (!IsEnterable(neighbor, ignoreTrees))
                    continue;

                var isDiagonal = offset.X != 0 && offset.Y != 0;

                // No squeezing between two blocked tiles: units would visibly clip through corners.
                if (isDiagonal &&
                    (!IsEnterable(new GridPos(current.X + offset.X, current.Y), ignoreTrees) ||
                     !IsEnterable(new GridPos(current.X, current.Y + offset.Y), ignoreTrees)))
                {
                    continue;
                }

                var stepCost = _map.TileAt(neighbor).Terrain.MoveCost() * (isDiagonal ? DiagonalCost : 1f);
                var tentativeCost = _costFromStart[currentIndex] + stepCost;
                var neighborIndex = Index(neighbor);

                var isUnvisited = _visitStamp[neighborIndex] != _currentSearch;
                if (!isUnvisited && tentativeCost >= _costFromStart[neighborIndex])
                    continue;

                _visitStamp[neighborIndex] = _currentSearch;
                _costFromStart[neighborIndex] = tentativeCost;
                _cameFrom[neighborIndex] = currentIndex;

                var remaining = Heuristic(neighbor, goal);
                _frontier.Enqueue(neighborIndex, tentativeCost + remaining);

                // Remember the nearest miss, so an impossible order still moves the unit the right way.
                if (remaining < closestDistance)
                {
                    closestDistance = remaining;
                    closestIndex = neighborIndex;
                }
            }
        }

        return closestIndex == startIndex ? [] : Reconstruct(closestIndex);
    }

    /// <summary>Whether a route exists at all, without the caller paying to build the path.</summary>
    public bool CanReach(GridPos start, PathGoal goal, bool ignoreTrees = false)
        => goal.IsSatisfiedBy(start) || FindPath(start, goal, ignoreTrees) is { Count: > 0 } path && goal.IsSatisfiedBy(path[^1]);

    /// <summary>
    /// The nearest walkable tile to <paramref name="near"/>, searched outwards in rings.
    /// </summary>
    /// <remarks>
    /// Used when something must be placed on solid ground - a unit trained by a building, a unit shoved
    /// out of a footprint - so those cases never have to guess.
    /// </remarks>
    public GridPos? FindNearestWalkable(GridPos near, int maxRadius = 12)
    {
        if (_map.IsWalkable(near))
            return near;

        for (var radius = 1; radius <= maxRadius; radius++)
        {
            for (var dy = -radius; dy <= radius; dy++)
            {
                for (var dx = -radius; dx <= radius; dx++)
                {
                    // Only the ring's edge; the interior was covered by a smaller radius.
                    if (Math.Abs(dx) != radius && Math.Abs(dy) != radius)
                        continue;

                    var candidate = new GridPos(near.X + dx, near.Y + dy);
                    if (_map.IsWalkable(candidate))
                        return candidate;
                }
            }
        }

        return null;
    }

    private bool IsEnterable(GridPos pos, bool ignoreTrees)
    {
        if (!_map.IsInBounds(pos) || _map.IsOccupied(pos))
            return false;

        var tile = _map.TileAt(pos);

        return ignoreTrees ? tile.Terrain.IsWalkable() : tile.IsWalkable;
    }

    /// <summary>Octile distance: exact for an empty grid, so it never overestimates and A* stays optimal.</summary>
    private static float Heuristic(GridPos from, PathGoal goal)
    {
        var target = goal.AimPoint;
        var dx = MathF.Abs(from.X + 0.5f - target.X);
        var dy = MathF.Abs(from.Y + 0.5f - target.Y);

        var straight = MathF.Abs(dx - dy);
        var diagonal = MathF.Min(dx, dy);

        return straight + diagonal * DiagonalCost;
    }

    private List<GridPos> Reconstruct(int endIndex)
    {
        var path = new List<GridPos>();

        for (var index = endIndex; index >= 0; index = _cameFrom[index])
            path.Add(Position(index));

        // Built end-first; the start tile is where the unit already stands, so drop it.
        path.RemoveAt(path.Count - 1);
        path.Reverse();

        return path;
    }

    private int Index(GridPos pos) => pos.Y * _map.Width + pos.X;
    private GridPos Position(int index) => new(index % _map.Width, index / _map.Width);
}
