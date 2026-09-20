namespace Robotron2084.Entities;

/// <summary>
/// An entity's life cycle: alive, playing its death animation, or finished and due to be
/// taken off the playfield.
/// </summary>
/// <remarks>
/// The arcade has no such field — an object is either running its own routine or has been
/// handed to a kill routine that erases its image and releases its record — so the port
/// keeps the state on the entity.
/// </remarks>
public enum EntityLifeState
{
    /// <summary>Alive: updating, colliding and drawn.</summary>
    Alive,

    /// <summary>
    /// Playing its death animation: still drawn, no longer collides, and the playfield
    /// removes it when the animation finishes.
    /// </summary>
    /// <remarks>
    /// A strip explosion, a shrivel, or the player's <c>PDTHV</c> flash-and-fade
    /// (RRX7.ASM).
    /// </remarks>
    Dying,

    /// <summary>Finished: the playfield prunes it.</summary>
    Dead,
}
