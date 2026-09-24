using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Level;
using Robotron2084.Rendering;
using Robotron2084.Tuning;

namespace Robotron2084.Entities;

/// <summary>The tank's shell: aimed once, flies straight, bounces off the walls and fizzles out.</summary>
/// <seealso cref="Tank"/>
/// <seealso cref="PlayField"/>
/// <remarks>ROM: RRTK4.ASM's <c>SHELL</c>/<c>SHELLP</c>/<c>SHLDIE</c> (notes §11.5). It is aimed once
/// at the player with ±1 px/frame jitter per axis ("not very accurate") — the arcade's spread comes
/// from a speed table the port does not yet reproduce, so the jitter is an approximation and still an
/// open item. It then flies straight on the shared mover, bouncing off all four border walls with a
/// bounce sound, and fizzles out after a random 48-79 ROM frames. A shell flies over electrodes and
/// never collides with one, and its box is the picture's own 8x7 arcade px.
/// Timers count 5 per tick and 6 per arcade frame, so an interval of N frames is due at 6 x N.</remarks>
public sealed class TankShell : IEntity, IAnimationFrameSource, IRemovable
{
    private readonly SpriteSet _sprites;
    /// <summary>The shell picture's own 8x7 arcade px box, in port pixels.</summary>
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
    /// <remarks>ROM: the lifespan is a random 48-79 ROM frames, rolled once here.</remarks>
    public TankShell(SpriteSet sprites, IntVector2 position, IntVector2 towardPlayerDirection, Random random)
    {
        _sprites = sprites;
        _position = position;
        // Aimed once, with ±1 px/frame jitter per axis ("not very accurate").
        _velocity = new IntVector2(
            Math.Sign(towardPlayerDirection.X) * GameplayConstants.TankShellSpeed + random.Next(-1, 2),
            Math.Sign(towardPlayerDirection.Y) * GameplayConstants.TankShellSpeed + random.Next(-1, 2));
        // Counts up to the fizzle: 5 per tick, 6 per arcade frame.
        _remainingLife = (random.Next(0, 32) + GameplayConstants.TankShellLifeBaseRomTicks) * 6;
        _moveTimer = 6;
    }

    /// <summary>Top-left of the collision box.</summary>
    public IntVector2 Position => _position;

    /// <summary>The shell picture's own 8x7 box at <see cref="Position"/>.</summary>
    public Rectangle Bounds => new(_position.X, _position.Y, BoxWidth, BoxHeight);

    /// <summary>Only ever transitions Alive -> Dead (immediate removal, no death animation).</summary>
    public EntityLifeState LifeState { get; private set; } = EntityLifeState.Alive;

    /// <summary>Hit: removed from the screen immediately (spec + ROM).</summary>
    public void Kill() => LifeState = EntityLifeState.Dead;

    /// <summary>True when this update bounced off a border wall, so the sound can be played.</summary>
    public bool BouncedThisUpdate { get; private set; }

    /// <summary>Ages the shell, moves it one frame's worth and bounces it off the walls.</summary>
    /// <param name="gameTime">Unused — the clocks are counted in ticks.</param>
    /// <param name="field">The playfield wall.</param>
    public void Update(GameTime gameTime, PlayField field)
    {
        if (LifeState == EntityLifeState.Dead)
        {
            return;
        }

        // Fizzles out at zero. The arcade does NOT decrement its per-wave shell counter here.
        _remainingLife -= 5;
        if (_remainingLife <= 0)
        {
            LifeState = EntityLifeState.Dead;
            return;
        }

        // One velocity integration per ROM frame; X is bounced before Y (see the remarks).
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
    /// <param name="sprites">The shared sprite set.</param>
    public void Draw(SpriteBatch spriteBatch)
    {
        if (LifeState == EntityLifeState.Alive)
        {
            _sprites.DrawSprite(spriteBatch, CurrentAnimationFrame, Bounds, Color.White);
        }
    }

    /// <summary>The shell picture — it never flashes.</summary>
    /// <param name="sprites">The shared sprite set.</param>
    public Texture2D CurrentAnimationFrame => _sprites.TankShell;
}
