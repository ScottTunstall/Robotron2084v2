using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Graphics;
using Robotron2084.Level;
using Robotron2084.Tuning;

namespace Robotron2084.Entities;

/// <summary>A hulk is a huge, tough robot that can't be killed by shooting it. A shot just knocks it back. It slowly stomps after you or a family member.</summary>
/// <seealso cref="Player"/>
/// <seealso cref="Human"/>
/// <remarks>
/// It acts on a beat. The <see cref="PlayField"/> calls <see cref="Update"/> on every tick, through
/// <see cref="FieldEntities"/> and <see cref="PlayField.UpdateEntity"/>. There are two times it does not: while the
/// hulk is still appearing at the start of a wave, and during the short freeze just after the player is killed.
/// <see cref="_beatTimer"/> gathers the ticks until it is time for the next beat (see <see cref="ArcadeClock"/>).
///
/// <list type="bullet">
/// <item>Original source: <c>RRH11.ASM</c>, routine <c>HULK</c> (with <c>HULKND</c>, <c>HULKIL</c>
/// sub-blocks)</item>
/// <item>Disassembly: <c>asm/robomame.asm</c> at <c>ANIMATE_HULK</c> (<c>$003E</c>), with movement in
/// <c>HULK_MOVE_HORIZONTALLY</c>/<c>MAKE_HULK_MOVE_VERTICALLY</c> and direction changes in
/// <c>HULK_CHANGE_DIRECTION</c></item>
/// </list>
/// </remarks>
public sealed class Hulk : IEntity, IAnimationFrameSource, IWaveStartRobot
{
    /// <summary>How long the arcade's routine sleeps between one look at whether the game is live and the next, in 50ths of a second. It decides how long after the game goes live the first step comes (<see cref="BeginPlay"/>).</summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>RRH11.ASM</c> <c>HULK</c> ("WAIT FOR STATUS TO GO"), <c>BITA #$7F / BEQ
    /// HULKL / NAP 8,HULK</c>, which runs on into its first step with no more sleep.</item>
    /// <item>Disassembly: <c>$0030</c>.</item>
    /// </list>
    /// </remarks>
    private const int LivePollRomFrames = 8;

    /// <summary>The hulk does not aim exactly at its target. A random number of arcade pixels is added to where the target is. That number is always less than this.</summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>RRH11.ASM</c> <c>HNDX</c>/<c>HNDY</c>.</item>
    /// <item>Disassembly: <c>asm/robomame.asm</c> <c>ANIMATE_HULK</c> (<c>$003E</c>).</item>
    /// </list>
    /// </remarks>
    private const int AimOffsetMaxExclusiveArcadePixels = 16;

    /// <summary>The smallest number of arcade pixels that can be added to where the target is (see <see cref="AimOffsetMaxExclusiveArcadePixels"/>). It is negative, which moves the aim left or up.</summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>RRH11.ASM</c> <c>HNDX</c>/<c>HNDY</c>.</item>
    /// <item>Disassembly: <c>asm/robomame.asm</c> <c>ANIMATE_HULK</c> (<c>$003E</c>).</item>
    /// </list>
    /// </remarks>
    private const int AimOffsetMinArcadePixels = -16;

    /// <summary>One more than the most steps a hulk may take before it aims again. The number of steps is picked at random, from <see cref="ReaimStepsMin"/> up to one less than this, and counted down in <see cref="_reaimStepsRemaining"/>.</summary>
    private const int ReaimStepsMaxExclusive = 32;

    /// <summary>The fewest steps a hulk takes before it aims again. The number of steps is picked at random, from this up to one less than <see cref="ReaimStepsMaxExclusive"/>, and counted down in <see cref="_reaimStepsRemaining"/>.</summary>
    private const int ReaimStepsMin = 1;

    /// <summary>A laser shoves a hulk sideways by one arcade pixel. Some shoves are this many times bigger (see <see cref="ShoveSidewaysDoubleRollSides"/>).</summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>RRH11.ASM</c> <c>HULKIL</c>.</item>
    /// <item>Disassembly: <c>asm/robomame.asm</c> <c>HULK_CHANGE_DIRECTION</c>.</item>
    /// </list>
    /// </remarks>
    private const int ShoveSidewaysDoubleFactor = 2;

    /// <summary>A sideways shove is made bigger when a random number, from 0 up to one less than this, comes up 0. That is half the time.</summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>RRH11.ASM</c> <c>HULKIL</c>.</item>
    /// <item>Disassembly: <c>asm/robomame.asm</c> <c>HULK_CHANGE_DIRECTION</c>.</item>
    /// </list>
    /// </remarks>
    private const int ShoveSidewaysDoubleRollSides = 2;

    /// <summary>A laser shoves a hulk up or down by one arcade pixel. Some shoves are this many times bigger (see <see cref="ShoveVerticalQuadrupleRollSides"/>).</summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>RRH11.ASM</c> <c>HULKIL</c>.</item>
    /// <item>Disassembly: <c>asm/robomame.asm</c> <c>HULK_CHANGE_DIRECTION</c>.</item>
    /// </list>
    /// </remarks>
    private const int ShoveVerticalQuadrupleFactor = 4;

    /// <summary>A shove up or down is made bigger when the random number comes up below this. That is three quarters of the time (see <see cref="ShoveVerticalQuadrupleRollSides"/>).</summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>RRH11.ASM</c> <c>HULKIL</c>.</item>
    /// <item>Disassembly: <c>asm/robomame.asm</c> <c>HULK_CHANGE_DIRECTION</c>.</item>
    /// </list>
    /// </remarks>
    private const int ShoveVerticalQuadrupleRollBelow = 3;

    /// <summary>The random number that decides whether a shove up or down is made bigger is from 0 up to one less than this (see <see cref="ShoveVerticalQuadrupleRollBelow"/>).</summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>RRH11.ASM</c> <c>HULKIL</c>.</item>
    /// <item>Disassembly: <c>asm/robomame.asm</c> <c>HULK_CHANGE_DIRECTION</c>.</item>
    /// </list>
    /// </remarks>
    private const int ShoveVerticalQuadrupleRollSides = 4;

    /// <summary>How far the longer sideways step goes, in arcade pixels. It is the second and fourth step of the walk pattern.</summary>
    private const int SidewaysLongStepArcadePixels = 4;

    /// <summary>How far the shorter sideways step goes, in arcade pixels. It is the first and third step of the walk pattern.</summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>RRH11.ASM</c> the horizontal animation tables (<c>HLKAL</c>/<c>HLKAR</c>).</item>
    /// <item>Disassembly: <c>asm/robomame.asm</c> <c>ANIMATE_HULK</c> (<c>$003E</c>).</item>
    /// </list>
    /// </remarks>
    private const int SidewaysShortStepArcadePixels = 3;

    /// <summary>How far every step up or down goes, in arcade pixels.</summary>
    private const int VerticalStepArcadePixels = 2;

    /// <summary>How many steps make up the walk pattern. <see cref="_walkCycleStep"/> counts up to this and then goes back to the first step.</summary>
    private const int WalkPatternLength = 4;

    /// <summary>How big the hulk is, in port pixels. It is the size of the hulk's sprite, and it is used to tell what the hulk touches.</summary>
    private static readonly (int Width, int Height) CollisionSize =
        (ScreenSize.ToPortPixels(CollisionSizes.HulkCollisionSize.Width), ScreenSize.ToPortPixels(CollisionSizes.HulkCollisionSize.Height));

    /// <summary>The walk animation frames for each direction, in the order they are shown, as places in <see cref="SpriteSet.HulkAnimationFrames"/>.</summary>
    /// <remarks>Each direction shows four animation frames, and the first and third are the same one. So each direction
    /// uses three of the nine: left uses the 1st, 2nd and 3rd, right uses the 7th, 8th and 9th, and up and down both use
    /// the 4th, 5th and 6th (ROM: <c>HLKAL</c>/<c>HLKAR</c>/<c>HLKAD</c>/<c>HLKAU</c>, animation frames <c>HLKLP1</c>).</remarks>
    private static readonly int[] LeftAnimationFrames = { 0, 1, 0, 2 };

    private static readonly int[] RightAnimationFrames = { 6, 7, 6, 8 };
    private static readonly int[] VerticalAnimationFrames = { 3, 4, 3, 5 };

    private readonly Random _random;
    private readonly SpriteSet _sprites;

    /// <summary>The time from one beat to the next, in clock units (see <see cref="ArcadeClock"/>). It is set from the wave's hulk speed.</summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>RRH11.ASM</c> the wave's <c>HLKSPD</c> frame count.</item>
    /// <item>Disassembly: <c>asm/robomame.asm</c> <c>ANIMATE_HULK</c> (<c>$003E</c>).</item>
    /// </list>
    /// </remarks>
    private readonly int _beatIntervalClockUnits;

    private readonly Func<IntVector2> _getTargetPosition;

    /// <summary>Which animation frame is showing, as a place in the sprite set's hulk animation frames.</summary>
    private int _animationFrameIndex;

    /// <summary>Counts up to the next beat.</summary>
    private int _beatTimer;

    private Direction8 _direction;
    private bool _hasAimed;
    private bool _isMovingHorizontally;

    /// <summary>The inside of the wall as it was on the last update. A shove from a laser uses it, to keep the hulk inside the wall.</summary>
    private Rectangle? _playfieldBounds;

    private IntVector2 _position;
    private int _reaimStepsRemaining;

    /// <summary>Which step of the walk pattern comes next.</summary>
    private int _walkCycleStep;

    /// <summary>Makes a hulk. It picks its first direction when the game goes live.</summary>
    /// <param name="sprites">The shared sprite set.</param>
    /// <param name="position">Where the hulk's top-left corner is.</param>
    /// <param name="random">Where its random numbers come from. They pick how many steps it takes before it turns, and move its aim a little.</param>
    /// <param name="beatIntervalRomFrames">How long the hulk waits between steps, in 50ths of a second (ROM: <c>HLKSPD</c>). A bigger number makes a slower hulk.</param>
    /// <param name="getTargetPosition">Gives the place the hulk is hunting now: where the player is, or where a family member is. Once that family member has gone, it gives where the player is.</param>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>RRH11.ASM</c> <c>HLKSPD</c>. The time between beats is from 5 to 8 fiftieths
    /// of a second.</item>
    /// <item>Disassembly: <c>asm/robomame.asm</c> <c>ANIMATE_HULK</c> (<c>$003E</c>).</item>
    /// </list>
    /// </remarks>
    public Hulk(
        SpriteSet sprites,
        IntVector2 position,
        Random random,
        int beatIntervalRomFrames,
        Func<IntVector2> getTargetPosition)
    {
        _sprites = sprites;
        _position = position;
        _random = random;
        _beatIntervalClockUnits = ArcadeClock.ToClockUnits(beatIntervalRomFrames);
        _getTargetPosition = getTargetPosition;
        _reaimStepsRemaining = RollReaimSteps();
        _direction = Direction8.Up; // Up is only a stand-in. When the hulk first aims, this changes to left or right.
        _animationFrameIndex = VerticalAnimationFrames[0];
    }

    /// <summary>The box the hulk takes up on the screen. It is used to tell what the hulk touches.</summary>
    public Rectangle GetBounds() => new(_position.X, _position.Y, CollisionSize.Width, CollisionSize.Height);

    /// <summary>The walk animation frame the hulk is showing. The appear effect needs it.</summary>
    /// <remarks>A hulk is never blown apart, but it does appear at the start of a wave.</remarks>
    public Texture2D GetCurrentAnimationFrame() => _sprites.HulkAnimationFrames[_animationFrameIndex];

    /// <summary>Always Alive. A hulk cannot be killed, so it is never Dying or Dead.</summary>
    public EntityLifeState LifeState => EntityLifeState.Alive;

    /// <summary>Where the hulk's top-left corner is.</summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>RRH11.ASM</c> <c>HULK</c>, the OBJX/OBJY registers.</item>
    /// <item>Disassembly: <c>asm/robomame.asm</c> <c>ANIMATE_HULK</c> (<c>$003E</c>).</item>
    /// </list>
    /// </remarks>
    public IntVector2 Position => _position;

    /// <summary>Which animation frame is showing, as a place in <see cref="SpriteSet.HulkAnimationFrames"/>. Tests use this.</summary>
    internal int AnimationFrameIndex => _animationFrameIndex;

    /// <summary>Which way the hulk is walking. Tests use this.</summary>
    internal Direction8 Direction => _direction;

    /// <summary>Shoves the hulk the way the laser that hit it was going. The hulk stops at the wall.</summary>
    /// <param name="direction">The way the laser was going: left, right or neither, and up, down or neither.</param>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>RRH11.ASM</c> <c>HULKIL</c>. A sideways shove is 1 arcade pixel, and 2 about half
    /// the time. A shove up or down is 1 arcade pixel, and 4 about three quarters of the time.</item>
    /// <item>Disassembly: <c>asm/robomame.asm</c> <c>HULK_CHANGE_DIRECTION</c>.</item>
    /// </list>
    /// </remarks>
    public void ApplyKnockback(IntVector2 direction)
    {
        int dx = direction.X != 0 && _random.Next(ShoveSidewaysDoubleRollSides) == 0
            ? direction.X * ShoveSidewaysDoubleFactor
            : direction.X;
        int dy = direction.Y != 0 && _random.Next(ShoveVerticalQuadrupleRollSides) < ShoveVerticalQuadrupleRollBelow
            ? direction.Y * ShoveVerticalQuadrupleFactor
            : direction.Y;
        _position += new IntVector2(ScreenSize.ToPortPixels(dx), ScreenSize.ToPortPixels(dy));
        if (_playfieldBounds is { } bounds)
        {
            int x = Math.Clamp(_position.X, bounds.X, bounds.Right - CollisionSize.Width);
            int y = Math.Clamp(_position.Y, bounds.Top, bounds.Bottom - CollisionSize.Height);
            _position = new IntVector2(x, y);
        }
    }

    /// <summary>Draws the walk animation frame that is showing.</summary>
    /// <param name="spriteBatch">What the hulk is drawn with.</param>
    public void Draw(SpriteBatch spriteBatch)
    {
        _sprites.Blitter.DrawSprite(spriteBatch, GetCurrentAnimationFrame(), GetBounds(), Color.White);
    }

    /// <summary>Aims the hulk and sets the time of its first step, on the tick the game goes live. The beat timer is set so that it comes due when that time has gone by.</summary>
    /// <param name="field">The playfield, which works out how long the wait is.</param>
    public void BeginPlay(PlayField field)
    {
        AimForTheFirstTime(field);
        _beatTimer = _beatIntervalClockUnits - field.GetClockUnitsToFirstBeat(LivePollRomFrames, napRomFrames: 0);
    }

    /// <summary>Runs one tick. On a beat the hulk takes a step. If the wall is in the way, it does not step and picks a new direction.</summary>
    /// <param name="gameTime">Not used. The hulk counts ticks.</param>
    /// <param name="field">The playfield.</param>
    public void Update(GameTime gameTime, PlayField field)
    {
        _playfieldBounds = field.Wall.PlayfieldBounds;
        if (field.RobotsFrozen())
        {
            return;
        }

        if (!_hasAimed)
        {
            AimForTheFirstTime(field);
            return;
        }

        _beatTimer += ArcadeClock.UnitsPerPortTick;
        if (_beatTimer < _beatIntervalClockUnits)
        {
            return;
        }

        _beatTimer -= _beatIntervalClockUnits;

        // Show this step's walk animation frame, then move. Sideways steps go short, long, short, long.
        _animationFrameIndex = GetFrames(_direction)[_walkCycleStep];
        int stepArcadePixels = _isMovingHorizontally
            ? (_walkCycleStep % 2 == 0 ? SidewaysShortStepArcadePixels : SidewaysLongStepArcadePixels)
            : VerticalStepArcadePixels;
        _walkCycleStep = (_walkCycleStep + 1) % WalkPatternLength;
        IntVector2 next = _position + _direction.ToIntVector() * ScreenSize.ToPortPixels(stepArcadePixels);
        if (field.HitsWall(new Rectangle(next.X, next.Y, CollisionSize.Width, CollisionSize.Height)))
        {
            // The step would go into the wall, so the hulk stays where it is and picks a new direction.
            Reaim(field);
            return;
        }

        _position = next;

        if (--_reaimStepsRemaining <= 0)
        {
            Reaim(field);
        }
    }

    /// <summary>Puts the hulk at <paramref name="position"/>. Tests use this.</summary>
    /// <param name="position">Where to put the hulk's top-left corner.</param>
    internal void TeleportTo(IntVector2 position) => _position = position;

    /// <summary>Gets the walk animation frames for a direction, in the order they are shown.</summary>
    /// <param name="direction">The way the hulk is walking.</param>
    private static int[] GetFrames(Direction8 direction) => direction switch
    {
        Direction8.Left => LeftAnimationFrames,
        Direction8.Right => RightAnimationFrames,
        _ => VerticalAnimationFrames, // Walking up and walking down use the same animation frames, as in the arcade.
    };

    /// <summary>Picks the hulk's first direction, which is always left or right, and starts its walk from its first animation frame.</summary>
    /// <param name="field">The playfield.</param>
    private void AimForTheFirstTime(PlayField field)
    {
        _hasAimed = true;
        _isMovingHorizontally = true;
        PickDirection(field);
        _animationFrameIndex = GetFrames(_direction)[0];
    }

    /// <summary>Picks which way the hulk walks. A hulk that is walking sideways picks left or right, and one that is walking up and down picks up or down. It picks the way that leads to a spot a random distance from its target.</summary>
    /// <param name="field">The playfield, whose walls limit where that spot can be.</param>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>RRH11.ASM</c> <c>HNDX</c>/<c>HNDY</c>. A spot outside the left or right wall is moved
    /// back inside. A spot above the top wall is changed to the bottom wall.</item>
    /// <item>Disassembly: <c>asm/robomame.asm</c> <c>ANIMATE_HULK</c> (<c>$003E</c>).</item>
    /// </list>
    /// </remarks>
    private void PickDirection(PlayField field)
    {
        IntVector2 target = _getTargetPosition();
        int offset = _random.Next(AimOffsetMinArcadePixels, AimOffsetMaxExclusiveArcadePixels);
        Rectangle bounds = field.Wall.PlayfieldBounds;
        if (_isMovingHorizontally)
        {
            int targetX = Math.Clamp(target.X + offset, bounds.X, bounds.Right - CollisionSize.Width);
            _direction = targetX <= _position.X ? Direction8.Left : Direction8.Right;
        }
        else
        {
            int targetY = target.Y + offset;
            if (targetY < bounds.Y) // The spot the hulk is aiming at is above the top wall, so it aims at the bottom wall instead.
            {
                targetY = bounds.Bottom - CollisionSize.Height;
            }

            _direction = targetY <= _position.Y ? Direction8.Up : Direction8.Down;
        }
    }

    /// <summary>Turns the hulk. A hulk that was walking sideways now walks up or down, and the other way about. It also picks at random how many steps it takes before its next turn.</summary>
    /// <param name="field">The playfield.</param>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>RRH11.ASM</c> <c>HULKND</c>/<c>HND10</c>. The walk starts again from its first
    /// animation frame.</item>
    /// <item>Disassembly: <c>asm/robomame.asm</c> <c>HULK_CHANGE_DIRECTION</c>.</item>
    /// </list>
    /// </remarks>
    private void Reaim(PlayField field)
    {
        _reaimStepsRemaining = RollReaimSteps();
        _isMovingHorizontally = !_isMovingHorizontally;
        PickDirection(field);
        _walkCycleStep = 0;
        _animationFrameIndex = GetFrames(_direction)[0];
    }

    /// <summary>Picks at random how many steps the hulk takes before its next turn.</summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>RRH11.ASM</c> <c>HULKND</c>.</item>
    /// <item>Disassembly: <c>asm/robomame.asm</c> <c>HULK_CHANGE_DIRECTION</c>.</item>
    /// </list>
    /// </remarks>
    private int RollReaimSteps() => _random.Next(ReaimStepsMin, ReaimStepsMaxExclusive);
}
