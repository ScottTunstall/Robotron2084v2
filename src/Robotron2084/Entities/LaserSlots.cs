using System.Diagnostics.CodeAnalysis;
using Microsoft.Xna.Framework;
using Robotron2084.Core;
using Robotron2084.Level;

namespace Robotron2084.Entities;

/// <summary>
/// The player's up-to-THREE active lasers, tracked as an array of slots
/// (spec: "Record these as SLOTs"). A slot frees the instant its laser is
/// deactivated, so a fresh laser can reuse it on the next <see cref="TryFire"/>.
/// </summary>
public sealed class LaserSlots
{
    public const int Capacity = 3;

    private readonly PlayerLaser?[] _slots = new PlayerLaser?[Capacity];

    /// <summary>All slots (null = empty). Exposed for tests and diagnostics.</summary>
    public IReadOnlyList<PlayerLaser?> Slots => _slots;

    /// <summary>
    /// Fires a laser into the first free slot. Returns false when all three
    /// slots hold live lasers.
    /// </summary>
    public bool TryFire(IntVector2 position, Direction8 direction, [NotNullWhen(true)] out PlayerLaser? laser)
    {
        for (int i = 0; i < Capacity; i++)
        {
            if (_slots[i] is null || _slots[i]!.LifeState != EntityLifeState.Alive)
            {
                laser = new PlayerLaser(position, direction);
                _slots[i] = laser;
                return true;
            }
        }

        laser = null;
        return false;
    }

    public void Update(GameTime gameTime, PlayField field)
    {
        for (int i = 0; i < Capacity; i++)
        {
            PlayerLaser? slot = _slots[i];
            if (slot is { LifeState: EntityLifeState.Alive })
            {
                slot.Update(gameTime, field);
            }
        }
    }

    /// <summary>The lasers currently alive (at most 3).</summary>
    public IEnumerable<PlayerLaser> ActiveLasers =>
        _slots.Where(laser => laser is { LifeState: EntityLifeState.Alive }).Select(laser => laser!);
}
