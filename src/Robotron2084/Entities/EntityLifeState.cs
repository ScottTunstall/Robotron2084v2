namespace Robotron2084.Entities;

/// <summary>Where an entity is in its life: alive, playing its death animation, or finished.</summary>
public enum EntityLifeState
{
    /// <summary>Alive: it moves, can be hit and is drawn.</summary>
    Alive,

    /// <summary>Playing its death animation: it is still drawn but can no longer be hit.</summary>
    /// <remarks>
    ///     A strip explosion, a shrivel, or the player's flash.
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRX7.ASM</c>, routine <c>PDTHV</c> (the player's flash)</item>
    ///         <item>
    ///             Disassembly: the player's flash is started from <c>KILL_PLAYER</c> (<c>$30EF</c>), an electrode's
    ///             shrivel is <c>ELECTRODE_DEATH</c> (<c>$3AE0</c>), and the strip explosions are the routines named on
    ///             <see cref="StripEngine" />
    ///         </item>
    ///     </list>
    /// </remarks>
    Dying,

    /// <summary>Finished: it is waiting to be taken off the field.</summary>
    Dead
}
