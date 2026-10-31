using Robotron2084.Entities;

namespace Robotron2084.Level.Collisions;

/// <summary>Questions about the field. None of them change it.</summary>
internal interface ICollisionScene
{
    /// <summary>Says whether the player can be killed just now.</summary>
    bool CanPlayerBeHurt();

    /// <summary>Lists the player's lasers that are in flight.</summary>
    IEnumerable<PlayerLaser> GetActiveLasers();

    /// <summary>Says whether the player is alive.</summary>
    bool IsPlayerAlive();

    /// <summary>Says whether the robots must stand still just now.</summary>
    bool RobotsFrozen();

    /// <summary>Says whether two entities are touching.</summary>
    /// <param name="a">One entity.</param>
    /// <param name="b">The other entity.</param>
    bool Touches(IEntity a, IEntity b);

    /// <summary>Says whether the player is touching an entity.</summary>
    /// <param name="entity">The entity to test.</param>
    bool TouchesPlayer(IEntity entity);
}
