using System.Collections;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Level;

namespace Robotron2084.Entities;

/// <summary>Holds every enemy or object of one kind that is currently on screen, and does the shared jobs of updating, cleaning out dead ones, and drawing them all.</summary>
/// <remarks>
/// <list type="bullet">
/// <item>Original source: no single matching routine — this is a C# implementation structure. It mirrors the original game's own convention of keeping a separate list per object kind, each object carrying its own "next" pointer (see <c>RRDX2.ASM</c>'s <c>NEXT</c>/<c>NEXTZ</c> field)</item>
/// <item>Disassembly: Not separately labelled in <c>asm/robomame.asm</c>, though it documents the same convention (for example <c>spheroids_enforcers_quarks_sparks_shells</c>, <c>family_list_pointer</c> and <c>electrode_list_pointer</c>, each described as a "linked list of..." its own object kind).</item>
/// </list>
/// </remarks>
/// <typeparam name="T">The entity type the list holds.</typeparam>
public sealed class EntityList<T> : IEntityList, IReadOnlyList<T>
    where T : class, IEntity
{
    private readonly List<T> _items = [];

    /// <inheritdoc/>
    public int Count => _items.Count;

    /// <summary>The entities in the list, as the common type the registry-driven phases walk.</summary>
    public IEnumerable<IEntity> Entities => _items;

    /// <summary>The entity at the end of the list — the ROM's "last slot" (<c>[^1]</c>).</summary>
    public T Last => _items[^1];

    /// <summary>The entity at <paramref name="index"/>.</summary>
    /// <param name="index">Its position in the list.</param>
    public T this[int index] => _items[index];

    /// <summary>Adds an entity to the list.</summary>
    /// <param name="entity">The entity to add.</param>
    public void Add(T entity) => _items.Add(entity);

    /// <summary>True when at least one entity in the list satisfies the predicate.</summary>
    /// <param name="predicate">What to look for.</param>
    public bool Any(Func<T, bool> predicate) => _items.Any(predicate);

    /// <inheritdoc/>
    public void DrawAll(SpriteBatch spriteBatch, PlayField field)
    {
        foreach (T entity in _items)
        {
            field.DrawEntity(entity, spriteBatch);
        }
    }

    /// <summary>Walks the list in order.</summary>
    public IEnumerator<T> GetEnumerator() => _items.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <inheritdoc/>
    public int GetLiveCount() => _items.Count(entity => !entity.IsDead());

    /// <inheritdoc/>
    public void PruneDead() => _items.RemoveAll(entity => entity.IsDead());

    /// <inheritdoc/>
    public void UpdateAll(GameTime gameTime, PlayField field)
    {
        foreach (T entity in _items)
        {
            field.UpdateEntity(entity, gameTime);
        }
    }
}
