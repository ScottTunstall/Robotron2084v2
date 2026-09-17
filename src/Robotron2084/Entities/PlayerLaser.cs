using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Level;
using Robotron2084.Rendering;
using Robotron2084.Tuning;

namespace Robotron2084.Entities;

/// <summary>
/// A player laser: a 4x4 (spec) collision box in a fixed direction, much
/// faster than the player (12 px/tick). Visible art = one of the four ROM
/// laser pictures (R5 $35BE-$35DC, LLPC/ULPC/DLLPC/ULLPC) picked by
/// direction per the ROM LTAB table — no flipping — and centered in the box
/// (see <see cref="SpriteSet"/>'s Laser* properties). Moves per tick,
/// deactivates (no death animation, spec) the moment it hits the wall;
/// robot/electrode/missile hits are resolved centrally in
/// <see cref="PlayField"/> (which also frees the slot).
/// </summary>
public sealed class PlayerLaser : IEntity
{
    private static readonly int Size = ScreenSize.Scaled(GameplayConstants.MissileSizeSpecPixels);

    private IntVector2 _position;

    public PlayerLaser(IntVector2 position, Direction8 direction)
    {
        _position = position;
        Direction = direction;
    }

    /// <summary>The direction the laser travels (never changes, spec).</summary>
    public Direction8 Direction { get; }

    public IntVector2 Position => _position;

    public Rectangle Bounds => new(_position.X, _position.Y, Size, Size);

    public EntityLifeState LifeState { get; private set; } = EntityLifeState.Alive;

    /// <summary>Removes the laser from the screen immediately, vacating its slot.</summary>
    public void Deactivate() => LifeState = EntityLifeState.Dead;

    public void Update(GameTime gameTime, PlayField field)
    {
        if (LifeState != EntityLifeState.Alive)
        {
            return;
        }

        _position += Direction.ToIntVector() * GameplayConstants.LaserSpeed;
        if (field.Wall.Intersects(Bounds))
        {
            // RRG23 LASDIE: a laser leaving the playfield leaves a brief flare in
            // the wave's LASCOL slot (notes §63) — 2 frames, then the wall colour.
            field.SpawnLaserWallFlare(Bounds, Direction);
            Deactivate();
        }
    }

    public void Draw(SpriteBatch spriteBatch, SpriteSet sprites)
    {
        if (LifeState != EntityLifeState.Alive)
        {
            return;
        }

        // ROM LTAB (RRG23, notes 2026-09-12 (19)): each of the 8 directions
        // picks one of the four laser pictures — no flipping.
        Texture2D art = Direction switch
        {
            Direction8.Left or Direction8.Right => sprites.LaserBar,
            Direction8.Up or Direction8.Down => sprites.LaserColumn,
            Direction8.UpLeft or Direction8.DownRight => sprites.LaserDiagonalMain,
            Direction8.DownLeft or Direction8.UpRight => sprites.LaserDiagonalAnti,
            _ => throw new InvalidOperationException($"Unexpected laser direction {Direction}"),
        };

        sprites.DrawSprite(spriteBatch, art, Bounds, Color.White);
    }

    /// <summary>Test-only positioning hook (InternalsVisibleTo the test assembly).</summary>
    internal void TeleportTo(IntVector2 position) => _position = position;
}
