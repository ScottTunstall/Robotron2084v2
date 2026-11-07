namespace Robotron2084.AttractMode;

/// <summary>Describes one kind of character in the attract movie: its sprite, and how it walks.</summary>
/// <remarks>
///     <list type="bullet">
///         <item>
///             The arcade stores it as: an animation frame table, the animation frame count, a 4, and for the walkers the
///             four walks (left, right, down, up) and a walk table.
///         </item>
///         <item>Original source: the movie's object descriptors (notes §95.6)</item>
///         <item>Disassembly: not separately labelled</item>
///     </list>
/// </remarks>
/// <param name="Animation">Which animation the object draws.</param>
/// <param name="AnimationFrameCount">How many animation frames the sprite has. The walk animation frames cycle within them.</param>
/// <param name="Walk">Which walker its MOVE opcodes use.</param>
/// <param name="StepSize">BR* walkers only: the descriptor's step byte (half-columns).</param>
/// <param name="StepNap">BR* walkers only: the descriptor's nap byte (fiftieths of a second a step).</param>
public readonly record struct MovieDescriptor(
    MovieAnimation Animation,
    int AnimationFrameCount,
    MovieWalk Walk = MovieWalk.None,
    int StepSize = 0,
    int StepNap = 0);
