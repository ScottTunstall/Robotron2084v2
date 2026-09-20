using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Level;
using Robotron2084.Rendering;
using Robotron2084.Tuning;

namespace Robotron2084.Entities;

/// <summary>
/// One of the player's lasers: a fast, straight-line projectile that never changes direction
/// and vanishes the moment it reaches the wall. Flying and the wall test are its own; what it
/// HITS is resolved centrally by <see cref="PlayField"/>, which also frees its slot in
/// <see cref="LaserSlots"/>. There is no death animation — the laser is simply gone.
/// </summary>
/// <remarks>
/// The collision box is a 4x4 spec-pixel square and the speed is 12 px/tick, far quicker than
/// the player. The picture is one of the ROM's four laser shapes (R5 `$35BE-$35DC`: `LLPC`,
/// `ULPC`, `DLLPC`, `ULLPC`), chosen for the direction by the ROM's `LTAB` table in RRG23.ASM.
/// The arcade never flips the art, so neither does the port, and the picture is centred in the
/// collision box (see <see cref="SpriteSet"/>'s Laser* properties).
/// </remarks>
public sealed class PlayerLaser : IEntity
{
    private static readonly int Size = ScreenSize.Scaled(GameplayConstants.MissileSizeSpecPixels);

    private IntVector2 _position;

    /// <summary>Starts a laser travelling in the given direction.</summary>
    /// <param name="position">Where it starts — the player's muzzle offset for that direction.</param>
    /// <param name="direction">The direction it travels in; it never changes.</param>
    public PlayerLaser(IntVector2 position, Direction8 direction)
    {
        _position = position;
        Direction = direction;
    }

    /// <summary>The direction the laser travels (never changes, spec).</summary>
    public Direction8 Direction { get; }

    /// <summary>Top-left of the collision box.</summary>
    public IntVector2 Position => _position;

    /// <summary>The 4x4 spec-pixel collision box at <see cref="Position"/>.</summary>
    public Rectangle Bounds => new(_position.X, _position.Y, Size, Size);

    /// <summary>Alive until it hits a wall or is hit; then immediately dead.</summary>
    public EntityLifeState LifeState { get; private set; } = EntityLifeState.Alive;

    /// <summary>Removes the laser from the screen immediately, vacating its slot.</summary>
    public void Deactivate() => LifeState = EntityLifeState.Dead;

    /// <summary>
    /// Flies one step in <see cref="Direction"/>, and deactivates as soon as it reaches the
    /// wall — leaving the brief flare the wave's laser colour draws there.
    /// </summary>
    /// <param name="gameTime">Unused — the laser moves a fixed step per tick.</param>
    /// <param name="field">The playfield, for the wall test and the flare.</param>
    /// <remarks>ROM RRG23 `LASDIE` (notes §63): 2 frames of flare, then the wall colour.</remarks>
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

    /// <summary>Draws the picture for this laser's direction.</summary>
    /// <param name="spriteBatch">The batch to draw into.</param>
    /// <param name="sprites">The shared sprite set, which holds the four laser pictures.</param>
    /// <remarks>ROM RRG23 `LTAB` (2026-09-12 (19)): each of the 8 directions picks one of the
    /// four pictures — no flipping.</remarks>
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
