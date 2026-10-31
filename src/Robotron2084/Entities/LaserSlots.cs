using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Graphics;
using Robotron2084.Level;

namespace Robotron2084.Entities;

/// <summary>Keeps track of the player's laser shots on screen, and stops there ever being more than three at once.</summary>
/// <remarks>
///     <list type="bullet">
///         <item>
///             Original source: <c>RRG23.ASM</c>, routine <c>LSPROC</c> (checks <c>LCNT</c> against the limit of
///             3 before creating a new laser), drawn by <c>RRS22.ASM</c>'s <c>LASER</c>
///         </item>
///         <item>
///             Disassembly: <c>asm/robomame.asm</c> at <c>$31D5</c>-<c>$31E5</c> (checks how many player lasers
///             have been fired before allowing another)
///         </item>
///     </list>
/// </remarks>
public sealed class LaserSlots
{
    /// <summary>How many lasers the player can have in flight at once.</summary>
    public const int Capacity = 3;

    private readonly PlayerLaser?[] _slots = new PlayerLaser?[Capacity];
    private readonly SpriteSet _spriteSet;

    /// <summary>Makes the empty slots, and keeps the sprites that new lasers are drawn with.</summary>
    /// <param name="spriteSet">The shared sprite set.</param>
    public LaserSlots(SpriteSet spriteSet)
    {
        _spriteSet = spriteSet;
    }

    /// <summary>Every slot in order. An empty slot is null. Tests and debugging tools use this.</summary>
    public IReadOnlyList<PlayerLaser?> Slots => _slots;

    /// <summary>Draws every laser that is in flight.</summary>
    /// <param name="spriteBatch">What the lasers are drawn with.</param>
    public void Draw(SpriteBatch spriteBatch)
    {
        foreach (var laser in _slots) laser?.Draw(spriteBatch);
    }

    /// <summary>Fires a laser into the first empty slot. Call <see cref="HasFreeSlot" /> first.</summary>
    /// <param name="position">Where the laser appears.</param>
    /// <param name="direction">The way it flies.</param>
    /// <exception cref="InvalidOperationException">Every slot is in use.</exception>
    public void Fire(IntVector2 position, Direction8 direction)
    {
        for (var i = 0; i < Capacity; i++)
            if (_slots[i] is not { } existing || !existing.IsAlive())
            {
                _slots[i] = new PlayerLaser(_spriteSet, position, direction);
                return;
            }

        throw new InvalidOperationException("Every laser slot is in use.");
    }

    /// <summary>The lasers that are in flight now. There are never more than <see cref="Capacity" />.</summary>
    public IEnumerable<PlayerLaser> GetActiveLasers()
    {
        return _slots.OfType<PlayerLaser>().Where(laser => laser.IsAlive());
    }

    /// <summary>Says whether a slot is free, so that <see cref="Fire" /> can put a laser into it.</summary>
    public bool HasFreeSlot()
    {
        for (var i = 0; i < Capacity; i++)
            if (_slots[i] is not { } existing || !existing.IsAlive())
                return true;

        return false;
    }

    /// <summary>Moves every laser that is in flight on by one <see cref="Robotron2084.Core.ArcadeClock.PortTicksPerSecond">tick</see>.</summary>
    /// <param name="gameTime">The game's own clock for this <see cref="Robotron2084.Core.ArcadeClock.PortTicksPerSecond">tick</see>.</param>
    /// <param name="playField">The playfield.</param>
    public void Update(GameTime gameTime, PlayField playField)
    {
        for (var i = 0; i < Capacity; i++)
            if (_slots[i] is { } slot && slot.IsAlive())
                slot.Update(gameTime, playField);
    }
}
