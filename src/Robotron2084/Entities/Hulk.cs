using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Graphics;
using Robotron2084.Level;
using Robotron2084.Tuning;

namespace Robotron2084.Entities;

/// <summary>
///     A hulk is a huge, tough robot that can't be killed by shooting it. A shot just knocks it back. It slowly
///     stomps after you, after a family member, or off into a corner.
/// </summary>
/// <seealso cref="Player" />
/// <seealso cref="Human" />
/// <remarks>
///     It acts on a <see cref="Robotron2084.Core.ArcadeFrameTimer">beat</see>.
///     <see cref="_beatTimer" /> says when it is time for the next <see cref="Robotron2084.Core.ArcadeFrameTimer">beat</see>.
///     <list type="bullet">
///         <item>
///             Original source: <c>RRH11.ASM</c>, routine <c>HULK</c> (with <c>HULKND</c>, <c>HULKIL</c>
///             sub-blocks)
///         </item>
///         <item>
///             Disassembly: <c>asm/robomame.asm</c> at <c>ANIMATE_HULK</c> (<c>$003E</c>), with movement in
///             <c>HULK_MOVE_HORIZONTALLY</c>/<c>MAKE_HULK_MOVE_VERTICALLY</c> and direction changes in
///             <c>HULK_CHANGE_DIRECTION</c>
///         </item>
///     </list>
/// </remarks>
public sealed class Hulk : IEntity, IAnimationFrameSource, IWaveStartRobot
{
    /// <summary>
    ///     How far past the right wall, in columns, the spot a hulk heads for can be before it wraps round to the left wall.
    /// </summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRH11.ASM</c> <c>HNDX</c>.</item>
    ///         <item>
    ///             Disassembly: <c>$012D</c> to <c>$0135</c>, <c>CMPA #$8F / BLS / CMPA #$CF / BLS / LDA #$07</c>. <c>$8F</c>
    ///             is the
    ///             right wall and <c>$CF</c> is 64 columns past it.
    ///         </item>
    ///     </list>
    /// </remarks>
    private const int DestinationWrapColumnsPastRightWall = 0xCF - 0x8F;

    /// <summary>
    ///     How far above the top wall, in rows, the spot a hulk heads for can be before it wraps round to the bottom wall.
    /// </summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRH11.ASM</c> <c>HNDY</c>.</item>
    ///         <item>
    ///             Disassembly: <c>$014D</c> to <c>$0151</c>, <c>CMPA #$06 / BCC / LDA #$EA</c>. The top wall is at row
    ///             <c>$18</c>,
    ///             which is 18 rows below row 6, and <c>$EA</c> is the bottom wall.
    ///         </item>
    ///     </list>
    /// </remarks>
    private const int DestinationWrapRowsAboveTopWall = 0x18 - 0x06;

    /// <summary>How long the arcade's routine sleeps between one look at whether the game is live and the next.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>
    ///             Original source: <c>RRH11.ASM</c> <c>HULK</c> ("WAIT FOR STATUS TO GO"),
    ///             <c>
    ///                 BITA #$7F / BEQ
    ///                 HULKL / NAP 8,HULK
    ///             </c>
    ///             , which runs on into its first step with no more sleep.
    ///         </item>
    ///         <item>Disassembly: <c>$0030</c>.</item>
    ///     </list>
    /// </remarks>
    private const int LivePollArcadeFrames = 8;

    /// <summary>One more than the most steps a hulk may take before it picks a new direction.</summary>
    private const int NewDirectionStepsMaxExclusive = 33;

    /// <summary>The fewest steps a hulk takes before it picks a new direction.</summary>
    private const int NewDirectionStepsMin = 1;

    /// <summary>How many columns past the right wall the phantom target is (see <see cref="GetPhantomTargetPosition" />).</summary>
    /// <remarks>Disassembly: the byte <c>$C8</c> at <c>$7E05</c>, read as a column. The right wall is at column <c>$8F</c>.</remarks>
    private const int PhantomColumnsPastRightWall = 0xC8 - 0x8F;

    /// <summary>How many rows above the top wall the phantom target is (see <see cref="GetPhantomTargetPosition" />).</summary>
    /// <remarks>Disassembly: the byte <c>$13</c> at <c>$7E06</c>, read as a row. The top wall is at row <c>$18</c>.</remarks>
    private const int PhantomRowsAboveTopWall = 0x18 - 0x13;

    /// <summary>
    ///     A laser shoves a hulk sideways by one arcade pixel. Some shoves are this many times bigger (see
    ///     <see cref="ShoveSidewaysDoubleRandomRange" />).
    /// </summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRH11.ASM</c> <c>HULKIL</c>.</item>
    ///         <item>Disassembly: <c>asm/robomame.asm</c> <c>HULK_CHANGE_DIRECTION</c>.</item>
    ///     </list>
    /// </remarks>
    private const int ShoveSidewaysDoubleFactor = 2;

    /// <summary>The range of the random number that makes some sideways shoves bigger. That is half the time.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRH11.ASM</c> <c>HULKIL</c>.</item>
    ///         <item>Disassembly: <c>asm/robomame.asm</c> <c>HULK_CHANGE_DIRECTION</c>.</item>
    ///     </list>
    /// </remarks>
    private const int ShoveSidewaysDoubleRandomRange = 2;

    /// <summary>
    ///     A laser shoves a hulk up or down by one arcade pixel. Some shoves are this many times bigger (see
    ///     <see cref="ShoveVerticalQuadrupleRandomRange" />).
    /// </summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRH11.ASM</c> <c>HULKIL</c>.</item>
    ///         <item>Disassembly: <c>asm/robomame.asm</c> <c>HULK_CHANGE_DIRECTION</c>.</item>
    ///     </list>
    /// </remarks>
    private const int ShoveVerticalQuadrupleFactor = 4;

    /// <summary>How often a shove up or down is made bigger: three quarters of the time.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRH11.ASM</c> <c>HULKIL</c>.</item>
    ///         <item>Disassembly: <c>asm/robomame.asm</c> <c>HULK_CHANGE_DIRECTION</c>.</item>
    ///     </list>
    /// </remarks>
    private const int ShoveVerticalQuadrupleRandomBelow = 3;

    /// <summary>The range of the random number that makes some shoves up or down bigger.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRH11.ASM</c> <c>HULKIL</c>.</item>
    ///         <item>Disassembly: <c>asm/robomame.asm</c> <c>HULK_CHANGE_DIRECTION</c>.</item>
    ///     </list>
    /// </remarks>
    private const int ShoveVerticalQuadrupleRandomRange = 4;

    /// <summary>How far the longer sideways step goes, in arcade pixels. It is the second and fourth step of the walk pattern.</summary>
    private const int SidewaysLongStepArcadePixels = 4;

    /// <summary>How far the shorter sideways step goes, in arcade pixels. It is the first and third step of the walk pattern.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRH11.ASM</c> the horizontal animation tables (<c>HLKAL</c>/<c>HLKAR</c>).</item>
    ///         <item>Disassembly: <c>asm/robomame.asm</c> <c>ANIMATE_HULK</c> (<c>$003E</c>).</item>
    ///     </list>
    /// </remarks>
    private const int SidewaysShortStepArcadePixels = 3;

    /// <summary>
    ///     The hulk does not head exactly for its target. The spot it heads for can be off to the right or below by up to one less than
    ///     this many columns or rows.
    /// </summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRH11.ASM</c> <c>HNDX</c>/<c>HNDY</c>.</item>
    ///         <item>
    ///             Disassembly: <c>HULK_MOVE_HORIZONTALLY</c> (<c>$0125</c>) and <c>MAKE_HULK_MOVE_VERTICALLY</c> (
    ///             <c>$0145</c>),
    ///             <c>ANDA #$1F / ADDA #$F0</c>, added to the column or the row of the target's top-left corner.
    ///         </item>
    ///     </list>
    /// </remarks>
    private const int TargetOffsetMaxExclusive = 16;

    /// <summary>
    ///     The furthest to the left or above its target that the spot the hulk heads for can be, in columns or rows (see
    ///     <see cref="TargetOffsetMaxExclusive" />). It is negative.
    /// </summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRH11.ASM</c> <c>HNDX</c>/<c>HNDY</c>.</item>
    ///         <item>Disassembly: <c>$0125</c> and <c>$0145</c>, <c>ADDA #$F0</c>.</item>
    ///     </list>
    /// </remarks>
    private const int TargetOffsetMin = -16;

    /// <summary>How far every step up or down goes, in arcade pixels.</summary>
    private const int VerticalStepArcadePixels = 2;

    /// <summary>How many steps make up the walk pattern.</summary>
    private const int WalkPatternLength = 4;

    /// <summary>
    ///     How big the hulk is, in port pixels. It is the size of the hulk's sprite.
    /// </summary>
    private static readonly (int Width, int Height) CollisionSize =
        (ScreenSize.ToPortPixelsFromArcadePixels(CollisionSizes.HulkCollisionSize.Width),
            ScreenSize.ToPortPixelsFromArcadePixels(CollisionSizes.HulkCollisionSize.Height));

    /// <summary>
    ///     The walk animation frames for each direction, in the order they are shown, as places in
    ///     <see cref="SpriteSet.HulkAnimationFrames" />.
    /// </summary>
    /// <remarks>
    ///     Each direction shows four animation frames, and the first and third are the same one. So each direction
    ///     uses three of the nine: left uses the 1st, 2nd and 3rd, right uses the 7th, 8th and 9th, and up and down both use
    ///     the 4th, 5th and 6th (ROM: <c>HLKAL</c>/<c>HLKAR</c>/<c>HLKAD</c>/<c>HLKAU</c>, animation frames <c>HLKLP1</c>).
    /// </remarks>
    private static readonly int[] LeftAnimationFrames = { 0, 1, 0, 2 };

    private static readonly int[] RightAnimationFrames = { 6, 7, 6, 8 };
    private static readonly int[] VerticalAnimationFrames = { 3, 4, 3, 5 };

    /// <summary>The time from one <see cref="Robotron2084.Core.ArcadeFrameTimer">beat</see> to the next.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRH11.ASM</c> the wave's <c>HLKSPD</c> frame count.</item>
    ///         <item>Disassembly: <c>asm/robomame.asm</c> <c>ANIMATE_HULK</c> (<c>$003E</c>).</item>
    ///     </list>
    /// </remarks>
    private readonly int _beatIntervalArcadeFrames;

    private readonly Func<IntVector2> _getTargetPosition;

    private readonly Random _random;
    private readonly SpriteSet _sprites;

    /// <summary>Which animation frame is showing, as a place in the sprite set's hulk animation frames.</summary>
    private int _animationFrameIndex;

    private ArcadeFrameTimer _beatTimer = new();

    private bool _hasPickedFirstDirection;
    private bool _isMovingHorizontally;

    private int _newDirectionStepsRemaining;

    /// <summary>The inside of the wall as it was on the last update, for keeping the hulk inside it.</summary>
    private Rectangle? _playfieldBounds;

    private IntVector2 _position;

    /// <summary>Which step of the walk pattern comes next.</summary>
    private int _walkCycleStep;

    /// <summary>Makes a hulk. It picks its first direction when the game goes live.</summary>
    /// <param name="sprites">The shared sprite set.</param>
    /// <param name="position">Where the hulk's top-left corner is.</param>
    /// <param name="random">
    ///     Where its random numbers come from. They pick how many steps it takes before it turns, and move
    ///     its direction a little.
    /// </param>
    /// <param name="beatIntervalArcadeFrames">
    ///     How long the hulk waits between steps (ROM: <c>HLKSPD</c>). A bigger number makes a
    ///     slower hulk.
    /// </param>
    /// <param name="getTargetPosition">
    ///     Gives the place the hulk is heading for now.
    ///     <see cref="Level.Spawning.HulkWaveSpawner" /> decides what that is: the player, a family member, or a spot off the
    ///     corner of the playfield.
    /// </param>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>
    ///             Original source: <c>RRH11.ASM</c> <c>HLKSPD</c>. The time between <see cref="Robotron2084.Core.ArcadeFrameTimer">beats</see> is set by the wave
    ///             of a second.
    ///         </item>
    ///         <item>Disassembly: <c>asm/robomame.asm</c> <c>ANIMATE_HULK</c> (<c>$003E</c>).</item>
    ///     </list>
    /// </remarks>
    public Hulk(
        SpriteSet sprites,
        IntVector2 position,
        Random random,
        int beatIntervalArcadeFrames,
        Func<IntVector2> getTargetPosition)
    {
        _sprites = sprites;
        _position = position;
        _random = random;
        _beatIntervalArcadeFrames = beatIntervalArcadeFrames;
        _getTargetPosition = getTargetPosition;
        _newDirectionStepsRemaining = GetRandomNewDirectionSteps();
        Direction = Direction8.Up; // Up is only a stand-in. When the hulk first picks a direction, this changes to left or right.
        _animationFrameIndex = VerticalAnimationFrames[0];
    }

    /// <summary>Always Alive. A hulk cannot be killed, so it is never Dying or Dead.</summary>
    public EntityLifeState LifeState => EntityLifeState.Alive;

    /// <summary>Where the hulk's top-left corner is.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRH11.ASM</c> <c>HULK</c>, the OBJX/OBJY registers.</item>
    ///         <item>Disassembly: <c>asm/robomame.asm</c> <c>ANIMATE_HULK</c> (<c>$003E</c>).</item>
    ///     </list>
    /// </remarks>
    public IntVector2 Position => _position;

    /// <summary>Which animation frame is showing, as a place in <see cref="SpriteSet.HulkAnimationFrames" />.</summary>
    internal int AnimationFrameIndex => _animationFrameIndex;

    /// <summary>Which way the hulk is walking.</summary>
    internal Direction8 Direction { get; private set; }

    /// <summary>Shoves the hulk the way the laser that hit it was going. The hulk stops at the wall.</summary>
    /// <param name="direction">The way the laser was going: left, right or neither, and up, down or neither.</param>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>
    ///             Original source: <c>RRH11.ASM</c> <c>HULKIL</c>. A sideways shove is 1 arcade pixel, and 2 about half
    ///             the time. A shove up or down is 1 arcade pixel, and 4 about three quarters of the time.
    ///         </item>
    ///         <item>Disassembly: <c>asm/robomame.asm</c> <c>HULK_CHANGE_DIRECTION</c>.</item>
    ///     </list>
    /// </remarks>
    public void ApplyKnockback(IntVector2 direction)
    {
        var dx = direction.X;
        var isShovedSidewaysDouble = direction.X != 0 && _random.Next(ShoveSidewaysDoubleRandomRange) == 0;
        if (isShovedSidewaysDouble) dx = direction.X * ShoveSidewaysDoubleFactor;

        var dy = direction.Y;
        var isShovedVerticalQuadruple = direction.Y != 0 &&
                                        _random.Next(ShoveVerticalQuadrupleRandomRange) < ShoveVerticalQuadrupleRandomBelow;
        if (isShovedVerticalQuadruple) dy = direction.Y * ShoveVerticalQuadrupleFactor;

        _position += new IntVector2(ScreenSize.ToPortPixelsFromArcadePixels(dx),
            ScreenSize.ToPortPixelsFromArcadePixels(dy));
        if (_playfieldBounds is { } bounds)
        {
            var x = Math.Clamp(_position.X, bounds.X, bounds.Right - CollisionSize.Width);
            var y = Math.Clamp(_position.Y, bounds.Top, bounds.Bottom - CollisionSize.Height);
            _position = new IntVector2(x, y);
        }
    }

    /// <summary>
    ///     Picks the hulk's first direction and sets the time of its first step, on the <see cref="Robotron2084.Core.ArcadeClock.PortTicksPerSecond">tick</see> the game goes live. The <see cref="Robotron2084.Core.ArcadeFrameTimer">beat</see> timer is set so
    ///     that it comes due when that time has gone by.
    /// </summary>
    /// <param name="field">The playfield, which works out how long the wait is.</param>
    public void BeginPlay(PlayField field)
    {
        SetFirstDirection(field);
        _beatTimer = field.CreateFirstBeatTimer(LivePollArcadeFrames, 0, _beatIntervalArcadeFrames);
    }

    /// <summary>Draws the walk animation frame that is showing.</summary>
    /// <param name="spriteBatch">What the hulk is drawn with.</param>
    public void Draw(SpriteBatch spriteBatch)
    {
        _sprites.Blitter.DrawSprite(spriteBatch, GetCurrentAnimationFrame(), GetBounds(), Color.White);
    }

    /// <summary>The box the hulk takes up on the screen.</summary>
    public Rectangle GetBounds()
    {
        return new Rectangle(_position.X, _position.Y, CollisionSize.Width, CollisionSize.Height);
    }

    /// <summary>The walk animation frame the hulk is showing. The appear effect needs it.</summary>
    /// <remarks>A hulk is never blown apart, but it does appear at the start of a wave.</remarks>
    public Texture2D GetCurrentAnimationFrame()
    {
        return _sprites.HulkAnimationFrames[_animationFrameIndex];
    }

    /// <summary>
    ///     Runs one <see cref="Robotron2084.Core.ArcadeClock.PortTicksPerSecond">tick</see>. On a <see cref="Robotron2084.Core.ArcadeFrameTimer">beat</see> the hulk takes a step. If the wall is in the way, it does not step and picks a new
    ///     direction.
    /// </summary>
    /// <param name="gameTime">Not used. The hulk counts <see cref="Robotron2084.Core.ArcadeClock.PortTicksPerSecond">ticks</see>.</param>
    /// <param name="field">The playfield.</param>
    public void Update(GameTime gameTime, PlayField field)
    {
        _playfieldBounds = field.Wall.PlayfieldBounds;
        if (field.RobotsFrozen()) return;

        if (!_hasPickedFirstDirection)
        {
            SetFirstDirection(field);
            return;
        }

        if (_beatTimer.Tick(_beatIntervalArcadeFrames)) ActOnBeat(field);
    }

    /// <summary>
    ///     Works out where a hulk with nothing real to stalk heads for: a fixed spot off the top-right corner of the
    ///     playfield. Such a hulk drifts to that corner and stays near it.
    /// </summary>
    /// <param name="playfieldBounds">The inside of the playfield wall, in port pixels.</param>
    /// <returns>The spot, in port pixels. It is outside the wall.</returns>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>
    ///             Original source: <c>RRH11.ASM</c> <c>HULKND</c>, which follows the hulk's target pointer without checking
    ///             it.
    ///         </item>
    ///         <item>
    ///             Disassembly: <c>$010D</c>, <c>LDY [$09,U]</c>. A hulk whose search of the family list found nobody holds a
    ///             target of 0, so this reads the two bytes at <c>$0000</c> and gets <c>$7E01</c>, which is not an object. The
    ///             hulk
    ///             takes the bytes at <c>$7E05</c> and <c>$7E06</c> (<c>$C8</c> and <c>$13</c>) as that object's column and
    ///             row.
    ///         </item>
    ///     </list>
    /// </remarks>
    internal static IntVector2 GetPhantomTargetPosition(Rectangle playfieldBounds)
    {
        return new IntVector2(
            playfieldBounds.Right + ScreenSize.ToPortPixelsFromColumns(PhantomColumnsPastRightWall),
            playfieldBounds.Y - ScreenSize.ToPortPixelsFromArcadePixels(PhantomRowsAboveTopWall));
    }

    /// <summary>
    ///     Where the hulk is heading for now: the player, a family member, or the spot off the corner of the playfield.
    /// </summary>
    internal IntVector2 GetTargetPosition()
    {
        return _getTargetPosition();
    }

    /// <summary>Puts the hulk at <paramref name="position" />.</summary>
    /// <param name="position">Where to put the hulk's top-left corner.</param>
    internal void TeleportTo(IntVector2 position)
    {
        _position = position;
    }

    /// <summary>Gets the walk animation frames for a direction, in the order they are shown.</summary>
    /// <param name="direction">The way the hulk is walking.</param>
    private static int[] GetFrames(Direction8 direction)
    {
        return direction switch
        {
            Direction8.Left => LeftAnimationFrames,
            Direction8.Right => RightAnimationFrames,
            _ => VerticalAnimationFrames // Walking up and walking down use the same animation frames, as in the arcade.
        };
    }

    /// <summary>
    ///     What the hulk does on its <see cref="Robotron2084.Core.ArcadeFrameTimer">beat</see>. It takes a step. If the wall is in the way, it does not step and picks a new
    ///     direction.
    /// </summary>
    /// <param name="field">The playfield.</param>
    private void ActOnBeat(PlayField field)
    {
        // Show this step's walk animation frame, then move. Sideways steps go short, long, short, long.
        _animationFrameIndex = GetFrames(Direction)[_walkCycleStep];
        var stepArcadePixels = VerticalStepArcadePixels;
        if (_isMovingHorizontally)
        {
            var isShortStep = _walkCycleStep % 2 == 0;
            stepArcadePixels = SidewaysLongStepArcadePixels;
            if (isShortStep) stepArcadePixels = SidewaysShortStepArcadePixels;
        }

        _walkCycleStep = (_walkCycleStep + 1) % WalkPatternLength;
        var next = _position + Direction.ToIntVector() * ScreenSize.ToPortPixelsFromArcadePixels(stepArcadePixels);
        if (field.HitsWall(new Rectangle(next.X, next.Y, CollisionSize.Width, CollisionSize.Height)))
        {
            // The step would go into the wall, so the hulk stays where it is and picks a new direction.
            TurnToNewDirection(field);
            return;
        }

        _position = next;

        if (--_newDirectionStepsRemaining <= 0) TurnToNewDirection(field);
    }

    /// <summary>Picks at random how many steps the hulk takes before its next turn.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRH11.ASM</c> <c>HULKND</c>.</item>
    ///         <item>Disassembly: <c>asm/robomame.asm</c> <c>HULK_CHANGE_DIRECTION</c>.</item>
    ///     </list>
    /// </remarks>
    private int GetRandomNewDirectionSteps()
    {
        return _random.Next(NewDirectionStepsMin, NewDirectionStepsMaxExclusive);
    }

    /// <summary>
    ///     Picks which way the hulk walks. A hulk that is walking sideways picks left or right, and one that is walking
    ///     up and down picks up or down. It picks the way that leads to a spot a random distance from its target.
    /// </summary>
    /// <param name="field">The playfield, whose walls are used when the spot is far outside them.</param>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRH11.ASM</c> <c>HNDX</c>/<c>HNDY</c>.</item>
    ///         <item>
    ///             Disassembly: <c>HULK_MOVE_HORIZONTALLY</c> (<c>$0125</c>) and <c>MAKE_HULK_MOVE_VERTICALLY</c> (
    ///             <c>$0145</c>). The
    ///             random distance is up to 16 columns sideways, or up to 16 rows up or down, so a hulk close to its target
    ///             often turns
    ///             the wrong way.
    ///         </item>
    ///     </list>
    /// </remarks>
    private void PickDirection(PlayField field)
    {
        var target = _getTargetPosition();
        var offsetFromTarget = _random.Next(TargetOffsetMin, TargetOffsetMaxExclusive);
        var bounds = field.Wall.PlayfieldBounds;
        if (_isMovingHorizontally)
        {
            var destinationX = target.X + ScreenSize.ToPortPixelsFromColumns(offsetFromTarget);
            if (destinationX > bounds.Right + ScreenSize.ToPortPixelsFromColumns(DestinationWrapColumnsPastRightWall)) destinationX = bounds.X;

            Direction = Direction8.Right;
            var isDestinationLeftOrLevel = destinationX <= _position.X;
            if (isDestinationLeftOrLevel) Direction = Direction8.Left;
        }
        else
        {
            var destinationY = target.Y + ScreenSize.ToPortPixelsFromArcadePixels(offsetFromTarget);
            if (destinationY < bounds.Y - ScreenSize.ToPortPixelsFromArcadePixels(DestinationWrapRowsAboveTopWall))
                destinationY = bounds.Bottom;

            Direction = Direction8.Down;
            var isDestinationAboveOrLevel = destinationY <= _position.Y;
            if (isDestinationAboveOrLevel) Direction = Direction8.Up;
        }
    }

    /// <summary>
    ///     Picks the hulk's first direction, which is always left or right, and starts its walk from its first animation
    ///     frame.
    /// </summary>
    /// <param name="field">The playfield.</param>
    private void SetFirstDirection(PlayField field)
    {
        _hasPickedFirstDirection = true;
        _isMovingHorizontally = true;
        PickDirection(field);
        _animationFrameIndex = GetFrames(Direction)[0];
    }

    /// <summary>
    ///     Turns the hulk. A hulk that was walking sideways now walks up or down, and the other way about. It also picks
    ///     at random how many steps it takes before its next turn.
    /// </summary>
    /// <param name="field">The playfield.</param>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>
    ///             Original source: <c>RRH11.ASM</c> <c>HULKND</c>/<c>HND10</c>. The walk starts again from its first
    ///             animation frame.
    ///         </item>
    ///         <item>Disassembly: <c>asm/robomame.asm</c> <c>HULK_CHANGE_DIRECTION</c>.</item>
    ///     </list>
    /// </remarks>
    private void TurnToNewDirection(PlayField field)
    {
        _newDirectionStepsRemaining = GetRandomNewDirectionSteps();
        _isMovingHorizontally = !_isMovingHorizontally;
        PickDirection(field);
        _walkCycleStep = 0;
        _animationFrameIndex = GetFrames(Direction)[0];
    }
}
