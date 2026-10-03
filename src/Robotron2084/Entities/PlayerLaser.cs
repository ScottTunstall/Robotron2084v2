using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Graphics;
using Robotron2084.Level;
using Robotron2084.Tuning;

namespace Robotron2084.Entities;

/// <summary>The player's laser shot: a straight bolt that flies until it hits a wall or a robot, then vanishes.</summary>
/// <seealso cref="LaserSlots"/>
/// <remarks>
/// <list type="bullet">
/// <item>Original source: <c>RRG23.ASM</c>, routine <c>LTAB</c> (sprite lookup for shapes <c>LLPC</c>/<c>ULPC</c>/<c>DLLPC</c>/<c>ULLPC</c>)</item>
/// <item>Disassembly: <c>asm/robomame.asm</c> at <c>$3237</c> (<c>LASER_DESCRIPTOR TABLE</c>)</item>
/// </list>
/// </remarks>
public sealed class PlayerLaser : IEntity, IAnimationFrameSource, IRemovable
{
    private static readonly int Size = ScreenSize.ToPortPixels(CollisionSizes.MissileSizeSpecPixels);
    private readonly SpriteSet _sprites;
    private IntVector2 _position;

    /// <summary>Starts a laser travelling in the given direction.</summary>
    /// <param name="sprites">The shared sprite set.</param>
    /// <param name="position">Where it starts — the player's muzzle offset for that direction.</param>
    /// <param name="direction">The direction it travels in; it never changes.</param>
    public PlayerLaser(SpriteSet sprites, IntVector2 position, Direction8 direction)
    {
        _sprites = sprites;
        _position = position;
        Direction = direction;
    }

    /// <summary>The 4x4 spec-pixel collision box at <see cref="Position"/>.</summary>
    public Rectangle Bounds => new(_position.X, _position.Y, Size, Size);

    /// <summary>The sprite for this laser's direction — the ROM's four laser sprites (`LTAB`, notes §19).</summary>
    public Texture2D GetCurrentAnimationFrame() =>
        Direction switch
        {
            Direction8.Left or Direction8.Right => _sprites.LaserBar,
            Direction8.Up or Direction8.Down => _sprites.LaserColumn,
            Direction8.UpLeft or Direction8.DownRight => _sprites.LaserDiagonalMain,
            Direction8.DownLeft or Direction8.UpRight => _sprites.LaserDiagonalAnti,
            _ => throw new InvalidOperationException($"Unexpected laser direction {Direction}"),
        };

    /// <summary>The direction the laser travels; never changes.</summary>
    public Direction8 Direction { get; }

    /// <summary>Alive until it hits a wall or is hit; then immediately dead.</summary>
    public EntityLifeState LifeState { get; private set; } = EntityLifeState.Alive;

    /// <summary>Top-left of the collision box.</summary>
    public IntVector2 Position => _position;

    /// <summary>Draws the sprite for this laser's direction.</summary>
    /// <param name="spriteBatch">The batch to draw into.</param>
    public void Draw(SpriteBatch spriteBatch)
    {
        if (LifeState != EntityLifeState.Alive)
        {
            return;
        }

        // The ROM draws the laser in its flashing palette slot, so the whole bolt flashes with it.
        _sprites.Blitter.DrawSpriteSolid(spriteBatch, GetCurrentAnimationFrame(), Bounds, _sprites.Blitter.GetSlotColour(PlayerTuning.LaserSlot));
    }

    /// <summary>Removes the laser at once, vacating its slot.</summary>
    public void Kill()
    {
        if (LifeState != EntityLifeState.Alive)
        {
            return;
        }

        LifeState = EntityLifeState.Dead;
    }

    /// <summary>Flies one step in <see cref="Direction"/> and dies at the wall.</summary>
    /// <param name="gameTime">Unused — the laser moves a fixed step per tick.</param>
    /// <param name="field">The playfield, for the wall test and the flare.</param>
    /// <remarks>ROM: RRG23.ASM's <c>LASDIE</c>; the flare lasts 2 frames.</remarks>
    public void Update(GameTime gameTime, PlayField field)
    {
        if (LifeState != EntityLifeState.Alive)
        {
            return;
        }

        // The wall is tested along the whole step, not only where the laser lands: a laser fired from beside the
        // wall can start inside it and finish a step past it, and would otherwise fly on.
        Rectangle before = Bounds;
        _position += Direction.ToIntVector() * PlayerTuning.LaserSpeed;
        if (field.Wall.Intersects(Rectangle.Union(before, Bounds)))
        {
            // RRG23 LASDIE: a brief flare in the wave's LASCOL slot, then the wall colour.
            field.SpawnLaserWallFlare(Bounds, Direction);
            Kill();
        }
    }
}
