namespace Robotron2084.AttractMode;

/// <summary>
/// A movie object descriptor (ROM format: FDB animation frame table, FCB animation frame count,
/// FCB 4, then for the walkers FDB walk-L/R/D/U and FDB walk-table).
/// </summary>
/// <param name="Animation">Which animation the object draws.</param>
/// <param name="AnimationFrameCount">The ROM's animation frame count (the walk animation frames cycle within it).</param>
/// <param name="Walk">Which walker its MOVE opcodes use.</param>
/// <param name="StepSize">BR* walkers only: the descriptor's step byte (half-columns).</param>
/// <param name="StepNap">BR* walkers only: the descriptor's nap byte (ROM frames a step).</param>
public readonly record struct MovieDescriptor(
    MovieAnimation Animation,
    int AnimationFrameCount,
    MovieWalk Walk = MovieWalk.None,
    int StepSize = 0,
    int StepNap = 0);
