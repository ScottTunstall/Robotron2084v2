using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Graphics;
using Robotron2084.Level;
using Robotron2084.Tuning;

namespace Robotron2084.Entities;

/// <summary>
///     A quark is a drifting robot that wanders the screen dropping tanks. Once it has dropped all its tanks, it
///     flees off the top or bottom edge.
/// </summary>
/// <seealso cref="Tank" />
/// <seealso cref="StripEffect" />
/// <remarks>
///     It acts on a beat. The <see cref="PlayField" /> calls <see cref="Update" /> on every tick, through
///     <see cref="FieldEntities" /> and <see cref="PlayField.UpdateEntity" />. The one time it does not is during the
///     short freeze just after the player is killed. <see cref="_beatTimer" /> gathers the ticks until it is time for
///     the next beat (see <see cref="ArcadeClock" />). It also moves steadily, timed by
///     <see cref="_moveTimer" />.
///     <list type="bullet">
///         <item>Original source: <c>RRTK4.ASM</c>, routine <c>SQUARE</c> (with <c>SQVEL</c>/<c>SQ3</c>)</item>
///         <item>Disassembly: <c>asm/robomame.asm</c> at <c>$4BFB</c> (<c>ANIMATE_QUARK</c>)</item>
///     </list>
/// </remarks>
public sealed class Quark : IEntity, IAnimationFrameSource, IRemovable
{
    /// <summary>
    ///     A random number below this decides each either-or choice: which way to drift, and which way to flee. Each
    ///     choice comes up half the time.
    /// </summary>
    private const int CoinFlipSides = 2;

    /// <summary>The most beats a quark waits between tank drops, when it is not told a number.</summary>
    private const int DefaultDropDelayBeats = 12;

    /// <summary>
    ///     The limit used to pick how many tanks a quark drops, when it is not told a number. The most it can drop is
    ///     half of this.
    /// </summary>
    private const int DefaultMaxDropsX2 = 10;

    /// <summary>The quark's top drifting speed, when it is not told a number.</summary>
    private const int DefaultSpeedCap = 50;

    /// <summary>
    ///     After a quark has dropped a tank, its drop delay is divided by this to give the most beats it waits before the
    ///     next drop. A random number up to that is stored in <see cref="_dropBeatsRemaining" />.
    /// </summary>
    private const int RepeatDropDelayDivisor = 2;

    /// <summary>
    ///     How big the quark is, in port pixels. It is the size of the quark's sprite, and it is used to tell what the
    ///     quark touches.
    /// </summary>
    private static readonly (int Width, int Height) CollisionSize = (
        ScreenSize.ToPortPixelsFromArcadePixels(CollisionSizes.QuarkCollisionSize.Width),
        ScreenSize.ToPortPixelsFromArcadePixels(CollisionSizes.QuarkCollisionSize.Height));

    /// <summary>
    ///     The time from one beat to the next, in clock units (see <see cref="ArcadeClock" />). <see cref="_beatTimer" />
    ///     counts up to this. When it gets there, a beat happens and this is taken off it.
    /// </summary>
    private static readonly int BeatIntervalClockUnits = ArcadeClock.ToClockUnits(QuarkTuning.BeatIntervalRomFrames);

    private readonly int _dropDelayBeats;
    private readonly Random _random;
    private readonly int _speedCap;
    private readonly SpriteSet _sprites;

    /// <summary>Which animation frame of the quark's spin is showing.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRTK4.ASM</c> <c>OPICT</c>, animation frames <c>SQP0</c> to <c>SQP8</c>.</item>
    ///         <item>Disassembly: <c>asm/robomame.asm</c> <c>ANIMATE_QUARK</c> (<c>$4BFB</c>).</item>
    ///     </list>
    /// </remarks>
    private int _animationFrameIndex;

    /// <summary>Counts up to the next beat.</summary>
    private int _beatTimer;

    /// <summary>Beats left before the next tank drop is due.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRTK4.ASM</c> <c>PD2</c>.</item>
    ///         <item>Disassembly: <c>asm/robomame.asm</c> <c>ANIMATE_QUARK</c> (<c>$4BFB</c>).</item>
    ///     </list>
    /// </remarks>
    private int _dropBeatsRemaining;

    /// <summary>True once the quark has started dropping tanks. It stays true until every tank has been dropped.</summary>
    private bool _isDroppingTanks;

    /// <summary>True once the quark is fleeing up or down to the edge of the playfield, where it vanishes.</summary>
    private bool _isFleeing;

    /// <summary>Counts up to the next move. The quark moves steadily.</summary>
    private int _moveTimer;

    private IntVector2 _position;

    /// <summary>Beats left before the quark picks a new direction to drift in.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRTK4.ASM</c> <c>PD7</c>.</item>
    ///         <item>Disassembly: <c>asm/robomame.asm</c> <c>ANIMATE_QUARK</c> (<c>$4BFB</c>).</item>
    ///     </list>
    /// </remarks>
    private int _reaimBeatsRemaining;

    /// <summary>
    ///     The fraction of a pixel left over from the last move, in 256ths of a pixel. It is added to the next move, so
    ///     even a slow drift gets somewhere.
    /// </summary>
    private IntVector2 _remainderSubpixels;

    /// <summary>How many tanks this quark still has left to drop.</summary>
    private int _tanksRemaining;

    /// <summary>How far the quark goes on each move, sideways and up or down, in 256ths of a pixel.</summary>
    private IntVector2 _velocitySubpixels;

    /// <summary>
    ///     Makes a quark at <paramref name="position" />. It picks at random how many tanks it will drop, and how long it
    ///     waits before the first.
    /// </summary>
    /// <param name="sprites">The shared sprite set.</param>
    /// <param name="position">Where the quark's top-left corner is.</param>
    /// <param name="random">
    ///     Where its random numbers come from. They pick how many tanks it drops, how it drifts and which way
    ///     it flees.
    /// </param>
    /// <param name="maxDropsX2">
    ///     This wave's limit for picking how many tanks the quark drops. The most it can drop is half of
    ///     this.
    /// </param>
    /// <param name="dropDelayBeats">The most beats this wave's quark waits between tank drops.</param>
    /// <param name="speedCap">The quark's top drifting speed on this wave (ROM: <c>SQSPD</c>).</param>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>
    ///             Original source: <c>RRTK4.ASM</c> <c>ENFNUM</c>, <c>TDPTIM</c> and <c>SQSPD</c>: this wave's limit on
    ///             tanks, its wait between drops and its top drifting speed.
    ///         </item>
    ///         <item>Disassembly: <c>asm/robomame.asm</c> <c>ANIMATE_QUARK</c> (<c>$4BFB</c>).</item>
    ///     </list>
    /// </remarks>
    public Quark(
        SpriteSet sprites,
        IntVector2 position,
        Random random,
        int maxDropsX2 = DefaultMaxDropsX2,
        int dropDelayBeats = DefaultDropDelayBeats,
        int speedCap = DefaultSpeedCap)
    {
        _sprites = sprites;
        _position = position;
        _random = random;
        _dropDelayBeats = dropDelayBeats;
        _speedCap = speedCap;
        // How many tanks the quark may drop: a random number up to this wave's limit, halved and rounded up (ROM: PD3).
        var roll = ArcadeRandom.PickUpTo(random, maxDropsX2);
        _tanksRemaining = (roll + 1) / 2;
        // The wait before the first tank drop. It counts down once each time the animation starts again, not once a beat (ROM: PD2).
        _dropBeatsRemaining = ArcadeRandom.PickUpTo(random, dropDelayBeats);
        // Both timers start full, so a new quark does not have to wait for its first beat or its first move.
        _beatTimer = BeatIntervalClockUnits;
        _moveTimer = ArcadeClock.UnitsPerRomFrame;
    }

    /// <summary>
    ///     The animation frame the quark is showing. The death burst is drawn from it (see
    ///     <see cref="IAnimationFrameSource" />).
    /// </summary>
    /// <returns>The animation frame that is showing.</returns>
    public Texture2D GetCurrentAnimationFrame()
    {
        return _sprites.QuarkAnimationFrames[_animationFrameIndex];
    }

    /// <summary>The box the quark takes up on the screen. It is used to tell what the quark touches.</summary>
    public Rectangle GetBounds()
    {
        return new Rectangle(_position.X, _position.Y, CollisionSize.Width, CollisionSize.Height);
    }

    /// <summary>Alive until it is shot or flees off the field. It is never Dying (see <see cref="Kill" />).</summary>
    public EntityLifeState LifeState { get; private set; } = EntityLifeState.Alive;

    /// <summary>Where the quark's top-left corner is.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRTK4.ASM</c> the OBJX/OBJY registers.</item>
    ///         <item>Disassembly: <c>asm/robomame.asm</c> <c>ANIMATE_QUARK</c> (<c>$4BFB</c>).</item>
    ///     </list>
    /// </remarks>
    public IntVector2 Position => _position;

    /// <summary>Draws the animation frame that is showing.</summary>
    /// <param name="spriteBatch">What the quark is drawn with.</param>
    public void Draw(SpriteBatch spriteBatch)
    {
        if (!this.IsAlive()) return;

        _sprites.Blitter.DrawSprite(spriteBatch, GetCurrentAnimationFrame(), GetBounds(), Color.White);
    }

    /// <summary>
    ///     Runs one tick. The quark moves when it is time to. On a beat it shows its next animation frame, may pick a new
    ///     way to drift, and may drop a tank.
    /// </summary>
    /// <param name="gameTime">Not used. The quark counts ticks.</param>
    /// <param name="field">The playfield.</param>
    public void Update(GameTime gameTime, PlayField field)
    {
        if (!this.IsAlive()) return;

        // The quark does not move while the robots are frozen: before the wave has started, and while the Player is dying (ROM: RRS22.ASM OPRC80; STATUS bit 3).
        if (!field.RobotsFrozen()) AdvanceMover(field);

        // Wait for the quark's next beat, which is its next turn to act (see ArcadeClock).
        _beatTimer += ArcadeClock.UnitsPerPortTick;
        if (_beatTimer < BeatIntervalClockUnits) return;

        _beatTimer -= BeatIntervalClockUnits;
        AdvanceAnimation();

        if (_isFleeing)
        {
            LeaveWhenClearOfTheField(field);
            return;
        }

        if (--_reaimBeatsRemaining <= 0) RollVelocity(field.GetPlayfieldBounds());

        // While the robots are frozen, the quark still animates and still picks new directions, but it does not count down to its next tank drop.
        if (field.RobotsFrozen()) return;

        AdvanceTankDrop(field);
    }

    /// <summary>Kills the quark at once. A quark that is shot leaves a death burst, not a strip explosion.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRTK4.ASM</c> <c>SQKIL</c>, which plays the quark's own shrinking burst.</item>
    ///         <item>Disassembly: <c>asm/robomame.asm</c> <c>ANIMATE_QUARK</c> (<c>$4BFB</c>).</item>
    ///     </list>
    ///     <see cref="RobotKinds" /> says that a quark killed by a laser leaves the burst that
    ///     <see cref="ScoreBurst.CreateForQuark" /> makes.
    /// </remarks>
    public void Kill()
    {
        if (!this.IsAlive()) return;

        LifeState = EntityLifeState.Dead;
    }

    /// <summary>Picks where a quark starts a wave: anywhere along the top wall or the bottom wall.</summary>
    /// <param name="playfieldBounds">The inside of the playfield wall.</param>
    /// <param name="random">
    ///     Where its random numbers come from. They pick how far along the wall, and whether the top or the
    ///     bottom.
    /// </param>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>
    ///             Original source: <c>RRTK4.ASM</c> <c>SQST1</c> and <c>SQST2</c> (<c>YMIN+2</c> or
    ///             <c>YMAX-14</c>, then a random place along the wall).
    ///         </item>
    ///         <item>Disassembly: <c>$4B48</c> to <c>$4B5A</c>.</item>
    ///     </list>
    /// </remarks>
    internal static IntVector2 GetStartPosition(Rectangle playfieldBounds, Random random)
    {
        var x = random.Next(playfieldBounds.X, playfieldBounds.Right - CollisionSize.Width + 1);
        var startsAtTop = random.Next(2) == 0;
        return new IntVector2(x, startsAtTop ? playfieldBounds.Y : playfieldBounds.Bottom - CollisionSize.Height);
    }

    /// <summary>
    ///     Counts one tick towards the quark's next move, and makes the move when it is due. The quark moves on its own
    ///     clock, which is more often than it has a beat.
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
    ///     Shows the next animation frame. Which animation frames are used depends on whether the quark is drifting,
    ///     dropping tanks or fleeing.
    /// </summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>
    ///             Original source: <c>RRTK4.ASM</c>: <c>SQP0</c> to <c>SQP4</c> while drifting, <c>SQP0</c> to <c>SQP8</c>
    ///             while dropping tanks, and <c>SQP8</c> back down to <c>SQP0</c> while fleeing.
    ///         </item>
    ///         <item>Disassembly: <c>asm/robomame.asm</c> <c>ANIMATE_QUARK</c> (<c>$4BFB</c>).</item>
    ///     </list>
    /// </remarks>
    private void AdvanceAnimation()
    {
        if (_isFleeing)
        {
            _animationFrameIndex = _animationFrameIndex <= 0
                ? QuarkTuning.TotalAnimationFrames - 1
                : _animationFrameIndex - 1;
            return;
        }

        var last = _isDroppingTanks ? QuarkTuning.TotalAnimationFrames - 1 : QuarkTuning.TravelAnimationFrames - 1;
        _animationFrameIndex = _animationFrameIndex >= last ? 0 : _animationFrameIndex + 1;
    }

    /// <summary>
    ///     Makes one move, by whole pixels. The fraction of a pixel left over is kept for the next move. The sideways
    ///     part is made only if it keeps the quark inside the playfield, and the same goes for the up-or-down part.
    /// </summary>
    /// <param name="field">The playfield, whose walls the quark stays inside.</param>
    private void AdvancePosition(PlayField field)
    {
        var bounds = field.GetPlayfieldBounds();

        _remainderSubpixels += _velocitySubpixels;
        var stepX = _remainderSubpixels.X / ScreenSize.SubpixelsPerPixel;
        var stepY = _remainderSubpixels.Y / ScreenSize.SubpixelsPerPixel;
        _remainderSubpixels -= new IntVector2(
            stepX * ScreenSize.SubpixelsPerPixel,
            stepY * ScreenSize.SubpixelsPerPixel);

        if (stepX != 0 && IsInsideX(bounds, _position.X + stepX))
            _position = _position with { X = _position.X + stepX };

        if (stepY != 0 && IsInsideY(bounds, _position.Y + stepY))
            _position = _position with { Y = _position.Y + stepY };
    }

    /// <summary>Counts down to the next tank drop. When the count runs out, it drops a tank if the playfield has room for one.</summary>
    /// <param name="field">The playfield. It says whether there is room for another tank, and the new tank is put on it.</param>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRTK4.ASM</c> <c>SQ2</c>/<c>TNKDRP</c>.</item>
    ///         <item>Disassembly: <c>asm/robomame.asm</c> <c>ANIMATE_QUARK</c> (<c>$4BFB</c>).</item>
    ///     </list>
    ///     Before the first drop, the count goes down once each time the animation starts again. After that it goes
    ///     down once a beat. When the quark has dropped all its tanks, it flees.
    /// </remarks>
    private void AdvanceTankDrop(PlayField field)
    {
        if (!_isDroppingTanks && _animationFrameIndex != 0) return;

        if (--_dropBeatsRemaining > 0) return;

        if (_tanksRemaining > 0 && field.CanDropTank())
        {
            _isDroppingTanks = true;
            _tanksRemaining--;
            // The new tank appears a little to the right of the quark and below it. It is one row lower when the quark is against the top wall (ROM: TNKDRP).
            var rowOffset = _position.Y == field.GetPlayfieldBounds().Y
                ? TankTuning.BirthOffsetRowsOnTopWall
                : TankTuning.BirthOffsetRowsOffTopWall;
            field.SpawnTank(_position + new IntVector2(
                ScreenSize.ToPortPixelsFromColumns(TankTuning.BirthOffsetColumns),
                ScreenSize.ToPortPixelsFromArcadePixels(rowOffset)));
            if (_tanksRemaining == 0)
            {
                StartFlee();
                return;
            }
        }

        // Pick the wait before the next drop. This is done even when no tank could be dropped this time.
        _dropBeatsRemaining = ArcadeRandom.PickUpTo(_random, _dropDelayBeats / RepeatDropDelayDivisor + 1);
    }

    /// <summary>Picks at random how fast the quark drifts, either sideways or up-and-down.</summary>
    /// <param name="scale">The arcade's number that the random speed is multiplied by.</param>
    /// <param name="isPositive">True to drift right or down, false to drift left or up.</param>
    /// <param name="coordinateUnitArcadePixels">
    ///     How many arcade pixels one of the arcade's units is: 2 for a column, 1 for a
    ///     row.
    /// </param>
    /// <returns>How far the quark goes on each move, in 256ths of a pixel. It is negative for left or up.</returns>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRTK4.ASM</c> <c>SQVEL</c>.</item>
    ///         <item>Disassembly: <c>asm/robomame.asm</c> <c>ANIMATE_QUARK</c> (<c>$4BFB</c>).</item>
    ///     </list>
    ///     The arcade counts across the screen in columns, which are 2 pixels wide, and down the screen in rows,
    ///     which are 1 pixel tall. So its sideways number of 4 and its up-and-down number of 8 give the same speed
    ///     on the screen.
    /// </remarks>
    private int ComputeAxisVelocitySubpixels(int scale, bool isPositive, int coordinateUnitArcadePixels)
    {
        var roll = ArcadeRandom.PickUpTo(_random, _speedCap);
        var subpixels = roll * scale * ScreenSize.ToPortPixelsFromArcadePixels(coordinateUnitArcadePixels);
        return isPositive ? subpixels : -subpixels;
    }

    /// <summary>Says whether the whole quark would be inside the playfield if its left edge were at <paramref name="x" />.</summary>
    /// <param name="bounds">The inside of the playfield wall.</param>
    /// <param name="x">Where the quark's left edge would be.</param>
    private bool IsInsideX(Rectangle bounds, int x)
    {
        return x >= bounds.X && x + CollisionSize.Width <= bounds.Right;
    }

    /// <summary>Says whether the whole quark would be inside the playfield if its top edge were at <paramref name="y" />.</summary>
    /// <param name="bounds">The inside of the playfield wall.</param>
    /// <param name="y">Where the quark's top edge would be.</param>
    private bool IsInsideY(Rectangle bounds, int y)
    {
        return y >= bounds.Y && y + CollisionSize.Height <= bounds.Bottom;
    }

    /// <summary>Takes the fleeing quark off the field once it has reached the top or bottom edge.</summary>
    /// <param name="field">The playfield, whose top and bottom edges the quark flees to.</param>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRTK4.ASM</c> <c>SQ3L</c>.</item>
    ///         <item>Disassembly: <c>asm/robomame.asm</c> <c>ANIMATE_QUARK</c> (<c>$4BFB</c>).</item>
    ///     </list>
    /// </remarks>
    private void LeaveWhenClearOfTheField(PlayField field)
    {
        var topExitY = field.GetPlayfieldBounds().Y +
                       ScreenSize.ToPortPixelsFromArcadePixels(QuarkTuning.FleeExitLowRows);
        var bottomExitY = field.GetPlayfieldBounds().Bottom -
                          ScreenSize.ToPortPixelsFromArcadePixels(QuarkTuning.FleeExitHighRows);
        if (_position.Y <= topExitY || _position.Y >= bottomExitY) LifeState = EntityLifeState.Dead;
    }

    /// <summary>
    ///     Picks at random a new way and speed to drift, and how many beats to keep it for. A quark that is close to a
    ///     wall always drifts away from that wall.
    /// </summary>
    /// <param name="bounds">The inside of the playfield wall.</param>
    private void RollVelocity(Rectangle bounds)
    {
        var lowX = bounds.X + ScreenSize.ToPortPixelsFromColumns(QuarkTuning.WallMarginLeftColumns);
        var highX = bounds.Right - ScreenSize.ToPortPixelsFromColumns(QuarkTuning.WallMarginRightColumns);
        var lowY = bounds.Y + ScreenSize.ToPortPixelsFromArcadePixels(QuarkTuning.WallMarginTopRows);
        var highY = bounds.Bottom - ScreenSize.ToPortPixelsFromArcadePixels(QuarkTuning.WallMarginBottomRows);

        var xPositive = _position.X <= lowX || (_position.X < highX && _random.Next(CoinFlipSides) == 0);
        var yPositive = _position.Y <= lowY || (_position.Y < highY && _random.Next(CoinFlipSides) != 0);

        _velocitySubpixels = new IntVector2(
            ComputeAxisVelocitySubpixels(QuarkTuning.VelocityXScale, xPositive, ScreenSize.ArcadePixelsPerByte),
            ComputeAxisVelocitySubpixels(QuarkTuning.VelocityYScale, yPositive, 1));

        _reaimBeatsRemaining = 1 + _random.Next(QuarkTuning.ReaimMaxBeats);
    }

    /// <summary>
    ///     Starts the quark fleeing. It stops going sideways and goes straight up or straight down at a set speed. Which
    ///     of the two is picked at random.
    /// </summary>
    /// <remarks>ROM: RRTK4.ASM's <c>SQ3</c>.</remarks>
    private void StartFlee()
    {
        _isFleeing = true;
        var subpixels = QuarkTuning.FleeVelocityRom * ScreenSize.ToPortPixelsFromArcadePixels(1);
        _velocitySubpixels = new IntVector2(0, _random.Next(CoinFlipSides) == 0 ? subpixels : -subpixels);
        _remainderSubpixels = IntVector2.Zero;
    }
}
