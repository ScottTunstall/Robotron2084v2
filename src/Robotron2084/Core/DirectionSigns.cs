namespace Robotron2084.Core;

/// <summary>
///     The sign of a step in each direction. Sideways, left is -1 and right is 1. Up and down: screen Y grows
///     downward, so up is -1 and down is 1.
/// </summary>
public static class DirectionSigns
{
    /// <summary>The sign of a step to the left.</summary>
    public const int Left = -1;

    /// <summary>The sign of a step to the right.</summary>
    public const int Right = 1;

    /// <summary>The sign of a step up the screen.</summary>
    public const int Up = -1;

    /// <summary>The sign of a step down the screen.</summary>
    public const int Down = 1;
}
