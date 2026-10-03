using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Graphics;
using Robotron2084.Level;
using Robotron2084.Tuning;

namespace Robotron2084.Entities;

/// <summary>A grunt is a slow, clumsy robot that shuffles towards you. It's the most common enemy in the game.</summary>
/// <seealso cref="PlayField"/>
/// <remarks>
/// <list type="bullet">
/// <item>Original source: <c>RRP8.ASM</c>, routine <c>ROBOT</c> (with <c>ROB0</c>..<c>ROB11</c> sub-blocks)</item>
/// <item>Disassembly: <c>asm/robomame.asm</c> at <c>$39E6</c> (grunt speed/movement timer check, part of the shared grunt/hulk/brain/prog/tank update loop)</item>
/// </list>
/// </remarks>
public sealed class Grunt : IEntity, IExplodable, IRemovable
{
    /// <summary>How many ROM frames one beat takes (4 vblanks).</summary>
    private const int BeatIntervalRomFrames = 4;

    /// <summary>The wave's re-roll limit when the caller gives none.</summary>
    private const int DefaultMoveLimitBeats = 15;

    /// <summary>How far a grunt steps sideways towards the player, in columns.</summary>
    /// <remarks>Original source: <c>RRP8.ASM</c> <c>ROB3</c> to <c>ROB4A</c>, a step of 2 columns. Disassembly: <c>MOVE_GRUNT</c> (<c>$39E6</c>) at <c>$3A15</c> and <c>$3A1D</c>.</remarks>
    private const int StepColumns = 2;

    /// <summary>How far a grunt steps up or down towards the player, in rows.</summary>
    /// <remarks>Original source: <c>RRP8.ASM</c> <c>ROB1B</c> to <c>ROB2A</c>, a step of 4 rows. Disassembly: <c>MOVE_GRUNT</c> (<c>$39E6</c>) at <c>$39F9</c> and <c>$3A01</c>.</remarks>
    private const int StepRows = 4;

    /// <summary>The up-and-down gap, in rows, at which a grunt level with the player stays put.</summary>
    /// <remarks>Original source: <c>RRP8.ASM</c> <c>ROB2</c> and the <c>CMPB #$FE</c> before <c>ROB2A</c>: a grunt only 1 row above or below the player does not move up or down. Disassembly: <c>MOVE_GRUNT</c> (<c>$39E6</c>) at <c>$39F5</c> and <c>$39FD</c>.</remarks>
    private const int StayPutGapRows = 1;

    /// <summary>The ROM's walk animation frames (RWDP1..4); the animation frame number wraps after the last.</summary>
    private const int WalkAnimationFrameCount = 4;

    /// <summary>The grunt sprite's own 10x13 arcade px box, in port pixels.</summary>
    private static readonly (int Width, int Height) CollisionSize =
        (ScreenSize.ToPortPixels(CollisionSizes.GruntCollisionSize.Width), ScreenSize.ToPortPixels(CollisionSizes.GruntCollisionSize.Height));

    /// <summary>One column, in port pixels.</summary>
    private static readonly int ColumnPixels = ScreenSize.ToPortPixelsFromColumns(1);

    /// <summary>One row, in port pixels.</summary>
    private static readonly int RowPixels = ScreenSize.ToPortPixels(1);

    /// <summary>How far one step moves the grunt sideways, in port pixels.</summary>
    private static readonly int StepXPixels = ScreenSize.ToPortPixelsFromColumns(StepColumns);

    /// <summary>How far one step moves the grunt up or down, in port pixels.</summary>
    private static readonly int StepYPixels = ScreenSize.ToPortPixels(StepRows);

    private readonly Random _random;
    private readonly SpriteSet _sprites;
    private int _beatTimer;

    private int _moveCountdownBeats;

    private int _moveLimitBeats;

    private IntVector2 _position;

    private int _walkAnimationFrameNumber = 1;

    /// <summary>Creates a grunt, with its first stagger already rolled.</summary>
    /// <param name="sprites">The shared sprite set.</param>
    /// <param name="position">Top-left of the grunt.</param>
    /// <param name="moveLimitBeats">This wave's re-roll limit: the upper bound of the random 1..N stagger.</param>
    /// <param name="random">The random source, or null to create one.</param>
    /// <remarks>ROM: <c>ROBSPD</c> — the stagger limit is a random 1..that many beats.</remarks>
    public Grunt(
        SpriteSet sprites,
        IntVector2 position,
        int moveLimitBeats = DefaultMoveLimitBeats,
        Random? random = null)
    {
        _sprites = sprites;
        _position = position;
        _moveLimitBeats = moveLimitBeats;
        _random = random ?? new Random();
        // The spawn countdown is a random 1..this wave's stagger limit, in beats.
        _moveCountdownBeats = _random.Next(1, _moveLimitBeats + 1);
    }

    /// <summary>The grunt sprite's own 10x13 box at <see cref="Position"/>.</summary>
    public Rectangle Bounds => new(_position.X, _position.Y, CollisionSize.Width, CollisionSize.Height);

    /// <summary>This grunt's current walk animation frame, for the appear and explosion effects.</summary>
    /// <returns>The texture for the current walk frame.</returns>
    public Texture2D CurrentAnimationFrame => _sprites.GruntAnimationFrames[GetAnimationFrameIndex(_walkAnimationFrameNumber)];

    /// <summary>Alive until shot or killed on contact; never Dying (see <see cref="Kill"/>).</summary>
    public EntityLifeState LifeState { get; private set; } = EntityLifeState.Alive;

    /// <summary>The current stagger limit, in ROM beats: the re-roll upper bound.</summary>
    public int MoveDelayBeats => _moveLimitBeats;

    /// <summary>True when the grunt took a step during the last update (it asks for the robot-move sound).</summary>
    public bool SteppedThisUpdate { get; private set; }

    // the ROM's walk animation frame 1..4; a freshly spawned grunt starts on animation frame 1
    /// <summary>Top-left of the grunt.</summary>
    /// <remarks>The ROM's OBJX/OBJY.</remarks>
    public IntVector2 Position => _position;

    /// <summary>The walk frame showing right now, 1..4 (test hook).</summary>
    /// <remarks>ROM RWDP animation frame.</remarks>
    internal int WalkAnimationFrameNumber => _walkAnimationFrameNumber;

    /// <summary>How many timer units between beats (a tick adds 5; an arcade frame is 6 units).</summary>
    private static readonly int BeatIntervalClockUnits = ArcadeClock.ToClockUnits(BeatIntervalRomFrames);

    /// <summary>Draws the current walk animation frame in its own colours.</summary>
    /// <param name="spriteBatch">The batch to draw into.</param>
    public void Draw(SpriteBatch spriteBatch)
    {
        if (LifeState != EntityLifeState.Alive)
        {
            return;
        }

        _sprites.Blitter.DrawSprite(spriteBatch, CurrentAnimationFrame, Bounds, Color.White);
    }

    /// <summary>Kills the grunt outright: no flash, no death animation.</summary>
    /// <remarks>ROM: RRP8.ASM's <c>ROBKIL</c> just explodes it.</remarks>
    public void Kill()
    {
        if (LifeState != EntityLifeState.Alive)
        {
            return;
        }

        LifeState = EntityLifeState.Dead;
    }

    /// <summary>Called when a grunt dies: drops this one's stagger limit to seven eighths of it.</summary>
    /// <param name="floorBeats">The wave's current floor, below which the limit must not go.</param>
    /// <remarks>The arcade takes seven eighths (truncated) only while that is still at or above the
    /// floor; otherwise it leaves the limit alone — it does not clamp it down to the floor. A pending
    /// countdown is untouched: each grunt picks the new limit up on its next re-roll.</remarks>
    public void SpeedUp(int floorBeats)
    {
        int next = _moveLimitBeats * 7 / 8;
        if (next >= floorBeats)
        {
            _moveLimitBeats = next;
        }
    }

    /// <summary>Runs one tick of the grunt. When its wait is over it takes a step towards the player and picks a new wait.</summary>
    /// <param name="gameTime">Unused — the beat is counted in ticks.</param>
    /// <param name="field">The playfield.</param>
    public void Update(GameTime gameTime, PlayField field)
    {
        SteppedThisUpdate = false;
        if (LifeState != EntityLifeState.Alive)
        {
            return;
        }

        if (field.RobotsFrozen)
        {
            return;
        }

        _beatTimer += ArcadeClock.UnitsPerPortTick;
        if (_beatTimer < BeatIntervalClockUnits)
        {
            return;
        }

        // One beat pass; only the step branch below advances the walk frame.
        _beatTimer -= BeatIntervalClockUnits;

        if (--_moveCountdownBeats > 0)
        {
            return;
        }

        _moveCountdownBeats = _random.Next(1, _moveLimitBeats + 1);
        SteppedThisUpdate = true;
        _walkAnimationFrameNumber = _walkAnimationFrameNumber % WalkAnimationFrameCount + 1; // DRAW_GRUNT: one frame per step

        StepTowardPlayer(field);
    }

    /// <summary>Divides, rounding towards the lower number, so a gap in pixels becomes a whole number of columns or rows.</summary>
    /// <param name="pixels">The gap in port pixels, which may be negative.</param>
    /// <param name="unitPixels">How many port pixels make one column or one row.</param>
    private static int DivideRoundingDown(int pixels, int unitPixels) =>
        (int)Math.Floor(pixels / (double)unitPixels);

    /// <summary>Takes one step towards the player on each axis. A step that would leave the playfield is not taken.</summary>
    /// <param name="field">The playfield, which holds the player and the walls.</param>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>RRP8.ASM</c> <c>ROB1B</c> to <c>ROB4A</c></item>
    /// <item>Disassembly: <c>MOVE_GRUNT</c> (<c>$39E6</c>) from <c>$39EF</c> to <c>$3A29</c></item>
    /// </list>
    /// The two directions are decided separately. Sideways, a grunt always steps: towards the player, or to the right when the columns match.
    /// Up and down, a grunt level with the player steps down, and only a gap of exactly one row is ignored.
    /// </remarks>
    private void StepTowardPlayer(PlayField field)
    {
        Rectangle bounds = field.Wall.PlayfieldBounds;
        IntVector2 player = field.Player.Position;
        int columnsRightOfPlayer = DivideRoundingDown(_position.X - player.X, ColumnPixels);
        int rowsBelowPlayer = DivideRoundingDown(_position.Y - player.Y, RowPixels);

        int dx = columnsRightOfPlayer > 0 ? -StepXPixels : StepXPixels;
        int dy = 0;
        if (rowsBelowPlayer > 0)
        {
            dy = rowsBelowPlayer > StayPutGapRows ? -StepYPixels : 0;
        }
        else if (rowsBelowPlayer != -StayPutGapRows)
        {
            dy = StepYPixels;
        }

        int y = _position.Y + dy;
        if (dy != 0 && y >= bounds.Y && y + CollisionSize.Height <= bounds.Bottom)
        {
            _position = _position with { Y = y };
        }

        int x = _position.X + dx;
        if (x >= bounds.X && x + CollisionSize.Width <= bounds.Right)
        {
            _position = _position with { X = x };
        }
    }

    /// <summary>Called on the level-progress tick: drops the stagger limit by the wave's step.</summary>
    /// <param name="floorBeats">The wave's floor; the limit never goes below it.</param>
    /// <param name="stepBeats">How much to drop the limit by; the ROM alternates 4, then 2.</param>
    /// <remarks>The arcade clamps the limit to the floor, which descends to 1: 4 arcade px a beat is
    /// the player's own 1 px a frame. A pending countdown finishes before the new limit applies.</remarks>
    public void WaveSpeedTick(int floorBeats, int stepBeats = 4)
    {
        _moveLimitBeats = Math.Max(floorBeats, _moveLimitBeats - stepBeats);
    }

    /// <summary>Maps the ROM's walk animation frame number (1..4) to an index into <see cref="SpriteSet.GruntAnimationFrames"/>.</summary>
    /// <param name="romAnimationFrameNumber">The ROM's walk animation frame number, 1..4.</param>
    /// <returns>The index into <see cref="SpriteSet.GruntAnimationFrames"/>.</returns>
    /// <remarks>Walk numbers 1/2/3/4 map to animation frames 1/2/1/3, so only three are unique.</remarks>
    internal static int GetAnimationFrameIndex(int romAnimationFrameNumber) => romAnimationFrameNumber switch
    {
        2 => 1,
        4 => 2,
        _ => 0,
    };
}
