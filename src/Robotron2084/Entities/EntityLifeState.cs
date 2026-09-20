namespace Robotron2084.Entities;

/// <summary>An entity's life cycle: alive, playing its death animation, or finished.</summary>
public enum EntityLifeState
{
    /// <summary>Alive: updating, colliding and drawn.</summary>
    Alive,

    /// <summary>Playing its death animation: still drawn, no longer collides.</summary>
    /// <remarks>A strip explosion, a shrivel, or the player's <c>PDTHV</c> flash (RRX7.ASM).</remarks>
    Dying,

    /// <summary>Finished: removed from play.</summary>
    Dead,
}
