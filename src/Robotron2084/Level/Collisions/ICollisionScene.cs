using Robotron2084.Entities;

namespace Robotron2084.Level.Collisions;

/// <summary>What a collision rule may ask about the field. It can only look: nothing here changes the field.</summary>
/// <remarks>The field implements this and hands it to each rule. Keeping the questions apart from the field's other methods means
/// a rule cannot tell the field what to do, whatever it is given (STR-10).</remarks>
internal interface ICollisionScene
{
    /// <summary>Says whether the robots must stand still: until the game goes live at the start of the wave, and while the player is dying.</summary>
    bool RobotsFrozen();

    /// <summary>Says whether the player can be killed just now: alive, and not the invincible playtest player.</summary>
    bool CanPlayerBeHurt();

    /// <summary>Lists the player's lasers that are in flight.</summary>
    IEnumerable<PlayerLaser> GetActiveLasers();

    /// <summary>Says whether the player is alive.</summary>
    bool IsPlayerAlive();

    /// <summary>Says whether two entities are touching.</summary>
    /// <param name="a">One entity.</param>
    /// <param name="b">The other entity.</param>
    bool Touches(IEntity a, IEntity b);

    /// <summary>Says whether the player is touching an entity.</summary>
    /// <param name="entity">The entity to test.</param>
    bool TouchesPlayer(IEntity entity);
}
