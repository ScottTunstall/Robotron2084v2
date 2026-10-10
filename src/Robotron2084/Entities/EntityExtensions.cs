namespace Robotron2084.Entities;

/// <summary>A helper that hands back an entity as a more exact type.</summary>
internal static class EntityExtensions
{
    /// <summary>Gives back the entity as the type that is asked for.</summary>
    /// <typeparam name="T">The type that is wanted.</typeparam>
    /// <param name="entity">The entity.</param>
    /// <exception cref="InvalidOperationException">The entity is not a <typeparamref name="T"/>.</exception>
    public static T Require<T>(this IEntity entity)
        where T : class =>
        entity as T ?? throw new InvalidOperationException($"{entity.GetType().Name} is not a {typeof(T).Name}.");
}
