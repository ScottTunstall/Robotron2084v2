using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Level;
using Robotron2084.Rendering;
using Robotron2084.Tuning;

namespace Robotron2084.Entities;

/// <summary>A grunt — the basic robot. It lumbers toward the player in staggered bursts.</summary>
/// <seealso cref="PlayField"/>
/// <remarks>ROM: RRP8.ASM's <c>ROBOT</c> and its <c>ROB0</c>..<c>ROB11</c> sub-blocks — the arcade's
/// "Ground Roving UNit Terminator". Its beat runs every 4 vblanks, counting down a random
/// 1..this wave's limit that is re-rolled after each step; it steps 4 arcade px on any axis it is
/// more than 2 arcade px from the player (the axes are independent). The walk frame advances only on
/// a step, and the survivors' speed-up is <c>RMXSPD</c> (notes §29). Timers count 5 per tick and 6 per
/// arcade frame, so an interval of N frames is due at 6 x N.</remarks>
public sealed class Grunt : IEntity, IExplodable
{
    /// <summary>The grunt picture's own 10x13 arcade px box, in port pixels.</summary>
    private static readonly (int Width, int Height) CollisionSize =
        (ScreenSize.Scaled(GameplayConstants.GruntCollisionSize.Width), ScreenSize.Scaled(GameplayConstants.GruntCollisionSize.Height));

    /// <summary>How far one step moves the grunt on each active axis, in port pixels (4 arcade px).</summary>
    private const int StepScreenPixels = 8;

    /// <summary>The per-axis dead zone, in port pixels (2 arcade px).</summary>
    private const int DeadZoneScreenPixels = 4;

    /// <summary>How many ROM frames one beat takes (4 vblanks).</summary>
    private const int BeatIntervalRomTicks = 4;

    /// <summary>How many timer units between beats (a tick adds 5; an arcade frame is 6 units).</summary>
    private static int BeatPeriod => BeatIntervalRomTicks * 6;

    private readonly Random _random;
    private IntVector2 _position;
    private int _moveLimitBeats;
    private int _beatTimer;
    private int _moveCountdownBeats;
    private int _walkFrame = 1; // walk frame 1..4; a freshly spawned grunt starts on frame 1

    /// <summary>Creates a grunt, with its first stagger already rolled.</summary>
    /// <param name="position">Top-left of the grunt.</param>
    /// <param name="moveLimitBeats">This wave's re-roll limit: the upper bound of the random 1..N stagger.</param>
    /// <param name="speedBonus">Unused (kept for the uniform spawn shape).</param>
    /// <param name="random">The random source, or null to create one.</param>
    /// <remarks>ROM: <c>ROBSPD</c> — the stagger limit is a random 1..that many beats.</remarks>
    public Grunt(IntVector2 position, int moveLimitBeats = 15, int speedBonus = 0, Random? random = null)
    {
        _position = position;
        _moveLimitBeats = moveLimitBeats;
        _random = random ?? new Random();
        // The spawn countdown is a random 1..this wave's stagger limit, in beats.
        _moveCountdownBeats = _random.Next(1, _moveLimitBeats + 1);
    }

    /// <summary>Top-left of the grunt.</summary>
    /// <remarks>The ROM's OBJX/OBJY.</remarks>
    public IntVector2 Position => _position;

    /// <summary>The grunt picture's own 10x13 box at <see cref="Position"/>.</summary>
    public Rectangle Bounds => new(_position.X, _position.Y, CollisionSize.Width, CollisionSize.Height);

    /// <summary>Alive until shot or killed on contact; never Dying (see <see cref="Kill"/>).</summary>
    public EntityLifeState LifeState { get; private set; } = EntityLifeState.Alive;

    /// <summary>The current stagger limit, in ROM beats: the re-roll upper bound.</summary>
    public int MoveDelayBeats => _moveLimitBeats;

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

    /// <summary>Called on the level-progress tick: drops the stagger limit by the wave's step.</summary>
    /// <param name="floorBeats">The wave's floor; the limit never goes below it.</param>
    /// <param name="stepBeats">How much to drop the limit by; the ROM alternates 4, then 2.</param>
    /// <remarks>The arcade clamps the limit to the floor, which descends to 1: 4 arcade px a beat is
    /// the player's own 1 px a frame. A pending countdown finishes before the new limit applies.</remarks>
    public void WaveSpeedTick(int floorBeats, int stepBeats = 4)
    {
        _moveLimitBeats = Math.Max(floorBeats, _moveLimitBeats - stepBeats);
    }

    /// <summary>Kills the grunt outright: no flash, no death animation.</summary>
    /// <remarks>ROM: RRP8.ASM's <c>ROBKIL</c> just explodes it.</remarks>
    public void Kill()
    {
        LifeState = EntityLifeState.Dead;
    }

    /// <summary>Runs the beat: counts the stagger down, then steps and re-rolls it.</summary>
    /// <param name="gameTime">Unused — the beat is counted in ticks.</param>
    /// <param name="field">The playfield.</param>
    public void Update(GameTime gameTime, PlayField field)
    {
        if (LifeState == EntityLifeState.Dead)
        {
            return;
        }

        if (field.RobotsFrozen)
        {
            return;
        }

        _beatTimer += 5;
        if (_beatTimer < BeatPeriod)
        {
            return;
        }

        // One beat pass; only the step branch below advances the walk frame.
        _beatTimer -= BeatPeriod;

        if (--_moveCountdownBeats > 0)
        {
            return;
        }

        _moveCountdownBeats = _random.Next(1, _moveLimitBeats + 1);
        _walkFrame = _walkFrame % 4 + 1; // DRAW_GRUNT: one frame per step

        // Per-axis step toward the player, with a dead zone; the axes are independent.
        Rectangle bounds = field.Wall.PlayfieldBounds;
        IntVector2 player = field.Player.Position;
        int dx = Math.Sign(player.X - _position.X) * StepScreenPixels;
        int dy = Math.Sign(player.Y - _position.Y) * StepScreenPixels;
        _position = new IntVector2(
            Math.Abs(player.X - _position.X) > DeadZoneScreenPixels ? _position.X + dx : _position.X,
            Math.Abs(player.Y - _position.Y) > DeadZoneScreenPixels ? _position.Y + dy : _position.Y);
        _position = new IntVector2(
            Math.Clamp(_position.X, bounds.X, bounds.Right - CollisionSize.Width),
            Math.Clamp(_position.Y, bounds.Y, bounds.Bottom - CollisionSize.Height));
    }

    /// <summary>The walk frame showing right now, 1..4 (test hook).</summary>
    /// <remarks>ROM RWDP frame.</remarks>
    internal int WalkFrame => _walkFrame;

    /// <summary>Maps a walk frame (1..4) to an index into <see cref="SpriteSet.GruntFrames"/>.</summary>
    /// <param name="romFrame">The walk frame, 1..4.</param>
    /// <returns>The index into <see cref="SpriteSet.GruntFrames"/>.</returns>
    /// <remarks>Walk frames 1/2/3/4 map to pictures 1/2/1/3, so only three pictures are unique.</remarks>
    internal static int WalkArtIndex(int romFrame) => romFrame switch
    {
        2 => 1,
        4 => 2,
        _ => 0,
    };

    /// <summary>This grunt's current walk picture, for the appear and explosion effects.</summary>
    /// <param name="sprites">The shared sprite set.</param>
    /// <returns>The texture for the current walk frame.</returns>
    public Texture2D CurrentFrameArt(SpriteSet sprites)
        => sprites.GruntFrames[WalkArtIndex(_walkFrame)];

    /// <summary>Draws the current walk picture in the art's own colours.</summary>
    /// <param name="spriteBatch">The batch to draw into.</param>
    /// <param name="sprites">The shared sprite set.</param>
    public void Draw(SpriteBatch spriteBatch, SpriteSet sprites)
    {
        if (LifeState == EntityLifeState.Dead)
        {
            return;
        }

        sprites.DrawSprite(spriteBatch, sprites.GruntFrames[WalkArtIndex(_walkFrame)], Bounds, Color.White);
    }
}
