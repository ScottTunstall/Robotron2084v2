using System.Diagnostics.CodeAnalysis;
using Microsoft.Xna.Framework;
using Robotron2084.Core;
using Robotron2084.Level;

namespace Robotron2084.Entities;

/// <summary>The slots that cap how many player lasers can be in flight at once.</summary>
/// <remarks>ROM: each laser is its own object record (RRG23.ASM, drawn by RRS22.ASM's <c>LASER</c>);
/// a laser is only created while a slot is free, so three is the limit even with auto-fire held down
/// (notes (24).1).</remarks>
public sealed class LaserSlots
{
    /// <summary>How many lasers the player can have in flight at once.</summary>
    public const int Capacity = 3;

    private readonly PlayerLaser?[] _slots = new PlayerLaser?[Capacity];

    /// <summary>Every slot in order; null means empty. Exposed for tests and diagnostics.</summary>
    public IReadOnlyList<PlayerLaser?> Slots => _slots;

    /// <summary>Fires a laser into the first empty slot.</summary>
    /// <param name="position">Where the laser appears.</param>
    /// <param name="direction">The direction it travels in.</param>
    /// <param name="laser">The laser that was fired, or null when every slot is busy.</param>
    /// <returns>True when a laser was fired, false when all slots are live.</returns>
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

    /// <summary>Updates every laser that is still alive.</summary>
    /// <param name="gameTime">Elapsed time for this tick.</param>
    /// <param name="field">The playfield.</param>
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

    /// <summary>The lasers currently alive, never more than <see cref="Capacity"/>.</summary>
    public IEnumerable<PlayerLaser> ActiveLasers =>
        _slots.Where(laser => laser is { LifeState: EntityLifeState.Alive }).Select(laser => laser!);
}
