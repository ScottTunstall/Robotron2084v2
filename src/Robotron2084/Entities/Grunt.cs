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
/// It acts on a beat. The <see cref="PlayField"/> calls <see cref="Update"/> on every tick, through
/// <see cref="FieldEntities"/> and <see cref="PlayField.UpdateEntity"/>. There are two times it does not: while the
/// grunt is still appearing at the start of a wave, and during the short freeze just after the player is killed.
/// <see cref="_beatTimer"/> gathers the ticks until it is time for the next beat (see <see cref="ArcadeClock"/>).
///
/// <list type="bullet">
/// <item>Original source: <c>RRP8.ASM</c>, routine <c>ROBOT</c> (with <c>ROB0</c>..<c>ROB11</c> sub-blocks)</item>
/// <item>Disassembly: <c>asm/robomame.asm</c> at <c>$39E6</c> (the check of the grunt's move timer, which is part of
/// the update loop that the grunt, hulk, brain, prog and tank share)</item>
/// </list>
/// </remarks>
public sealed class Grunt : IExplodable, IRemovable, IWaveStartRobot
{
    /// <summary>How long the arcade's routine sleeps between one look at whether the game is live and the next, in 50ths of a second. It decides how long after the game goes live the first beat comes (<see cref="BeginPlay"/>).</summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>RRP8.ASM</c> <c>ROBOT</c>, <c>BITA #$7F / BEQ ROB0A / NAP 2,ROBOT</c>.</item>
    /// <item>Disassembly: <c>$39B7</c>.</item>
    /// </list>
    /// </remarks>
    private const int LivePollRomFrames = 2;

    /// <summary>How long the arcade's routine sleeps after the look that finds the game live, before the first beat, in 50ths of a second.</summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>RRP8.ASM</c> <c>ROB0A</c>, <c>NAP 10,ROB0</c>.</item>
    /// <item>Disassembly: <c>$39B7</c> onwards.</item>
    /// </list>
    /// </remarks>
    private const int FirstBeatNapRomFrames = 10;

    /// <summary>How long one beat lasts, in 50ths of a second.</summary>
    private const int BeatIntervalRomFrames = 4;

    /// <summary>The most beats a grunt waits between steps, when it is not told a number.</summary>
    private const int DefaultMoveLimitBeats = 15;

    /// <summary>How many walk animation frames a grunt has. <see cref="_walkAnimationFrameNumber"/> counts up to this and then goes back to the first.</summary>
    private const int WalkAnimationFrameCount = 4;

    /// <summary>How big the grunt is, in port pixels. It is the size of the grunt's sprite, and it is used to tell what the grunt touches.</summary>
    private static readonly (int Width, int Height) CollisionSize =
        (ScreenSize.ToPortPixels(CollisionSizes.GruntCollisionSize.Width), ScreenSize.ToPortPixels(CollisionSizes.GruntCollisionSize.Height));

    private readonly Random _random;
    private readonly SpriteSet _sprites;
    private int _beatTimer;

    private int _moveCountdownBeats;

    private int _landingY;

    private int _moveLimitBeats;

    private IntVector2 _position;

    private int _walkAnimationFrameNumber = 1; // Which walk animation frame is showing. The first one is number 1, as in the arcade.

    /// <summary>Makes a grunt, and picks at random how long it waits before its first step.</summary>
    /// <param name="sprites">The shared sprite set.</param>
    /// <param name="position">Where the grunt's top-left corner is.</param>
    /// <param name="moveLimitBeats">The most beats a grunt waits between steps on this wave. Each wait is a random number of beats from 1 up to this.</param>
    /// <param name="random">Where its random numbers come from. If this is null, the grunt makes its own.</param>
    /// <remarks>ROM: <c>ROBSPD</c>.</remarks>
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
        // The grunt waits a random number of beats before its first step.
        _moveCountdownBeats = _random.Next(1, _moveLimitBeats + 1);
    }

    /// <summary>The box the grunt takes up on the screen. It is used to tell what the grunt touches.</summary>
    public Rectangle GetBounds() => new(_position.X, _position.Y, CollisionSize.Width, CollisionSize.Height);

    /// <summary>This grunt's current walk animation frame, for the appear and explosion effects.</summary>
    /// <returns>The animation frame that is showing.</returns>
    public Texture2D GetCurrentAnimationFrame() =>
        _sprites.GruntAnimationFrames[GetAnimationFrameIndex(_walkAnimationFrameNumber)];

    /// <summary>Alive until it is killed. It is never Dying, because it has no death animation (see <see cref="Kill"/>).</summary>
    public EntityLifeState LifeState { get; private set; } = EntityLifeState.Alive;

    /// <summary>The most beats the grunt waits between steps, as it stands now. It gets smaller as the wave goes on.</summary>
    public int MoveDelayBeats => _moveLimitBeats;

    /// <summary>True while the grunt is still falling after being dropped by Gorf. It does not move on until it lands.</summary>
    public bool IsFalling() => _position.Y < _landingY;

    /// <summary>True when the grunt took a step during the last update. The playfield uses it to play the sound of the robots moving.</summary>
    public bool SteppedThisUpdate { get; private set; }

    /// <summary>Where the grunt's top-left corner is.</summary>
    /// <remarks>The ROM's OBJX/OBJY.</remarks>
    public IntVector2 Position => _position;

    /// <summary>Which walk animation frame is showing, counting from 1. Tests use this.</summary>
    /// <remarks>ROM: the <c>RWDP</c> animation frames.</remarks>
    internal int WalkAnimationFrameNumber => _walkAnimationFrameNumber;

    /// <summary>The time from one beat to the next, in clock units (see <see cref="ArcadeClock"/>). <see cref="_beatTimer"/> counts up to this. When it gets there, a beat happens and this is taken off it.</summary>
    private static readonly int BeatIntervalClockUnits = ArcadeClock.ToClockUnits(BeatIntervalRomFrames);

    /// <summary>Draws the current walk animation frame in its own colours.</summary>
    /// <param name="spriteBatch">What the grunt is drawn with.</param>
    public void Draw(SpriteBatch spriteBatch)
    {
        if (!this.IsAlive())
        {
            return;
        }

        _sprites.Blitter.DrawSprite(spriteBatch, GetCurrentAnimationFrame(), GetBounds(), Color.White);
    }

    /// <summary>Lets the grunt fall from where it is to the ground, as when Gorf drops it. It waits there until it lands.</summary>
    /// <param name="landingY">The top of the grunt when it is standing on the ground, in port pixels.</param>
    /// <remarks>This fall is not in the arcade game. It belongs to Gorf, the author's own robot (notes §138.2).</remarks>
    internal void BeginFall(int landingY) => _landingY = landingY;

    /// <summary>Kills the grunt at once, with no flash and no death animation.</summary>
    /// <remarks>ROM: RRP8.ASM's <c>ROBKIL</c>, which just explodes it.</remarks>
    public void Kill()
    {
        if (!this.IsAlive())
        {
            return;
        }

        LifeState = EntityLifeState.Dead;
    }

    /// <summary>Speeds this grunt up when any grunt is killed. The most beats it waits between steps is cut to seven eighths of what it was.</summary>
    /// <param name="floorBeats">The smallest that number is allowed to be on this wave.</param>
    /// <remarks>The arcade only makes the cut when the smaller number, rounded down, is still at least
    /// <paramref name="floorBeats"/>. Otherwise it leaves the number alone. It does not set it to
    /// <paramref name="floorBeats"/>. A wait that has already started is not changed. The grunt uses the new number
    /// when it next picks a wait.</remarks>
    public void SpeedUp(int floorBeats)
    {
        int next = _moveLimitBeats * 7 / 8;
        if (next >= floorBeats)
        {
            _moveLimitBeats = next;
        }
    }

    /// <summary>Sets the time of the grunt's first beat, on the tick the game goes live. The beat timer is set so that it comes due when that time has gone by.</summary>
    /// <param name="field">The playfield, which works out how long the wait is.</param>
    public void BeginPlay(PlayField field) =>
        _beatTimer = BeatIntervalClockUnits - field.GetClockUnitsToFirstBeat(LivePollRomFrames, FirstBeatNapRomFrames);

    /// <summary>Runs one tick of the grunt. When its wait is over it takes a step towards the player and picks a new wait.</summary>
    /// <param name="gameTime">Not used. The grunt counts ticks.</param>
    /// <param name="field">The playfield.</param>
    public void Update(GameTime gameTime, PlayField field)
    {
        SteppedThisUpdate = false;
        if (!this.IsAlive())
        {
            return;
        }

        if (field.RobotsFrozen())
        {
            return;
        }

        if (IsFalling())
        {
            _position = _position with { Y = Math.Min(_landingY, _position.Y + GorfTuning.FallPixelsPerTick) };
            return;
        }

        _beatTimer += ArcadeClock.UnitsPerPortTick;
        if (_beatTimer < BeatIntervalClockUnits)
        {
            return;
        }

        // A beat has come, which is the grunt's turn to act (see ArcadeClock). It only takes a step on the beat when its countdown runs out.
        _beatTimer -= BeatIntervalClockUnits;

        if (--_moveCountdownBeats > 0)
        {
            return;
        }

        _moveCountdownBeats = _random.Next(1, _moveLimitBeats + 1);
        SteppedThisUpdate = true;
        _walkAnimationFrameNumber = _walkAnimationFrameNumber % WalkAnimationFrameCount + 1; // Each step shows the next walk animation frame (DRAW_GRUNT).

        _position = GruntChaseStep.GetNextPosition(_position, field.Player.Position, field.Wall.PlayfieldBounds, CollisionSize);
    }

    /// <summary>Speeds the grunt up as the wave goes on, by taking some beats off the most it waits between steps.</summary>
    /// <param name="floorBeats">The smallest that number is allowed to be on this wave.</param>
    /// <param name="stepBeats">How many beats to take off. The arcade takes off 4, then 2, then 4, and so on.</param>
    /// <remarks>Here the arcade does stop the number at <paramref name="floorBeats"/>, which itself goes down as far as 1.
    /// A grunt that steps on every beat goes as fast as the player. A wait that has already started is finished
    /// before the new number is used.</remarks>
    public void WaveSpeedTick(int floorBeats, int stepBeats = 4)
    {
        _moveLimitBeats = Math.Max(floorBeats, _moveLimitBeats - stepBeats);
    }

    /// <summary>Works out which of the grunt's animation frames to draw, from the arcade's walk number.</summary>
    /// <param name="romAnimationFrameNumber">The arcade's walk number, from 1 to 4.</param>
    /// <returns>The place of the animation frame in <see cref="SpriteSet.GruntAnimationFrames"/>.</returns>
    /// <remarks>Walk numbers 1, 2, 3 and 4 show the first, second, first and third animation frames, so there are only three different ones.</remarks>
    internal static int GetAnimationFrameIndex(int romAnimationFrameNumber) => romAnimationFrameNumber switch
    {
        2 => 1,
        4 => 2,
        _ => 0,
    };
}
