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
/// <item>Original source: <c>RRG23.ASM</c>, routine <c>LSPROC</c> (checks <c>LCNT</c> against the limit of
/// 3 before creating a new laser), drawn by <c>RRS22.ASM</c>'s <c>LASER</c></item>
/// <item>Disassembly: <c>asm/robomame.asm</c> at <c>$31D5</c>-<c>$31E5</c> (checks how many player lasers
/// have been fired before allowing another)</item>
/// </list>
/// </remarks>
public sealed class LaserSlots
{
    /// <summary>How many lasers the player can have in flight at once. It is the size of <see cref="_slots"/>.</summary>
    public const int Capacity = 3;

    private readonly PlayerLaser?[] _slots = new PlayerLaser?[Capacity];
    private readonly SpriteSet _sprites;

    /// <summary>Makes the empty slots, and keeps the sprites that new lasers are drawn with.</summary>
    /// <param name="sprites">The shared sprite set.</param>
    public LaserSlots(SpriteSet sprites) => _sprites = sprites;

    /// <summary>The lasers that are in flight now. There are never more than <see cref="Capacity"/>.</summary>
    public IEnumerable<PlayerLaser> GetActiveLasers() =>
        _slots.OfType<PlayerLaser>().Where(laser => laser.IsAlive());

    /// <summary>Every slot in order. An empty slot is null. Tests and debugging tools use this.</summary>
    public IReadOnlyList<PlayerLaser?> Slots => _slots;

    /// <summary>Draws every laser that is in flight.</summary>
    /// <param name="spriteBatch">What the lasers are drawn with.</param>
    public void Draw(SpriteBatch spriteBatch)
    {
        foreach (PlayerLaser? laser in _slots)
        {
            laser?.Draw(spriteBatch);
        }
    }

    /// <summary>Fires a laser into the first empty slot.</summary>
    /// <param name="position">Where the laser appears.</param>
    /// <param name="direction">The way it flies.</param>
    /// <param name="laser">The laser that was fired, or null when every slot is in use.</param>
    /// <returns>True when a laser was fired, false when every slot is in use.</returns>
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

    /// <summary>Moves every laser that is in flight on by one tick.</summary>
    /// <param name="gameTime">The game's own clock for this tick.</param>
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
