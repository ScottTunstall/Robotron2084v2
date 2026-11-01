namespace Robotron2084.Entities;

/// <summary>
///     Which way a walker is walking, which decides the three walk animation frames it uses. The numbers are in
///     the arcade's own order, so <c>(int)walkSequence * 3</c> is the place of the first of the three.
/// </summary>
/// <remarks>
///     <list type="bullet">
///         <item>
///             Original source: <c>RRH11.ASM</c>, where every walker's pictures come in the order left, right, down, up:
///             <c>MLP1</c>, <c>MRP1</c>, <c>MDP1</c> and <c>MUP1</c> for Mommy, <c>DLP1</c> to <c>DUP1</c> for Daddy,
///             <c>KIDLP1</c> to <c>KIDUP1</c> for Mikey, and <c>HLKAL</c>, <c>HLKAR</c>, <c>HLKAD</c> and <c>HLKAU</c> for
///             the hulk
///         </item>
///         <item>Disassembly: not separately labelled</item>
///     </list>
/// </remarks>
public enum WalkSequence
{
    /// <summary>Walking left. Walking diagonally to the left uses it too.</summary>
    Left = 0,

    /// <summary>Walking right. Walking diagonally to the right uses it too.</summary>
    Right = 1,

    /// <summary>Walking down.</summary>
    Down = 2,

    /// <summary>Walking up.</summary>
    Up = 3
}
