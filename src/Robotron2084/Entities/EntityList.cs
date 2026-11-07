using System.Collections;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Level;

namespace Robotron2084.Entities;

/// <summary>
///     Holds every enemy or object of one kind that is on the field, and does the jobs they all share: updating them,
///     clearing out the dead ones and drawing them.
/// </summary>
/// <remarks>
///     <list type="bullet">
///         <item>
///             Original source: no single matching routine. This list belongs to the C# version. It copies
///             the original game's habit of keeping a separate list for each kind of object, where each object
///             holds a pointer to the next one (see <c>RRDX2.ASM</c>'s <c>NEXT</c>/<c>NEXTZ</c> field)
///         </item>
///         <item>
///             Disassembly: no single matching routine, though it describes the same
///             habit (for example <c>spheroids_enforcers_quarks_sparks_shells</c> ($9817),
///             <c>family_list_pointer</c> and <c>electrode_list_pointer</c>, each described as a "linked list
///             of..." its own kind of object).
///         </item>
///     </list>
/// </remarks>
/// <typeparam name="T">The kind of entity the list holds.</typeparam>
public sealed class EntityList<T> : IEntityList, IReadOnlyList<T>
    where T : class, IEntity
{
    private readonly List<T> _items = [];

    /// <inheritdoc />
    public int Count => _items.Count;

    /// <summary>Every entity in the list, whatever kind it is.</summary>
    public IEnumerable<IEntity> FieldEntities => _items;

    /// <summary>The entity at <paramref name="index" />.</summary>
    /// <param name="index">Its place in the list, counting from 0.</param>
    public T this[int index] => _items[index];

    /// <summary>Adds an entity to the list.</summary>
    /// <param name="entity">The entity to add.</param>
    public void Add(T entity)
    {
        _items.Add(entity);
    }

    /// <inheritdoc />
    public void DrawAll(SpriteBatch spriteBatch, PlayField playField)
    {
        foreach (var entity in _items) playField.DrawEntity(entity, spriteBatch);
    }

    /// <summary>Goes through the list in order.</summary>
    public IEnumerator<T> GetEnumerator()
    {
        return _items.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    /// <inheritdoc />
    public void PruneDead()
    {
        _items.RemoveAll(entity => entity.IsDead());
    }

    /// <inheritdoc />
    public void UpdateAll(GameTime gameTime, PlayField playField)
    {
        foreach (var entity in _items) playField.UpdateEntity(entity, gameTime);
    }
}
