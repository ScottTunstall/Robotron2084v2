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
/// A new kind of robot of the author's own, with no arcade routine behind it (notes §138). It moves as a grunt does
/// (<see cref="GruntChaseStep"/>, the same four-frame beat and the same wait), dies as one does, to a laser or by walking onto an
/// electrode, and kills the player on touch. Its look is its own: it stands still in a six-frame cycle until it first moves,
/// then shows the walk for the way it is heading, which it works out from which gap to the player is larger.
/// Shooting is planned and not built.
/// </remarks>
public sealed class BerzerkRobot : IExplodable, IRemovable
{
    /// <summary>How many ROM frames one beat takes, the same as a grunt's.</summary>
    private const int BeatIntervalRomFrames = 4;

    /// <summary>The wave's re-roll limit when the caller gives none, the same as a grunt's.</summary>
    private const int DefaultMoveLimitBeats = 15;

    /// <summary>How many frames the sideways walks have. They play in order, 1 then 2.</summary>
    private const int SidewaysWalkFrameCount = 2;

    /// <summary>The order the up and down walks play their three frames in: 1, 2, 3, 2.</summary>
    private static readonly int[] UpAndDownWalkOrder = [0, 1, 2, 1];

    /// <summary>The robot's own box, in port pixels.</summary>
    private static readonly (int Width, int Height) CollisionSize =
        (ScreenSize.ToPortPixels(CollisionSizes.BerzerkRobotCollisionSize.Width), ScreenSize.ToPortPixels(CollisionSizes.BerzerkRobotCollisionSize.Height));

    /// <summary>How many timer units between beats (a tick adds 5; an arcade frame is 6 units).</summary>
    private static readonly int BeatIntervalClockUnits = ArcadeClock.ToClockUnits(BeatIntervalRomFrames);

    private readonly Random _random;
    private readonly SpriteSet _sprites;
    private int _animationStep;
    private int _beatTimer;
    private BerzerkRobotFacing _facing = BerzerkRobotFacing.Down;
    private bool _hasMoved;
    private int _moveCountdownBeats;
    private int _moveLimitBeats;
    private IntVector2 _position;

    /// <summary>Creates a robot, with its first wait already rolled.</summary>
    /// <param name="sprites">The shared sprite set.</param>
    /// <param name="position">Top-left of the robot.</param>
    /// <param name="moveLimitBeats">The upper bound of the random 1..N wait between steps, as a grunt's.</param>
    /// <param name="random">The random source, or null to create one.</param>
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

    /// <summary>The robot's own box at <see cref="Position"/>.</summary>
    public Rectangle Bounds => new(_position.X, _position.Y, CollisionSize.Width, CollisionSize.Height);

    /// <summary>The way the robot is walking.</summary>
    public BerzerkRobotFacing Facing => _facing;

    /// <summary>Alive until shot or killed on contact; never Dying (see <see cref="Kill"/>).</summary>
    public EntityLifeState LifeState { get; private set; } = EntityLifeState.Alive;

    /// <summary>The upper bound of the wait between steps, in beats.</summary>
    public int MoveDelayBeats => _moveLimitBeats;

    /// <summary>Top-left of the robot.</summary>
    public IntVector2 Position => _position;

    /// <summary>True when the robot took a step during the last update.</summary>
    public bool SteppedThisUpdate { get; private set; }

    /// <summary>Gets the animation frame the robot is showing, for drawing and for the appear and explosion effects.</summary>
    /// <returns>The texture for the current frame.</returns>
    public Texture2D GetCurrentAnimationFrame()
    {
        if (!_hasMoved)
        {
            return _sprites.BerzerkRobotIdleFrames[_animationStep % _sprites.BerzerkRobotIdleFrames.Length];
        }

        Texture2D[] frames = GetWalkFrames(_facing);
        return frames[GetWalkFrameIndex(_facing, _animationStep)];
    }

    /// <summary>Draws the current animation frame in its own colours.</summary>
    /// <param name="spriteBatch">The batch to draw into.</param>
    public void Draw(SpriteBatch spriteBatch)
    {
        if (!this.IsAlive())
        {
            return;
        }

        _sprites.Blitter.DrawSprite(spriteBatch, GetCurrentAnimationFrame(), Bounds, Color.White);
    }

    /// <summary>Kills the robot outright: no flash, no death animation.</summary>
    public void Kill()
    {
        if (!this.IsAlive())
        {
            return;
        }

        LifeState = EntityLifeState.Dead;
    }

    /// <summary>Runs one tick. When its wait is over the robot takes a step towards the player and picks a new wait.</summary>
    /// <param name="gameTime">Unused — the beat is counted in ticks.</param>
    /// <param name="field">The playfield.</param>
    public void Update(GameTime gameTime, PlayField field)
    {
        SteppedThisUpdate = false;
        if (!this.IsAlive() || field.RobotsFrozen)
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

    /// <summary>Picks which set of frames to show, and how far into it, from the frame number.</summary>
    /// <param name="facing">The way the robot is walking.</param>
    /// <param name="animationStep">How many steps the robot has taken.</param>
    /// <returns>The index into that facing's frames.</returns>
    /// <remarks>Walking sideways plays 1, 2. Walking up or down plays 1, 2, 3, 2, the author's order (notes §138).</remarks>
    internal static int GetWalkFrameIndex(BerzerkRobotFacing facing, int animationStep) => facing switch
    {
        BerzerkRobotFacing.Left or BerzerkRobotFacing.Right => animationStep % SidewaysWalkFrameCount,
        _ => UpAndDownWalkOrder[animationStep % UpAndDownWalkOrder.Length],
    };

    /// <summary>Works out the way to face from where the player is: along the larger gap.</summary>
    /// <param name="from">The robot's top-left corner.</param>
    /// <param name="player">The player's top-left corner.</param>
    /// <returns>The facing.</returns>
    internal static BerzerkRobotFacing GetFacingTowards(IntVector2 from, IntVector2 player)
    {
        int gapX = player.X - from.X;
        int gapY = player.Y - from.Y;
        if (Math.Abs(gapX) >= Math.Abs(gapY))
        {
            return gapX >= 0 ? BerzerkRobotFacing.Right : BerzerkRobotFacing.Left;
        }

        return gapY >= 0 ? BerzerkRobotFacing.Down : BerzerkRobotFacing.Up;
    }

    private Texture2D[] GetWalkFrames(BerzerkRobotFacing facing) => facing switch
    {
        BerzerkRobotFacing.Right => _sprites.BerzerkRobotWalkRightFrames,
        BerzerkRobotFacing.Left => _sprites.BerzerkRobotWalkLeftFrames,
        BerzerkRobotFacing.Up => _sprites.BerzerkRobotWalkUpFrames,
        _ => _sprites.BerzerkRobotWalkDownFrames,
    };

    private void Step(PlayField field)
    {
        IntVector2 player = field.Player.Position;
        SteppedThisUpdate = true;
        _hasMoved = true;
        _animationStep++;
        _position = GruntChaseStep.GetNextPosition(_position, player, field.Wall.PlayfieldBounds, CollisionSize);
        _facing = GetFacingTowards(_position, player);
    }
}
