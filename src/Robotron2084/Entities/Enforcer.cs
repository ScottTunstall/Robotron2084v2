using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Graphics;
using Robotron2084.Level;
using Robotron2084.Tuning;

namespace Robotron2084.Entities;

/// <summary>
///     An enforcer is a small, fast robot dropped by spheroids. It grows in place for a moment, then flies around
///     firing sparks at you.
/// </summary>
/// <seealso cref="Spheroid" />
/// <seealso cref="Spark" />
/// <remarks>
///     It acts on a <see cref="Robotron2084.Core.ArcadeFrameTimer">beat</see>. <see cref="_beatTimer" /> says when it is time for the next <see cref="Robotron2084.Core.ArcadeFrameTimer">beat</see>.
///     It also moves steadily, timed by <see cref="_moveTimer" />, and <see cref="_growTimer" /> times its growing.
///     <list type="bullet">
///         <item>
///             Original source: <c>RRC11.ASM</c>, routine <c>ENFR1</c> (with <c>ENFNV</c>, <c>ENFDRP</c>
///             sub-blocks)
///         </item>
///         <item>
///             Disassembly: <c>asm/robomame.asm</c> at <c>ENFORCER_AI</c> (<c>$139D</c>). The enforcer is made in
///             <c>DROP_ENFORCER</c>/<c>CREATE_ENFORCER</c>
///         </item>
///     </list>
/// </remarks>
public sealed class Enforcer : IEntity, IExplodable, IRemovable
{
    /// <summary>
    ///     The enforcer heads for a random spot in an area below and to the right of the player. This is how many columns
    ///     wide that area is.
    /// </summary>
    /// <remarks>ROM: <c>ENFR1</c>.</remarks>
    private const int DestinationZoneColumns = 32;

    /// <summary>How many rows tall the area the enforcer heads for is (see <see cref="DestinationZoneColumns" />).</summary>
    /// <remarks>ROM: <c>ENFR1</c>.</remarks>
    private const int DestinationZoneRows = 32;

    /// <summary>
    ///     How much slower the enforcer goes than the gap to the spot it is heading for. The bigger this is, the slower it
    ///     goes.
    /// </summary>
    private const int ApproachDivisor = 2;

    /// <summary>The most <see cref="Robotron2084.Core.ArcadeFrameTimer">beats</see> an enforcer waits between shots, when it is not told a number.</summary>
    private const int DefaultFireIntervalBeats = 24;

    /// <summary>
    ///     One more than the most <see cref="Robotron2084.Core.ArcadeFrameTimer">beats</see> the enforcer waits before it
    ///     picks a new spot to head for. The fewest is 0.
    /// </summary>
    /// <remarks>ROM: <c>ENFR1</c>.</remarks>
    private const int NewDestinationBeatsMaxExclusive = 32;

    /// <summary>
    ///     How big the enforcer is, in port pixels. It is the size of the enforcer's sprite.
    /// </summary>
    private static readonly (int Width, int Height) CollisionSize =
        (ScreenSize.ToPortPixelsFromArcadePixels(CollisionSizes.EnforcerCollisionSize.Width),
            ScreenSize.ToPortPixelsFromArcadePixels(CollisionSizes.EnforcerCollisionSize.Height));

    private readonly int _fireIntervalBeats;
    private readonly Random _random;
    private readonly SpriteSet _sprites;
    private readonly ArcadeFrameTimer _beatTimer = new();
    private int _fireBeatsRemaining;

    /// <summary>Which of the growing animation frames is showing, counting from 0.</summary>
    private int _growStep;

    private readonly ArcadeFrameTimer _growTimer = new();

    /// <summary>Times the enforcer's moves.</summary>
    private readonly ArcadeFrameTimer _moveTimer = ArcadeFrameTimer.CreateDue(ArcadeClock.MoveIntervalArcadeFrames);

    private IntVector2 _position;

    private int _newDestinationBeatsRemaining;

    private IntVector2 _remainderSubpixels;

    /// <summary>
    ///     How far the enforcer goes on each move, sideways and up or down, in 256ths of a pixel. It moves steadily,
    ///     separately from its <see cref="Robotron2084.Core.ArcadeFrameTimer">beat</see>.
    /// </summary>
    private IntVector2 _velocitySubpixels;

    /// <summary>Makes an enforcer. It cannot move until it has grown.</summary>
    /// <param name="sprites">The shared sprite set.</param>
    /// <param name="position">Where the enforcer's top-left corner is.</param>
    /// <param name="random">
    ///     Where its random numbers come from. They pick where it heads for and how long it waits between
    ///     shots.
    /// </param>
    /// <param name="fireIntervalBeats">
    ///     The most <see cref="Robotron2084.Core.ArcadeFrameTimer">beats</see> this wave's enforcer waits between shots. Each wait is a random number
    ///     of <see cref="Robotron2084.Core.ArcadeFrameTimer">beats</see> from 1 up to this.
    /// </param>
    /// <remarks>ROM: <c>ENSTIM</c>.</remarks>
    public Enforcer(
        SpriteSet sprites,
        IntVector2 position,
        Random random,
        int fireIntervalBeats = DefaultFireIntervalBeats)
    {
        _sprites = sprites;
        _position = position;
        _random = random;
        _fireIntervalBeats = fireIntervalBeats;
        // The enforcer picks where to go on its first beat. Its first shot comes after a random number of beats.
        _newDestinationBeatsRemaining = 0;
        _fireBeatsRemaining = ArcadeRandom.PickUpTo(random, _fireIntervalBeats);
    }

    /// <summary>The box the enforcer takes up on the screen.</summary>
    public Rectangle GetBounds()
    {
        return new Rectangle(_position.X, _position.Y, CollisionSize.Width, CollisionSize.Height);
    }

    /// <inheritdoc />
    /// <remarks>It is the same box as <see cref="GetBounds" />.</remarks>
    public Rectangle GetExplosionBounds()
    {
        return GetBounds();
    }

    /// <inheritdoc />
    /// <remarks>It is the animation frame showing now (<see cref="GetCurrentAnimationFrame" />).</remarks>
    public Texture2D GetExplosionAnimationFrame()
    {
        return GetCurrentAnimationFrame();
    }

    /// <summary>Alive until it is killed. It is never Dying, because it has no death animation.</summary>
    public EntityLifeState LifeState { get; private set; } = EntityLifeState.Alive;

    /// <summary>Where the enforcer's top-left corner is.</summary>
    /// <remarks>The ROM's OBJX/OBJY.</remarks>
    public IntVector2 Position => _position;

    /// <summary>Draws the enforcer: a growing animation frame while it grows, and the full-size one after that.</summary>
    /// <param name="spriteBatch">What the enforcer is drawn with.</param>
    public void Draw(SpriteBatch spriteBatch)
    {
        if (!this.IsAlive()) return;

        _sprites.Blitter.DrawSprite(spriteBatch, GetCurrentAnimationFrame(), GetBounds(), Color.White);
    }

    /// <summary>
    ///     Runs one <see cref="Robotron2084.Core.ArcadeClock.PortTicksPerSecond">tick</see>. The enforcer grows, then it moves, and on a <see cref="Robotron2084.Core.ArcadeFrameTimer">beat</see> it picks where to head for and fires a spark
    ///     if it is time to.
    /// </summary>
    /// <param name="gameTime">Not used. The enforcer counts <see cref="Robotron2084.Core.ArcadeClock.PortTicksPerSecond">ticks</see>.</param>
    /// <param name="field">The playfield.</param>
    public void Update(GameTime gameTime, PlayField field)
    {
        if (!this.IsAlive()) return;

        if (field.RobotsFrozen()) return;

        // A growing enforcer cannot move or shoot. It may finish growing on this tick, and then it moves at once.
        if (IsGrowing() && _growTimer.Tick(EnforcerTuning.GrowIntervalArcadeFrames)) TakeGrowStep();
        if (IsGrowing()) return;

        if (_moveTimer.Tick(ArcadeClock.MoveIntervalArcadeFrames)) MoveOnce(field);
        if (_beatTimer.Tick(EnforcerTuning.BeatIntervalArcadeFrames)) ActOnBeat(field);
    }

    /// <summary>
    ///     The animation frame the enforcer is showing: one of the growing ones while it grows, and the full-size one
    ///     after that.
    /// </summary>
    /// <remarks>
    ///     The growing animation frames are the ROM's <c>ENGD1</c> to <c>ENGD5</c>. They are the second to the sixth in
    ///     the set.
    /// </remarks>
    public Texture2D GetCurrentAnimationFrame()
    {
        if (!this.IsAlive() || !IsGrowing()) return _sprites.EnforcerSprite;

        var frame = Math.Clamp(GetGrowAnimationFrameIndex(), 0, _sprites.EnforcerAnimationFrames.Length - 2);
        return _sprites.EnforcerAnimationFrames[1 + frame];
    }

    /// <summary>Kills the enforcer at once. It has no death animation (ROM: <c>ENFKIL</c>).</summary>
    public void Kill()
    {
        if (!this.IsAlive()) return;

        LifeState = EntityLifeState.Dead;
    }

    /// <summary>Which of the growing animation frames is showing, counting from 0, or -1 once the enforcer has grown.</summary>
    internal int GetGrowAnimationFrameIndex()
    {
        return IsGrowing() ? _growStep : -1;
    }

    /// <summary>Picks at random how many <see cref="Robotron2084.Core.ArcadeFrameTimer">beats</see> the enforcer waits before it picks a new spot to head for.</summary>
    private static int GetRandomNewDestinationBeats(Random random)
    {
        return random.Next(0, NewDestinationBeatsMaxExclusive);
    }

    /// <summary>True while the enforcer is still growing, when it must not move or shoot.</summary>
    private bool IsGrowing()
    {
        return _growStep < EnforcerTuning.GrowSteps;
    }

    /// <summary>Shows the next growing animation frame.</summary>
    /// <remarks>ROM: <c>ENFR0</c> and <c>ENFRCE</c>.</remarks>
    private void TakeGrowStep()
    {
        _growStep++;
    }

    /// <summary>
    ///     Makes one move. The sideways part of the move is made only if it keeps the enforcer inside the playfield, and
    ///     the same goes for the up-and-down part.
    /// </summary>
    /// <param name="field">The playfield, whose walls the enforcer stays inside.</param>
    /// <remarks>ROM: the routine that moves every moving object (RRS22.ASM's <c>OPB80</c>).</remarks>
    private void MoveOnce(PlayField field)
    {
        var bounds = field.Wall.PlayfieldBounds;
        _remainderSubpixels += _velocitySubpixels;
        var stepX = _remainderSubpixels.X / ScreenSize.SubpixelsPerPixel;
        var stepY = _remainderSubpixels.Y / ScreenSize.SubpixelsPerPixel;
        _remainderSubpixels -=
            new IntVector2(stepX * ScreenSize.SubpixelsPerPixel, stepY * ScreenSize.SubpixelsPerPixel);

        var x = _position.X + stepX;
        if (stepX != 0 && x >= bounds.X && x + CollisionSize.Width <= bounds.Right)
            _position = _position with { X = x };

        var y = _position.Y + stepY;
        if (stepY != 0 && y >= bounds.Y && y + CollisionSize.Height <= bounds.Bottom)
            _position = _position with { Y = y };
    }

    /// <summary>
    ///     What the enforcer does on its <see cref="Robotron2084.Core.ArcadeFrameTimer">beat</see>. It counts down to picking a new spot to head for and to its next shot (ROM: <c>ENFR1</c>).
    /// </summary>
    /// <param name="field">The playfield.</param>
    private void ActOnBeat(PlayField field)
    {
        if (--_newDestinationBeatsRemaining <= 0)
        {
            _newDestinationBeatsRemaining = GetRandomNewDestinationBeats(_random);
            SetRandomVelocity(field);
        }

        if (--_fireBeatsRemaining <= 0)
        {
            _fireBeatsRemaining = GetRandomFireBeats(_random);
            if (field.GetActiveSparkCount() < SparkTuning.GlobalActiveSparkCap)
                // The spark is aimed at the place where the Player is now.
                field.SpawnSpark(_position, field.Player.Position);
        }
    }

    /// <summary>
    ///     Picks at random how many <see cref="Robotron2084.Core.ArcadeFrameTimer">beats</see> the enforcer waits before its next shot. The pick is the arcade's own (see
    ///     <see cref="ArcadeRandom" />), which gives longer waits more often than an even spread would.
    /// </summary>
    private int GetRandomFireBeats(Random random)
    {
        return ArcadeRandom.PickUpTo(random, _fireIntervalBeats);
    }

    /// <summary>
    ///     Picks the next spot to head for, which is a random distance below and to the right of the player, and sets the
    ///     enforcer's speed to get there.
    /// </summary>
    /// <param name="field">The playfield. It says where the player is, and its walls limit where the spot can be.</param>
    private void SetRandomVelocity(PlayField field)
    {
        var bounds = field.Wall.PlayfieldBounds;
        var targetX = field.GetPlayerPosition().X + ScreenSize.ToPortPixelsFromColumns(_random.Next(0, DestinationZoneColumns));
        var targetY = field.GetPlayerPosition().Y +
                      ScreenSize.ToPortPixelsFromArcadePixels(_random.Next(0, DestinationZoneRows));
        targetX = Math.Clamp(targetX, bounds.X, bounds.Right - CollisionSize.Width);
        targetY = Math.Clamp(targetY, bounds.Y, bounds.Bottom - CollisionSize.Height);

        IntVector2 delta = new(targetX - _position.X, targetY - _position.Y);

        // The further the enforcer is from the spot it is heading for, the faster it goes.
        _velocitySubpixels = new IntVector2(delta.X / ApproachDivisor, delta.Y / ApproachDivisor);
    }
}
