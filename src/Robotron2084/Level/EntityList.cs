using System.Collections;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Entities;
using Robotron2084.Rendering;

namespace Robotron2084.Level;

/// <summary>
/// One kind's entities on the field, with the passes EVERY list of them needs — update, prune, draw — so the
/// field walks its lists in single loops instead of repeating each pass once per kind.
/// </summary>
/// <remarks>
/// Adding an entity kind is therefore a field here plus a row in <see cref="RobotKinds"/> (and its class), not
/// another edit in each of the field's passes. The field also holds the lists for the nearest-robot scan and
/// the per-kind collision phases, which are typed and read as the ROM's own phases do.
/// </remarks>
/// <typeparam name="T">The entity type the list holds.</typeparam>
public sealed class EntityList<T> : IEntityList, IReadOnlyList<T>
    where T : class, IEntity
{
    private readonly List<T> _items = [];

    /// <summary>The entities in the list, as the common type the registry-driven phases walk.</summary>
    public IEnumerable<IEntity> Entities => _items;

    /// <inheritdoc/>
    public int Count => _items.Count;

    /// <summary>The entity at <paramref name="index"/>.</summary>
    /// <param name="index">Its position in the list.</param>
    public T this[int index] => _items[index];

    /// <summary>The entity at the end of the list — the ROM's "last slot" (<c>[^1]</c>).</summary>
    public T Last => _items[^1];

    /// <summary>Adds an entity to the list.</summary>
    /// <param name="entity">The entity to add.</param>
    public void Add(T entity) => _items.Add(entity);

    /// <summary>True when at least one entity in the list satisfies the predicate.</summary>
    /// <param name="predicate">What to look for.</param>
    public bool Any(Func<T, bool> predicate) => _items.Any(predicate);

    /// <summary>Walks the list in order.</summary>
    public IEnumerator<T> GetEnumerator() => _items.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <inheritdoc/>
    public void UpdateAll(GameTime gameTime, PlayField field)
    {
        foreach (T entity in _items)
        {
            field.UpdateEntity(entity, gameTime);
        }
    }

    /// <inheritdoc/>
    public void PruneDead() => _items.RemoveAll(entity => entity.LifeState == EntityLifeState.Dead);

    /// <inheritdoc/>
    public void DrawAll(SpriteBatch spriteBatch, PlayField field)
    {
        foreach (T entity in _items)
        {
            field.DrawEntity(entity, spriteBatch);
        }
    }
}
