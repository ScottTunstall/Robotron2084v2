using Robotron2084.Entities;

namespace Robotron2084.Level;

/// <summary>What the registry's rows ask of the entities their kind's list holds.</summary>
internal static class EntityExtensions
{
    /// <summary>The entity as the type its kind's row needs.</summary>
    /// <typeparam name="T">The type or interface the row needs.</typeparam>
    /// <param name="entity">An entity from the kind's list.</param>
    /// <exception cref="InvalidOperationException">The list holds an entity that is not a <typeparamref name="T"/>.</exception>
    public static T Require<T>(this IEntity entity)
        where T : class =>
        entity as T ?? throw new InvalidOperationException($"{entity.GetType().Name} is not a {typeof(T).Name}.");
}
