using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Level;
using Robotron2084.Rendering;
using Robotron2084.Tuning;

namespace Robotron2084.Entities;

/// <summary>
/// Enemy missile fired by Tanks — identical structure to <see cref="Spark"/>
/// (4x4, immediate removal on laser hit) but with ROM-faithful flight (R5
/// disasm 4E46-4FCF, notes §11.5): aimed ONCE at spawn (player-aimed delta
/// with ±1 px/tick jitter — the port's approximation of the ROM's SHLSPD-based
/// aim spread, which is still pending), then flies in a straight line,
/// BOUNCES off all four border walls (delta sign-flip + sound, never leaves
/// the playfield), and fizzles after (RND &amp; $1F) + $30 ROM ticks. Spec:
/// "TANK SHELLS fly over electrodes" (missiles never collide with electrodes).
/// The arcade's 20-shells-per-wave fire counter (the fizzle bug) lives on
/// <see cref="PlayField"/>.
/// </summary>
public sealed class TankShell : IEntity
{
    /// <summary>
    /// The ROM bounds and collides a projectile against the picture it is showing:
    /// `SHLP1` is `FCB 4,7` — 4 BYTES wide (a byte is 2 px) by 7 rows = 8x7 px
    /// (notes §53).
    /// </summary>
    private static readonly int BoxWidth = ScreenSize.Scaled(GameplayConstants.TankShellCollisionSize.Width);
    private static readonly int BoxHeight = ScreenSize.Scaled(GameplayConstants.TankShellCollisionSize.Height);
    private IntVector2 _position;
    private IntVector2 _velocity;
    private int _remainingLifeFifths;

    public TankShell(IntVector2 position, IntVector2 towardPlayerDirection, Random random)
    {
        _position = position;
        // Aimed at the player with ±1 px/tick jitter per axis ("not very
        // accurate"); the ROM sets its velocity once at creation too.
        _velocity = new IntVector2(
            Math.Sign(towardPlayerDirection.X) * GameplayConstants.TankShellSpeed + random.Next(-1, 2),
            Math.Sign(towardPlayerDirection.Y) * GameplayConstants.TankShellSpeed + random.Next(-1, 2));
        // R5 4F82-4F8A: lifespan = (RND & $1F) + $30 ROM frames (48..79).
        // Held in exact 6ths (notes §52/§65): 48 frames is 57.6 port ticks, which
        // truncated PortTicks(48) = 57 cut short.
        _remainingLifeFifths = (random.Next(0, 32) + GameplayConstants.TankShellLifeBaseRomTicks) * 6;
    }

    public IntVector2 Position => _position;

    public Rectangle Bounds => new(_position.X, _position.Y, BoxWidth, BoxHeight);

    /// <summary>Only ever transitions Alive -> Dead (immediate removal, no death animation).</summary>
    public EntityLifeState LifeState { get; private set; } = EntityLifeState.Alive;

    /// <summary>Laser hit: removed from the screen immediately (spec + ROM).</summary>
    public void Destroy() => LifeState = EntityLifeState.Dead;

    /// <summary>
    /// Set when this update bounced off a border wall (R5 $4FCD requests the
    /// bounce sound $4B16 there). PlayField reads + clears it.
    /// </summary>
    public bool BouncedThisUpdate { get; private set; }

    public void Update(GameTime gameTime, PlayField field)
    {
        if (LifeState == EntityLifeState.Dead)
        {
            return;
        }

        // Fizzles out after the ROM lifespan (4FBB: counter hits 0 -> remove;
        // note the arcade counter at $98F1 is NOT decremented — fizzle bug).
        _remainingLifeFifths -= 5;
        if (_remainingLifeFifths <= 0)
        {
            LifeState = EntityLifeState.Dead;
            return;
        }

        // Straight-line flight; bounces off the four border walls (R5
        // 4F94-4FCD: COM the delta — the X wall is checked before the Y wall —
        // then the shell keeps flying. It never exits the playfield.)
        IntVector2 next = _position + _velocity;
        Rectangle bounds = field.Wall.PlayfieldBounds;
        BouncedThisUpdate = false;
        if (next.X < bounds.X || next.X + BoxWidth > bounds.Right)
        {
            _velocity = new IntVector2(-_velocity.X, _velocity.Y);
            BouncedThisUpdate = true;
        }
        else if (next.Y < bounds.Y || next.Y + BoxHeight > bounds.Bottom)
        {
            _velocity = new IntVector2(_velocity.X, -_velocity.Y);
            BouncedThisUpdate = true;
        }

        next = _position + _velocity;
        _position = new IntVector2(
            Math.Clamp(next.X, bounds.X, bounds.Right - BoxWidth),
            Math.Clamp(next.Y, bounds.Y, bounds.Bottom - BoxHeight));
    }

    public void Draw(SpriteBatch spriteBatch, SpriteSet sprites)
    {
        // Solid; the spec requires no flashing for missiles. The picture IS the
        // collision box now (8x7 px, notes §53), so it draws at its ROM size.
        if (LifeState == EntityLifeState.Alive)
        {
            sprites.DrawSprite(spriteBatch, sprites.TankShell, Bounds, Color.White);
        }
    }
}
