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
///     It acts on a <see cref="Robotron2084.Core.ArcadeFrameTimer">beat</see>. <see cref="_beatTimer" /> says when it is time for the next <see cref="Robotron2084.Core.ArcadeFrameTimer">beat</see>.
///     It also moves steadily, timed by <see cref="_moveTimer" />.
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
    ///     The most <see cref="Robotron2084.Core.ArcadeFrameTimer">beats</see> a spheroid keeps changing its speed by the
    ///     same amounts before it picks new ones.
    /// </summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRC11.ASM</c> <c>CIRNAC</c>.</item>
    ///         <item>Disassembly: <c>asm/robomame.asm</c> <c>ANIMATE_SPHEROID</c> (<c>$11AF</c>).</item>
    ///     </list>
    /// </remarks>
    private const int AccelBeatsMax = 15;

    /// <summary>The strongest push to the left that a spheroid's sideways speed can change by.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRC11.ASM</c> <c>CIRNAC</c>.</item>
    ///         <item>Disassembly: <c>asm/robomame.asm</c> <c>ANIMATE_SPHEROID</c> (<c>$11AF</c>).</item>
    ///     </list>
    /// </remarks>
    private const int AccelXOffset = 16;

    /// <summary>How many different amounts a spheroid's sideways push can be.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRC11.ASM</c> <c>CIRNAC</c>.</item>
    ///         <item>Disassembly: <c>asm/robomame.asm</c> <c>ANIMATE_SPHEROID</c> (<c>$11AF</c>).</item>
    ///     </list>
    /// </remarks>
    private const int AccelXRandomRange = 32;

    /// <summary>The strongest push upwards that a spheroid's up-and-down speed can change by.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRC11.ASM</c> <c>CIRNAC</c>.</item>
    ///         <item>Disassembly: <c>asm/robomame.asm</c> <c>ANIMATE_SPHEROID</c> (<c>$11AF</c>).</item>
    ///     </list>
    /// </remarks>
    private const int AccelYOffset = 32;

    /// <summary>
    ///     How many different amounts a spheroid's up-and-down push can be. It is twice the sideways number, because a
    ///     column is 2 pixels wide and a row is 1 pixel tall.
    /// </summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRC11.ASM</c> <c>CIRNAC</c>.</item>
    ///         <item>Disassembly: <c>asm/robomame.asm</c> <c>ANIMATE_SPHEROID</c> (<c>$11AF</c>).</item>
    ///     </list>
    /// </remarks>
    private const int AccelYRandomRange = 64;

    /// <summary>A small extra slowing of a spheroid, on top of <see cref="DampingPer256" />.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRC11.ASM</c> <c>CIRGO</c>.</item>
    ///         <item>Disassembly: <c>asm/robomame.asm</c> <c>ANIMATE_SPHEROID</c> (<c>$11AF</c>).</item>
    ///     </list>
    /// </remarks>
    private const int DampingBias = 4;

    /// <summary>How much a spheroid slows down: this many 256ths of its speed.</summary>
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
    ///     The limit on how many enforcers a spheroid drops, when it is not told a number. The most it can drop is half of
    ///     this.
    /// </summary>
    private const int DefaultMaxDropsX2 = 10;

    /// <summary>The last animation frame the spheroid shows while it is dropping enforcers, counting from 0.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRC11.ASM</c> <c>CIRC2L</c>, which starts again after its eighth animation frame.</item>
    ///         <item>Disassembly: <c>asm/robomame.asm</c> <c>ANIMATE_SPHEROID</c> (<c>$11AF</c>).</item>
    ///     </list>
    /// </remarks>
    private const int DropLastAnimationFrame = 7;

    /// <summary>How many times shorter a spheroid's wait between drops is once it has made its first drop.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRC11.ASM</c> <c>CIRC2</c>.</item>
    ///         <item>Disassembly: <c>asm/robomame.asm</c> <c>ANIMATE_SPHEROID</c> (<c>$11AF</c>).</item>
    ///     </list>
    /// </remarks>
    private const int RepeatDropDelayDivisor = 4;

    /// <summary>The last animation frame the spheroid shows while it spins or escapes, counting from 0.</summary>
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
    ///     How big the spheroid is, in port pixels. It is the size of the spheroid's sprite.
    /// </summary>
    private static readonly (int Width, int Height) CollisionSize =
        (ScreenSize.ToPortPixelsFromArcadePixels(CollisionSizes.SpheroidCollisionSize.Width),
            ScreenSize.ToPortPixelsFromArcadePixels(CollisionSizes.SpheroidCollisionSize.Height));

    private readonly int _dropDelayRotations;
    private readonly Random _random;
    private readonly SpriteSet _sprites;

    /// <summary><see cref="Robotron2084.Core.ArcadeFrameTimer">Beats</see> left before the spheroid picks new amounts to change its speed by.</summary>
    private int _accelBeatsRemaining;

    /// <summary>The spheroid's sideways push. A negative number pushes it left.</summary>
    private int _accelX;

    /// <summary>The spheroid's up-and-down push. A negative number pushes it up.</summary>
    private int _accelY;

    /// <summary>Which animation frame is showing. It counts round as the spheroid spins.</summary>
    private int _animationFrameIndex;

    private readonly ArcadeFrameTimer _beatTimer = new();

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

    /// <summary>Times the spheroid's moves.</summary>
    private readonly ArcadeFrameTimer _moveTimer = ArcadeFrameTimer.CreateDue(ArcadeClock.MoveIntervalArcadeFrames);

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
    ///     <see cref="Robotron2084.Core.ArcadeFrameTimer">beat</see>, so there is no single number that could make it faster.
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
        var randomDrops = ArcadeRandom.PickUpTo(random, maxDropsX2);
        _enforcersRemaining = (randomDrops + 1) / 2;
        // Pick at random whether the spheroid will escape to the left or to the right.
        _escapeDirectionSignX = 1;
        var isEscapingLeft = random.Next(2) == 0;
        if (isEscapingLeft) _escapeDirectionSignX = -1;

        // How many times the spheroid spins before it first drops an enforcer.
        _dropRotationsRemaining = ArcadeRandom.PickUpTo(random, dropDelayRotations);
        // The spheroid starts on the last animation frame of a spin, so its first beat finishes a spin.
        _animationFrameIndex = SpinLastAnimationFrame;
        SetRandomAccelerations();
    }

    /// <summary>True once the spheroid has started to escape off the side.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRC11.ASM</c>, the <c>CIRC3</c> escape.</item>
    ///         <item>Disassembly: <c>asm/robomame.asm</c> <c>ANIMATE_SPHEROID</c> (<c>$11AF</c>).</item>
    ///     </list>
    /// </remarks>
    internal bool IsEscaping => _isEscaping;

    /// <summary>Which animation frame is showing, counting from 0.</summary>
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

    /// <summary>The box the spheroid takes up on the screen.</summary>
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
    ///     Runs one <see cref="Robotron2084.Core.ArcadeClock.PortTicksPerSecond">tick</see>. The spheroid moves when it is time to. On a <see cref="Robotron2084.Core.ArcadeFrameTimer">beat</see> its speed changes, and it spins, drops an
    ///     enforcer or escapes.
    /// </summary>
    /// <param name="gameTime">Not used. The spheroid counts <see cref="Robotron2084.Core.ArcadeClock.PortTicksPerSecond">ticks</see>.</param>
    /// <param name="field">The playfield.</param>
    public void Update(GameTime gameTime, PlayField field)
    {
        if (!this.IsAlive()) return;

        // The spheroid does not move while the robots are frozen, before the wave has started, and while the Player is dying (ROM: RRS22.ASM OPRC80; STATUS bit 3).
        if (!field.RobotsFrozen() && _moveTimer.Tick(ArcadeClock.MoveIntervalArcadeFrames)) MoveOnce(field);

        if (_beatTimer.Tick(SpheroidTuning.BeatIntervalArcadeFrames)) ActOnBeat(field);
    }

    /// <summary>Kills the spheroid at once.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>
    ///             Original source: <c>RRC11.ASM</c> <c>CIRKIL</c>, which plays a burst of 7 animation frames and then shows
    ///             "1000".
    ///         </item>
    ///         <item>Disassembly: <c>asm/robomame.asm</c> <c>ANIMATE_SPHEROID</c> (<c>$11AF</c>).</item>
    ///     </list>
    /// </remarks>
    public void Kill()
    {
        if (!this.IsAlive()) return;

        LifeState = EntityLifeState.Dead;
    }

    /// <summary>
    ///     What the spheroid does on its <see cref="Robotron2084.Core.ArcadeFrameTimer">beat</see>. Its speed changes, and it spins, drops an enforcer or escapes.
    /// </summary>
    /// <param name="field">The playfield.</param>
    private void ActOnBeat(PlayField field)
    {
        // wrapPass is true when the animation is on its last animation frame. The count of spins left before a drop only goes down on those beats.
        var lastAnimationFrame = SpinLastAnimationFrame;
        var isDroppingAnEnforcer = _isDropping && !_isEscaping;
        if (isDroppingAnEnforcer) lastAnimationFrame = DropLastAnimationFrame;

        var wrapPass = _animationFrameIndex >= lastAnimationFrame;

        if (_isEscaping)
        {
            ActOnEscapeBeat(field, wrapPass);
            return;
        }

        // On every beat the spheroid's speed changes. When its countdown runs out, it picks a new random amount for the speed to change by.
        AccelerateAndDamp();
        if (--_accelBeatsRemaining <= 0) SetRandomAccelerations();

        if (!wrapPass)
        {
            _animationFrameIndex++;
            return;
        }

        ActOnSpinEndBeat(field);
    }

    /// <summary>
    ///     Works out one move, either sideways or up-and-down, by whole pixels. The fraction of a pixel left over is
    ///     kept for the next move. The move is not made if it would take the spheroid out of the playfield.
    /// </summary>
    /// <param name="position">Where the spheroid is, measured sideways or up-and-down.</param>
    /// <param name="velocitySubpixels">How far it goes on each move, in 256ths of a pixel.</param>
    /// <param name="remainderSubpixels">The fraction of a pixel left over from the last move.</param>
    /// <param name="min">The lowest place the spheroid may be.</param>
    /// <param name="max">The highest place the spheroid may be.</param>
    /// <returns>Where the spheroid is after the move, and the fraction of a pixel left over from it.</returns>
    private static (int Position, int RemainderSubpixels) GetAxisAfterMove(
        int position, int velocitySubpixels, int remainderSubpixels, int min, int max)
    {
        var nextRemainder = remainderSubpixels + velocitySubpixels;
        var step = nextRemainder >> ScreenSize.SubpixelBits;
        var next = position + step;
        // The move would leave the playfield, so it is not made.
        if (next < min || next > max) return (position, remainderSubpixels);

        return (next, nextRemainder - (step << ScreenSize.SubpixelBits));
    }

    /// <summary>Keeps a speed within the top speed, and then slows it down a little.</summary>
    /// <param name="velocitySubpixels">The speed, in 256ths of a pixel for each move.</param>
    /// <param name="limitSubpixels">The top speed, measured the same way.</param>
    /// <returns>The new speed.</returns>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>
    ///             Original source: <c>RRC11.ASM</c>, the second half of <c>CIRGO</c>. The speed is pushed the same way on
    ///             every <see cref="Robotron2084.Core.ArcadeFrameTimer">beat</see> and slowed a little on every <see cref="Robotron2084.Core.ArcadeFrameTimer">beat</see>, so it settles at about 64 times the push. That is usually
    ///             more than the top speed, so the spheroid usually ends up going at its top speed.
    ///         </item>
    ///         <item>Disassembly: <c>asm/robomame.asm</c> <c>ANIMATE_SPHEROID</c> (<c>$11AF</c>).</item>
    ///     </list>
    /// </remarks>
    private static int GetClampedAndDampedVelocity(int velocitySubpixels, int limitSubpixels)
    {
        velocitySubpixels = Math.Clamp(velocitySubpixels, -limitSubpixels, limitSubpixels);
        return velocitySubpixels + ((-DampingPer256 * velocitySubpixels - DampingBias) >> ScreenSize.SubpixelBits);
    }

    /// <summary>
    ///     Changes the spheroid's speed for one <see cref="Robotron2084.Core.ArcadeFrameTimer">beat</see>. It adds the push, keeps the speed within the top speed, and then
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
        _velocityXSubpixels = GetClampedAndDampedVelocity(_velocityXSubpixels + _accelX, SpheroidTuning.MaxVelocityXSubpixels);
        _velocityYSubpixels = GetClampedAndDampedVelocity(_velocityYSubpixels + _accelY, SpheroidTuning.MaxVelocityYSubpixels);
    }

    /// <summary>
    ///     Runs the <see cref="Robotron2084.Core.ArcadeFrameTimer">beat</see> that ends a spin. The spheroid counts down its spins. When the count runs out, it drops an
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
    private void ActOnSpinEndBeat(PlayField field)
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
            SetRandomDropCountdown();
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

        SetRandomDropCountdown();
        _animationFrameIndex = 0;
    }

    /// <summary>
    ///     Runs one <see cref="Robotron2084.Core.ArcadeFrameTimer">beat</see> of the escape. The spheroid shows its next animation frame. At the end of each spin, it is taken
    ///     off the field if it has reached the edge.
    /// </summary>
    /// <param name="field">The playfield, whose left and right edges the spheroid escapes to.</param>
    /// <param name="wrapPass">True on the <see cref="Robotron2084.Core.ArcadeFrameTimer">beat</see> when the last animation frame of the spin is showing.</param>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>
    ///             Original source: <c>RRC11.ASM</c> <c>CIRC3L</c>. The check for the edge is made only at the end of a
    ///             spin.
    ///         </item>
    ///         <item>Disassembly: <c>asm/robomame.asm</c> <c>ANIMATE_SPHEROID</c> (<c>$11AF</c>).</item>
    ///     </list>
    /// </remarks>
    private void ActOnEscapeBeat(PlayField field, bool wrapPass)
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
    private void MoveOnce(PlayField field)
    {
        var bounds = field.Wall.PlayfieldBounds;
        var x = GetAxisAfterMove(_position.X, _velocityXSubpixels, _remainderXSubpixels, bounds.X,
            bounds.Right - CollisionSize.Width);
        var y = GetAxisAfterMove(_position.Y, _velocityYSubpixels, _remainderYSubpixels, bounds.Y,
            bounds.Bottom - CollisionSize.Height);
        _position = new IntVector2(x.Position, y.Position);
        _remainderXSubpixels = x.RemainderSubpixels;
        _remainderYSubpixels = y.RemainderSubpixels;
    }

    /// <summary>Picks at random how many times the spheroid spins before its next drop.</summary>
    /// <remarks>ROM: <c>CIRC2</c>.</remarks>
    private void SetRandomDropCountdown()
    {
        _dropRotationsRemaining = ArcadeRandom.PickUpTo(_random, _dropDelayRotations / RepeatDropDelayDivisor);
    }

    /// <summary>
    ///     Picks new random amounts to change the spheroid's speed by, sideways and up-and-down, and how many <see cref="Robotron2084.Core.ArcadeFrameTimer">beats</see> to
    ///     keep them for.
    /// </summary>
    /// <remarks>ROM: <c>CIRNAC</c>.</remarks>
    private void SetRandomAccelerations()
    {
        _accelX = _random.Next(0, AccelXRandomRange) - AccelXOffset;
        _accelY = _random.Next(0, AccelYRandomRange) - AccelYOffset;
        _accelBeatsRemaining = 1 + _random.Next(0, AccelBeatsMax);
    }

    /// <summary>
    ///     Starts the escape. The spheroid stops going up or down and goes sideways at its top speed. Its animation frame
    ///     is left as it is.
    /// </summary>
    /// <remarks>
    ///     ROM: <c>CIRC3</c>. The arcade leaves the last animation frame of the drop showing, and the first <see cref="Robotron2084.Core.ArcadeFrameTimer">beat</see> of
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
