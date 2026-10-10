using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Graphics;
using Robotron2084.Level;
using Robotron2084.Tuning;

namespace Robotron2084.Entities;

/// <summary>A BerzerkRobot is a robot that shuffles towards you exactly as a grunt does. It cannot shoot yet.</summary>
/// <seealso cref="Grunt"/>
/// <remarks>
/// It acts on a beat. The <see cref="PlayField"/> calls <see cref="Update"/> on every tick, through
/// <see cref="FieldEntities"/> and <see cref="PlayField.UpdateEntity"/>.
/// There are two times it does not: while the robot is still appearing at the start of a wave, and during the
/// short freeze just after the player is killed.
///
/// <see cref="_beatTimer"/> gathers the ticks
/// until it is time for the next beat (see <see cref="ArcadeClock"/>).
///
/// This robot is the author's own. It is not in the arcade game (notes §138). It moves as a grunt does (<see cref="GruntChaseStep"/>),
/// with the same time between beats and the same wait between steps. It dies as a grunt does, to a laser or by walking onto an
/// electrode, and it kills the player by touching them. It has its own look. Until it first moves, it stands still and plays its
/// standing animation. After that it shows the walk for the way it is facing (<see cref="GetWalkSequenceTowards"/>).
/// It cannot shoot yet. Shooting is planned.
/// </remarks>
public sealed class BerzerkRobot : IExplodable, IRemovable, IWaveStartRobot
{
    /// <summary>How long the robot waits between one look at whether the game is live and the next, in 50ths of a second. It decides how long after the game goes live the first beat comes (<see cref="BeginPlay"/>).</summary>
    /// <remarks>
    /// This number is not from the arcade (FID-4a), because this robot is not in the arcade game. The author chose it
    /// to be like the grunt's (<see cref="Grunt"/>).
    /// </remarks>
    private const int LivePollRomFrames = 2;

    /// <summary>How long the robot waits after the look that finds the game live, before its first beat, in 50ths of a second.</summary>
    /// <remarks>
    /// This number is not from the arcade (FID-4a), because this robot is not in the arcade game. The author chose it
    /// to be like the grunt's (<see cref="Grunt"/>).
    /// </remarks>
    private const int FirstBeatNapRomFrames = 10;

    /// <summary>How long one beat lasts, in 50ths of a second. It is the same as a grunt's.</summary>
    private const int BeatIntervalRomFrames = 4;

    /// <summary>The most beats the robot waits between steps, when it is not told a number. It is the same as a grunt's.</summary>
    private const int DefaultMoveLimitBeats = 15;

    /// <summary>How many animation frames each sideways walk has. They are shown in order: the first, then the second.</summary>
    private const int SidewaysWalkFrameCount = 2;

    /// <summary>The order the up and down walks show their three animation frames in: first, second, third, second.</summary>
    private static readonly int[] UpAndDownWalkOrder = [0, 1, 2, 1];

    /// <summary>How big the robot is, in port pixels. This size is used to tell what the robot touches.</summary>
    private static readonly (int Width, int Height) CollisionSize =
        (ScreenSize.ToPortPixelsFromArcadePixels(CollisionSizes.BerzerkRobotCollisionSize.Width), ScreenSize.ToPortPixelsFromArcadePixels(CollisionSizes.BerzerkRobotCollisionSize.Height));

    /// <summary>The time from one beat to the next, in clock units (see <see cref="ArcadeClock"/>). <see cref="_beatTimer"/> counts up to this. When it gets there, a beat happens and this is taken off it.</summary>
    private static readonly int BeatIntervalClockUnits = ArcadeClock.ToClockUnits(BeatIntervalRomFrames);

    private readonly Random _random;
    private readonly SpriteSet _sprites;
    private int _animationStep;
    private int _beatTimer;
    private WalkSequence _walkSequence = WalkSequence.Down;
    private bool _hasMoved;
    private int _moveCountdownBeats;
    private int _moveLimitBeats;
    private IntVector2 _position;

    /// <summary>Makes a robot, and picks at random how long it waits before its first step.</summary>
    /// <param name="sprites">The shared sprite set.</param>
    /// <param name="position">Where the robot's top-left corner is.</param>
    /// <param name="moveLimitBeats">The most beats the robot waits between steps. Each wait is a random number of beats from 1 up to this, as a grunt's is.</param>
    /// <param name="random">Where its random numbers come from. If this is null, the robot makes its own.</param>
    public BerzerkRobot(
        SpriteSet sprites,
        IntVector2 position,
        int moveLimitBeats = DefaultMoveLimitBeats,
        Random? random = null)
    {
        _sprites = sprites;
        _position = position;
        _moveLimitBeats = moveLimitBeats;
        _random = random ?? new Random();
        _moveCountdownBeats = _random.Next(1, _moveLimitBeats + 1);
    }

    /// <summary>The box the robot takes up on the screen. It is used to tell what the robot touches.</summary>
    public Rectangle GetBounds() => new(_position.X, _position.Y, CollisionSize.Width, CollisionSize.Height);

    /// <summary>The way the robot is walking.</summary>
    public WalkSequence WalkSequence => _walkSequence;

    /// <summary>Alive until it is killed. It is never Dying, because it has no death animation (see <see cref="Kill"/>).</summary>
    public EntityLifeState LifeState { get; private set; } = EntityLifeState.Alive;

    /// <summary>The most beats the robot waits between steps.</summary>
    public int MoveDelayBeats => _moveLimitBeats;

    /// <summary>Where the robot's top-left corner is.</summary>
    public IntVector2 Position => _position;

    /// <summary>True when the robot took a step during the last update.</summary>
    public bool SteppedThisUpdate { get; private set; }

    /// <summary>Gets the animation frame the robot is showing, for drawing and for the appear and explosion effects.</summary>
    /// <returns>The animation frame that is showing.</returns>
    public Texture2D GetCurrentAnimationFrame()
    {
        if (!_hasMoved)
        {
            return _sprites.BerzerkRobotIdleFrames[_animationStep % _sprites.BerzerkRobotIdleFrames.Length];
        }

        Texture2D[] frames = GetWalkFrames(_walkSequence);
        return frames[GetWalkFrameIndex(_walkSequence, _animationStep)];
    }

    /// <summary>Draws the current animation frame in its own colours.</summary>
    /// <param name="spriteBatch">What the robot is drawn with.</param>
    public void Draw(SpriteBatch spriteBatch)
    {
        if (!this.IsAlive())
        {
            return;
        }

        _sprites.Blitter.DrawSprite(spriteBatch, GetCurrentAnimationFrame(), GetBounds(), Color.White);
    }

    /// <summary>Kills the robot at once, with no flash and no death animation.</summary>
    public void Kill()
    {
        if (!this.IsAlive())
        {
            return;
        }

        LifeState = EntityLifeState.Dead;
    }

    /// <summary>Sets the time of the robot's first beat, on the tick the game goes live. The beat timer is set so that it comes due when that time has gone by.</summary>
    /// <param name="field">The playfield, which works out how long the wait is.</param>
    public void BeginPlay(PlayField field) =>
        _beatTimer = BeatIntervalClockUnits - field.GetClockUnitsToFirstBeat(LivePollRomFrames, FirstBeatNapRomFrames);

    /// <summary>Runs one tick. When its wait is over the robot takes a step towards the player and picks a new wait.</summary>
    /// <param name="gameTime">Not used. The robot counts ticks.</param>
    /// <param name="field">The playfield.</param>
    public void Update(GameTime gameTime, PlayField field)
    {
        SteppedThisUpdate = false;
        if (!this.IsAlive() || field.RobotsFrozen())
        {
            return;
        }

        _beatTimer += ArcadeClock.UnitsPerPortTick;
        if (_beatTimer < BeatIntervalClockUnits)
        {
            return;
        }

        _beatTimer -= BeatIntervalClockUnits;
        if (!_hasMoved)
        {
            _animationStep++;
        }

        if (--_moveCountdownBeats > 0)
        {
            return;
        }

        _moveCountdownBeats = _random.Next(1, _moveLimitBeats + 1);
        Step(field);
    }

    /// <summary>Works out which animation frame of a walk to show.</summary>
    /// <param name="walkSequence">The way the robot is walking.</param>
    /// <param name="animationStep">How many steps the robot has taken.</param>
    /// <returns>The place of the animation frame in that walk's set.</returns>
    /// <remarks>A sideways walk shows the first, then the second. A walk up or down shows the first, second, third, then the second again. This order is the author's (notes §138).</remarks>
    internal static int GetWalkFrameIndex(WalkSequence walkSequence, int animationStep) => walkSequence switch
    {
        WalkSequence.Left or WalkSequence.Right => animationStep % SidewaysWalkFrameCount,
        _ => UpAndDownWalkOrder[animationStep % UpAndDownWalkOrder.Length],
    };

    /// <summary>Works out which way the robot faces. It faces sideways if the player is at least as far away sideways as up or down. Otherwise it faces up or down.</summary>
    /// <param name="from">The robot's top-left corner.</param>
    /// <param name="playerPosition">The player's top-left corner.</param>
    /// <returns>The walk for the way the robot faces.</returns>
    internal static WalkSequence GetWalkSequenceTowards(IntVector2 from, IntVector2 playerPosition)
    {
        int gapX = playerPosition.X - from.X;
        int gapY = playerPosition.Y - from.Y;
        if (Math.Abs(gapX) >= Math.Abs(gapY))
        {
            return gapX >= 0 ? WalkSequence.Right : WalkSequence.Left;
        }

        return gapY >= 0 ? WalkSequence.Down : WalkSequence.Up;
    }

    /// <summary>Gets the set of animation frames for a walk.</summary>
    /// <param name="walkSequence">The way the robot is walking.</param>
    private Texture2D[] GetWalkFrames(WalkSequence walkSequence) => walkSequence switch
    {
        WalkSequence.Right => _sprites.BerzerkRobotWalkRightFrames,
        WalkSequence.Left => _sprites.BerzerkRobotWalkLeftFrames,
        WalkSequence.Up => _sprites.BerzerkRobotWalkUpFrames,
        _ => _sprites.BerzerkRobotWalkDownFrames,
    };

    /// <summary>Takes one step towards the player, then turns to face the player.</summary>
    /// <param name="field">The playfield.</param>
    private void Step(PlayField field)
    {
        IntVector2 playerPosition = field.Player.Position;
        SteppedThisUpdate = true;
        _hasMoved = true;
        _animationStep++;
        _position = GruntChaseStep.GetNextPosition(_position, playerPosition, field.Wall.PlayfieldBounds, CollisionSize);
        _walkSequence = GetWalkSequenceTowards(_position, playerPosition);
    }
}
