namespace Robotron2084.Entities;

/// <summary>
///     Questions about where an entity is in its life, so that nothing outside the entity compares its life state
///     directly.
/// </summary>
/// <remarks>
///     Each method says what the answer is for. Ask <c>IsAlive</c>, not "is the life state Alive", so that the meaning of
///     a
///     state is in one place if it ever changes. They are extension methods so that every kind of entity, the player
///     included, answers them.
/// </remarks>
public static class EntityLifeExtensions
{
    /// <summary>Says whether the entity is on the field, doing things: it moves, can be hit and is drawn.</summary>
    /// <param name="entity">The entity to ask.</param>
    public static bool IsAlive(this IEntity entity)
    {
        return entity.LifeState == EntityLifeState.Alive;
    }

    /// <summary>Says whether the entity is gone and waiting to be taken off the field.</summary>
    /// <param name="entity">The entity to ask.</param>
    public static bool IsDead(this IEntity entity)
    {
        return entity.LifeState == EntityLifeState.Dead;
    }

    /// <summary>Says whether the entity is playing its death animation: it is still drawn but can no longer be hit.</summary>
    /// <param name="entity">The entity to ask.</param>
    public static bool IsDying(this IEntity entity)
    {
        return entity.LifeState == EntityLifeState.Dying;
    }
}
