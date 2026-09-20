using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Level;
using Robotron2084.Rendering;
using Robotron2084.Tuning;

namespace Robotron2084.Entities;

/// <summary>
/// The tank's projectile — the missile a tank enemy fires at the player. It is aimed once, at
/// the moment it's fired, and then flies in a straight line forever after (it never re-aims or
/// homes in); instead of leaving the playfield when it reaches a wall it bounces off like a ball,
/// and it passes harmlessly over electrode hazards rather than colliding with them. It
/// disappears on its own after a random amount of time ("fizzles out"), or instantly if the
/// player shoots it, with no death animation either way.
/// </summary>
/// <remarks>
/// <para>
/// See the terminology glossary on <see cref="IEntity"/> for what "ROM frame", the
/// "..Timer" fixed-point clock and "notes §NN" mean generally.
/// </para>
/// Ported from the arcade's own shell behaviour (ROM: RRTK4.ASM, the `SHELL`/`SHELLP`/`SHLDIE`
/// routines; notes §11.5):
///
/// - it is AIMED ONCE, straight at the player, with ±1 px/frame of jitter on each axis ("not
///   very accurate"). The arcade's spread comes from a speed table the port doesn't yet
///   reproduce exactly, so the port's jitter is an approximation and is still an open item;
/// - it then flies straight (the ROM's shared straight-line mover) and BOUNCES off all four
///   border walls, playing the bounce sound each time;
/// - it fizzles out after a random 48-79 ROM frames.
///
/// Spec: "TANK SHELLS fly over electrodes" — a shell never collides with an electrode. The
/// collision box is the shell picture's own size, 8x7 arcade px (notes §54). The arcade's
/// 20-shells-per-wave fire counter and the fizzle bug that goes with it live on
/// <see cref="PlayField"/>.
/// </remarks>
public sealed class TankShell : IEntity
{
    /// <summary>The collision box: the shell picture's own size, 8x7 arcade px, in port pixels.</summary>
    /// <remarks>The ROM collides a projectile against the picture it is showing, which is why the box
    /// is the art's own size, not a fixed cell (notes §54).</remarks>
    private static readonly int BoxWidth = ScreenSize.Scaled(GameplayConstants.TankShellCollisionSize.Width);
    private static readonly int BoxHeight = ScreenSize.Scaled(GameplayConstants.TankShellCollisionSize.Height);
    private IntVector2 _position;
    private IntVector2 _velocity; // how far the shell moves per ROM frame, in port px per axis (ROM: OXV/OYV)
    private int _remainingLife;
    private int _moveTimer; // counts up to one ROM frame's worth of ticks so the shell moves once per frame, not once per tick

    /// <summary>Fires a shell from the given position, aimed once at the player.</summary>
    /// <param name="position">Where the shell starts — the tank's muzzle, in port pixels.</param>
    /// <param name="towardPlayerDirection">Direction from the tank to the player; only its SIGNS are used, so the aim is always a 45° line.</param>
    /// <param name="random">Source of the ±1 px/frame aim jitter and of the fizzle time.</param>
    /// <remarks>The shell's lifespan is a random 48-79 ROM frames, rolled once here (ROM: notes §11.5).</remarks>
    public TankShell(IntVector2 position, IntVector2 towardPlayerDirection, Random random)
    {
        _position = position;
        // Aimed at the player with ±1 px/frame jitter per axis ("not very
        // accurate"); the velocity is set once here and never changes. It's a
        // per-FRAME value — the mover below integrates it once per ROM frame,
        // not per tick (notes §93).
        _velocity = new IntVector2(
            Math.Sign(towardPlayerDirection.X) * GameplayConstants.TankShellSpeed + random.Next(-1, 2),
            Math.Sign(towardPlayerDirection.Y) * GameplayConstants.TankShellSpeed + random.Next(-1, 2));
        // Lifespan: a random 48-79 ROM frames. Held in exact 6ths (notes
        // §52/§65) rather than rounded to whole ticks, which would cut the
        // shell's life slightly short.
        _remainingLife = (random.Next(0, 32) + GameplayConstants.TankShellLifeBaseRomTicks) * 6;
        // The mover moves it from the first frame (notes §93).
        _moveTimer = 6;
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
    /// <remarks>The ROM plays the bounce sound at the same point in its own bounce logic.</remarks>
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

        // Fizzles out once its lifespan counter reaches zero. Note: the arcade's
        // separate 20-shells-per-wave fire counter is NOT decremented when a shell
        // fizzles this way — that's a ROM bug the port reproduces (see PlayField).
        _remainingLife -= 5;
        if (_remainingLife <= 0)
        {
            LifeState = EntityLifeState.Dead;
            return;
        }

        // Straight-line flight: the shared mover (notes §43/§93) integrates the
        // velocity once per ROM frame — a frame is 6/5 of a tick, so every 6
        // fifth-ticks, not every tick (running it every tick made the shell 20% too
        // fast). Bounces off the four border walls by flipping the velocity's
        // sign on the axis that hit — X is checked before Y — then the shell
        // keeps flying; it never leaves the playfield.
        _moveTimer += 5;
        if (_moveTimer >= 6)
        {
            _moveTimer -= 6;
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
