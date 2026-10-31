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
///     It acts on a <see cref="Robotron2084.Core.ArcadeFrameTimer">beat</see>. <see cref="_beatArcadeFrameTimer" /> says when it is time for the next <see cref="Robotron2084.Core.ArcadeFrameTimer">beat</see>.
///     It also moves steadily, timed by <see cref="_moveArcadeFrameTimer" />.
///     <list type="bullet">
///         <item>Original source: <c>RRTK4.ASM</c>, routine <c>SQUARE</c> (with <c>SQVEL</c>/<c>SQ3</c>)</item>
///         <item>Disassembly: <c>asm/robomame.asm</c> at <c>$4BFB</c> (<c>ANIMATE_QUARK</c>)</item>
///     </list>
/// </remarks>
public sealed class Quark : IEntity, IAnimationFrameSource, IRemovable
{
    /// <summary>
    ///     The range of the random number behind each either-or choice a quark makes: which way to drift, and which way to
    ///     flee. Each choice comes up half the time.
    /// </summary>
    private const int CoinFlipSides = 2;

    /// <summary>The most <see cref="Robotron2084.Core.ArcadeFrameTimer">beats</see> a quark waits between tank drops, when it is not told a number.</summary>
    private const int DefaultDropIntervalMaxBeats = 12;

    /// <summary>
    ///     The limit on how many tanks a quark drops, when it is not told a number. The most it can drop is half of this.
    /// </summary>
    private const int DefaultMaxDropsX2 = 10;

    /// <summary>The quark's top drifting speed, when it is not told a number.</summary>
    private const int DefaultMaxSpeed = 50;

    /// <summary>How many times shorter a quark's wait between tank drops is once it has dropped its first tank.</summary>
    private const int RepeatDropIntervalDivisor = 2;

    /// <summary>
    ///     How big the quark is, in port pixels. It is the size of the quark's sprite.
    /// </summary>
    private static readonly (int Width, int Height) CollisionSize = (
        ScreenSize.ToPortPixelsFromArcadePixels(CollisionSizes.QuarkCollisionSize.Width),
        ScreenSize.ToPortPixelsFromArcadePixels(CollisionSizes.QuarkCollisionSize.Height));

    /// <summary>Times the quark's <see cref="Robotron2084.Core.ArcadeFrameTimer">beats</see>.</summary>
    private readonly ArcadeFrameTimer _beatArcadeFrameTimer = ArcadeFrameTimer.CreateDue(QuarkTuning.BeatIntervalArcadeFrames);

    private readonly int _dropIntervalMaxBeats;

    /// <summary>Times the quark's moves.</summary>
    private readonly ArcadeFrameTimer _moveArcadeFrameTimer = ArcadeFrameTimer.CreateDue(ArcadeClock.MoveIntervalArcadeFrames);

    private readonly Random _random;
    private readonly int _maxSpeed;
    private readonly TextureSet _textureSet;

    /// <summary>Which animation frame of the quark's spin is showing.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRTK4.ASM</c> <c>OPICT</c>, animation frames <c>SQP0</c> to <c>SQP8</c>.</item>
    ///         <item>Disassembly: <c>asm/robomame.asm</c> <c>ANIMATE_QUARK</c> (<c>$4BFB</c>).</item>
    ///     </list>
    /// </remarks>
    private int _animationFrameIndex;

    /// <summary><see cref="Robotron2084.Core.ArcadeFrameTimer">Beats</see> left before the next tank drop is due.</summary>
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

    /// <summary><see cref="Robotron2084.Core.ArcadeFrameTimer">Beats</see> left before the quark picks a new direction to drift in.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRTK4.ASM</c> <c>PD7</c>.</item>
    ///         <item>Disassembly: <c>asm/robomame.asm</c> <c>ANIMATE_QUARK</c> (<c>$4BFB</c>).</item>
    ///     </list>
    /// </remarks>
    private int _newDirectionBeatsRemaining;

    private IntVector2 _position;

    /// <summary>The fraction of a pixel left over from the last move, in 256ths of a pixel.</summary>
    private IntVector2 _remainderSubpixels;

    /// <summary>How many tanks this quark still has left to drop.</summary>
    private int _tanksRemaining;

    /// <summary>How far the quark goes on each move, sideways and up or down, in 256ths of a pixel.</summary>
    private IntVector2 _velocitySubpixels;

    /// <summary>
    ///     Makes a quark at <paramref name="position" />. It picks at random how many tanks it will drop, and how long it
    ///     waits before the first.
    /// </summary>
    /// <param name="textureSet">The shared texture set.</param>
    /// <param name="position">Where the quark's top-left corner is.</param>
    /// <param name="random">
    ///     Where its random numbers come from. They pick how many tanks it drops, how it drifts and which way
    ///     it flees.
    /// </param>
    /// <param name="maxDropsX2">
    ///     This wave's limit for picking how many tanks the quark drops. The most it can drop is half of
    ///     this.
    /// </param>
    /// <param name="dropIntervalMaxBeats">The most <see cref="Robotron2084.Core.ArcadeFrameTimer">beats</see> this wave's quark waits between tank drops.</param>
    /// <param name="maxSpeed">The quark's top drifting speed on this wave (ROM: <c>SQSPD</c>).</param>
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
        TextureSet textureSet,
        IntVector2 position,
        Random random,
        int maxDropsX2 = DefaultMaxDropsX2,
        int dropIntervalMaxBeats = DefaultDropIntervalMaxBeats,
        int maxSpeed = DefaultMaxSpeed)
    {
        _textureSet = textureSet;
        _position = position;
        _random = random;
        _dropIntervalMaxBeats = dropIntervalMaxBeats;
        _maxSpeed = maxSpeed;
        // How many tanks the quark may drop: a random number up to this wave's limit, halved and rounded up (ROM: PD3).
        var randomDrops = ArcadeRandom.PickUpTo(random, maxDropsX2);
        _tanksRemaining = (randomDrops + 1) / 2;
        // The wait before the first tank drop. It counts down once each time the animation starts again, not once a beat (ROM: PD2).
        _dropBeatsRemaining = ArcadeRandom.PickUpTo(random, dropIntervalMaxBeats);
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

        _textureSet.BlitterDraw.DrawSprite(spriteBatch, GetCurrentAnimationFrameTexture(), GetBounds(), Color.White);
    }

    /// <summary>The box the quark takes up on the screen.</summary>
    public Rectangle GetBounds()
    {
        return new Rectangle(_position.X, _position.Y, CollisionSize.Width, CollisionSize.Height);
    }

    /// <summary>
    ///     The animation frame the quark is showing. The death burst is drawn from it (see
    ///     <see cref="IAnimationFrameSource" />).
    /// </summary>
    /// <returns>The animation frame that is showing.</returns>
    public Texture2D GetCurrentAnimationFrameTexture()
    {
        return _textureSet.QuarkAnimationFrameTextures[_animationFrameIndex];
    }

    /// <summary>Kills the quark at once.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRTK4.ASM</c> <c>SQKIL</c>, which plays the quark's own shrinking burst.</item>
    ///         <item>Disassembly: <c>asm/robomame.asm</c> <c>ANIMATE_QUARK</c> (<c>$4BFB</c>).</item>
    ///     </list>
    /// </remarks>
    public void Kill()
    {
        if (!this.IsAlive()) return;

        LifeState = EntityLifeState.Dead;
    }

    /// <summary>
    ///     Runs one <see cref="Robotron2084.Core.ArcadeClock.PortTicksPerSecond">tick</see>. The quark moves when it is
    ///     time to, and on a <see cref="Robotron2084.Core.ArcadeFrameTimer">beat</see> it shows its next animation frame,
    ///     may pick a new way to drift, and may drop a tank.
    /// </summary>
    /// <param name="gameTime">Not used. The quark counts <see cref="Robotron2084.Core.ArcadeClock.PortTicksPerSecond">ticks</see>.</param>
    /// <param name="playField">The playfield.</param>
    public void Update(GameTime gameTime, PlayField playField)
    {
        if (!this.IsAlive()) return;

        // The quark does not move while the robots are frozen: before the wave has started, and while the Player is dying (ROM: RRS22.ASM OPRC80; STATUS bit 3).
        if (!playField.RobotsFrozen() && _moveArcadeFrameTimer.Tick(ArcadeClock.MoveIntervalArcadeFrames)) MoveOnce(playField);

        if (_beatArcadeFrameTimer.Tick(QuarkTuning.BeatIntervalArcadeFrames)) ActOnBeat(playField);
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
        var y = playfieldBounds.Bottom - CollisionSize.Height;
        if (startsAtTop) y = playfieldBounds.Y;

        return new IntVector2(x, y);
    }

    /// <summary>
    ///     What the quark does on its <see cref="Robotron2084.Core.ArcadeFrameTimer">beat</see>. It shows its next animation frame, may pick a new way to drift, and may drop
    ///     a tank.
    /// </summary>
    /// <param name="playField">The playfield.</param>
    private void ActOnBeat(PlayField playField)
    {
        AdvanceAnimation();

        if (_isFleeing)
        {
            LeaveWhenClearOfTheField(playField);
            return;
        }

        if (--_newDirectionBeatsRemaining <= 0) SetRandomVelocity(playField.GetPlayfieldBounds());

        // While the robots are frozen, the quark still animates and still picks new directions, but it does not count down to its next tank drop.
        if (playField.RobotsFrozen()) return;

        AdvanceTankDrop(playField);
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
            var isOnFirstFrame = _animationFrameIndex <= 0;
            if (isOnFirstFrame) _animationFrameIndex = QuarkTuning.TotalAnimationFrames - 1;
            else _animationFrameIndex--;

            return;
        }

        var last = QuarkTuning.TravelAnimationFrames - 1;
        if (_isDroppingTanks) last = QuarkTuning.TotalAnimationFrames - 1;

        var isOnLastFrame = _animationFrameIndex >= last;
        var nextFrameIndex = _animationFrameIndex + 1;
        if (isOnLastFrame) nextFrameIndex = 0;

        _animationFrameIndex = nextFrameIndex;
    }

    /// <summary>Counts down to the next tank drop. When the count runs out, it drops a tank if the playfield has room for one.</summary>
    /// <param name="playField">The playfield. It says whether there is room for another tank, and the new tank is put on it.</param>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRTK4.ASM</c> <c>SQ2</c>/<c>TNKDRP</c>.</item>
    ///         <item>Disassembly: <c>asm/robomame.asm</c> <c>ANIMATE_QUARK</c> (<c>$4BFB</c>).</item>
    ///     </list>
    ///     Before the first drop, the count goes down once each time the animation starts again. After that it goes
    ///     down once a <see cref="Robotron2084.Core.ArcadeFrameTimer">beat</see>. When the quark has dropped all its tanks, it flees.
    /// </remarks>
    private void AdvanceTankDrop(PlayField playField)
    {
        if (!_isDroppingTanks && _animationFrameIndex != 0) return;

        if (--_dropBeatsRemaining > 0) return;

        if (_tanksRemaining > 0 && playField.CanDropTank())
        {
            _isDroppingTanks = true;
            _tanksRemaining--;
            // The new tank appears a little to the right of the quark and below it. It is one row lower when the quark is against the top wall (ROM: TNKDRP).
            var rowOffset = TankTuning.BirthOffsetRowsOffTopWall;
            var isAgainstTopWall = _position.Y == playField.GetPlayfieldBounds().Y;
            if (isAgainstTopWall) rowOffset = TankTuning.BirthOffsetRowsOnTopWall;

            playField.SpawnTank(_position + new IntVector2(
                ScreenSize.ToPortPixelsFromColumns(TankTuning.BirthOffsetColumns),
                ScreenSize.ToPortPixelsFromArcadePixels(rowOffset)));
            if (_tanksRemaining == 0)
            {
                StartFlee();
                return;
            }
        }

        // Pick the wait before the next drop. This is done even when no tank could be dropped this time.
        _dropBeatsRemaining = ArcadeRandom.PickUpTo(_random, _dropIntervalMaxBeats / RepeatDropIntervalDivisor + 1);
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
    private int GetAxisVelocitySubpixels(int scale, bool isPositive, int coordinateUnitArcadePixels)
    {
        var randomSpeed = ArcadeRandom.PickUpTo(_random, _maxSpeed);
        var subpixels = randomSpeed * scale * ScreenSize.ToPortPixelsFromArcadePixels(coordinateUnitArcadePixels);
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
    /// <param name="playField">The playfield, whose top and bottom edges the quark flees to.</param>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRTK4.ASM</c> <c>SQ3L</c>.</item>
    ///         <item>Disassembly: <c>asm/robomame.asm</c> <c>ANIMATE_QUARK</c> (<c>$4BFB</c>).</item>
    ///     </list>
    /// </remarks>
    private void LeaveWhenClearOfTheField(PlayField playField)
    {
        var topExitY = playField.GetPlayfieldBounds().Y +
                       ScreenSize.ToPortPixelsFromArcadePixels(QuarkTuning.FleeExitLowRows);
        var bottomExitY = playField.GetPlayfieldBounds().Bottom -
                          ScreenSize.ToPortPixelsFromArcadePixels(QuarkTuning.FleeExitHighRows);
        if (_position.Y <= topExitY || _position.Y >= bottomExitY) LifeState = EntityLifeState.Dead;
    }

    /// <summary>Makes one move, by whole pixels. The fraction of a pixel left over is kept for the next move.</summary>
    /// <param name="playField">The playfield, whose walls the quark stays inside.</param>
    /// <remarks>
    ///     The sideways part is made only if it keeps the quark inside the playfield, and the same goes for the up-or-down
    ///     part.
    /// </remarks>
    private void MoveOnce(PlayField playField)
    {
        var bounds = playField.GetPlayfieldBounds();

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

    /// <summary>
    ///     Picks at random a new way and speed to drift, and how many <see cref="Robotron2084.Core.ArcadeFrameTimer">beats</see> to keep it for. A quark that is close to a
    ///     wall always drifts away from that wall.
    /// </summary>
    /// <param name="bounds">The inside of the playfield wall.</param>
    private void SetRandomVelocity(Rectangle bounds)
    {
        var lowX = bounds.X + ScreenSize.ToPortPixelsFromColumns(QuarkTuning.WallMarginLeftColumns);
        var highX = bounds.Right - ScreenSize.ToPortPixelsFromColumns(QuarkTuning.WallMarginRightColumns);
        var lowY = bounds.Y + ScreenSize.ToPortPixelsFromArcadePixels(QuarkTuning.WallMarginTopRows);
        var highY = bounds.Bottom - ScreenSize.ToPortPixelsFromArcadePixels(QuarkTuning.WallMarginBottomRows);

        var xPositive = _position.X <= lowX || (_position.X < highX && _random.Next(CoinFlipSides) == 0);
        var yPositive = _position.Y <= lowY || (_position.Y < highY && _random.Next(CoinFlipSides) != 0);

        _velocitySubpixels = new IntVector2(
            GetAxisVelocitySubpixels(QuarkTuning.VelocityXScale, xPositive, ScreenSize.ArcadePixelsPerByte),
            GetAxisVelocitySubpixels(QuarkTuning.VelocityYScale, yPositive, 1));

        _newDirectionBeatsRemaining = 1 + _random.Next(QuarkTuning.NewDirectionMaxBeats);
    }

    /// <summary>
    ///     Starts the quark fleeing. It stops going sideways and goes straight up or straight down at a set speed.
    /// </summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRTK4.ASM</c>, routine <c>SQ3</c></item>
    ///         <item>
    ///             Disassembly: <c>asm/robomame.asm</c> at <c>$4C6F</c>, which sets the sideways speed to 0 and the
    ///             up-and-down speed to plus or minus <c>$0200</c>, and then runs on into <c>MAKE_QUARK_VAMOOSE</c>
    ///             (<c>$4C7F</c>)
    ///         </item>
    ///     </list>
    /// </remarks>
    private void StartFlee()
    {
        _isFleeing = true;
        var subpixels = QuarkTuning.FleeVelocityRom * ScreenSize.ToPortPixelsFromArcadePixels(1);
        var verticalSubpixels = -subpixels;
        var isFleeingDown = _random.Next(CoinFlipSides) == 0;
        if (isFleeingDown) verticalSubpixels = subpixels;

        _velocitySubpixels = new IntVector2(0, verticalSubpixels);
        _remainderSubpixels = IntVector2.Zero;
    }
}
