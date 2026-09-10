namespace MakeMeAnRTS.Core;

/// <summary>
/// Dense row-major 2D array. Indexing throws off-map; use <see cref="GetOrDefault"/> when a
/// neighbour lookup may legitimately fall outside the map, so out-of-bounds bugs surface loudly
/// instead of silently reading a neighbouring row.
/// </summary>
public sealed class Grid2D<T>
{
    private readonly T[] _cells;

    public int Width { get; }
    public int Height { get; }

    public Grid2D(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);

        Width = width;
        Height = height;
        _cells = new T[width * height];
    }

    public Grid2D(int width, int height, T fill) : this(width, height) => Array.Fill(_cells, fill);

    public bool IsInBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;
    public bool IsInBounds(GridPos pos) => IsInBounds(pos.X, pos.Y);

    public T this[int x, int y]
    {
        get
        {
            ThrowIfOutOfBounds(x, y);
            return _cells[y * Width + x];
        }
        set
        {
            ThrowIfOutOfBounds(x, y);
            _cells[y * Width + x] = value;
        }
    }

    public T this[GridPos pos]
    {
        get => this[pos.X, pos.Y];
        set => this[pos.X, pos.Y] = value;
    }

    /// <summary>
    /// A reference to a cell, so callers can mutate fields of a struct element in place. Without this,
    /// <c>grid[x, y].Field = value</c> would silently write to a copy.
    /// </summary>
    public ref T At(int x, int y)
    {
        ThrowIfOutOfBounds(x, y);
        return ref _cells[y * Width + x];
    }

    public ref T At(GridPos pos) => ref At(pos.X, pos.Y);

    public T GetOrDefault(int x, int y, T fallback) => IsInBounds(x, y) ? _cells[y * Width + x] : fallback;
    public T GetOrDefault(GridPos pos, T fallback) => GetOrDefault(pos.X, pos.Y, fallback);

    public Span<T> AsSpan() => _cells;

    public void Fill(T value) => Array.Fill(_cells, value);

    /// <summary>Every tile position, row by row. Ordering is stable, which keeps generation deterministic.</summary>
    public IEnumerable<GridPos> Positions()
    {
        for (var y = 0; y < Height; y++)
            for (var x = 0; x < Width; x++)
                yield return new GridPos(x, y);
    }

    private void ThrowIfOutOfBounds(int x, int y)
    {
        if (!IsInBounds(x, y))
            throw new ArgumentOutOfRangeException($"({x},{y})", $"Tile ({x},{y}) is outside the {Width}x{Height} grid.");
    }
}
