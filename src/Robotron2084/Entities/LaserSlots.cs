using System.Diagnostics.CodeAnalysis;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Graphics;
using Robotron2084.Level;

namespace Robotron2084.Entities;

/// <summary>Keeps track of the player's laser shots on screen, and stops there ever being more than three at once.</summary>
/// <remarks>
/// <list type="bullet">
/// <item>Original source: <c>RRG23.ASM</c>, routine <c>LSPROC</c> (checks <c>LCNT</c> against the limit of 3 before creating a new laser), drawn by <c>RRS22.ASM</c>'s <c>LASER</c></item>
/// <item>Disassembly: <c>asm/robomame.asm</c> at <c>$31D5</c>-<c>$31E5</c> (checks how many player lasers have been fired before allowing another)</item>
/// </list>
/// </remarks>
public sealed class LaserSlots
{
    /// <summary>How many lasers the player can have in flight at once.</summary>
    public const int Capacity = 3;

    private readonly PlayerLaser?[] _slots = new PlayerLaser?[Capacity];
    private readonly SpriteSet _sprites;

    /// <summary>Wires the slots to the sprites their lasers are drawn with.</summary>
    /// <param name="sprites">The shared sprite set.</param>
    public LaserSlots(SpriteSet sprites) => _sprites = sprites;

    /// <summary>The lasers currently alive, never more than <see cref="Capacity"/>.</summary>
    public IEnumerable<PlayerLaser> GetActiveLasers() =>
        _slots.OfType<PlayerLaser>().Where(laser => laser.IsAlive());

    /// <summary>Every slot in order; null means empty. Exposed for tests and diagnostics.</summary>
    public IReadOnlyList<PlayerLaser?> Slots => _slots;

    /// <summary>Draws every laser that is in flight.</summary>
    /// <param name="spriteBatch">The batch to draw into.</param>
    public void Draw(SpriteBatch spriteBatch)
    {
        foreach (PlayerLaser? laser in _slots)
        {
            laser?.Draw(spriteBatch);
        }
    }

    /// <summary>Fires a laser into the first empty slot.</summary>
    /// <param name="position">Where the laser appears.</param>
    /// <param name="direction">The direction it travels in.</param>
    /// <param name="laser">The laser that was fired, or null when every slot is busy.</param>
    /// <returns>True when a laser was fired, false when all slots are live.</returns>
    public bool TryFire(IntVector2 position, Direction8 direction, [NotNullWhen(true)] out PlayerLaser? laser)
    {
        for (int i = 0; i < Capacity; i++)
        {
            if (_slots[i] is not { } existing || !existing.IsAlive())
            {
                laser = new PlayerLaser(_sprites, position, direction);
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
            if (_slots[i] is { } slot && slot.IsAlive())
            {
                slot.Update(gameTime, field);
            }
        }
    }
}
