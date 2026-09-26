namespace Robotron2084.Entities;

/// <summary>
/// Which of the four walk sequences a walker is showing. The numeric values are the ROM's own block order, so
/// <c>(int)facing * 3</c> is the first animation frame of the facing's three.
/// </summary>
public enum WalkFacing
{
    /// <summary>Walking left (diagonals to the left reuse it).</summary>
    Left = 0,

    /// <summary>Walking right (diagonals to the right reuse it).</summary>
    Right = 1,

    /// <summary>Walking down.</summary>
    Down = 2,

    /// <summary>Walking up.</summary>
    Up = 3,
}
