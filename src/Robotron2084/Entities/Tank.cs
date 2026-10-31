using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Graphics;
using Robotron2084.Level;
using Robotron2084.Tuning;

namespace Robotron2084.Entities;

/// <summary>A tank is a slow, heavily armoured robot dropped by quarks. It rolls around and fires shells at you.</summary>
/// <seealso cref="Quark" />
/// <seealso cref="TankShell" />
/// <remarks>
///     It acts on a <see cref="Robotron2084.Core.ArcadeFrameTimer">beat</see>.
///     <see cref="_beatTimer" /> says when it is time for the next <see cref="Robotron2084.Core.ArcadeFrameTimer">beat</see>, and <see cref="_growTimer" /> times the stages
///     of its growing.
///     <list type="bullet">
///         <item>Original source: <c>RRTK4.ASM</c>, routine <c>TANK</c></item>
///         <item>
///             Disassembly: <c>asm/robomame.asm</c> at <c>ANIMATE_TANK</c> (<c>$4D99</c>, near the check of the wait
///             between shots at <c>$4D55</c>)
///         </item>
///     </list>
/// </remarks>
public sealed class Tank : IExplodable, IRemovable, IWaveStartRobot
{
    /// <summary>How many <see cref="Robotron2084.Core.ArcadeFrameTimer">beats</see> a tank waits between shots, when it is not told a number.</summary>
    private const int DefaultFireIntervalBeats = 32;

    /// <summary>The range of the random number behind where a tank goes: from 0 up to one less than this.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRTK4.ASM</c> <c>ANIMATE_TANK</c>.</item>
    ///         <item>Disassembly: <c>asm/robomame.asm</c> <c>ANIMATE_TANK</c> (<c>$4D99</c>).</item>
    ///     </list>
    /// </remarks>
    private const int DestinationRandomRange = 256;

    /// <summary>
    ///     One more than the most extra <see cref="Robotron2084.Core.ArcadeFrameTimer">beats</see> a tank's first shot
    ///     waits.
    /// </summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRTK4.ASM</c> <c>TNKSHT</c>.</item>
    ///         <item>Disassembly: <c>asm/robomame.asm</c> <c>ANIMATE_TANK</c> (<c>$4D99</c>).</item>
    ///     </list>
    /// </remarks>
    private const int FirstShotExtraBeatsMaxExclusive = 32;

    /// <summary>How long the arcade's routine sleeps between one look at whether the game is live and the next.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>
    ///             Original source: <c>RRTK4.ASM</c> <c>TANK</c>, <c>BITA #$7F / BEQ TANKL / NAP 15,TANK</c>,
    ///             which runs on into its first <see cref="Robotron2084.Core.ArcadeFrameTimer">beat</see> with no more sleep.
    ///         </item>
    ///         <item>Disassembly: <c>$4D8B</c>.</item>
    ///     </list>
    /// </remarks>
    private const int LivePollArcadeFrames = 15;

    /// <summary>
    ///     One more than the most <see cref="Robotron2084.Core.ArcadeFrameTimer">beats</see> a tank waits before it picks a
    ///     new place to go.
    /// </summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRTK4.ASM</c> <c>TANKND</c>.</item>
    ///         <item>Disassembly: <c>asm/robomame.asm</c> <c>ANIMATE_TANK</c> (<c>$4D99</c>).</item>
    ///     </list>
    /// </remarks>
    private const int NewDestinationBeatsMaxExclusive = 32;

    /// <summary>The fewest <see cref="Robotron2084.Core.ArcadeFrameTimer">beats</see> a tank waits before it picks a new place to go.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRTK4.ASM</c> <c>TANKND</c>.</item>
    ///         <item>Disassembly: <c>asm/robomame.asm</c> <c>ANIMATE_TANK</c> (<c>$4D99</c>).</item>
    ///     </list>
    /// </remarks>
    private const int NewDestinationBeatsMin = 1;

    /// <summary>How often a tank goes for the player when it picks where to go: about 38 times in 100.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRTK4.ASM</c> <c>ANIMATE_TANK</c>.</item>
    ///         <item>Disassembly: <c>asm/robomame.asm</c> <c>ANIMATE_TANK</c> (<c>$4D99</c>).</item>
    ///     </list>
    /// </remarks>
    private const int PlayerDestinationRandomAtMost = 96;

    /// <summary>
    ///     A tank only goes up or down when the place it is going to is more than this many arcade pixels above or below it.
    /// </summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRTK4.ASM</c> <c>TANK</c> (ROM <c>$4E11</c>).</item>
    ///         <item>Disassembly: <c>asm/robomame.asm</c> <c>ANIMATE_TANK</c> (<c>$4D99</c>).</item>
    ///     </list>
    /// </remarks>
    private const int VerticalMoveThresholdArcadePixels = 16;

    /// <summary>
    ///     How big the tank is, in port pixels. It is the size of the tank's sprite.
    /// </summary>
    private static readonly (int Width, int Height) CollisionSize =
        (ScreenSize.ToPortPixelsFromArcadePixels(CollisionSizes.TankCollisionSize.Width),
            ScreenSize.ToPortPixelsFromArcadePixels(CollisionSizes.TankCollisionSize.Height));

    private static readonly int VerticalMoveThreshold =
        ScreenSize.ToPortPixelsFromArcadePixels(VerticalMoveThresholdArcadePixels);

    private readonly int _fireIntervalBeats;
    private readonly ArcadeFrameTimer _growTimer = new();
    private readonly Random _random;
    private readonly SpriteSet _sprites;
    private ArcadeFrameTimer _beatTimer = new();

    private IntVector2 _destination;
    private int _fireBeatsRemaining;

    /// <summary>Which of the growing animation frames is showing, counting from 0.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRTK4.ASM</c> <c>MTANK</c>, the growing tank.</item>
    ///         <item>Disassembly: <c>asm/robomame.asm</c> <c>ANIMATE_TANK</c> (<c>$4D99</c>).</item>
    ///     </list>
    /// </remarks>
    private int _growStep;

    private int _newDestinationBeatsRemaining;
    private IntVector2 _position;

    private IntVector2
        _stepDirection; // Which way the tank steps on each beat: left or right (or neither), and up or down (or neither).

    /// <summary>How many <see cref="Robotron2084.Core.ArcadeFrameTimer">beats</see> the tank has had.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRTK4.ASM</c> <c>TANK3</c>, which moves the animation on once a <see cref="Robotron2084.Core.ArcadeFrameTimer">beat</see>.</item>
    ///         <item>Disassembly: <c>asm/robomame.asm</c> <c>ANIMATE_TANK</c> (<c>$4D99</c>).</item>
    ///     </list>
    /// </remarks>
    private int _treadAnimationFrameCounter;

    /// <summary>
    ///     Makes a tank at <paramref name="position" />. Unless it starts fully grown, it has to grow before it can move
    ///     or fire.
    /// </summary>
    /// <param name="sprites">The shared sprite set.</param>
    /// <param name="position">Where the tank's top-left corner is.</param>
    /// <param name="random">
    ///     Where its random numbers come from. They pick where it goes, how long before it picks again, and
    ///     the wait before its first shot.
    /// </param>
    /// <param name="fireIntervalBeats">How many <see cref="Robotron2084.Core.ArcadeFrameTimer">beats</see> this wave's tank waits between shots.</param>
    /// <param name="startFullyGrown">True for a tank that is on the field at the start of a life. It does not grow first.</param>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>
    ///             Original source: <c>RRTK4.ASM</c>. <c>TNKSHT</c> is this wave's number of <see cref="Robotron2084.Core.ArcadeFrameTimer">beats</see> between shots.
    ///             <c>TNKSTV</c>
    ///             puts a tank on at full size. Only a tank dropped by a quark grows (<c>MTANK</c>).
    ///         </item>
    ///         <item>Disassembly: <c>asm/robomame.asm</c> <c>ANIMATE_TANK</c> (<c>$4D99</c>).</item>
    ///     </list>
    /// </remarks>
    public Tank(
        SpriteSet sprites,
        IntVector2 position,
        Random random,
        int fireIntervalBeats = DefaultFireIntervalBeats,
        bool startFullyGrown = false)
    {
        _growStep = 0;
        if (startFullyGrown) _growStep = TankTuning.GrowSteps;

        _sprites = sprites;
        _position = position;
        _random = random;
        _fireIntervalBeats = fireIntervalBeats;
        _destination = position;
        // The tank picks where to go at the end of its first beat (ROM: TANKL).
        _newDestinationBeatsRemaining = 0;
        // The first shot waits this wave's gap between shots, plus a random number of extra beats.
        _fireBeatsRemaining = fireIntervalBeats + random.Next(0, FirstShotExtraBeatsMaxExclusive);
    }

    /// <summary>Alive until it is killed. It is never Dying, because it has no death animation (see <see cref="Kill" />).</summary>
    public EntityLifeState LifeState { get; private set; } = EntityLifeState.Alive;

    /// <summary>Where the tank's top-left corner is.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRTK4.ASM</c> the OBJX/OBJY registers.</item>
    ///         <item>Disassembly: <c>asm/robomame.asm</c> <c>ANIMATE_TANK</c> (<c>$4D99</c>).</item>
    ///     </list>
    /// </remarks>
    public IntVector2 Position => _position;

    /// <summary>Which tread animation frame is showing, as a place in <see cref="SpriteSet.TankAnimationFrames" />.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>
    ///             Original source: <c>RRTK4.ASM</c> <c>TANK3</c>. Playing the animation backwards while the tank goes
    ///             left is the arcade's own rule.
    ///         </item>
    ///         <item>Disassembly: <c>asm/robomame.asm</c> <c>ANIMATE_TANK</c> (<c>$4D99</c>).</item>
    ///     </list>
    /// </remarks>
    internal int TreadFrameIndex
    {
        get
        {
            var animationFrameCount = _sprites.TankAnimationFrames.Length;
            var forward = _treadAnimationFrameCounter % animationFrameCount;
            return _stepDirection.X < 0 ? animationFrameCount - 1 - forward : forward;
        }
    }

    /// <summary>
    ///     Sets the time of the first <see cref="Robotron2084.Core.ArcadeFrameTimer">beat</see> of a tank that was on the field when the wave started, on the <see cref="Robotron2084.Core.ArcadeClock.PortTicksPerSecond">tick</see> the game
    ///     goes live. The <see cref="Robotron2084.Core.ArcadeFrameTimer">beat</see> timer is set so that it comes due when that time has gone by.
    /// </summary>
    /// <param name="field">The playfield, which works out how long the wait is.</param>
    public void BeginPlay(PlayField field)
    {
        _beatTimer = field.CreateFirstBeatTimer(LivePollArcadeFrames, 0, TankTuning.BeatIntervalArcadeFrames);
    }

    /// <summary>Draws the tank: a growing animation frame while it grows, and a tread one after that.</summary>
    /// <param name="spriteBatch">What the tank is drawn with.</param>
    public void Draw(SpriteBatch spriteBatch)
    {
        if (!this.IsAlive()) return;

        // A tank being born is drawn with the arcade's own small tank animation frames, each at its own size (ROM: MTNKP1..4). They are not the full-size tank made smaller.
        if (_growStep < TankTuning.GrowSteps)
        {
            var (growWidth, growHeight) = TankTuning.GrowSizes[_growStep];
            Rectangle growBounds = new(
                _position.X,
                _position.Y,
                ScreenSize.ToPortPixelsFromArcadePixels(growWidth),
                ScreenSize.ToPortPixelsFromArcadePixels(growHeight));
            _sprites.Blitter.DrawSprite(spriteBatch, _sprites.TankGrowAnimationFrames[_growStep], growBounds,
                Color.White);
            return;
        }

        _sprites.Blitter.DrawSprite(spriteBatch, GetCurrentAnimationFrame(), GetBounds(), Color.White);
    }

    /// <summary>
    ///     The box the tank takes up on the screen. While the tank is growing, the box is the size of the growing
    ///     animation frame that is showing.
    /// </summary>
    public Rectangle GetBounds()
    {
        if (_growStep < TankTuning.GrowSteps)
        {
            var (w, h) = TankTuning.GrowSizes[_growStep];
            return new Rectangle(_position.X, _position.Y, ScreenSize.ToPortPixelsFromArcadePixels(w),
                ScreenSize.ToPortPixelsFromArcadePixels(h));
        }

        return new Rectangle(_position.X, _position.Y, CollisionSize.Width, CollisionSize.Height);
    }

    /// <summary>
    ///     The animation frame the tank is showing: a growing one while it grows, and a tread one after that. An
    ///     explosion is made from it (see <see cref="IAnimationFrameSource" />).
    /// </summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>
    ///             Original source: <c>RRTK4.ASM</c> <c>TANK3</c>. The animation moves on once a <see cref="Robotron2084.Core.ArcadeFrameTimer">beat</see>. It plays
    ///             backwards while the tank is going left.
    ///         </item>
    ///         <item>Disassembly: <c>asm/robomame.asm</c> <c>ANIMATE_TANK</c> (<c>$4D99</c>).</item>
    ///     </list>
    /// </remarks>
    public Texture2D GetCurrentAnimationFrame()
    {
        if (_growStep < TankTuning.GrowSteps) return _sprites.TankGrowAnimationFrames[_growStep];

        return _sprites.TankAnimationFrames[TreadFrameIndex];
    }

    /// <inheritdoc />
    /// <remarks>It is the animation frame showing now (<see cref="GetCurrentAnimationFrame" />).</remarks>
    public Texture2D GetExplosionAnimationFrame()
    {
        return GetCurrentAnimationFrame();
    }

    /// <inheritdoc />
    /// <remarks>It is the same box as <see cref="GetBounds" />.</remarks>
    public Rectangle GetExplosionBounds()
    {
        return GetBounds();
    }

    /// <summary>Kills the tank at once, with no death animation.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRTK4.ASM</c> <c>TNKIL</c>.</item>
    ///         <item>Disassembly: <c>asm/robomame.asm</c> <c>ANIMATE_TANK</c> (<c>$4D99</c>).</item>
    ///     </list>
    /// </remarks>
    public void Kill()
    {
        if (!this.IsAlive()) return;

        LifeState = EntityLifeState.Dead;
    }

    /// <summary>
    ///     Runs one <see cref="Robotron2084.Core.ArcadeClock.PortTicksPerSecond">tick</see>. A growing tank only grows. After that, on a <see cref="Robotron2084.Core.ArcadeFrameTimer">beat</see>, the tank fires if it is time to, takes a
    ///     step, and may pick a new place to go.
    /// </summary>
    /// <param name="gameTime">Not used. The tank counts <see cref="Robotron2084.Core.ArcadeClock.PortTicksPerSecond">ticks</see>.</param>
    /// <param name="field">The playfield.</param>
    public void Update(GameTime gameTime, PlayField field)
    {
        if (!this.IsAlive()) return;

        if (field.RobotsFrozen()) return;

        // A growing tank cannot move or fire. It may finish growing on this tick, and then it can have a beat at once.
        if (IsGrowing() && _growTimer.Tick(TankTuning.GrowIntervalArcadeFrames)) TakeGrowStep();
        if (IsGrowing()) return;

        if (_beatTimer.Tick(TankTuning.BeatIntervalArcadeFrames)) ActOnBeat(field);
    }

    /// <summary>Moves a spot so that a tank standing on it is wholly inside the playfield.</summary>
    /// <param name="playfieldBounds">The inside of the playfield wall.</param>
    /// <param name="position">Where the tank would stand, which may be against or past a wall.</param>
    /// <returns>The nearest spot where the whole tank fits.</returns>
    internal static IntVector2 GetPositionInside(Rectangle playfieldBounds, IntVector2 position)
    {
        return new IntVector2(
            Math.Clamp(position.X, playfieldBounds.X, playfieldBounds.Right - CollisionSize.Width),
            Math.Clamp(position.Y, playfieldBounds.Y, playfieldBounds.Bottom - CollisionSize.Height));
    }

    /// <summary>True while the tank is still growing, when it must not move or fire.</summary>
    internal bool IsGrowing()
    {
        return _growStep < TankTuning.GrowSteps;
    }

    /// <summary>Picks at random how many <see cref="Robotron2084.Core.ArcadeFrameTimer">beats</see> the tank waits before it picks a new place to go.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRTK4.ASM</c> <c>TANKND</c>.</item>
    ///         <item>Disassembly: <c>asm/robomame.asm</c> <c>ANIMATE_TANK</c> (<c>$4D99</c>).</item>
    ///     </list>
    /// </remarks>
    private static int GetRandomNewDestinationBeats(Random random)
    {
        return random.Next(NewDestinationBeatsMin, NewDestinationBeatsMaxExclusive);
    }

    /// <summary>
    ///     What the tank does on its <see cref="Robotron2084.Core.ArcadeFrameTimer">beat</see>. It fires, if it is time to, then steps or bounces, and may pick a new place to
    ///     go (ROM: the <c>TANK</c> process).
    /// </summary>
    /// <param name="field">The playfield.</param>
    private void ActOnBeat(PlayField field)
    {
        FireIfDue(field);
        StepOrBounce(field);

        _treadAnimationFrameCounter++; // The treads show their next animation frame on each beat (ROM: TANK3).

        // When the new-destination countdown runs out, the tank picks a new place to go. Sometimes it is where the Player is, and otherwise it is a random place (ROM: TANKND).
        if (--_newDestinationBeatsRemaining <= 0)
        {
            _newDestinationBeatsRemaining = GetRandomNewDestinationBeats(_random);
            PickDestination(field);
        }
    }

    /// <summary>
    ///     Counts down one <see cref="Robotron2084.Core.ArcadeFrameTimer">beat</see> towards the next shot. When the count runs out, the tank fires a shell, if the playfield
    ///     allows another shell.
    /// </summary>
    /// <param name="field">The playfield.</param>
    private void FireIfDue(PlayField field)
    {
        if (--_fireBeatsRemaining > 0) return;

        if (field.CanFireShell()) field.SpawnTankShell(_position);

        // Every shot after the first waits this wave's gap between shots, with no random extra beats.
        _fireBeatsRemaining = _fireIntervalBeats;
    }

    /// <summary>Picks a random place inside the playfield, at least a tank's size away from every wall.</summary>
    /// <param name="field">The playfield, whose walls the place is inside.</param>
    private IntVector2 GetRandomPointIn(PlayField field)
    {
        var bounds = field.Wall.PlayfieldBounds;
        return new IntVector2(
            _random.Next(bounds.X + CollisionSize.Width, bounds.Right - CollisionSize.Width + 1),
            _random.Next(bounds.Y + CollisionSize.Height, bounds.Bottom - CollisionSize.Height + 1));
    }

    /// <summary>
    ///     Picks the place the tank goes next. About 38 times in 100 it is where the player is. Otherwise it is a random
    ///     place.
    /// </summary>
    /// <param name="field">The playfield, which says where the player and the walls are.</param>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRTK4.ASM</c> <c>ANIMATE_TANK</c>.</item>
    ///         <item>Disassembly: <c>asm/robomame.asm</c> <c>ANIMATE_TANK</c> (<c>$4D99</c>).</item>
    ///     </list>
    /// </remarks>
    private void PickDestination(PlayField field)
    {
        var isHeadingForPlayer = _random.Next(DestinationRandomRange) <= PlayerDestinationRandomAtMost;
        if (isHeadingForPlayer) _destination = field.Player.Position;
        else _destination = GetRandomPointIn(field);

        var dx = Math.Sign(_destination.X - _position.X);
        var dy = 0;
        var isFarEnoughAwayVertically = Math.Abs(_destination.Y - _position.Y) > VerticalMoveThreshold;
        if (isFarEnoughAwayVertically) dy = Math.Sign(_destination.Y - _position.Y);

        _stepDirection = new IntVector2(dx, dy);
    }

    /// <summary>
    ///     Takes one step. If the step would hit the wall, the tank does not take it and turns round instead. A wall to
    ///     its left or right turns it round sideways, and a wall above or below it turns it round up-and-down.
    /// </summary>
    /// <param name="field">The playfield.</param>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>
    ///             Original source: <c>RRTK4.ASM</c>, the <c>TANK1</c> step. On each <see cref="Robotron2084.Core.ArcadeFrameTimer">beat</see> the tank goes one arcade pixel
    ///             sideways, one arcade pixel up or down, or both.
    ///         </item>
    ///         <item>Disassembly: <c>asm/robomame.asm</c> <c>ANIMATE_TANK</c> (<c>$4D99</c>).</item>
    ///     </list>
    /// </remarks>
    private void StepOrBounce(PlayField field)
    {
        var stepPixels = ScreenSize.ToPortPixelsFromArcadePixels(TankTuning.StepArcadePixels);
        IntVector2 step = new(_stepDirection.X * stepPixels, _stepDirection.Y * stepPixels);
        var next = _position + step;
        if (!field.HitsWall(new Rectangle(next.X, next.Y, CollisionSize.Width, CollisionSize.Height)))
        {
            _position = next;
            return;
        }

        // The tank bounces off the wall. A wall to its left or right turns its left-or-right direction round. A wall above or below it turns its up-or-down direction round.
        if (field.HitsWall(new Rectangle(_position.X + step.X, _position.Y, CollisionSize.Width, CollisionSize.Height)))
        {
            _stepDirection = new IntVector2(-_stepDirection.X, _stepDirection.Y);
            step = new IntVector2(-step.X, step.Y);
        }

        if (field.HitsWall(new Rectangle(_position.X, _position.Y + step.Y, CollisionSize.Width, CollisionSize.Height)))
            _stepDirection = new IntVector2(_stepDirection.X, -_stepDirection.Y);
    }

    /// <summary>Shows the next growing animation frame.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>
    ///             Original source: <c>RRTK4.ASM</c> <c>MTANK</c> ("MINI TANK GROW"): four small-tank animation
    ///             frames, each shown for a short time.
    ///         </item>
    ///         <item>Disassembly: <c>asm/robomame.asm</c> <c>ANIMATE_TANK</c> (<c>$4D99</c>).</item>
    ///     </list>
    /// </remarks>
    private void TakeGrowStep()
    {
        // The growing tank moves up and to the left a little each time its animation frame changes, so that the full-size tank ends up centred on the spot where it was dropped (notes §53).
        var (columns, rows) = TankTuning.GrowDeltas[_growStep];
        _position += new IntVector2(ScreenSize.ToPortPixelsFromColumns(columns),
            ScreenSize.ToPortPixelsFromArcadePixels(rows));

        _growStep++;
    }
}
