using System.Diagnostics.CodeAnalysis;
using Microsoft.Xna.Framework;
using Robotron2084.Core;
using Robotron2084.Level;

namespace Robotron2084.Entities;

/// <summary>
/// The player's lasers, held as three numbered slots so the fire button can tell whether
/// another shot is allowed. A slot frees the moment its laser is deactivated, so the next
/// shot reuses it (spec: "Record these as SLOTs").
/// </summary>
/// <remarks>
/// The arcade keeps each player laser in an object record of its own too (RRG23.ASM is
/// "ROBOT GAME — player, lasers", and RRS22.ASM carries the <c>LASER</c> drawing routine),
/// and the fire button only produces a new one while a slot is free — which is why three is
/// the binding limit while the port's auto-fire is held down (notes (24).1).
/// </remarks>
public sealed class LaserSlots
{
    /// <summary>How many lasers the player can have in flight at once.</summary>
    public const int Capacity = 3;

    private readonly PlayerLaser?[] _slots = new PlayerLaser?[Capacity];

    /// <summary>Every slot in order; null means that slot is empty. Exposed for tests and diagnostics.</summary>
    public IReadOnlyList<PlayerLaser?> Slots => _slots;

    /// <summary>Fires a laser into the first empty slot.</summary>
    /// <param name="position">Where the laser appears — the muzzle offset for the firing direction.</param>
    /// <param name="direction">The direction the laser travels in (it never changes).</param>
    /// <param name="laser">The laser that was fired, or null when every slot is busy.</param>
    /// <returns>True when a laser was fired; false when all <see cref="Capacity"/> slots hold live lasers.</returns>
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

    /// <summary>Updates every laser that is still alive; empty slots are skipped.</summary>
    /// <param name="gameTime">Elapsed time for this tick.</param>
    /// <param name="field">The playfield, which resolves what each laser hits.</param>
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
