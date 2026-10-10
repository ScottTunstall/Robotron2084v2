using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Graphics;
using Robotron2084.Level;
using Robotron2084.Tuning;

namespace Robotron2084.Entities;

/// <summary>A spheroid is a drifting ring that floats around dropping little enforcer robots, then flies off the edge of the screen.</summary>
/// <seealso cref="Enforcer"/>
/// <remarks>
/// It acts on a beat. The <see cref="PlayField"/> calls <see cref="Update"/> on nearly every tick, through
/// <see cref="FieldEntities"/> and <see cref="PlayField.UpdateEntity"/>. <see cref="_beatTimer"/> gathers the ticks
/// until it is time for the next beat (see <see cref="ArcadeClock"/>). It also moves every ROM frame, timed by
/// <see cref="_moveTimer"/>.
///
/// <list type="bullet">
/// <item>Original source: <c>RRC11.ASM</c>, routine <c>CIRCLE</c> (with
/// <c>CIRNAC</c>/<c>CIRGO</c>/<c>CIRC2L</c>/<c>CIRC3L</c>)</item>
/// <item>Disassembly: <c>asm/robomame.asm</c> at <c>$11AF</c> (<c>ANIMATE_SPHEROID</c>)</item>
/// </list>
/// </remarks>
public sealed class Spheroid : IEntity, IAnimationFrameSource, IRemovable
{
    /// <summary>The accelerations hold for a random 1 to this many beats.</summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>RRC11.ASM</c> <c>CIRNAC</c>.</item>
    /// <item>Disassembly: <c>asm/robomame.asm</c> <c>ANIMATE_SPHEROID</c> (<c>$11AF</c>).</item>
    /// </list>
    /// </remarks>
    private const int AccelBeatsMax = 15;

    /// <summary>The sideways acceleration roll is offset down by this much, so it can be negative.</summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>RRC11.ASM</c> <c>CIRNAC</c>.</item>
    /// <item>Disassembly: <c>asm/robomame.asm</c> <c>ANIMATE_SPHEROID</c> (<c>$11AF</c>).</item>
    /// </list>
    /// </remarks>
    private const int AccelXOffset = 16;

    /// <summary>The sideways acceleration roll has this many sides (from -16 to +15 once offset).</summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>RRC11.ASM</c> <c>CIRNAC</c>.</item>
    /// <item>Disassembly: <c>asm/robomame.asm</c> <c>ANIMATE_SPHEROID</c> (<c>$11AF</c>).</item>
    /// </list>
    /// </remarks>
    private const int AccelXRollSides = 32;

    /// <summary>The up-and-down acceleration roll is offset down by this much, so it can be negative.</summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>RRC11.ASM</c> <c>CIRNAC</c>.</item>
    /// <item>Disassembly: <c>asm/robomame.asm</c> <c>ANIMATE_SPHEROID</c> (<c>$11AF</c>).</item>
    /// </list>
    /// </remarks>
    private const int AccelYOffset = 32;

    /// <summary>The up-and-down acceleration roll has this many sides (from -32 to +31 once offset) — twice the sideways roll, because a column is 2 pixels.</summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>RRC11.ASM</c> <c>CIRNAC</c>.</item>
    /// <item>Disassembly: <c>asm/robomame.asm</c> <c>ANIMATE_SPHEROID</c> (<c>$11AF</c>).</item>
    /// </list>
    /// </remarks>
    private const int AccelYRollSides = 64;

    /// <summary>The damping is reduced by this small constant, so the velocity converges on a multiple of the acceleration.</summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>RRC11.ASM</c> <c>CIRGO</c>.</item>
    /// <item>Disassembly: <c>asm/robomame.asm</c> <c>ANIMATE_SPHEROID</c> (<c>$11AF</c>).</item>
    /// </list>
    /// </remarks>
    private const int DampingBias = 4;

    /// <summary>Each beat the velocity is damped by this many 256ths of itself (a 64th).</summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>RRC11.ASM</c> <c>CIRGO</c>.</item>
    /// <item>Disassembly: <c>asm/robomame.asm</c> <c>ANIMATE_SPHEROID</c> (<c>$11AF</c>).</item>
    /// </list>
    /// </remarks>
    private const int DampingPer256 = 4;

    /// <summary>The wave's rotation delay when the caller gives none.</summary>
    private const int DefaultDropDelayRotations = 24;

    /// <summary>The wave's enforcer-allotment bound when the caller gives none.</summary>
    private const int DefaultMaxDropsX2 = 10;

    /// <summary>The drop phase's wrap boundary, so it spins all eight animation frames.</summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>RRC11.ASM</c> <c>CIRC2L</c> wraps after its eighth animation frame.</item>
    /// <item>Disassembly: <c>asm/robomame.asm</c> <c>ANIMATE_SPHEROID</c> (<c>$11AF</c>).</item>
    /// </list>
    /// </remarks>
    private const int DropLastAnimationFrame = 7;

    /// <summary>A re-armed drop countdown is a random 1 to (the wave's delay over this) rotations.</summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>RRC11.ASM</c> <c>CIRC2</c>.</item>
    /// <item>Disassembly: <c>asm/robomame.asm</c> <c>ANIMATE_SPHEROID</c> (<c>$11AF</c>).</item>
    /// </list>
    /// </remarks>
    private const int DropRerollDivisor = 4;

    /// <summary>The last animation frame of the spin and escape, i.e. the pointer value whose step wraps.</summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>RRC11.ASM</c> <c>CIRCLE</c>/<c>CIRC3L</c> wrap after animation frame 5
    /// (index 4).</item>
    /// <item>Disassembly: <c>asm/robomame.asm</c> <c>ANIMATE_SPHEROID</c> (<c>$11AF</c>).</item>
    /// </list>
    /// </remarks>
    private const int SpinLastAnimationFrame = 4;

    /// <summary>Collision box = the ROM sprite dimensions (16x15 arcade px), top-left anchored at <see cref="Position"/>.</summary>
    private static readonly (int Width, int Height) CollisionSize =
        (ScreenSize.ToPortPixels(CollisionSizes.SpheroidCollisionSize.Width), ScreenSize.ToPortPixels(CollisionSizes.SpheroidCollisionSize.Height));

    private readonly int _dropDelayRotations;
    private readonly Random _random;
    private readonly SpriteSet _sprites;

    /// <summary>Beats left before the accelerations are rolled again.</summary>
    private int _accelBeatsRemaining;

    /// <summary>The sideways acceleration now, in 1/256 column per ROM frame per beat.</summary>
    private int _accelX;

    /// <summary>The up-and-down acceleration now, in 1/256 row per ROM frame per beat.</summary>
    private int _accelY;

    /// <summary>Which animation frame is showing. It counts round as the spheroid spins.</summary>
    private int _animationFrameIndex;

    /// <summary>Counts up to the next beat.</summary>
    private int _beatTimer;

    /// <summary>Rotations left until the next enforcer is dropped.</summary>
    private int _dropRotationsRemaining;

    /// <summary>How many enforcers this spheroid still has to drop.</summary>
    private int _enforcersRemaining;

    /// <summary>Which way the spheroid runs when it escapes: -1 for left, +1 for right.</summary>
    private int _escapeDirectionSignX;

    /// <summary>True once the spheroid has stopped spinning and started dropping enforcers.</summary>
    private bool _isDropping;

    /// <summary>True once the spheroid is running sideways for the edge of the playfield, where it vanishes.</summary>
    private bool _isEscaping;

    /// <summary>Counts up to the next move: one move per ROM frame.</summary>
    private int _moveTimer;

    private IntVector2 _position;
    private int _remainderXSubpixels;
    private int _remainderYSubpixels;

    /// <summary>The sideways velocity, in 1/256 column per ROM frame.</summary>
    private int _velocityXSubpixels;

    /// <summary>The up-and-down velocity, in 1/256 row per ROM frame.</summary>
    private int _velocityYSubpixels;

    // current animation frame: 0..4 while spinning/escaping, 0..7 while dropping
    // sideways run toward the edge of the field, then vanish
    /// <summary>Drops a spheroid at <paramref name="position"/> with its enforcer allotment already rolled; it is born mid-spin.</summary>
    /// <param name="sprites">The shared sprite set.</param>
    /// <param name="position">Top-left of the spheroid.</param>
    /// <param name="random">The random source: the allotment, the accelerations and the escape direction.</param>
    /// <param name="maxDropsX2">This wave's enforcer-allotment bound; the roll happens here.</param>
    /// <param name="dropDelayRotations">The most rotations this wave's spheroid makes before it drops an enforcer.</param>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>RRC11.ASM</c> <c>ENFNUM</c> and <c>CDPTIM</c> — this wave's allotment
    /// bound and rotation delay.</item>
    /// <item>Disassembly: <c>asm/robomame.asm</c> <c>ANIMATE_SPHEROID</c> (<c>$11AF</c>).</item>
    /// </list>
    ///
    /// There
    /// is deliberately no speed parameter: the spheroid's speed IS its accumulated, damped glide velocity,
    /// so a "speed bonus" cannot be expressed in this model.
    /// </remarks>
    public Spheroid(
        SpriteSet sprites,
        IntVector2 position,
        Random random,
        int maxDropsX2 = DefaultMaxDropsX2,
        int dropDelayRotations = DefaultDropDelayRotations)
    {
        _sprites = sprites;
        _position = position;
        _random = random;
        _dropDelayRotations = dropDelayRotations;
        // The allotment: a random roll (never 0), halved and rounded up — always 1..5.
        int roll = random.Next(1, maxDropsX2 + 1);
        _enforcersRemaining = (roll + 1) / 2;
        // A coin flip; the arcade derives it from its own random-seed byte.
        _escapeDirectionSignX = random.Next(2) == 0 ? -1 : 1;
        // The first drop countdown, in rotations.
        _dropRotationsRemaining = random.Next(1, dropDelayRotations + 1);
        _moveTimer = ArcadeClock.UnitsPerRomFrame;
        // Born on the spin phase's last animation frame, so the first beat is already a wrap pass.
        _animationFrameIndex = SpinLastAnimationFrame;
        RollAccelerations();
    }

    /// <summary>The spheroid sprite's own 16x15 box at <see cref="Position"/>.</summary>
    public Rectangle GetBounds() => new(_position.X, _position.Y, CollisionSize.Width, CollisionSize.Height);

    /// <summary>The current animation frame, for the death burst (see <see cref="IAnimationFrameSource"/>).</summary>
    /// <returns>The texture for the current rotation frame.</returns>
    public Texture2D GetCurrentAnimationFrame() =>
        _sprites.SpheroidAnimationFrames[_animationFrameIndex % _sprites.SpheroidAnimationFrames.Length];

    /// <summary>Alive until shot or until it finishes its sideways escape; never Dying (see <see cref="Kill"/>).</summary>
    public EntityLifeState LifeState { get; private set; } = EntityLifeState.Alive;

    /// <summary>Top-left of the spheroid.</summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>RRC11.ASM</c> the OBJX/OBJY registers.</item>
    /// <item>Disassembly: <c>asm/robomame.asm</c> <c>ANIMATE_SPHEROID</c> (<c>$11AF</c>).</item>
    /// </list>
    /// </remarks>
    public IntVector2 Position => _position;

    /// <summary>Test hook: true once the sideways exit run has started.</summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>RRC11.ASM</c> the <c>CIRC3</c> escape phase.</item>
    /// <item>Disassembly: <c>asm/robomame.asm</c> <c>ANIMATE_SPHEROID</c> (<c>$11AF</c>).</item>
    /// </list>
    /// </remarks>
    internal bool IsEscaping => _isEscaping;

    /// <summary>Test hook: which animation frame is showing — 0..4 spinning or escaping, 0..7 dropping.</summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>RRC11.ASM</c> <c>CIRCLE</c> — the current-animation-frame pointer.</item>
    /// <item>Disassembly: <c>asm/robomame.asm</c> <c>ANIMATE_SPHEROID</c> (<c>$11AF</c>).</item>
    /// </list>
    /// </remarks>
    internal int AnimationFrameIndex => _animationFrameIndex;

    /// <summary>Draws the current animation frame; its shimmer comes from cycling palette slots, not a flash.</summary>
    /// <param name="spriteBatch">The batch to draw into.</param>
    public void Draw(SpriteBatch spriteBatch)
    {
        if (!this.IsAlive())
        {
            return;
        }

        _sprites.Blitter.DrawSprite(spriteBatch, GetCurrentAnimationFrame(), GetBounds(), Color.White);
    }

    /// <summary>Kills the spheroid outright; a laser hit plays its own burst instead of the strip explosion.</summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>RRC11.ASM</c> <c>CIRKIL</c> plays a 7-frame bubble burst then a "1000".</item>
    /// <item>Disassembly: <c>asm/robomame.asm</c> <c>ANIMATE_SPHEROID</c> (<c>$11AF</c>).</item>
    /// </list>
    ///
    /// <see cref="RobotKinds"/> wires <see cref="ScoreBurst.CreateForSpheroid"/> to the laser phase.
    /// </remarks>
    public void Kill()
    {
        if (!this.IsAlive())
        {
            return;
        }

        LifeState = EntityLifeState.Dead;
    }

    /// <summary>Runs the glide, then the beat: accelerate and damp, or drop, or escape.</summary>
    /// <param name="gameTime">Unused — the clocks are counted in ticks.</param>
    /// <param name="field">The playfield.</param>
    public void Update(GameTime gameTime, PlayField field)
    {
        if (!this.IsAlive())
        {
            return;
        }

        // The arcade does not move its motion objects while the game is held: before it goes live, and while the player dies
        // (ROM: RRS22.ASM OPRC80, BITA #8 / BNE O80, "NO VELOCITY REFRESH ONLY"; STATUS bit 3).
        if (!field.RobotsFrozen())
        {
            AdvanceMover(field);
        }

        // Every phase runs on the same 3-frame beat (see the remarks).
        _beatTimer += ArcadeClock.UnitsPerPortTick;
        if (_beatTimer < ArcadeClock.ToClockUnits(SpheroidTuning.BeatIntervalRomFrames))
        {
            return;
        }

        _beatTimer -= ArcadeClock.ToClockUnits(SpheroidTuning.BeatIntervalRomFrames);

        // Wrap pass = the beat on the phase's last animation frame; the phase's countdown lives there.
        int lastAnimationFrame = _isDropping && !_isEscaping ? DropLastAnimationFrame : SpinLastAnimationFrame;
        bool wrapPass = _animationFrameIndex >= lastAnimationFrame;

        if (_isEscaping)
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
            _animationFrameIndex++;
            return;
        }

        AdvanceDropBeat(field);
    }

    /// <summary>Counts one tick towards the next ROM frame, and moves the spheroid when the frame comes: once per ROM frame, not once per tick (see the remarks on the class).</summary>
    /// <param name="field">The playfield.</param>
    private void AdvanceMover(PlayField field)
    {
        _moveTimer += ArcadeClock.UnitsPerPortTick;
        if (_moveTimer >= ArcadeClock.UnitsPerRomFrame)
        {
            _moveTimer -= ArcadeClock.UnitsPerRomFrame;
            AdvancePosition(field);
        }
    }

    /// <summary>One axis of the mover's step: whole pixels, keeping the 1/256 fraction for next time.</summary>
    private static int AdvanceAxis(int position, int velocitySubpixels, ref int remainderSubpixels, int min, int max)
    {
        int nextRemainder = remainderSubpixels + velocitySubpixels;
        int step = nextRemainder >> ScreenSize.SubpixelBits;
        int next = position + step;
        if (next < min || next > max)
        {
            return position; // out of bounds: keep the old coordinate AND its carried fraction
        }

        remainderSubpixels = nextRemainder - (step << ScreenSize.SubpixelBits);
        return next;
    }

    /// <summary>Clamps the velocity to the limit, then damps it toward 64 x the acceleration.</summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>RRC11.ASM</c> the second half of <c>CIRGO</c> — the damping nudges the
    /// velocity toward -4x itself minus a small constant, so it converges on 64 x the acceleration
    /// and the clamp is the usual terminal state.</item>
    /// <item>Disassembly: <c>asm/robomame.asm</c> <c>ANIMATE_SPHEROID</c> (<c>$11AF</c>).</item>
    /// </list>
    /// </remarks>
    private static int ClampThenDamp(int velocitySubpixels, int limitSubpixels)
    {
        velocitySubpixels = Math.Clamp(velocitySubpixels, -limitSubpixels, limitSubpixels);
        return velocitySubpixels + (((-DampingPer256 * velocitySubpixels) - DampingBias) >> ScreenSize.SubpixelBits);
    }

    /// <summary>Adds the acceleration, clamps to the top speed, then damps by a 64th.</summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>RRC11.ASM</c> <c>CIRGO</c>.</item>
    /// <item>Disassembly: <c>asm/robomame.asm</c> <c>ANIMATE_SPHEROID</c> (<c>$11AF</c>).</item>
    /// </list>
    /// </remarks>
    private void AccelerateAndDamp()
    {
        _velocityXSubpixels = ClampThenDamp(_velocityXSubpixels + _accelX, SpheroidTuning.MaxVelocityXSubpixels);
        _velocityYSubpixels = ClampThenDamp(_velocityYSubpixels + _accelY, SpheroidTuning.MaxVelocityYSubpixels);
    }

    /// <summary>The wrap beat of the spin/drop cycle: hold the animation frame, release it, or drop an enforcer.</summary>
    /// <param name="field">The playfield, which owns the enforcer cap and the new enforcer.</param>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>RRC11.ASM</c> <c>CIRC2</c>.</item>
    /// <item>Disassembly: <c>asm/robomame.asm</c> <c>ANIMATE_SPHEROID</c> (<c>$11AF</c>).</item>
    /// </list>
    ///
    /// While spinning, a frozen game holds the animation frame at
    /// its wrap target without decrementing the countdown; drop and escape have no such freeze check,
    /// because the arcade's freeze test is per routine, not per object.
    /// </remarks>
    private void AdvanceDropBeat(PlayField field)
    {
        if (!_isDropping && field.RobotsFrozen())
        {
            _animationFrameIndex = 0;
            return;
        }

        if (--_dropRotationsRemaining > 0)
        {
            _animationFrameIndex = 0;
            return;
        }

        if (!_isDropping)
        {
            // Spin-to-drop does NOT reset the animation frame pointer: the drop phase carries the same
            // animation frame on for one more beat.
            _isDropping = true;
            RerollDropCountdown();
            return;
        }

        // A drop capped by the enforcer limit is not deferred — it re-rolls and tries again.
        if (field.CanDropEnforcer())
        {
            field.SpawnEnforcer(_position);
            if (--_enforcersRemaining <= 0)
            {
                StartEscape(); // the animation frame stays where it is; the first escape beat wraps it
                return;
            }
        }

        RerollDropCountdown();
        _animationFrameIndex = 0;
    }

    /// <summary>One escape beat: step the animation frame, or leave for good once the far edge is reached.</summary>
    /// <param name="field">The playfield, whose bounds the exit is measured against.</param>
    /// <param name="wrapPass">True on the beat that lands on the phase's last animation frame.</param>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>RRC11.ASM</c> <c>CIRC3L</c> — the exit test lives inside the wrap branch,
    /// so it is tried once per animation frame cycle.</item>
    /// <item>Disassembly: <c>asm/robomame.asm</c> <c>ANIMATE_SPHEROID</c> (<c>$11AF</c>).</item>
    /// </list>
    /// </remarks>
    private void AdvanceEscapeBeat(PlayField field, bool wrapPass)
    {
        if (!wrapPass)
        {
            _animationFrameIndex++;
            return;
        }

        Rectangle bounds = field.Wall.PlayfieldBounds;
        int leftExit = bounds.X + ScreenSize.ToPortPixelsFromColumns(SpheroidTuning.EscapeExitLeftColumn);
        int rightExit = ScreenSize.ToPortPixelsFromColumns(SpheroidTuning.EscapeExitRightColumn);
        if (_position.X <= leftExit || _position.X >= rightExit)
        {
            LifeState = EntityLifeState.Dead; // removed at once, no burst (ROM: `CIR4`)
            return;
        }

        _animationFrameIndex = 0;
    }

    /// <summary>Integrates the velocity on both axes for one frame; a step that would leave the field is rejected.</summary>
    private void AdvancePosition(PlayField field)
    {
        Rectangle bounds = field.Wall.PlayfieldBounds;
        _position = new IntVector2(
            AdvanceAxis(_position.X, _velocityXSubpixels, ref _remainderXSubpixels, bounds.X, bounds.Right - CollisionSize.Width),
            AdvanceAxis(_position.Y, _velocityYSubpixels, ref _remainderYSubpixels, bounds.Y, bounds.Bottom - CollisionSize.Height));
    }

    /// <summary>Re-arms the drop countdown: a random count of rotations between drops.</summary>
    /// <remarks>ROM: the <c>CIRC2</c> re-arm.</remarks>
    private void RerollDropCountdown() =>
        _dropRotationsRemaining = 1 + _random.Next(0, _dropDelayRotations / DropRerollDivisor);

    /// <summary>Re-rolls both accelerations and the timer that re-rolls them.</summary>
    /// <remarks>ROM: `CIRNAC`.</remarks>
    private void RollAccelerations()
    {
        _accelX = _random.Next(0, AccelXRollSides) - AccelXOffset;
        _accelY = _random.Next(0, AccelYRollSides) - AccelYOffset;
        _accelBeatsRemaining = 1 + _random.Next(0, AccelBeatsMax);
    }

    /// <summary>Starts the escape: Y velocity 0, X exactly ±1 column per frame, animation frame untouched.</summary>
    /// <remarks>ROM: <c>CIRC3</c> — the arcade leaves the animation frame on the last drop-phase frame, and
    /// the first escape beat wraps it back to the first spin animation frame.</remarks>
    private void StartEscape()
    {
        _isEscaping = true;
        _velocityXSubpixels = _escapeDirectionSignX * SpheroidTuning.MaxVelocityXSubpixels;
        _velocityYSubpixels = 0;
        _remainderXSubpixels = 0;
        _remainderYSubpixels = 0;
    }
}
