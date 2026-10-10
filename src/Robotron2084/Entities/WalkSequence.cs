namespace Robotron2084.Entities;

/// <summary>
/// Which way a walker is walking, which decides the three walk animation frames it uses. The numbers are in
/// the arcade's own order, so <c>(int)walkSequence * 3</c> is the place of the first of the three.
/// </summary>
public enum WalkSequence
{
    /// <summary>Walking left. Walking diagonally to the left uses it too.</summary>
    Left = 0,

    /// <summary>Walking right. Walking diagonally to the right uses it too.</summary>
    Right = 1,

    /// <summary>Walking down.</summary>
    Down = 2,

    /// <summary>Walking up.</summary>
    Up = 3,
}
