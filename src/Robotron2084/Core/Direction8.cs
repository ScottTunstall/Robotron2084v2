namespace Robotron2084.Core;

/// <summary>
/// The 8 compass directions of an 8-way digital control scheme.
/// Screen Y grows downward, so "up" is -Y.
/// </summary>
public enum Direction8
{
    Up,
    UpRight,
    Right,
    DownRight,
    Down,
    DownLeft,
    Left,
    UpLeft,
}

/// <summary>
/// <see cref="Direction8"/> conversions, all pure integer math: the 8 nonzero
/// sign combinations of a delta (Math.Sign per axis) exhaust the 8 compass
/// directions exactly, so no trigonometry is ever needed.
/// </summary>
public static class Direction8Extensions
{
    /// <summary>
    /// The (-1/0/1, -1/0/1) unit vector, NOT normalized: diagonal movement is
    /// the same per-axis speed as cardinal movement (deliberate — matches
    /// original arcade behaviour).
    /// </summary>
    public static IntVector2 ToIntVector(this Direction8 direction) => direction switch
    {
        Direction8.Up => new(0, -1),
        Direction8.UpRight => new(1, -1),
        Direction8.Right => new(1, 0),
        Direction8.DownRight => new(1, 1),
        Direction8.Down => new(0, 1),
        Direction8.DownLeft => new(-1, 1),
        Direction8.Left => new(-1, 0),
        Direction8.UpLeft => new(-1, -1),
        _ => IntVector2.Zero, // defensive: unknown (out-of-range) enum values never occur in gameplay
    };

    /// <summary>The direction 4 steps around the ring (Up&lt;-&gt;Down, UpRight&lt;-&gt;DownLeft, ...).</summary>
    public static Direction8 Opposite(this Direction8 direction) => (Direction8)(((int)direction + 4) % 8);

    /// <summary>
    /// Maps a delta to the compass direction of its per-axis signs;
    /// <see langword="null"/> for the zero delta.
    /// </summary>
    public static Direction8? FromDelta(IntVector2 delta)
    {
        if (delta == IntVector2.Zero)
        {
            return null;
        }

        int dx = Math.Sign(delta.X);
        int dy = Math.Sign(delta.Y);

        return (dx, dy) switch
        {
            (0, -1) => Direction8.Up,
            (1, -1) => Direction8.UpRight,
            (1, 0) => Direction8.Right,
            (1, 1) => Direction8.DownRight,
            (0, 1) => Direction8.Down,
            (-1, 1) => Direction8.DownLeft,
            (-1, 0) => Direction8.Left,
            (-1, -1) => Direction8.UpLeft,
            _ => null,
        };
    }
}
