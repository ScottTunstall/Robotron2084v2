using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Core.Models;
using Robotron2084.Entities.Extensions;
using Robotron2084.Entities.Models;
using Robotron2084.Events;
using Robotron2084.Events.Models;
using Robotron2084.Graphics;
using Robotron2084.Level;
using Robotron2084.Level.Models;
using Robotron2084.Tuning;

namespace Robotron2084.Entities;

/// <summary>
///     A BerzerkRobot is a robot that walks towards you at a steady, slow pace, as the robots in the Berzerk arcade game
///     do. It cannot shoot yet.
/// </summary>
/// <seealso cref="Grunt" />
/// <remarks>
///     It acts on a <see cref="Robotron2084.Core.ArcadeFrameTimer">beat</see>.
///     <see cref="_beatArcadeFrameTimer" /> says when it is time for the next <see cref="Robotron2084.Core.ArcadeFrameTimer">beat</see>.
///     This robot is the author's own. It is not in the arcade game (notes §138). Unlike a grunt, it never waits a random
///     time between steps: every <see cref="StepIntervalBeats" /> beats it takes one small step towards the player
///     (<see cref="GetNextPosition" />), so it glides along at the same pace all the time. It is meant to be a little
///     cleverer than a grunt; how is still to be decided. It dies as a grunt does, to a laser or by walking onto an
///     electrode, and it kills the player by touching them. It has its own look, drawn all in one <see cref="Colour" />
///     that each robot can have its own of, blue unless it is given another. Until it first moves, it stands still and
///     plays its standing animation. After that it shows the walk for the way it is facing
///     (<see cref="GetWalkSequenceTowards" />). It cannot shoot yet. Shooting is planned.
///     <para>
///         Announces <see cref="GameEventKind.RobotKilled" /> (for a <see cref="RobotKind.BerzerkRobot" />) to the <see cref="IGameEvents" /> it is given.
///     </para>
/// </remarks>
public sealed class BerzerkRobot : IExplodable, IRemovable, IWaveStartRobot, IStripDrawer
{
    /// <summary>
    ///     The robot's colour when it is not given one: blue, the arcade's colour byte <c>$C0</c> (0, 0, 240).
    /// </summary>
    /// <remarks>Not from the arcade: the author's choice.</remarks>
    public static readonly Color DefaultColour = RobotronColour.CreateFromByte(0xC0);

    /// <summary>How long one <see cref="Robotron2084.Core.ArcadeFrameTimer">beat</see> lasts. It is the same as a grunt's.</summary>
    internal const int BeatIntervalArcadeFrames = 4;

    /// <summary>
    ///     How many <see cref="Robotron2084.Core.ArcadeFrameTimer">beats</see> pass between one step and the next: always
    ///     the same, so the robot moves at a steady pace. Two beats are 8 arcade frames.
    /// </summary>
    /// <remarks>
    ///     Not from the arcade: the author asked for "a constant slow speed" for now. With <see cref="StepArcadePixels" />,
    ///     it is about 6 arcade pixels a second, near a grunt's average pace on wave 1.
    /// </remarks>
    internal const int StepIntervalBeats = 2;

    /// <summary>How far one step goes, in port pixels.</summary>
    internal static readonly int StepPixels = ScreenSize.ToPortPixelsFromArcadePixels(StepArcadePixels);

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

    /// <summary>
    ///     How far one step goes, in arcade pixels: sideways, or up or down, towards the player. Never both at once, so
    ///     the robot never moves on a slant.
    /// </summary>
    /// <remarks>
    ///     A robot in the Berzerk arcade game also moves one pixel at a time (the author's Berzerk disassembly,
    ///     <c>berzerk.asm</c> <c>MOVE_ANIMATE_VECTOR</c>, <c>$27A9</c>). The author: "The berzerk robots cannot move
    ///     diagonally".
    /// </remarks>
    private const int StepArcadePixels = 1;

    /// <summary>How big the robot is, in port pixels.</summary>
    private static readonly (int Width, int Height) CollisionSize =
        (ScreenSize.ToPortPixelsFromArcadePixels(CollisionSizes.BerzerkRobotCollisionSize.Width),
            ScreenSize.ToPortPixelsFromArcadePixels(CollisionSizes.BerzerkRobotCollisionSize.Height));

    /// <summary>The order the up and down walks show their three animation frames in: first, second, third, second.</summary>
    private static readonly int[] UpAndDownWalkOrder = [0, 1, 2, 1];

    private readonly IGameEvents _gameEvents;
    private readonly TextureSet _textureSet;
    private int _animationStep;
    private ArcadeFrameTimer _beatArcadeFrameTimer = new();
    private bool _hasMoved;

    private IntVector2 _position;

    /// <summary><see cref="Robotron2084.Core.ArcadeFrameTimer">Beats</see> left before the robot takes its next step.</summary>
    private int _stepBeatsRemaining = StepIntervalBeats;

    /// <summary>Makes a robot, standing still until its first step.</summary>
    /// <param name="entityParams">
    ///     What the robot shares with the rest of the game: its sprites and where it announces what happens to it.
    /// </param>
    /// <param name="position">Where the robot's top-left corner is.</param>
    /// <param name="colour">The robot's colour, or null for <see cref="DefaultColour" />.</param>
    public BerzerkRobot(EntityParams entityParams, IntVector2 position, Color? colour = null)
    {
        _textureSet = entityParams.TextureSet;
        _gameEvents = entityParams.GameEvents;
        _position = position;
        Colour = colour ?? DefaultColour;
    }

    /// <summary>
    ///     The robot's colour. Every pixel of its sprite is drawn in it, whatever colour the sprite's own pixels are, and
    ///     so are the strips of its appear and its explosion.
    /// </summary>
    public Color Colour { get; }

    /// <summary>Alive until it is killed. It is never Dying, because it has no death animation (see <see cref="Kill" />).</summary>
    public EntityLifeState LifeState { get; private set; } = EntityLifeState.Alive;

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

    /// <summary>
    ///     Draws the current animation frame in the robot's <see cref="Colour" />, through the one-colour pixel shader
    ///     (<see cref="BlitterDraw.DrawSpriteSolid" />).
    /// </summary>
    /// <param name="spriteBatch">What the robot is drawn with.</param>
    public void Draw(SpriteBatch spriteBatch)
    {
        if (!this.IsAlive()) return;

        _textureSet.BlitterDraw.DrawSpriteSolid(spriteBatch, GetCurrentAnimationFrameTexture(), GetBounds(), Colour);
    }

    /// <inheritdoc />
    /// <remarks>The strip is drawn in the robot's <see cref="Colour" />, as the robot is (<see cref="BlitterDraw.DrawSpriteSolidPiece" />).</remarks>
    public void DrawStrip(SpriteBatch spriteBatch, Texture2D animationFrameTexture, Rectangle source, Rectangle destination)
    {
        _textureSet.BlitterDraw.DrawSpriteSolidPiece(spriteBatch, animationFrameTexture, source, destination, Colour);
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
        _gameEvents.Raise(new GameEvent(GameEventKind.RobotKilled, Position, RobotKind.BerzerkRobot));
    }

    /// <summary>Runs one <see cref="Robotron2084.Core.ArcadeClock.PortTicksPerSecond">tick</see>. Every <see cref="StepIntervalBeats" /> beats the robot takes a step towards the player.</summary>
    /// <param name="gameTime">Not used. The robot counts <see cref="Robotron2084.Core.ArcadeClock.PortTicksPerSecond">ticks</see>.</param>
    /// <param name="playField">The playfield.</param>
    public void Update(GameTime gameTime, PlayField playField)
    {
        SteppedThisUpdate = false;
        if (!this.IsAlive() || playField.RobotsFrozen()) return;

        if (_beatArcadeFrameTimer.Tick(BeatIntervalArcadeFrames)) ActOnBeat(playField);
    }

    /// <summary>
    ///     Works out where the robot is after one step towards the player. The step goes the way the robot faces
    ///     (<see cref="GetWalkSequenceTowards" />): sideways, or up or down, never on a slant.
    /// </summary>
    /// <param name="position">The robot's top-left corner now, in port pixels.</param>
    /// <param name="playerPosition">The player's top-left corner.</param>
    /// <param name="playfieldBounds">The inside of the wall.</param>
    /// <returns>
    ///     Where the robot's top-left corner is after the step. A step never goes past the player's line, and a step that
    ///     would leave the playfield is not taken.
    /// </returns>
    internal static IntVector2 GetNextPosition(IntVector2 position, IntVector2 playerPosition, Rectangle playfieldBounds)
    {
        var gapX = playerPosition.X - position.X;
        var gapY = playerPosition.Y - position.Y;
        var stepX = 0;
        var stepY = 0;
        var walkSequence = GetWalkSequenceTowards(position, playerPosition);
        if (walkSequence is WalkSequence.Left or WalkSequence.Right)
            stepX = Math.Sign(gapX) * Math.Min(StepPixels, Math.Abs(gapX));
        else
            stepY = Math.Sign(gapY) * Math.Min(StepPixels, Math.Abs(gapY));

        IntVector2 nextPosition = new(position.X + stepX, position.Y + stepY);
        var isInsideWall = nextPosition.X >= playfieldBounds.X && nextPosition.X + CollisionSize.Width <= playfieldBounds.Right &&
                           nextPosition.Y >= playfieldBounds.Y && nextPosition.Y + CollisionSize.Height <= playfieldBounds.Bottom;
        return isInsideWall ? nextPosition : position;
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
    ///     What the robot does on its <see cref="Robotron2084.Core.ArcadeFrameTimer">beat</see>. It takes a step every
    ///     <see cref="StepIntervalBeats" /> beats.
    /// </summary>
    /// <param name="playField">The playfield.</param>
    /// <remarks>
    ///     Until it first moves, it plays its standing animation.
    /// </remarks>
    private void ActOnBeat(PlayField playField)
    {
        if (!_hasMoved) _animationStep++;

        if (--_stepBeatsRemaining > 0) return;

        _stepBeatsRemaining = StepIntervalBeats;
        Step(playField);
    }

    /// <summary>Faces the player, then takes one step that way.</summary>
    /// <param name="playField">The playfield.</param>
    private void Step(PlayField playField)
    {
        var playerPosition = playField.Player.Position;
        SteppedThisUpdate = true;
        _hasMoved = true;
        _animationStep++;
        WalkSequence = GetWalkSequenceTowards(_position, playerPosition);
        _position = GetNextPosition(_position, playerPosition, playField.PlayfieldWall.PlayfieldBounds);
    }
}
