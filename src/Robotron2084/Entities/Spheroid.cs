using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Graphics;
using Robotron2084.Level;
using Robotron2084.Tuning;

namespace Robotron2084.Entities;

/// <summary>
///     A spheroid is a drifting ring that floats around dropping little enforcer robots, then flies off the edge of
///     the screen.
/// </summary>
/// <seealso cref="Enforcer" />
/// <remarks>
///     It acts on a beat. The <see cref="PlayField" /> calls <see cref="Update" /> on every tick, through
///     <see cref="FieldEntities" /> and <see cref="PlayField.UpdateEntity" />. The one time it does not is during the
///     short freeze just after the player is killed. <see cref="_beatTimer" /> gathers the ticks until it is time for
///     the next beat (see <see cref="ArcadeClock" />). It also moves steadily, timed by
///     <see cref="_moveTimer" />.
///     <list type="bullet">
///         <item>
///             Original source: <c>RRC11.ASM</c>, routine <c>CIRCLE</c> (with
///             <c>CIRNAC</c>/<c>CIRGO</c>/<c>CIRC2L</c>/<c>CIRC3L</c>)
///         </item>
///         <item>Disassembly: <c>asm/robomame.asm</c> at <c>$11AF</c> (<c>ANIMATE_SPHEROID</c>)</item>
///     </list>
/// </remarks>
public sealed class Spheroid : IEntity, IAnimationFrameSource, IRemovable
{
    /// <summary>
    ///     The spheroid changes its speed by the same amounts for a random number of beats, from 1 up to this. Then it
    ///     picks new amounts.
    /// </summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRC11.ASM</c> <c>CIRNAC</c>.</item>
    ///         <item>Disassembly: <c>asm/robomame.asm</c> <c>ANIMATE_SPHEROID</c> (<c>$11AF</c>).</item>
    ///     </list>
    /// </remarks>
    private const int AccelBeatsMax = 15;

    /// <summary>
    ///     This is taken off the random number that sets <see cref="_accelX" />, so that the answer can be negative,
    ///     which means to the left.
    /// </summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRC11.ASM</c> <c>CIRNAC</c>.</item>
    ///         <item>Disassembly: <c>asm/robomame.asm</c> <c>ANIMATE_SPHEROID</c> (<c>$11AF</c>).</item>
    ///     </list>
    /// </remarks>
    private const int AccelXOffset = 16;

    /// <summary>
    ///     The random number that sets <see cref="_accelX" /> is from 0 up to one less than this, before
    ///     <see cref="AccelXOffset" /> is taken off.
    /// </summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRC11.ASM</c> <c>CIRNAC</c>.</item>
    ///         <item>Disassembly: <c>asm/robomame.asm</c> <c>ANIMATE_SPHEROID</c> (<c>$11AF</c>).</item>
    ///     </list>
    /// </remarks>
    private const int AccelXRollSides = 32;

    /// <summary>
    ///     This is taken off the random number that sets <see cref="_accelY" />, so that the answer can be negative,
    ///     which means upwards.
    /// </summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRC11.ASM</c> <c>CIRNAC</c>.</item>
    ///         <item>Disassembly: <c>asm/robomame.asm</c> <c>ANIMATE_SPHEROID</c> (<c>$11AF</c>).</item>
    ///     </list>
    /// </remarks>
    private const int AccelYOffset = 32;

    /// <summary>
    ///     The random number that sets <see cref="_accelY" /> is from 0 up to one less than this, before
    ///     <see cref="AccelYOffset" /> is taken off. It is twice the sideways number, because a column is 2 pixels wide and a
    ///     row is 1 pixel tall.
    /// </summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRC11.ASM</c> <c>CIRNAC</c>.</item>
    ///         <item>Disassembly: <c>asm/robomame.asm</c> <c>ANIMATE_SPHEROID</c> (<c>$11AF</c>).</item>
    ///     </list>
    /// </remarks>
    private const int AccelYRollSides = 64;

    /// <summary>
    ///     A small extra amount that is taken off the speed on each beat, along with the slowing down that
    ///     <see cref="DampingPer256" /> does.
    /// </summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRC11.ASM</c> <c>CIRGO</c>.</item>
    ///         <item>Disassembly: <c>asm/robomame.asm</c> <c>ANIMATE_SPHEROID</c> (<c>$11AF</c>).</item>
    ///     </list>
    /// </remarks>
    private const int DampingBias = 4;

    /// <summary>On each beat the spheroid is slowed down by this many 256ths of its speed.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRC11.ASM</c> <c>CIRGO</c>.</item>
    ///         <item>Disassembly: <c>asm/robomame.asm</c> <c>ANIMATE_SPHEROID</c> (<c>$11AF</c>).</item>
    ///     </list>
    /// </remarks>
    private const int DampingPer256 = 4;

    /// <summary>The most times a spheroid spins before it drops an enforcer, when it is not told a number.</summary>
    private const int DefaultDropDelayRotations = 24;

    /// <summary>
    ///     The limit used to pick how many enforcers a spheroid drops, when it is not told a number. The most it can drop
    ///     is half of this.
    /// </summary>
    private const int DefaultMaxDropsX2 = 10;

    /// <summary>
    ///     The last animation frame the spheroid shows while it is dropping enforcers, counting from 0. After it, the
    ///     animation starts again.
    /// </summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRC11.ASM</c> <c>CIRC2L</c>, which starts again after its eighth animation frame.</item>
    ///         <item>Disassembly: <c>asm/robomame.asm</c> <c>ANIMATE_SPHEROID</c> (<c>$11AF</c>).</item>
    ///     </list>
    /// </remarks>
    private const int DropLastAnimationFrame = 7;

    /// <summary>
    ///     After the first drop, the wave's number of spins between drops is divided by this. The spheroid then waits a
    ///     random number of spins, from 1 up to the answer, before each later drop.
    /// </summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRC11.ASM</c> <c>CIRC2</c>.</item>
    ///         <item>Disassembly: <c>asm/robomame.asm</c> <c>ANIMATE_SPHEROID</c> (<c>$11AF</c>).</item>
    ///     </list>
    /// </remarks>
    private const int DropRerollDivisor = 4;

    /// <summary>
    ///     The last animation frame the spheroid shows while it spins or escapes, counting from 0. After it, the
    ///     animation starts again.
    /// </summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>
    ///             Original source: <c>RRC11.ASM</c> <c>CIRCLE</c>/<c>CIRC3L</c>, which start again after the fifth
    ///             animation frame.
    ///         </item>
    ///         <item>Disassembly: <c>asm/robomame.asm</c> <c>ANIMATE_SPHEROID</c> (<c>$11AF</c>).</item>
    ///     </list>
    /// </remarks>
    private const int SpinLastAnimationFrame = 4;

    /// <summary>
    ///     How big the spheroid is, in port pixels. It is the size of the spheroid's sprite, and it is used to tell what
    ///     the spheroid touches.
    /// </summary>
    private static readonly (int Width, int Height) CollisionSize =
        (ScreenSize.ToPortPixelsFromArcadePixels(CollisionSizes.SpheroidCollisionSize.Width),
            ScreenSize.ToPortPixelsFromArcadePixels(CollisionSizes.SpheroidCollisionSize.Height));

    private readonly int _dropDelayRotations;
    private readonly Random _random;
    private readonly SpriteSet _sprites;

    /// <summary>Beats left before the spheroid picks new amounts to change its speed by.</summary>
    private int _accelBeatsRemaining;

    /// <summary>How much is added to the spheroid's sideways speed on each beat. A negative number pushes it left.</summary>
    private int _accelX;

    /// <summary>How much is added to the spheroid's up-and-down speed on each beat. A negative number pushes it up.</summary>
    private int _accelY;

    /// <summary>Which animation frame is showing. It counts round as the spheroid spins.</summary>
    private int
        _animationFrameIndex; // It goes up to SpinLastAnimationFrame while the spheroid spins or escapes, and up to DropLastAnimationFrame while it drops enforcers.

    /// <summary>Counts up to the next beat.</summary>
    private int _beatTimer;

    /// <summary>How many more times the spheroid spins before it drops its next enforcer.</summary>
    private int _dropRotationsRemaining;

    /// <summary>How many enforcers this spheroid still has to drop.</summary>
    private int _enforcersRemaining;

    /// <summary>Which way the spheroid goes when it escapes: -1 for left, +1 for right.</summary>
    private readonly int _escapeDirectionSignX;

    /// <summary>True once the spheroid has stopped spinning and started dropping enforcers.</summary>
    private bool _isDropping;

    /// <summary>True once the spheroid is running sideways for the edge of the playfield, where it vanishes.</summary>
    private bool _isEscaping;

    /// <summary>Counts up to the next move. The spheroid moves steadily.</summary>
    private int _moveTimer;

    private IntVector2 _position;
    private int _remainderXSubpixels;
    private int _remainderYSubpixels;

    /// <summary>How far the spheroid goes sideways on each move, in 256ths of a pixel. A negative number is to the left.</summary>
    private int _velocityXSubpixels;

    /// <summary>How far the spheroid goes up or down on each move, in 256ths of a pixel. A negative number is up.</summary>
    private int _velocityYSubpixels;

    /// <summary>
    ///     Makes a spheroid at <paramref name="position" />, part of the way through a spin. It picks at random how many
    ///     enforcers it will drop.
    /// </summary>
    /// <param name="sprites">The shared sprite set.</param>
    /// <param name="position">Where the spheroid's top-left corner is.</param>
    /// <param name="random">
    ///     Where its random numbers come from. They pick how many enforcers it drops, how its speed changes
    ///     and which way it escapes.
    /// </param>
    /// <param name="maxDropsX2">
    ///     This wave's limit for picking how many enforcers the spheroid drops. The most it can drop is
    ///     half of this.
    /// </param>
    /// <param name="dropDelayRotations">The most times this wave's spheroid spins before it drops an enforcer.</param>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>
    ///             Original source: <c>RRC11.ASM</c> <c>ENFNUM</c> and <c>CDPTIM</c>: this wave's limit on enforcers
    ///             and its number of spins between drops.
    ///         </item>
    ///         <item>Disassembly: <c>asm/robomame.asm</c> <c>ANIMATE_SPHEROID</c> (<c>$11AF</c>).</item>
    ///     </list>
    ///     There is no speed to pass in, on purpose. A spheroid's speed comes from the pushes it gives itself on each
    ///     beat, so there is no single number that could make it faster.
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
        // How many enforcers the spheroid may drop: a random number from 1 up to this wave's limit, halved and rounded up.
        var roll = ArcadeRandom.PickUpTo(random, maxDropsX2);
        _enforcersRemaining = (roll + 1) / 2;
        // Pick at random whether the spheroid will escape to the left or to the right.
        _escapeDirectionSignX = random.Next(2) == 0 ? -1 : 1;
        // How many times the spheroid spins before it first drops an enforcer.
        _dropRotationsRemaining = ArcadeRandom.PickUpTo(random, dropDelayRotations);
        _moveTimer = ArcadeClock.UnitsPerRomFrame;
        // The spheroid starts on the last animation frame of a spin, so its first beat finishes a spin.
        _animationFrameIndex = SpinLastAnimationFrame;
        RollAccelerations();
    }

    /// <summary>True once the spheroid has started to escape off the side. Tests use this.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRC11.ASM</c>, the <c>CIRC3</c> escape.</item>
    ///         <item>Disassembly: <c>asm/robomame.asm</c> <c>ANIMATE_SPHEROID</c> (<c>$11AF</c>).</item>
    ///     </list>
    /// </remarks>
    internal bool IsEscaping => _isEscaping;

    /// <summary>Which animation frame is showing, counting from 0. Tests use this.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRC11.ASM</c> <c>CIRCLE</c>, the pointer to the animation frame that is showing.</item>
    ///         <item>Disassembly: <c>asm/robomame.asm</c> <c>ANIMATE_SPHEROID</c> (<c>$11AF</c>).</item>
    ///     </list>
    /// </remarks>
    internal int AnimationFrameIndex => _animationFrameIndex;

    /// <summary>
    ///     The animation frame the spheroid is showing. The death burst is drawn from it (see
    ///     <see cref="IAnimationFrameSource" />).
    /// </summary>
    /// <returns>The animation frame that is showing.</returns>
    public Texture2D GetCurrentAnimationFrame()
    {
        return _sprites.SpheroidAnimationFrames[_animationFrameIndex % _sprites.SpheroidAnimationFrames.Length];
    }

    /// <summary>The box the spheroid takes up on the screen. It is used to tell what the spheroid touches.</summary>
    public Rectangle GetBounds()
    {
        return new Rectangle(_position.X, _position.Y, CollisionSize.Width, CollisionSize.Height);
    }

    /// <summary>Alive until it is shot or it has escaped off the side. It is never Dying (see <see cref="Kill" />).</summary>
    public EntityLifeState LifeState { get; private set; } = EntityLifeState.Alive;

    /// <summary>Where the spheroid's top-left corner is.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRC11.ASM</c> the OBJX/OBJY registers.</item>
    ///         <item>Disassembly: <c>asm/robomame.asm</c> <c>ANIMATE_SPHEROID</c> (<c>$11AF</c>).</item>
    ///     </list>
    /// </remarks>
    public IntVector2 Position => _position;

    /// <summary>
    ///     Draws the animation frame that is showing. The spheroid shimmers because it is drawn in palette slots whose
    ///     colours keep changing.
    /// </summary>
    /// <param name="spriteBatch">What the spheroid is drawn with.</param>
    public void Draw(SpriteBatch spriteBatch)
    {
        if (!this.IsAlive()) return;

        _sprites.Blitter.DrawSprite(spriteBatch, GetCurrentAnimationFrame(), GetBounds(), Color.White);
    }

    /// <summary>
    ///     Runs one tick. The spheroid moves when it is time to. On a beat its speed changes, and it spins, drops an
    ///     enforcer or escapes.
    /// </summary>
    /// <param name="gameTime">Not used. The spheroid counts ticks.</param>
    /// <param name="field">The playfield.</param>
    public void Update(GameTime gameTime, PlayField field)
    {
        if (!this.IsAlive()) return;

        // The spheroid does not move while the robots are frozen, before the wave has started, and while the Player is dying (ROM: RRS22.ASM OPRC80; STATUS bit 3).
        if (!field.RobotsFrozen()) AdvanceMover(field);

        // Wait for the spheroid's next beat, which is its next turn to act (see ArcadeClock).
        _beatTimer += ArcadeClock.UnitsPerPortTick;
        if (_beatTimer < ArcadeClock.ToClockUnits(SpheroidTuning.BeatIntervalRomFrames)) return;

        _beatTimer -= ArcadeClock.ToClockUnits(SpheroidTuning.BeatIntervalRomFrames);

        // wrapPass is true when the animation is on its last animation frame. The count of spins left before a drop only goes down on those beats.
        var lastAnimationFrame = _isDropping && !_isEscaping ? DropLastAnimationFrame : SpinLastAnimationFrame;
        var wrapPass = _animationFrameIndex >= lastAnimationFrame;

        if (_isEscaping)
        {
            AdvanceEscapeBeat(field, wrapPass);
            return;
        }

        // On every beat the spheroid's speed changes. When its countdown runs out, it picks a new random amount for the speed to change by.
        AccelerateAndDamp();
        if (--_accelBeatsRemaining <= 0) RollAccelerations();

        if (!wrapPass)
        {
            _animationFrameIndex++;
            return;
        }

        AdvanceDropBeat(field);
    }

    /// <summary>Kills the spheroid at once. A spheroid that is shot leaves a death burst, not a strip explosion.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>
    ///             Original source: <c>RRC11.ASM</c> <c>CIRKIL</c>, which plays a burst of 7 animation frames and then shows
    ///             "1000".
    ///         </item>
    ///         <item>Disassembly: <c>asm/robomame.asm</c> <c>ANIMATE_SPHEROID</c> (<c>$11AF</c>).</item>
    ///     </list>
    ///     <see cref="RobotKinds" /> says that a spheroid killed by a laser leaves the burst that
    ///     <see cref="ScoreBurst.CreateForSpheroid" /> makes.
    /// </remarks>
    public void Kill()
    {
        if (!this.IsAlive()) return;

        LifeState = EntityLifeState.Dead;
    }

    /// <summary>
    ///     Counts one tick towards the spheroid's next move, and makes the move when it is due. The spheroid moves on its
    ///     own clock, which is more often than it has a beat.
    /// </summary>
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

    /// <summary>
    ///     Makes one move, either sideways or up-and-down, by whole pixels. The fraction of a pixel left over is kept for
    ///     the next move. The move is not made if it would take the spheroid out of the playfield.
    /// </summary>
    /// <param name="position">Where the spheroid is, measured sideways or up-and-down.</param>
    /// <param name="velocitySubpixels">How far it goes on each move, in 256ths of a pixel.</param>
    /// <param name="remainderSubpixels">The fraction of a pixel left over from the last move. This is brought up to date.</param>
    /// <param name="min">The lowest place the spheroid may be.</param>
    /// <param name="max">The highest place the spheroid may be.</param>
    /// <returns>Where the spheroid is after the move.</returns>
    private static int AdvanceAxis(int position, int velocitySubpixels, ref int remainderSubpixels, int min, int max)
    {
        var nextRemainder = remainderSubpixels + velocitySubpixels;
        var step = nextRemainder >> ScreenSize.SubpixelBits;
        var next = position + step;
        if (next < min || next > max) return position; // The move would leave the playfield, so it is not made.

        remainderSubpixels = nextRemainder - (step << ScreenSize.SubpixelBits);
        return next;
    }

    /// <summary>Keeps a speed within the top speed, and then slows it down a little.</summary>
    /// <param name="velocitySubpixels">The speed, in 256ths of a pixel for each move.</param>
    /// <param name="limitSubpixels">The top speed, measured the same way.</param>
    /// <returns>The new speed.</returns>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>
    ///             Original source: <c>RRC11.ASM</c>, the second half of <c>CIRGO</c>. The speed is pushed the same way on
    ///             every beat and slowed a little on every beat, so it settles at about 64 times the push. That is usually
    ///             more than the top speed, so the spheroid usually ends up going at its top speed.
    ///         </item>
    ///         <item>Disassembly: <c>asm/robomame.asm</c> <c>ANIMATE_SPHEROID</c> (<c>$11AF</c>).</item>
    ///     </list>
    /// </remarks>
    private static int ClampThenDamp(int velocitySubpixels, int limitSubpixels)
    {
        velocitySubpixels = Math.Clamp(velocitySubpixels, -limitSubpixels, limitSubpixels);
        return velocitySubpixels + ((-DampingPer256 * velocitySubpixels - DampingBias) >> ScreenSize.SubpixelBits);
    }

    /// <summary>
    ///     Changes the spheroid's speed for one beat. It adds the push, keeps the speed within the top speed, and then
    ///     slows it down a little.
    /// </summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRC11.ASM</c> <c>CIRGO</c>.</item>
    ///         <item>Disassembly: <c>asm/robomame.asm</c> <c>ANIMATE_SPHEROID</c> (<c>$11AF</c>).</item>
    ///     </list>
    /// </remarks>
    private void AccelerateAndDamp()
    {
        _velocityXSubpixels = ClampThenDamp(_velocityXSubpixels + _accelX, SpheroidTuning.MaxVelocityXSubpixels);
        _velocityYSubpixels = ClampThenDamp(_velocityYSubpixels + _accelY, SpheroidTuning.MaxVelocityYSubpixels);
    }

    /// <summary>
    ///     Runs the beat that ends a spin. The spheroid counts down its spins. When the count runs out, it drops an
    ///     enforcer if the playfield has room for one.
    /// </summary>
    /// <param name="field">
    ///     The playfield. It says whether there is room for another enforcer, and the new enforcer is put on
    ///     it.
    /// </param>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRC11.ASM</c> <c>CIRC2</c>.</item>
    ///         <item>Disassembly: <c>asm/robomame.asm</c> <c>ANIMATE_SPHEROID</c> (<c>$11AF</c>).</item>
    ///     </list>
    ///     While the robots are frozen, a spinning spheroid goes back to its first animation frame but does not count
    ///     down. A spheroid that is dropping or escaping is not held up like this, because the arcade only makes this
    ///     check in its spinning routine.
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
            // When the spheroid changes from spinning to dropping, the animation does not start again. It carries on from the animation frame it is on.
            _isDropping = true;
            RerollDropCountdown();
            return;
        }

        // If there are already too many enforcers on the field, none is dropped this time. The spheroid picks a new wait and tries again after it.
        if (field.CanDropEnforcer())
        {
            field.SpawnEnforcer(_position);
            if (--_enforcersRemaining <= 0)
            {
                StartEscape(); // The animation frame does not change until the first beat of the escape.
                return;
            }
        }

        RerollDropCountdown();
        _animationFrameIndex = 0;
    }

    /// <summary>
    ///     Runs one beat of the escape. The spheroid shows its next animation frame. At the end of each spin, it is taken
    ///     off the field if it has reached the edge.
    /// </summary>
    /// <param name="field">The playfield, whose left and right edges the spheroid escapes to.</param>
    /// <param name="wrapPass">True on the beat when the last animation frame of the spin is showing.</param>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>
    ///             Original source: <c>RRC11.ASM</c> <c>CIRC3L</c>. The check for the edge is made only at the end of a
    ///             spin.
    ///         </item>
    ///         <item>Disassembly: <c>asm/robomame.asm</c> <c>ANIMATE_SPHEROID</c> (<c>$11AF</c>).</item>
    ///     </list>
    /// </remarks>
    private void AdvanceEscapeBeat(PlayField field, bool wrapPass)
    {
        if (!wrapPass)
        {
            _animationFrameIndex++;
            return;
        }

        var bounds = field.Wall.PlayfieldBounds;
        var leftExit = bounds.X + ScreenSize.ToPortPixelsFromColumns(SpheroidTuning.EscapeExitLeftColumn);
        var rightExit = ScreenSize.ToPortPixelsFromColumns(SpheroidTuning.EscapeExitRightColumn);
        if (_position.X <= leftExit || _position.X >= rightExit)
        {
            LifeState = EntityLifeState
                .Dead; // The spheroid has reached the edge. It vanishes at once, with no death burst (ROM: CIR4).
            return;
        }

        _animationFrameIndex = 0;
    }

    /// <summary>
    ///     Makes one move. The sideways part is made only if it keeps the spheroid inside the playfield, and the same
    ///     goes for the up-or-down part.
    /// </summary>
    /// <param name="field">The playfield, whose walls the spheroid stays inside.</param>
    private void AdvancePosition(PlayField field)
    {
        var bounds = field.Wall.PlayfieldBounds;
        _position = new IntVector2(
            AdvanceAxis(_position.X, _velocityXSubpixels, ref _remainderXSubpixels, bounds.X,
                bounds.Right - CollisionSize.Width),
            AdvanceAxis(_position.Y, _velocityYSubpixels, ref _remainderYSubpixels, bounds.Y,
                bounds.Bottom - CollisionSize.Height));
    }

    /// <summary>Picks at random how many times the spheroid spins before its next drop.</summary>
    /// <remarks>ROM: <c>CIRC2</c>.</remarks>
    private void RerollDropCountdown()
    {
        _dropRotationsRemaining = ArcadeRandom.PickUpTo(_random, _dropDelayRotations / DropRerollDivisor);
    }

    /// <summary>
    ///     Picks new random amounts to change the spheroid's speed by, sideways and up-and-down, and how many beats to
    ///     keep them for.
    /// </summary>
    /// <remarks>ROM: <c>CIRNAC</c>.</remarks>
    private void RollAccelerations()
    {
        _accelX = _random.Next(0, AccelXRollSides) - AccelXOffset;
        _accelY = _random.Next(0, AccelYRollSides) - AccelYOffset;
        _accelBeatsRemaining = 1 + _random.Next(0, AccelBeatsMax);
    }

    /// <summary>
    ///     Starts the escape. The spheroid stops going up or down and goes sideways at its top speed. Its animation frame
    ///     is left as it is.
    /// </summary>
    /// <remarks>
    ///     ROM: <c>CIRC3</c>. The arcade leaves the last animation frame of the drop showing, and the first beat of
    ///     the escape goes back to the first animation frame of the spin.
    /// </remarks>
    private void StartEscape()
    {
        _isEscaping = true;
        _velocityXSubpixels = _escapeDirectionSignX * SpheroidTuning.MaxVelocityXSubpixels;
        _velocityYSubpixels = 0;
        _remainderXSubpixels = 0;
        _remainderYSubpixels = 0;
    }
}
