using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Level;
using Robotron2084.Rendering;
using Robotron2084.Tuning;

namespace Robotron2084.Entities;

/// <summary>The player's laser: a straight bolt, gone the moment it hits the wall or a robot.</summary>
/// <seealso cref="LaserSlots"/>
/// <remarks>The box is a 4x4 spec-pixel square and it flies at
/// <see cref="GameplayConstants.LaserSpeed"/> px/tick, far quicker than the player. The picture is one of the ROM's four laser shapes (R5 $35BE-$35DC: <c>LLPC</c>,
/// <c>ULPC</c>, <c>DLLPC</c>, <c>ULLPC</c>), chosen for the direction by <c>LTAB</c> (RRG23.ASM) and
/// centred in the box — the arcade never flips the art (notes §19).</remarks>
public sealed class PlayerLaser : IEntity, IAnimationFrameSource, IRemovable
{
    private readonly SpriteSet _sprites;

    private static readonly int Size = ScreenSize.Scaled(GameplayConstants.MissileSizeSpecPixels);

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

    /// <summary>The direction the laser travels; never changes.</summary>
    public Direction8 Direction { get; }

    /// <summary>Top-left of the collision box.</summary>
    public IntVector2 Position => _position;

    /// <summary>The 4x4 spec-pixel collision box at <see cref="Position"/>.</summary>
    public Rectangle Bounds => new(_position.X, _position.Y, Size, Size);

    /// <summary>Alive until it hits a wall or is hit; then immediately dead.</summary>
    public EntityLifeState LifeState { get; private set; } = EntityLifeState.Alive;

    /// <summary>Removes the laser at once, vacating its slot.</summary>
    public void Kill() => LifeState = EntityLifeState.Dead;

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

        _position += Direction.ToIntVector() * GameplayConstants.LaserSpeed;
        if (field.Wall.Intersects(Bounds))
        {
            // RRG23 LASDIE: a brief flare in the wave's LASCOL slot, then the wall colour.
            field.SpawnLaserWallFlare(Bounds, Direction);
            Kill();
        }
    }

    /// <summary>Draws the picture for this laser's direction.</summary>
    /// <param name="spriteBatch">The batch to draw into.</param>
    public void Draw(SpriteBatch spriteBatch)
    {
        if (LifeState != EntityLifeState.Alive)
        {
            return;
        }

        _sprites.DrawSprite(spriteBatch, CurrentAnimationFrame, Bounds, Color.White);
    }

    /// <summary>The picture for this laser's direction — the ROM's four laser arts (`LTAB`, notes §19).</summary>
    public Texture2D CurrentAnimationFrame => Direction switch
    {
        Direction8.Left or Direction8.Right => _sprites.LaserBar,
        Direction8.Up or Direction8.Down => _sprites.LaserColumn,
        Direction8.UpLeft or Direction8.DownRight => _sprites.LaserDiagonalMain,
        Direction8.DownLeft or Direction8.UpRight => _sprites.LaserDiagonalAnti,
        _ => throw new InvalidOperationException($"Unexpected laser direction {Direction}"),
    };

    /// <summary>Test-only positioning hook (InternalsVisibleTo the test assembly).</summary>
    internal void TeleportTo(IntVector2 position) => _position = position;
}
