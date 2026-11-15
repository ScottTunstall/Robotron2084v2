namespace Robotron2084.AttractMode.Models;

/// <summary>The action a movie character's program has started and carries on with, a step at a time, until it is done.</summary>
internal enum MovieAction
{
    /// <summary>No action: the program reads its next instruction.</summary>
    None,

    /// <summary>Walking, a step at a time (the arcade's <c>ANA</c> and <c>BR</c> walkers).</summary>
    Walk,

    /// <summary>Showing its animation frames in turn without moving (<c>CYCLE</c>).</summary>
    Cycle,

    /// <summary>Drawn in one colour inside a coloured box (<c>MONO</c>).</summary>
    Mono,

    /// <summary>Shaking up and down while a brain reprograms it (<c>RPROG</c>).</summary>
    ReprogramShake
}
