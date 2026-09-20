using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Level;
using Robotron2084.Rendering;
using Robotron2084.Tuning;

namespace Robotron2084.Entities;

/// <summary>
/// The missile a tank fires at the player. It is aimed once, when it is created, and then flies
/// in a straight line until it fizzles out — bouncing off the border walls rather than leaving
/// the playfield, and passing over electrodes. A laser hit removes it instantly, with no death
/// animation.
/// </summary>
/// <remarks>
/// From the shell routines in RRTK4.ASM (`SHELL`, `SHELLP`, `SHLDIE`; R5 disassembly 4E46-4FCF,
/// notes §11.5):
///
/// - it is AIMED ONCE, straight at the player, with ±1 px/frame of jitter on each axis ("not
///   very accurate"). The arcade's spread comes from a `SHLSPD` table, so the port's jitter is an
///   approximation and is still an open item;
/// - it then flies straight (the ROM's generic mover, `OPB80`, in RRS22.ASM) and BOUNCES off all
///   four border walls — the ROM negates the X delta in `XVNEG` and the Y delta in `YVNEG` and
///   asks for the bounce sound;
/// - it fizzles out after `(RND &amp; $1F) + $30` ROM frames.
///
/// Spec: "TANK SHELLS fly over electrodes" — a shell never collides with an electrode. The
/// collision box is the shell picture's own size: `SHLP1` is `FCB 4,7`, i.e. 4 bytes wide (a
/// byte is 2 pixels) by 7 rows = 8x7 px (notes §53). The arcade's 20-shells-per-wave fire
/// counter (`TNKFIR`) and the fizzle bug that goes with it live on <see cref="PlayField"/>.
/// </remarks>
public sealed class TankShell : IEntity
{
    /// <summary>The collision box: the shell picture's own size, 8x7 arcade px, in port pixels.</summary>
    /// <remarks>The ROM bounds and collides a projectile against the picture it is showing —
    /// `SHLP1` is `FCB 4,7` — which is why the box is the art's size (notes §53).</remarks>
    private static readonly int BoxWidth = ScreenSize.Scaled(GameplayConstants.TankShellCollisionSize.Width);
    private static readonly int BoxHeight = ScreenSize.Scaled(GameplayConstants.TankShellCollisionSize.Height);
    private IntVector2 _position;
    private IntVector2 _velocity; // ROM OXV/OYV: port px per ROM FRAME (the generic mover's step)
    private int _remainingLifeFifths;
    private int _moverSixths; // OPB80 cadence: one velocity integration per 6 sixths = 1 ROM frame

    /// <summary>Fires a shell from the given position, aimed once at the player.</summary>
    /// <param name="position">Where the shell starts — the tank's muzzle, in port pixels.</param>
    /// <param name="towardPlayerDirection">Direction from the tank to the player; only its SIGNS are used, so the aim is always a 45° line.</param>
    /// <param name="random">Source of the ±1 px/frame aim jitter and of the fizzle time.</param>
    /// <remarks>R5 4F82-4F8A: lifespan = `(RND &amp; $1F) + $30` ROM frames (48..79).</remarks>
    public TankShell(IntVector2 position, IntVector2 towardPlayerDirection, Random random)
    {
        _position = position;
        // Aimed at the player with ±1 px/frame jitter per axis ("not very
        // accurate"); the ROM sets its velocity once at creation too. The
        // speed is a per-FRAME value — the generic mover integrates it once per
        // ROM frame, not per tick (notes §93).
        _velocity = new IntVector2(
            Math.Sign(towardPlayerDirection.X) * GameplayConstants.TankShellSpeed + random.Next(-1, 2),
            Math.Sign(towardPlayerDirection.Y) * GameplayConstants.TankShellSpeed + random.Next(-1, 2));
        // R5 4F82-4F8A: lifespan = (RND & $1F) + $30 ROM frames (48..79).
        // Held in exact 6ths (notes §52/§65): 48 frames is 57.6 port ticks, which
        // truncated PortTicks(48) = 57 cut short.
        _remainingLifeFifths = (random.Next(0, 32) + GameplayConstants.TankShellLifeBaseRomTicks) * 6;
        // The mover moves it from the first frame (notes §93).
        _moverSixths = 6;
    }

    /// <summary>Top-left of the collision box.</summary>
    public IntVector2 Position => _position;

    /// <summary>The shell picture's own 8x7 box at <see cref="Position"/>.</summary>
    public Rectangle Bounds => new(_position.X, _position.Y, BoxWidth, BoxHeight);

    /// <summary>Only ever transitions Alive -> Dead (immediate removal, no death animation).</summary>
    public EntityLifeState LifeState { get; private set; } = EntityLifeState.Alive;

    /// <summary>Laser hit: removed from the screen immediately (spec + ROM).</summary>
    public void Destroy() => LifeState = EntityLifeState.Dead;

    /// <summary>
    /// True when this update bounced off a border wall, so the playfield can play the bounce
    /// sound. The playfield reads it and clears it each tick.
    /// </summary>
    /// <remarks>R5 $4FCD requests the bounce sound there.</remarks>
    public bool BouncedThisUpdate { get; private set; }

    /// <summary>
    /// Ages the shell, moves it one ROM frame's worth when the mover's clock says so, and bounces
    /// it off the border walls; a shell past its lifespan is removed.
    /// </summary>
    /// <param name="gameTime">Unused — the shell's clocks are counted in ROM frames.</param>
    /// <param name="field">The playfield wall the shell bounces off.</param>
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

        // Straight-line flight: the generic mover (OPB80, notes §43/§93) integrates
        // the velocity once per ROM frame — a frame is 6/5 of a tick, so every 6
        // sixths, not every tick (that ran the shell 60/50 = 20% fast). Bounces off
        // the four border walls (R5 4F94-4FCD: COM the delta — the X wall is
        // checked before the Y wall — then the shell keeps flying. It never exits
        // the playfield.)
        _moverSixths += 5;
        if (_moverSixths >= 6)
        {
            _moverSixths -= 6;
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
    }

    /// <summary>Draws the shell picture at its own size; it never flashes.</summary>
    /// <param name="spriteBatch">The batch to draw into.</param>
    /// <param name="sprites">The shared sprite set, which holds the shell picture.</param>
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
