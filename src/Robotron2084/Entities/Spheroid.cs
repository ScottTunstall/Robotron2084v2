using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Level;
using Robotron2084.Rendering;
using Robotron2084.Tuning;

namespace Robotron2084.Entities;

/// <summary>A spheroid — the drifting ring that drops enforcers, then exits off the nearest edge.</summary>
/// <seealso cref="Enforcer"/>
/// <remarks>ROM: RRC11.ASM's <c>CIRCLE</c>/<c>CIRNAC</c>/<c>CIRGO</c>/<c>CIRC2L</c>/<c>CIRC3L</c>
/// routines (notes §56). Its glide is unsteered: a random acceleration is re-rolled every 1..15 beats
/// and damped toward a top speed of 1 column (2 arcade px) per frame on X and 2 rows on Y — the same
/// speed in pixels — and each axis CLAMPS at the walls rather than reflecting. It flies over
/// electrodes. It steps the picture pointer one per beat through three phases: spin (5 pictures, the
/// drop countdown on a full 5-picture wrap), drop (8 pictures, one enforcer per rotation until the
/// allotment — half a random roll, rounded up — is gone) and escape (X fixed at 1 column a frame, Y
/// stopped, running off the edge to vanish with no animation). A laser hit plays its own bubble burst
/// (<see cref="ScoreBurst.ForSpheroid"/>), not the strip explosion; see <see cref="RobotKinds"/>. Timers count
/// 5 per tick and 6 per arcade frame, so an interval of N frames is due at 6 x N.</remarks>
public sealed class Spheroid : IEntity, IAnimationFrameSource, IRemovable
{
    private readonly SpriteSet _sprites;
    /// <summary>Collision box = the ROM picture dimensions (16x15 arcade px), top-left anchored at <see cref="Position"/>.</summary>
    private static readonly (int Width, int Height) CollisionSize =
        (ScreenSize.Scaled(GameplayConstants.SpheroidCollisionSize.Width), ScreenSize.Scaled(GameplayConstants.SpheroidCollisionSize.Height));

    /// <summary>The last picture of the spin and escape, i.e. the pointer value whose step wraps.</summary>
    /// <remarks>ROM: <c>CIRCLE</c>/<c>CIRC3L</c> wrap after picture 5 (index 4).</remarks>
    private const int SpinLastPicture = 4;

    /// <summary>The drop phase's wrap boundary, so it spins all eight pictures.</summary>
    /// <remarks>ROM: <c>CIRC2L</c> wraps after its eighth picture.</remarks>
    private const int DropLastPicture = 7;

    private readonly Random _random;
    private readonly int _dropDelayRomTicks;
    private IntVector2 _position;
    private int _velocityXSubpixels; // X velocity, in 1/256 column per frame (a column is 2 arcade px)
    private int _velocityYSubpixels; // Y velocity, in 1/256 row per frame
    private int _remainderXSubpixels;
    private int _remainderYSubpixels;
    private int _accelX; // current X acceleration: -16..+15, in 1/256 column per frame per beat
    private int _accelY; // current Y acceleration: -32..+31, in 1/256 row per frame per beat
    private int _accelBeatsRemaining; // beats left before the accelerations are re-rolled: 1..15
    private int _beatTimer;
    private int _moveTimer; // Counts up to the next move: one per ROM frame
    private int _enforcersRemaining; // how many enforcers this spheroid still owes: 1..5, never 0
    private int _dropRotationsRemaining; // rotations left until the next enforcer drop
    private int _rotation; // current picture: 0..4 while spinning/escaping, 0..7 while dropping
    private bool _dropping; // spinning -> dropping enforcers
    private bool _escaping; // sideways run toward the edge of the field, then vanish
    private int _escapeDirection;

    /// <summary>Drops a spheroid at <paramref name="position"/> with its enforcer allotment already rolled; it is born mid-spin.</summary>
    /// <param name="sprites">The shared sprite set.</param>
    /// <param name="position">Top-left of the spheroid.</param>
    /// <param name="random">The random source: the allotment, the accelerations and the escape direction.</param>
    /// <param name="maxDropsX2">This wave's enforcer-allotment bound; the roll happens here.</param>
    /// <param name="dropDelayRomTicks">This wave's rotation countdown, in ROM frames.</param>
    /// <remarks>ROM: <c>ENFNUM</c> and <c>CDPTIM</c> — this wave's allotment bound and rotation
    /// delay. There is deliberately no speed parameter: the spheroid's speed IS its accumulated,
    /// damped glide velocity, so a "speed bonus" cannot be expressed in this model.</remarks>
    public Spheroid(
        SpriteSet sprites,
        IntVector2 position,
        Random random,
        int maxDropsX2 = 10,
        int dropDelayRomTicks = 24)
    {
        _sprites = sprites;
        _position = position;
        _random = random;
        _dropDelayRomTicks = dropDelayRomTicks;
        // The allotment: a random roll (never 0), halved and rounded up — always 1..5.
        int roll = random.Next(1, maxDropsX2 + 1);
        _enforcersRemaining = (roll + 1) / 2;
        // A coin flip; the arcade derives it from its own random-seed byte.
        _escapeDirection = random.Next(2) == 0 ? -1 : 1;
        // The first drop countdown, in rotations.
        _dropRotationsRemaining = random.Next(1, dropDelayRomTicks + 1);
        _moveTimer = ArcadeClock.UnitsPerRomFrame;
        // Born on the spin phase's last picture, so the first beat is already a wrap pass.
        _rotation = SpinLastPicture;
        RollAccelerations();
    }

    /// <summary>Top-left of the spheroid (the ROM's OBJX/OBJY).</summary>
    public IntVector2 Position => _position;

    /// <summary>The spheroid picture's own 16x15 box at <see cref="Position"/>.</summary>
    public Rectangle Bounds => new(_position.X, _position.Y, CollisionSize.Width, CollisionSize.Height);

    /// <summary>Alive until shot or until it finishes its sideways escape; never Dying (see <see cref="Kill"/>).</summary>
    public EntityLifeState LifeState { get; private set; } = EntityLifeState.Alive;

    /// <summary>Test hook: which picture is showing — 0..4 spinning or escaping, 0..7 dropping.</summary>
    /// <remarks>The ROM's current-picture pointer.</remarks>
    internal int PictureIndex => _rotation;

    /// <summary>Test hook: true once the sideways exit run has started.</summary>
    /// <remarks>ROM: the `CIRC3` escape phase.</remarks>
    internal bool IsEscaping => _escaping;

    /// <summary>Kills the spheroid outright; a laser hit plays its own burst instead of the strip explosion.</summary>
    /// <remarks>ROM: <c>CIRKIL</c> plays a 7-frame bubble burst then a "1000"; <see cref="RobotKinds"/> wires
    /// <see cref="ScoreBurst.ForSpheroid"/> to the laser phase.</remarks>
    public void Kill() => LifeState = EntityLifeState.Dead;

    /// <summary>Runs the glide, then the beat: accelerate and damp, or drop, or escape.</summary>
    /// <param name="gameTime">Unused — the clocks are counted in ticks.</param>
    /// <param name="field">The playfield.</param>
    public void Update(GameTime gameTime, PlayField field)
    {
        if (LifeState == EntityLifeState.Dead)
        {
            return;
        }

        // Mover: once per ROM frame, not once per tick (see the remarks).
        _moveTimer += ArcadeClock.UnitsPerPortTick;
        if (_moveTimer >= ArcadeClock.UnitsPerRomFrame)
        {
            _moveTimer -= ArcadeClock.UnitsPerRomFrame;
            AdvancePosition(field);
        }

        // Every phase runs on the same 3-frame beat (see the remarks).
        _beatTimer += ArcadeClock.UnitsPerPortTick;
        if (_beatTimer < ArcadeClock.Units(GameplayConstants.SpheroidBeatRomFrames))
        {
            return;
        }

        _beatTimer -= ArcadeClock.Units(GameplayConstants.SpheroidBeatRomFrames);

        // Wrap pass = the beat on the phase's last picture; the phase's countdown lives there.
        int lastPicture = _dropping && !_escaping ? DropLastPicture : SpinLastPicture;
        bool wrapPass = _rotation >= lastPicture;

        if (_escaping)
        {
            AdvanceEscapeBeat(field, wrapPass);
            return;
        }

        // Accelerate/damp and re-roll the accel timer once per beat; the escape skips both.
        AccelerateAndDamp();
        if (--_accelBeatsRemaining <= 0)
        {
            RollAccelerations();
        }

        if (!wrapPass)
        {
            _rotation++;
            return;
        }

        AdvanceDropBeat(field);
    }

    /// <summary>One escape beat: step the picture, or leave for good once the far edge is reached.</summary>
    /// <param name="field">The playfield, whose bounds the exit is measured against.</param>
    /// <param name="wrapPass">True on the beat that lands on the phase's last picture.</param>
    /// <remarks>The exit test lives inside the wrap branch, so it is tried once per picture cycle (ROM:
    /// <c>CIRC3L</c>).</remarks>
    private void AdvanceEscapeBeat(PlayField field, bool wrapPass)
    {
        if (!wrapPass)
        {
            _rotation++;
            return;
        }

        Rectangle bounds = field.Wall.PlayfieldBounds;
        int leftExit = bounds.X + ScreenSize.Scaled(2 * GameplayConstants.SpheroidEscapeExitLeftColumn);
        int rightExit = ScreenSize.Scaled(2 * GameplayConstants.SpheroidEscapeExitRightColumn);
        if (_position.X <= leftExit || _position.X >= rightExit)
        {
            LifeState = EntityLifeState.Dead; // removed at once, no burst (ROM: `CIR4`)
            return;
        }

        _rotation = 0;
    }

    /// <summary>The wrap beat of the spin/drop cycle: hold the picture, release it, or drop an enforcer.</summary>
    /// <param name="field">The playfield, which owns the enforcer cap and the new enforcer.</param>
    /// <remarks>ROM: <c>CIRC2</c>. While spinning, a frozen game holds the picture at its wrap target without
    /// decrementing the countdown; drop and escape have no such freeze check, because the arcade's freeze test
    /// is per routine, not per object.</remarks>
    private void AdvanceDropBeat(PlayField field)
    {
        if (!_dropping && field.RobotsFrozen)
        {
            _rotation = 0;
            return;
        }

        if (--_dropRotationsRemaining > 0)
        {
            _rotation = 0;
            return;
        }

        if (!_dropping)
        {
            // Spin-to-drop does NOT reset the picture pointer: the drop phase carries the same
            // picture on for one more beat.
            _dropping = true;
            RerollDropCountdown();
            return;
        }

        // A drop capped by the enforcer limit is not deferred — it re-rolls and tries again.
        if (field.CanDropEnforcer)
        {
            field.SpawnEnforcer(_position);
            if (--_enforcersRemaining <= 0)
            {
                StartEscape(); // the picture stays where it is; the first escape beat wraps it
                return;
            }
        }

        RerollDropCountdown();
        _rotation = 0;
    }

    /// <summary>Re-rolls both accelerations and the 1..15-beat re-roll timer.</summary>
    /// <remarks>ROM: `CIRNAC`.</remarks>
    private void RollAccelerations()
    {
        // X: -16..+15; Y: -32..+31 — twice as large because a column is 2 pixels.
        _accelX = _random.Next(0, 32) - 16;
        _accelY = _random.Next(0, 64) - 32;
        _accelBeatsRemaining = 1 + _random.Next(0, 15); // 1..15 beats until the next re-roll
    }

    /// <summary>Re-arms the drop countdown: a random 1..(this wave's delay / 4) rotations between drops.</summary>
    /// <remarks>ROM: the <c>CIRC2</c> re-arm.</remarks>
    private void RerollDropCountdown() =>
        _dropRotationsRemaining = 1 + _random.Next(0, _dropDelayRomTicks / 4);

    /// <summary>Starts the escape: Y velocity 0, X exactly ±1 column per frame, picture untouched.</summary>
    /// <remarks>ROM: <c>CIRC3</c> — the arcade leaves the picture on the last drop-phase frame, and
    /// the first escape beat wraps it back to the first spin picture.</remarks>
    private void StartEscape()
    {
        _escaping = true;
        _velocityXSubpixels = _escapeDirection * GameplayConstants.SpheroidMaxVelocityXSubpixels;
        _velocityYSubpixels = 0;
        _remainderXSubpixels = 0;
        _remainderYSubpixels = 0;
    }

    /// <summary>Adds the acceleration, clamps to the top speed, then damps by a 64th.</summary>
    /// <remarks>ROM: <c>CIRGO</c>.</remarks>
    private void AccelerateAndDamp()
    {
        _velocityXSubpixels = ClampThenDamp(_velocityXSubpixels + _accelX, GameplayConstants.SpheroidMaxVelocityXSubpixels);
        _velocityYSubpixels = ClampThenDamp(_velocityYSubpixels + _accelY, GameplayConstants.SpheroidMaxVelocityYSubpixels);
    }

    /// <summary>Clamps the velocity to the limit, then damps it toward 64 x the acceleration.</summary>
    /// <remarks>ROM: the second half of <c>CIRGO</c> — the damping nudges the velocity toward -4x
    /// itself minus a small constant, so it converges on 64 x the acceleration and the clamp is the
    /// usual terminal state.</remarks>
    private static int ClampThenDamp(int velocitySubpixels, int limitSubpixels)
    {
        velocitySubpixels = Math.Clamp(velocitySubpixels, -limitSubpixels, limitSubpixels);
        return velocitySubpixels + (((-4 * velocitySubpixels) - 4) >> 8);
    }

    /// <summary>Integrates the velocity on both axes for one frame; a step that would leave the field is rejected.</summary>
    private void AdvancePosition(PlayField field)
    {
        Rectangle bounds = field.Wall.PlayfieldBounds;
        _position = new IntVector2(
            AdvanceAxis(_position.X, _velocityXSubpixels, ref _remainderXSubpixels, bounds.X, bounds.Right - CollisionSize.Width),
            AdvanceAxis(_position.Y, _velocityYSubpixels, ref _remainderYSubpixels, bounds.Y, bounds.Bottom - CollisionSize.Height));
    }

    /// <summary>One axis of the mover's step: whole pixels, keeping the 1/256 fraction for next time.</summary>
    private static int AdvanceAxis(int position, int velocitySubpixels, ref int remainderSubpixels, int min, int max)
    {
        int nextRemainder = remainderSubpixels + velocitySubpixels;
        int step = nextRemainder >> 8;
        int next = position + step;
        if (next < min || next > max)
        {
            return position; // out of bounds: keep the old coordinate AND its carried fraction
        }

        remainderSubpixels = nextRemainder - (step << 8);
        return next;
    }

    /// <summary>Draws the current picture; its shimmer comes from cycling palette slots, not a flash.</summary>
    /// <param name="spriteBatch">The batch to draw into.</param>
    public void Draw(SpriteBatch spriteBatch)
    {
        if (LifeState == EntityLifeState.Dead)
        {
            return;
        }

        _sprites.DrawSprite(spriteBatch, CurrentAnimationFrame, Bounds, Color.White);
    }

    /// <summary>The current picture, for the death burst (see <see cref="IAnimationFrameSource"/>).</summary>
    /// <returns>The texture for the current rotation frame.</returns>
    public Texture2D CurrentAnimationFrame
        => _sprites.SpheroidFrames[_rotation % _sprites.SpheroidFrames.Length];
}
