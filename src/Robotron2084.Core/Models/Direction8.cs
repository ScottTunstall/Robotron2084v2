namespace Robotron2084.Core.Models;

/// <summary>
///     The 8 compass directions of an 8-way digital control scheme.
///     Screen Y grows downward, so "up" is -Y.
/// </summary>
public enum Direction8
{
    /// <summary>Straight up the screen.</summary>
    Up,

    /// <summary>Up and to the right.</summary>
    UpRight,

    /// <summary>Straight to the right.</summary>
    Right,

    /// <summary>Down and to the right.</summary>
    DownRight,

    /// <summary>Straight down the screen.</summary>
    Down,

    /// <summary>Down and to the left.</summary>
    DownLeft,

    /// <summary>Straight to the left.</summary>
    Left,

    /// <summary>Up and to the left.</summary>
    UpLeft
}
