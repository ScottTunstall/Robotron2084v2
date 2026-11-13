using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Graphics;
using Robotron2084.Level;
using Robotron2084.Tuning;

namespace Robotron2084.Entities;

/// <summary>A BerzerkRobot is a robot that shuffles towards you exactly as a grunt does. It cannot shoot yet.</summary>
/// <seealso cref="Grunt" />
/// <remarks>
///     It acts on a <see cref="Robotron2084.Core.ArcadeFrameTimer">beat</see>.
///     <see cref="_beatArcadeFrameTimer" /> says when it is time for the next <see cref="Robotron2084.Core.ArcadeFrameTimer">beat</see>.
///     This robot is the author's own. It is not in the arcade game (notes §138). It moves as a grunt does (
///     <see cref="GruntChaseStep" />),
///     with the same time between <see cref="Robotron2084.Core.ArcadeFrameTimer">beats</see> and the same wait between steps. It dies as a grunt does, to a laser or by walking
///     onto an
///     electrode, and it kills the player by touching them. It has its own look. Until it first moves, it stands still and
///     plays its
///     standing animation. After that it shows the walk for the way it is facing (<see cref="GetWalkSequenceTowards" />).
///     It cannot shoot yet. Shooting is planned.
/// </remarks>
public sealed class BerzerkRobot : IExplodable, IRemovable, IWaveStartRobot
{
    /// <summary>How long one <see cref="Robotron2084.Core.ArcadeFrameTimer">beat</see> lasts. It is the same as a grunt's.</summary>
    internal const int BeatIntervalArcadeFrames = 4;

    /// <summary>The most <see cref="Robotron2084.Core.ArcadeFrameTimer">beats</see> the robot waits between steps, when it is not told a number. It is the same as a grunt's.</summary>
    private const int DefaultMoveIntervalMaxBeats = 15;

    /// <summary>How long the robot waits after the look that finds the game live, before its first <see cref="Robotron2084.Core.ArcadeFrameTimer">beat</see>.</summary>
    /// <remarks>
    ///     This number is not from the arcade (FID-4a), because this robot is not in the arcade game. The author chose it
    ///     to be like the grunt's (<see cref="Grunt" />).
    /// </remarks>
    private const int FirstBeatNapArcadeFrames = 10;

    /// <summary>How long the robot waits between one look at whether the game is live and the next.</summary>
    /// <remarks>
    ///     This number is not from the arcade (FID-4a), because this robot is not in the arcade game. The author chose it
    ///     to be like the grunt's (<see cref="Grunt" />).
    /// </remarks>
    private const int LivePollArcadeFrames = 2;

    /// <summary>How many animation frames each sideways walk has. They are shown in order: the first, then the second.</summary>
    private const int SidewaysWalkFrameCount = 2;

    /// <summary>How big the robot is, in port pixels.</summary>
    private static readonly (int Width, int Height) CollisionSize =
        (ScreenSize.ToPortPixelsFromArcadePixels(CollisionSizes.BerzerkRobotCollisionSize.Width),
            ScreenSize.ToPortPixelsFromArcadePixels(CollisionSizes.BerzerkRobotCollisionSize.Height));

    /// <summary>The order the up and down walks show their three animation frames in: first, second, third, second.</summary>
    private static readonly int[] UpAndDownWalkOrder = [0, 1, 2, 1];

    private readonly Random _random;
    private readonly TextureSet _textureSet;
    private int _animationStep;
    private ArcadeFrameTimer _beatArcadeFrameTimer = new();
    private bool _hasMoved;

    /// <summary><see cref="Robotron2084.Core.ArcadeFrameTimer">Beats</see> left before the robot takes its next step.</summary>
    private int _moveBeatsRemaining;

    private IntVector2 _position;

    /// <summary>Makes a robot, and picks at random how long it waits before its first step.</summary>
    /// <param name="textureSet">The shared texture set.</param>
    /// <param name="position">Where the robot's top-left corner is.</param>
    /// <param name="random">Where its random numbers come from.</param>
    /// <param name="moveIntervalMaxBeats">
    ///     The most <see cref="Robotron2084.Core.ArcadeFrameTimer">beats</see> the robot waits between steps. Each wait is a random number of <see cref="Robotron2084.Core.ArcadeFrameTimer">beats</see> from 1
    ///     up to this, as a grunt's is.
    /// </param>
    public BerzerkRobot(
        TextureSet textureSet,
        IntVector2 position,
        Random random,
        int moveIntervalMaxBeats = DefaultMoveIntervalMaxBeats)
    {
        _textureSet = textureSet;
        _position = position;
        MoveIntervalMaxBeats = moveIntervalMaxBeats;
        _random = random;
        _moveBeatsRemaining = _random.Next(1, MoveIntervalMaxBeats + 1);
    }

    /// <summary>Alive until it is killed. It is never Dying, because it has no death animation (see <see cref="Kill" />).</summary>
    public EntityLifeState LifeState { get; private set; } = EntityLifeState.Alive;

    /// <summary>The most <see cref="Robotron2084.Core.ArcadeFrameTimer">beats</see> the robot waits between steps.</summary>
    public int MoveIntervalMaxBeats { get; }

    /// <summary>Where the robot's top-left corner is.</summary>
    public IntVector2 Position => _position;

    /// <summary>True when the robot took a step during the last update.</summary>
    public bool SteppedThisUpdate { get; private set; }

    /// <summary>The way the robot is walking.</summary>
    public WalkSequence WalkSequence { get; private set; } = WalkSequence.Down;

    /// <summary>
    ///     Sets the time of the robot's first <see cref="Robotron2084.Core.ArcadeFrameTimer">beat</see>, on the <see cref="Robotron2084.Core.ArcadeClock.PortTicksPerSecond">tick</see> the game goes live. The <see cref="Robotron2084.Core.ArcadeFrameTimer">beat</see> timer is set so that it
    ///     comes due when that time has gone by.
    /// </summary>
    /// <param name="playField">The playfield, which works out how long the wait is.</param>
    public void BeginPlay(PlayField playField)
    {
        _beatArcadeFrameTimer = playField.CreateFirstBeatTimer(LivePollArcadeFrames, FirstBeatNapArcadeFrames, BeatIntervalArcadeFrames);
    }

    /// <summary>Draws the current animation frame in its own colours.</summary>
    /// <param name="spriteBatch">What the robot is drawn with.</param>
    public void Draw(SpriteBatch spriteBatch)
    {
        if (!this.IsAlive()) return;

        _textureSet.BlitterDraw.DrawSprite(spriteBatch, GetCurrentAnimationFrameTexture(), GetBounds(), Color.White);
    }

    /// <summary>The box the robot takes up on the screen.</summary>
    public Rectangle GetBounds()
    {
        return new Rectangle(_position.X, _position.Y, CollisionSize.Width, CollisionSize.Height);
    }

    /// <summary>Gets the animation frame the robot is showing.</summary>
    /// <returns>The animation frame that is showing.</returns>
    public Texture2D GetCurrentAnimationFrameTexture()
    {
        if (!_hasMoved) return _textureSet.BerzerkRobotIdleAnimationFrameTextures[_animationStep % _textureSet.BerzerkRobotIdleAnimationFrameTextures.Length];

        var frames = GetWalkFrames(_textureSet, WalkSequence);
        return frames[GetWalkFrameIndex(WalkSequence, _animationStep)];
    }

    /// <inheritdoc />
    /// <remarks>It is the animation frame showing now (<see cref="GetCurrentAnimationFrameTexture" />).</remarks>
    public Texture2D GetExplosionAnimationFrameTexture()
    {
        return GetCurrentAnimationFrameTexture();
    }

    /// <inheritdoc />
    /// <remarks>It is the same box as <see cref="GetBounds" />.</remarks>
    public Rectangle GetExplosionBounds()
    {
        return GetBounds();
    }

    /// <summary>Kills the robot at once, with no flash and no death animation.</summary>
    public void Kill()
    {
        if (!this.IsAlive()) return;

        LifeState = EntityLifeState.Dead;
    }

    /// <summary>Runs one <see cref="Robotron2084.Core.ArcadeClock.PortTicksPerSecond">tick</see>. When its wait is over the robot takes a step towards the player and picks a new wait.</summary>
    /// <param name="gameTime">Not used. The robot counts <see cref="Robotron2084.Core.ArcadeClock.PortTicksPerSecond">ticks</see>.</param>
    /// <param name="playField">The playfield.</param>
    public void Update(GameTime gameTime, PlayField playField)
    {
        SteppedThisUpdate = false;
        if (!this.IsAlive() || playField.RobotsFrozen()) return;

        if (_beatArcadeFrameTimer.Tick(BeatIntervalArcadeFrames)) ActOnBeat(playField);
    }

    /// <summary>Works out which animation frame of a walk to show.</summary>
    /// <param name="textureSet">The textures the frames come from.</param>
    /// <param name="walkSequence">The way the robot is walking.</param>
    /// <param name="animationStep">How many steps the robot has taken.</param>
    /// <returns>The place of the animation frame in that walk's set.</returns>
    /// <remarks>
    ///     A sideways walk shows the first, then the second. A walk up or down shows the first, second, third, then the
    ///     second again. This order is the author's (notes §138).
    /// </remarks>
    internal static int GetWalkFrameIndex(WalkSequence walkSequence, int animationStep)
    {
        return walkSequence switch
        {
            WalkSequence.Left or WalkSequence.Right => animationStep % SidewaysWalkFrameCount,
            _ => UpAndDownWalkOrder[animationStep % UpAndDownWalkOrder.Length]
        };
    }

    /// <summary>
    ///     Works out which way the robot faces. It faces sideways if the player is at least as far away sideways as up or
    ///     down.
    /// </summary>
    /// <param name="from">The robot's top-left corner.</param>
    /// <param name="playerPosition">The player's top-left corner.</param>
    /// <returns>The walk for the way the robot faces.</returns>
    /// <remarks>
    ///     Otherwise it faces up or down.
    /// </remarks>
    internal static WalkSequence GetWalkSequenceTowards(IntVector2 from, IntVector2 playerPosition)
    {
        var gapX = playerPosition.X - from.X;
        var gapY = playerPosition.Y - from.Y;
        var isSidewaysFurther = Math.Abs(gapX) >= Math.Abs(gapY);
        if (isSidewaysFurther)
        {
            var isPlayerToTheRight = gapX >= 0;
            return isPlayerToTheRight ? WalkSequence.Right : WalkSequence.Left;
        }

        var isPlayerBelow = gapY >= 0;
        return isPlayerBelow ? WalkSequence.Down : WalkSequence.Up;
    }

    /// <summary>
    ///     What the robot does on its <see cref="Robotron2084.Core.ArcadeFrameTimer">beat</see>. It takes a step when its
    ///     move countdown runs out.
    /// </summary>
    /// <param name="playField">The playfield.</param>
    /// <remarks>
    ///     Until it first moves, it plays its standing animation.
    /// </remarks>
    private void ActOnBeat(PlayField playField)
    {
        if (!_hasMoved) _animationStep++;

        if (--_moveBeatsRemaining > 0) return;

        _moveBeatsRemaining = _random.Next(1, MoveIntervalMaxBeats + 1);
        Step(playField);
    }

    /// <summary>Gets the set of animation frames for a walk.</summary>
    /// <param name="textureSet">The textures the frames come from.</param>
    /// <param name="walkSequence">The way the robot is walking.</param>
    internal static Texture2D[] GetWalkFrames(TextureSet textureSet, WalkSequence walkSequence)
    {
        return walkSequence switch
        {
            WalkSequence.Right => textureSet.BerzerkRobotWalkRightAnimationFrameTextures,
            WalkSequence.Left => textureSet.BerzerkRobotWalkLeftAnimationFrameTextures,
            WalkSequence.Up => textureSet.BerzerkRobotWalkUpAnimationFrameTextures,
            _ => textureSet.BerzerkRobotWalkDownAnimationFrameTextures
        };
    }

    /// <summary>Takes one step towards the player, then turns to face the player.</summary>
    /// <param name="playField">The playfield.</param>
    private void Step(PlayField playField)
    {
        var playerPosition = playField.Player.Position;
        SteppedThisUpdate = true;
        _hasMoved = true;
        _animationStep++;
        _position = GruntChaseStep.GetNextPosition(_position, playerPosition, playField.PlayfieldWall.PlayfieldBounds,
            CollisionSize);
        WalkSequence = GetWalkSequenceTowards(_position, playerPosition);
    }
}
