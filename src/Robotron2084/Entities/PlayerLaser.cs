using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Graphics;
using Robotron2084.Level;
using Robotron2084.Tuning;

namespace Robotron2084.Entities;

/// <summary>The player's laser shot: a straight bolt that flies until it hits a wall or a robot, then vanishes.</summary>
/// <seealso cref="LaserSlots" />
/// <remarks>
///     It has no beat and no timer. <see cref="LaserSlots" /> holds it and calls <see cref="Update" />. The
///     <see cref="PlayField" /> calls <see cref="LaserSlots.Update" /> on every tick, in <see cref="PlayField.Update" />.
///     The one time it does not is during the short freeze just after the player is killed.
///     <list type="bullet">
///         <item>
///             Original source: <c>RRG23.ASM</c>, routine <c>LTAB</c> (the table of laser sprites:
///             <c>LLPC</c>/<c>ULPC</c>/<c>DLLPC</c>/<c>ULLPC</c>)
///         </item>
///         <item>Disassembly: <c>asm/robomame.asm</c> at <c>$3237</c> (<c>LASER_DESCRIPTOR TABLE</c>)</item>
///     </list>
/// </remarks>
public sealed class PlayerLaser : IEntity, IAnimationFrameSource, IRemovable
{
    private static readonly int Size = ScreenSize.ToPortPixelsFromArcadePixels(CollisionSizes.MissileSizeArcadePixels);
    private readonly SpriteSet _sprites;
    private IntVector2 _position;

    /// <summary>Makes a laser that flies in the given direction.</summary>
    /// <param name="sprites">The shared sprite set.</param>
    /// <param name="position">Where it starts. The player works this out from the way it is fired.</param>
    /// <param name="direction">The way it flies. This never changes.</param>
    public PlayerLaser(SpriteSet sprites, IntVector2 position, Direction8 direction)
    {
        _sprites = sprites;
        _position = position;
        Direction = direction;
    }

    /// <summary>The way the laser flies. It never changes.</summary>
    public Direction8 Direction { get; }

    /// <summary>The sprite for the way this laser is flying. The arcade has four laser sprites (<c>LTAB</c>, notes §19).</summary>
    public Texture2D GetCurrentAnimationFrame()
    {
        return Direction switch
        {
            Direction8.Left or Direction8.Right => _sprites.LaserBarSprite,
            Direction8.Up or Direction8.Down => _sprites.LaserColumnSprite,
            Direction8.UpLeft or Direction8.DownRight => _sprites.LaserDiagonalMainSprite,
            Direction8.DownLeft or Direction8.UpRight => _sprites.LaserDiagonalAntiSprite,
            _ => throw new InvalidOperationException($"Unexpected laser direction {Direction}")
        };
    }

    /// <summary>The box used to tell what the laser hits.</summary>
    public Rectangle GetBounds()
    {
        return new Rectangle(_position.X, _position.Y, Size, Size);
    }

    /// <summary>Alive until it reaches the wall or hits something. Then it is dead at once.</summary>
    public EntityLifeState LifeState { get; private set; } = EntityLifeState.Alive;

    /// <summary>Where the top-left corner of the laser's box is.</summary>
    public IntVector2 Position => _position;

    /// <summary>Draws the sprite for the way this laser is flying.</summary>
    /// <param name="spriteBatch">What the laser is drawn with.</param>
    public void Draw(SpriteBatch spriteBatch)
    {
        if (!this.IsAlive()) return;

        // The laser's palette slot keeps changing colour, so the whole laser flashes.
        _sprites.Blitter.DrawSpriteSolid(spriteBatch, GetCurrentAnimationFrame(), GetBounds(),
            _sprites.Blitter.GetSlotColour(PlayerTuning.LaserSlot));
    }

    /// <summary>Runs one tick. The laser flies one step the way it is going, and dies if it reaches the wall.</summary>
    /// <param name="gameTime">Not used. The laser goes the same distance on every tick.</param>
    /// <param name="field">The playfield. It tells the laser when it has reached the wall, and makes the flash there.</param>
    /// <remarks>ROM: RRG23.ASM's <c>LASDIE</c>.</remarks>
    public void Update(GameTime gameTime, PlayField field)
    {
        if (!this.IsAlive()) return;

        // The wall is checked along the whole of this move, not just where the laser ends up. Otherwise a laser could jump right over the wall.
        var boundsBeforeMove = GetBounds();
        _position += Direction.ToIntVector() * PlayerTuning.LaserSpeed;
        if (field.HitsWall(Rectangle.Union(boundsBeforeMove, GetBounds())))
        {
            // The laser stops at the wall and leaves a short flash there (ROM: RRG23 LASDIE).
            field.SpawnLaserWallFlare(GetBounds(), Direction);
            Kill();
        }
    }

    /// <summary>Kills the laser at once, which frees its slot for a new laser.</summary>
    public void Kill()
    {
        if (!this.IsAlive()) return;

        LifeState = EntityLifeState.Dead;
    }
}
