using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Level;
using Robotron2084.Rendering;
using Robotron2084.Tuning;

namespace Robotron2084.Entities;

/// <summary>An enforcer — a small, fast robot spheroids drop. It grows in place, then flies and shoots.</summary>
/// <seealso cref="Spheroid"/>
/// <seealso cref="Spark"/>
/// <remarks>ROM: RRC11.ASM's <c>ENFR1</c>/<c>ENFNV</c>/<c>ENFDRP</c> (notes §17). Growing takes five
/// pictures of 9 frames (40 in all) and is immobile. It then aims at a spot in a 32x32 zone
/// down-right of the player and moves at half the remaining distance, so it loiters as it closes. It
/// flies over electrodes and dies outright when hit, with no Dying state and no flash. The fire timer
/// re-arms before the 20-spark cap is checked, so a shot the cap swallows is simply lost. Timers count
/// 5 per tick and 6 per arcade frame, so an interval of N frames is due at 6 x N.</remarks>
public sealed class Enforcer : IEntity, IExplodable, IRemovable
{
    private readonly SpriteSet _sprites;
    /// <summary>The enforcer picture's own 10x11 arcade px box, in port pixels.</summary>
    private static readonly (int Width, int Height) CollisionSize =
        (ScreenSize.Scaled(GameplayConstants.EnforcerCollisionSize.Width), ScreenSize.Scaled(GameplayConstants.EnforcerCollisionSize.Height));
    private readonly Random _random;
    private readonly int _fireDelayRomTicks;
    private IntVector2 _position;

    /// <summary>Velocity in 1/256 port units per ROM frame, carried by a remainder.</summary>
    private IntVector2 _velocitySubpixels;

    private IntVector2 _remainderSubpixels;
    private int _beatTimer;
    private int _moveTimer; // Counts up to the next move: one per ROM frame
    private int _reaimBeatsRemaining;
    private int _fireCooldownBeats;
    private int _growthRemaining;

    /// <summary>Creates an enforcer; it is immobile until it has grown.</summary>
    /// <param name="position">Top-left of the enforcer.</param>
    /// <param name="random">The random source for the re-aim destination and the fire timer.</param>
    /// <param name="fireDelayRomTicks">This wave's fire delay, in ROM frames: the interval is a random 1..this.</param>
    /// <remarks>ROM: <c>ENSTIM</c> — the interval is a random 1..that many AI passes.</remarks>
    public Enforcer(
        SpriteSet sprites,
        IntVector2 position,
        Random random,
        int fireDelayRomTicks = 24)
    {
        _sprites = sprites;
        _position = position;
        _random = random;
        _fireDelayRomTicks = fireDelayRomTicks;
        _growthRemaining = GameplayConstants.EnforcerGrowUpRomFrames * 6;
        // Both countdowns are in beats; the arcade re-aims as soon as the grow-up ends.
        _reaimBeatsRemaining = 0;
        _fireCooldownBeats = 1 + random.Next(0, _fireDelayRomTicks);
        // The mover starts on its first active frame; the accumulator is still while growing.
        _moveTimer = 6;
    }

    /// <summary>Top-left of the enforcer.</summary>
    /// <remarks>The ROM's OBJX/OBJY.</remarks>
    public IntVector2 Position => _position;

    /// <summary>The enforcer picture's own 10x11 box at <see cref="Position"/>.</summary>
    public Rectangle Bounds => new(_position.X, _position.Y, CollisionSize.Width, CollisionSize.Height);

    /// <summary>Alive until killed; never Dying — there is no death animation.</summary>
    public EntityLifeState LifeState { get; private set; } = EntityLifeState.Alive;

    /// <summary>Kills it at once (ROM <c>ENFKIL</c>); there is no death animation.</summary>
    public void Kill() => LifeState = EntityLifeState.Dead;

    /// <summary>Runs the grow-up, the per-frame mover and the AI beat.</summary>
    /// <param name="gameTime">Unused — the clocks are counted in ticks.</param>
    /// <param name="field">The playfield.</param>
    public void Update(GameTime gameTime, PlayField field)
    {
        if (LifeState != EntityLifeState.Alive)
        {
            return;
        }

        if (field.RobotsFrozen)
        {
            return;
        }

        // Grow-up: immobile and silent; the carry keeps the pictures on the ROM's 9-frame mark.
        if (_growthRemaining > 0)
        {
            _growthRemaining -= 5;
            if (_growthRemaining > 0)
            {
                return;
            }
        }

        // One velocity integration per ROM frame.
        _moveTimer += 5;
        if (_moveTimer >= 6)
        {
            _moveTimer -= 6;
            AdvancePosition(field);
        }

        _beatTimer += 5;
        if (_beatTimer < GameplayConstants.EnforcerBeatRomFrames * 6)
        {
            return;
        }

        _beatTimer -= GameplayConstants.EnforcerBeatRomFrames * 6;

        // Both countdowns tick once per beat (ROM: ENFR1).
        if (--_reaimBeatsRemaining <= 0)
        {
            _reaimBeatsRemaining = NextReaimBeats(_random);
            RollVelocity(field);
        }

        if (--_fireCooldownBeats <= 0)
        {
            _fireCooldownBeats = NextFireBeats(_random);
            if (field.ActiveSparkCount < GameplayConstants.GlobalActiveSparkCap)
            {
                // The ROM aims with the player's position, so velocity follows distance per axis.
                field.SpawnSpark(_position, field.Player.Position);
            }
        }
    }

    /// <summary>Picks the next destination — the player plus a random offset — and sets the velocity.</summary>
    /// <param name="field">The playfield: the player to aim past, and the wall to clamp to.</param>
    private void RollVelocity(PlayField field)
    {
        Rectangle bounds = field.Wall.PlayfieldBounds;
        int targetX = field.Player.Position.X + ScreenSize.Scaled(2 * _random.Next(0, 32));
        int targetY = field.Player.Position.Y + ScreenSize.Scaled(_random.Next(0, 32));
        targetX = Math.Clamp(targetX, bounds.X, bounds.Right - CollisionSize.Width);
        targetY = Math.Clamp(targetY, bounds.Y, bounds.Bottom - CollisionSize.Height);

        IntVector2 delta = new(targetX - _position.X, targetY - _position.Y);

        // Velocity is in 1/256-port-px units per ROM frame.
        _velocitySubpixels = new IntVector2(delta.X / 2, delta.Y / 2);
    }

    /// <summary>Moves one frame's worth of velocity; an axis that would leave the field is refused.</summary>
    /// <param name="field">The playfield wall.</param>
    /// <remarks>The ROM's generic mover (RRS22.ASM's <c>OPB80</c>).</remarks>
    private void AdvancePosition(PlayField field)
    {
        Rectangle bounds = field.Wall.PlayfieldBounds;
        _remainderSubpixels += _velocitySubpixels;
        int stepX = _remainderSubpixels.X / 256;
        int stepY = _remainderSubpixels.Y / 256;
        _remainderSubpixels -= new IntVector2(stepX * 256, stepY * 256);

        int x = _position.X + stepX;
        if (stepX != 0 && x >= bounds.X && x + CollisionSize.Width <= bounds.Right)
        {
            _position = _position with { X = x };
        }

        int y = _position.Y + stepY;
        if (stepY != 0 && y >= bounds.Y && y + CollisionSize.Height <= bounds.Bottom)
        {
            _position = _position with { Y = y };
        }
    }

    /// <summary>Rolls the re-aim countdown: 0..31 beats (0 re-aims again next beat).</summary>
    private static int NextReaimBeats(Random random) => random.Next(0, 32);

    /// <summary>Which of the five grow-up pictures is showing (0..4), or -1 once grown (test hook).</summary>
    internal int GrowFrameIndex => _growthRemaining > 0
        ? (GameplayConstants.EnforcerGrowUpRomFrames * 6 - _growthRemaining)
            / (GameplayConstants.EnforcerGrowStepRomFrames * 6)
        : -1;

    /// <summary>The interval until the next shot: 1..the wave's fire delay, in beats.</summary>
    private int NextFireBeats(Random random) => random.Next(1, _fireDelayRomTicks + 1);

    /// <summary>Draws the grow-up picture while it is growing, and the full picture afterwards.</summary>
    /// <param name="spriteBatch">The batch to draw into.</param>
    /// <param name="sprites">The shared sprite set.</param>
    public void Draw(SpriteBatch spriteBatch)
    {
        if (LifeState != EntityLifeState.Alive)
        {
            return;
        }

        _sprites.DrawSprite(spriteBatch, CurrentAnimationFrame, Bounds, Color.White);
    }

    /// <summary>The frame an explosion would copy: the grow-up picture, or the full picture once grown.</summary>
    /// <summary>The picture on screen: a grow-up frame while it grows, else the full picture.</summary>
    /// <remarks>The grow frames are the ROM's ENGD1..5, which are frames 2..6 (1-based) of the set.</remarks>
    public Texture2D CurrentAnimationFrame
    {
        get
        {
            if (LifeState != EntityLifeState.Alive || _growthRemaining <= 0)
            {
                return _sprites.Enforcer;
            }

            int frame = Math.Clamp(GrowFrameIndex, 0, _sprites.EnforcerFrames.Length - 2);
            return _sprites.EnforcerFrames[1 + frame];
        }
    }
}
