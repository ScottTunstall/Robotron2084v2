using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Graphics;
using Robotron2084.Level;
using Robotron2084.Tuning;

namespace Robotron2084.Entities;

/// <summary>A grunt is a slow, clumsy robot that shuffles towards you. It's the most common enemy in the game.</summary>
/// <seealso cref="PlayField" />
/// <remarks>
///     It acts on a <see cref="Robotron2084.Core.ArcadeFrameTimer">beat</see>.
///     <see cref="_beatArcadeFrameTimer" /> says when it is time for the next <see cref="Robotron2084.Core.ArcadeFrameTimer">beat</see>.
///     <list type="bullet">
///         <item>Original source: <c>RRP8.ASM</c>, routine <c>ROBOT</c> (with <c>ROB0</c>..<c>ROB11</c> sub-blocks)</item>
///         <item>
///             Disassembly: <c>asm/robomame.asm</c> at <c>$39E6</c> (the check of the grunt's move timer, which is part of
///             the update loop that the grunt, hulk, brain, prog and tank share)
///         </item>
///     </list>
/// </remarks>
public sealed class Grunt : IExplodable, IRemovable, IWaveStartRobot
{
    /// <summary>How long one <see cref="Robotron2084.Core.ArcadeFrameTimer">beat</see> lasts.</summary>
    internal const int BeatIntervalArcadeFrames = 4;

    /// <summary>The most <see cref="Robotron2084.Core.ArcadeFrameTimer">beats</see> a grunt waits between steps, when it is not told a number.</summary>
    private const int DefaultMoveIntervalMaxBeats = 15;

    /// <summary>How long the arcade's routine sleeps after the look that finds the game live, before the first <see cref="Robotron2084.Core.ArcadeFrameTimer">beat</see>.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRP8.ASM</c> <c>ROB0A</c>, <c>NAP 10,ROB0</c>.</item>
    ///         <item>Disassembly: <c>$39B7</c> onwards.</item>
    ///     </list>
    /// </remarks>
    private const int FirstBeatNapArcadeFrames = 10;

    /// <summary>How long the arcade's routine sleeps between one look at whether the game is live and the next.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRP8.ASM</c> <c>ROBOT</c>, <c>BITA #$7F / BEQ ROB0A / NAP 2,ROBOT</c>.</item>
    ///         <item>Disassembly: <c>$39B7</c>.</item>
    ///     </list>
    /// </remarks>
    private const int LivePollArcadeFrames = 2;

    /// <summary>How many walk animation frames a grunt has.</summary>
    internal const int WalkAnimationFrameCount = 4;

    /// <summary>
    ///     How big the grunt is, in port pixels. It is the size of the grunt's sprite.
    /// </summary>
    private static readonly (int Width, int Height) CollisionSize =
        (ScreenSize.ToPortPixelsFromArcadePixels(CollisionSizes.GruntCollisionSize.Width),
            ScreenSize.ToPortPixelsFromArcadePixels(CollisionSizes.GruntCollisionSize.Height));

    private readonly Random _random;
    private readonly TextureSet _textureSet;
    private ArcadeFrameTimer _beatArcadeFrameTimer = new();

    private int _landingY;

    /// <summary><see cref="Robotron2084.Core.ArcadeFrameTimer">Beats</see> left before the grunt takes its next step.</summary>
    private int _moveBeatsRemaining;

    private IntVector2 _position;

    private int
        _walkAnimationFrameNumber =
            1; // Which walk animation frame is showing. The first one is number 1, as in the arcade.

    /// <summary>Makes a grunt, and picks at random how long it waits before its first step.</summary>
    /// <param name="textureSet">The shared texture set.</param>
    /// <param name="position">Where the grunt's top-left corner is.</param>
    /// <param name="random">Where its random numbers come from.</param>
    /// <param name="moveIntervalMaxBeats">
    ///     The most <see cref="Robotron2084.Core.ArcadeFrameTimer">beats</see> a grunt waits between steps on this wave. Each wait is a random number of
    ///     <see cref="Robotron2084.Core.ArcadeFrameTimer">beats</see> from 1 up to this.
    /// </param>
    /// <remarks>ROM: <c>ROBSPD</c>.</remarks>
    public Grunt(
        TextureSet textureSet,
        IntVector2 position,
        Random random,
        int moveIntervalMaxBeats = DefaultMoveIntervalMaxBeats)
    {
        _textureSet = textureSet;
        _position = position;
        MoveIntervalMaxBeats = moveIntervalMaxBeats;
        _random = random;
        // The grunt waits a random number of beats before its first step.
        _moveBeatsRemaining = _random.Next(1, MoveIntervalMaxBeats + 1);
    }

    /// <summary>Alive until it is killed. It is never Dying, because it has no death animation (see <see cref="Kill" />).</summary>
    public EntityLifeState LifeState { get; private set; } = EntityLifeState.Alive;

    /// <summary>The most <see cref="Robotron2084.Core.ArcadeFrameTimer">beats</see> the grunt waits between steps, as it stands now. It gets smaller as the wave goes on.</summary>
    public int MoveIntervalMaxBeats { get; private set; }

    /// <summary>Where the grunt's top-left corner is.</summary>
    /// <remarks>The ROM's OBJX/OBJY.</remarks>
    public IntVector2 Position => _position;

    /// <summary>True when the grunt took a step during the last update.</summary>
    public bool SteppedThisUpdate { get; private set; }

    /// <summary>Which walk animation frame is showing, counting from 1.</summary>
    /// <remarks>ROM: the <c>RWDP</c> animation frames.</remarks>
    internal int WalkAnimationFrameNumber => _walkAnimationFrameNumber;

    /// <summary>
    ///     Sets the time of the grunt's first <see cref="Robotron2084.Core.ArcadeFrameTimer">beat</see>, on the <see cref="Robotron2084.Core.ArcadeClock.PortTicksPerSecond">tick</see> the game goes live. The <see cref="Robotron2084.Core.ArcadeFrameTimer">beat</see> timer is set so that it
    ///     comes due when that time has gone by.
    /// </summary>
    /// <param name="playField">The playfield, which works out how long the wait is.</param>
    public void BeginPlay(PlayField playField)
    {
        _beatArcadeFrameTimer = playField.CreateFirstBeatTimer(LivePollArcadeFrames, FirstBeatNapArcadeFrames, BeatIntervalArcadeFrames);
    }

    /// <summary>Draws the current walk animation frame in its own colours.</summary>
    /// <param name="spriteBatch">What the grunt is drawn with.</param>
    public void Draw(SpriteBatch spriteBatch)
    {
        if (!this.IsAlive()) return;

        _textureSet.BlitterDraw.DrawSprite(spriteBatch, GetCurrentAnimationFrameTexture(), GetBounds(), Color.White);
    }

    /// <summary>The box the grunt takes up on the screen.</summary>
    public Rectangle GetBounds()
    {
        return new Rectangle(_position.X, _position.Y, CollisionSize.Width, CollisionSize.Height);
    }

    /// <summary>This grunt's current walk animation frame.</summary>
    /// <returns>The animation frame that is showing.</returns>
    public Texture2D GetCurrentAnimationFrameTexture()
    {
        return _textureSet.GruntAnimationFrameTextures[GetAnimationFrameIndex(_walkAnimationFrameNumber)];
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

    /// <summary>True while the grunt is still falling. It does not move on until it lands.</summary>
    public bool IsFalling()
    {
        return _position.Y < _landingY;
    }

    /// <summary>Kills the grunt at once, with no flash and no death animation.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRP8.ASM</c>, routine <c>ROBKIL</c>, which just explodes it</item>
    ///         <item>Disassembly: <c>asm/robomame.asm</c> at <c>GRUNT_COLLISION_HANDLER</c> (<c>$3A76</c>)</item>
    ///     </list>
    /// </remarks>
    public void Kill()
    {
        if (!this.IsAlive()) return;

        LifeState = EntityLifeState.Dead;
    }

    /// <summary>
    ///     Speeds this grunt up when any grunt is killed. The most <see cref="Robotron2084.Core.ArcadeFrameTimer">beats</see> it waits between steps is cut to seven eighths
    ///     of what it was.
    /// </summary>
    /// <param name="floorBeats">The smallest that number is allowed to be on this wave.</param>
    /// <remarks>
    ///     The arcade only makes the cut when the smaller number, rounded down, is still at least
    ///     <paramref name="floorBeats" />. Otherwise it leaves the number alone. It does not set it to
    ///     <paramref name="floorBeats" />. A wait that has already started is not changed. The grunt uses the new number
    ///     when it next picks a wait.
    /// </remarks>
    public void SpeedUp(int floorBeats)
    {
        var next = MoveIntervalMaxBeats * 7 / 8;
        if (next >= floorBeats) MoveIntervalMaxBeats = next;
    }

    /// <summary>Speeds the grunt up as the wave goes on, by taking some <see cref="Robotron2084.Core.ArcadeFrameTimer">beats</see> off the most it waits between steps.</summary>
    /// <param name="floorBeats">The smallest that number is allowed to be on this wave.</param>
    /// <param name="stepBeats">How many <see cref="Robotron2084.Core.ArcadeFrameTimer">beats</see> to take off. The arcade takes off 4, then 2, then 4, and so on.</param>
    /// <remarks>
    ///     Here the arcade does stop the number at <paramref name="floorBeats" />, which itself goes down as far as 1.
    ///     A grunt that steps on every <see cref="Robotron2084.Core.ArcadeFrameTimer">beat</see> goes as fast as the player. A wait that has already started is finished
    ///     before the new number is used.
    /// </remarks>
    public void SpeedUpAsWaveGoesOn(int floorBeats, int stepBeats = 4)
    {
        MoveIntervalMaxBeats = Math.Max(floorBeats, MoveIntervalMaxBeats - stepBeats);
    }

    /// <summary>Runs one <see cref="Robotron2084.Core.ArcadeClock.PortTicksPerSecond">tick</see> of the grunt. When its wait is over it takes a step towards the player and picks a new wait.</summary>
    /// <param name="gameTime">Not used. The grunt counts <see cref="Robotron2084.Core.ArcadeClock.PortTicksPerSecond">ticks</see>.</param>
    /// <param name="playField">The playfield.</param>
    public void Update(GameTime gameTime, PlayField playField)
    {
        SteppedThisUpdate = false;
        if (!this.IsAlive()) return;

        if (playField.RobotsFrozen()) return;

        if (IsFalling())
        {
            _position = _position with { Y = Math.Min(_landingY, _position.Y + GorfTuning.FallPixelsPerTick) };
            return;
        }

        if (_beatArcadeFrameTimer.Tick(BeatIntervalArcadeFrames)) ActOnBeat(playField);
    }

    /// <summary>Works out which of the grunt's animation frames to draw, from the arcade's walk number.</summary>
    /// <param name="arcadeAnimationFrameNumber">The arcade's walk number, from 1 to 4.</param>
    /// <returns>The place of the animation frame in <see cref="TextureSet.GruntAnimationFrameTextures" />.</returns>
    /// <remarks>
    ///     Walk numbers 1, 2, 3 and 4 show the first, second, first and third animation frames, so there are only three
    ///     different ones.
    /// </remarks>
    internal static int GetAnimationFrameIndex(int arcadeAnimationFrameNumber)
    {
        return arcadeAnimationFrameNumber switch
        {
            2 => 1,
            4 => 2,
            _ => 0
        };
    }

    /// <summary>Lets the grunt fall from where it is to the ground. It waits there until it lands.</summary>
    /// <param name="landingY">The top of the grunt when it is standing on the ground, in port pixels.</param>
    /// <remarks>This fall is not in the arcade game. It is the author's own (notes §138.2).</remarks>
    internal void BeginFall(int landingY)
    {
        _landingY = landingY;
    }

    /// <summary>What the grunt does on its <see cref="Robotron2084.Core.ArcadeFrameTimer">beat</see>. It only takes a step when <see cref="_moveBeatsRemaining" /> runs out.</summary>
    /// <param name="playField">The playfield.</param>
    private void ActOnBeat(PlayField playField)
    {
        if (--_moveBeatsRemaining > 0) return;

        _moveBeatsRemaining = _random.Next(1, MoveIntervalMaxBeats + 1);
        SteppedThisUpdate = true;
        _walkAnimationFrameNumber =
            _walkAnimationFrameNumber % WalkAnimationFrameCount +
            1; // Each step shows the next walk animation frame (DRAW_GRUNT).

        _position = GruntChaseStep.GetNextPosition(_position, playField.Player.Position, playField.PlayfieldWall.PlayfieldBounds,
            CollisionSize);
    }
}
