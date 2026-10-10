namespace Robotron2084.Entities;

/// <summary>Where an entity is in its life: alive, playing its death animation, or finished.</summary>
public enum EntityLifeState
{
    /// <summary>Alive: it moves, can be hit and is drawn.</summary>
    Alive,

    /// <summary>Playing its death animation: it is still drawn but can no longer be hit.</summary>
    /// <remarks>A strip explosion, a shrivel, or the player's <c>PDTHV</c> flash (RRX7.ASM).</remarks>
    Dying,

    /// <summary>Finished: it is waiting to be taken off the field.</summary>
    Dead
}
