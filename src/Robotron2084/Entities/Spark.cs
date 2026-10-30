using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Graphics;
using Robotron2084.Level;
using Robotron2084.Tuning;

namespace Robotron2084.Entities;

/// <summary>A shot fired by an enforcer. Its path curves, and it stops at a wall and does not bounce.</summary>
/// <seealso cref="Enforcer" />
/// <remarks>
///     It acts on a <see cref="Robotron2084.Core.RomFrameTimer">beat</see>. The <see cref="PlayField" /> calls <see cref="Update" /> on every <see cref="Robotron2084.Core.ArcadeClock.PortTicksPerSecond">tick</see>, through
///     <see cref="FieldEntities" /> and <see cref="PlayField.UpdateEntity" />. The one time it does not is during the
///     short freeze just after the player is killed. <see cref="_beatTimer" /> says when it is time for the next <see cref="Robotron2084.Core.RomFrameTimer">beat</see>,
///     <see cref="_moveTimer" /> times its moves, and <see cref="_lifeTimer" /> times how long it lasts.
///     <list type="bullet">
///         <item>
///             Original source: <c>RRC11.ASM</c>, routine <c>SPARK</c> (fired by <c>ENFSHT</c>. Its flicker animation
///             frames are <c>SPKP0</c> to <c>SPKP3</c>)
///         </item>
///         <item>Disassembly: <c>asm/robomame.asm</c> at <c>$1404</c> (<c>CREATE_SPARK</c>)</item>
///     </list>
/// </remarks>
public sealed class Spark : IEntity, IAnimationFrameSource, IRemovable
{
    private static readonly int Size = ScreenSize.ToPortPixelsFromArcadePixels(CollisionSizes.MissileSizeArcadePixels);

    // How much is added to the spark's speed each time the speed changes: one amount for sideways and one for up or down, in 256ths of a pixel. They are picked at random when the spark is made and never change (ROM: PD2/PD4).
    private readonly IntVector2 _accelerationSubpixels;
    private readonly RomFrameTimer _beatTimer = new();

    // How long the spark lasts. It is picked at random when the spark is made.
    private readonly int _lifeRomFrames;

    private readonly RomFrameTimer _lifeTimer = new();

    // Starts due, so the spark does not have to wait for its first move.
    private readonly RomFrameTimer _moveTimer = RomFrameTimer.CreateDue(ArcadeClock.MoveIntervalRomFrames);

    private readonly Random _random;
    private readonly SpriteSet _sprites;

    // Which flicker animation frame is showing.
    private int _animationFrameIndex;

    private IntVector2 _position;

    // The fraction of a pixel left over from the last move, in 256ths of a pixel. It is added to the next move, so nothing is lost.
    private IntVector2 _positionRemainderSubpixels;

    private IntVector2
        _velocitySubpixels; // How far the spark goes on each move, sideways and up or down, in 256ths of a pixel (ROM: OXV/OYV).

    /// <summary>Makes a spark and aims it near the player. It is aimed only this once.</summary>
    /// <param name="sprites">The shared sprite set.</param>
    /// <param name="position">Where it starts, which is where the enforcer that fired it is.</param>
    /// <param name="playerPosition">Where the player is, which the spark is aimed at.</param>
    /// <param name="random">
    ///     Where its random numbers come from. It stands in for the arcade's own random numbers (<c>SEED</c>,
    ///     <c>LSEED</c> and <c>HSEED</c>).
    /// </param>
    /// <param name="playfieldBounds">
    ///     The inside of the wall. When the player is near the left wall, the spark's aim is not
    ///     moved sideways. If this is null, that rule is not used.
    /// </param>
    public Spark(
        SpriteSet sprites,
        IntVector2 position,
        IntVector2 playerPosition,
        Random random,
        Rectangle? playfieldBounds = null)
    {
        _sprites = sprites;
        _position = position;
        _random = random;

        // The spark is not aimed exactly at the Player. Its aim is moved a random amount sideways and a random amount up or down. There is no sideways change when the Player is near the left wall.
        var jitterX = _random.Next(-SparkTuning.SparkJitterRange, SparkTuning.SparkJitterRange);
        var jitterY = _random.Next(-SparkTuning.SparkJitterRange, SparkTuning.SparkJitterRange);
        if (playfieldBounds is { } bounds &&
            playerPosition.X < bounds.X + ScreenSize.ToPortPixelsFromColumns(SparkTuning.SparkLeftWallJitterColumns))
            jitterX = 0;

        var deltaX = playerPosition.X + ScreenSize.ToPortPixelsFromColumns(jitterX) - position.X;
        var deltaY = playerPosition.Y + ScreenSize.ToPortPixelsFromArcadePixels(jitterY) - position.Y;

        // The starting speed is set so that, if it never changed, the spark would reach the spot it is aimed at after SparkTuning.SparkAimDivisor moves.
        var subpixelsPerPortPixelPerMove = ScreenSize.SubpixelsPerPixel / SparkTuning.SparkAimDivisor;
        _velocitySubpixels =
            new IntVector2(deltaX * subpixelsPerPortPixelPerMove, deltaY * subpixelsPerPortPixelPerMove);

        // Pick how much the speed will change by each time. The up-or-down amount is halved, because the arcade counts up and down in rows, and a row is half the size of a column.
        _accelerationSubpixels = new IntVector2(
            _random.Next(-SparkTuning.SparkAccelRomRange, SparkTuning.SparkAccelRomRange) *
            subpixelsPerPortPixelPerMove,
            _random.Next(-SparkTuning.SparkAccelRomRange, SparkTuning.SparkAccelRomRange) * subpixelsPerPortPixelPerMove
            * ScreenSize.ToPortPixelsFromArcadePixels(1) / ScreenSize.ToPortPixelsFromColumns(1));

        // Pick at random how long the spark lasts.
        _lifeRomFrames = _random.Next(SparkTuning.SparkLifeMinRomFrames, SparkTuning.SparkLifeMaxRomFrames + 1);
    }

    /// <summary>
    ///     How much is added to the spark's speed each time the speed changes: one amount for sideways and one for up or
    ///     down, in 256ths of a pixel. Tests use this.
    /// </summary>
    /// <remarks>It is picked at random when the spark is made, and it never changes.</remarks>
    internal IntVector2 AccelerationSubpixels => _accelerationSubpixels;

    /// <summary>How far the spark goes on each move, sideways and up or down, in 256ths of a pixel. Tests use this.</summary>
    internal IntVector2 VelocitySubpixels => _velocitySubpixels;

    /// <summary>
    ///     The flicker animation frame the spark is showing. It is also used to tell, pixel by pixel, whether the spark
    ///     is touching something.
    /// </summary>
    public Texture2D GetCurrentAnimationFrame()
    {
        return _sprites.SparkAnimationFrames[GetAnimationFrameIndex()];
    }

    /// <summary>The box used to tell what the spark hits.</summary>
    public Rectangle GetBounds()
    {
        return new Rectangle(_position.X, _position.Y, Size, Size);
    }

    /// <summary>Alive until it is shot or its time runs out. Then it is Dead at once, with no death animation.</summary>
    public EntityLifeState LifeState { get; private set; } = EntityLifeState.Alive;

    /// <summary>Where the top-left corner of the spark's box is.</summary>
    public IntVector2 Position => _position;

    /// <summary>Draws the flicker animation frame that is showing.</summary>
    /// <param name="spriteBatch">What the spark is drawn with.</param>
    public void Draw(SpriteBatch spriteBatch)
    {
        if (this.IsAlive())
            _sprites.Blitter.DrawSprite(spriteBatch, GetCurrentAnimationFrame(), GetBounds(), Color.White);
    }

    /// <summary>
    ///     Runs one <see cref="Robotron2084.Core.ArcadeClock.PortTicksPerSecond">tick</see>. When its time is up the spark is gone. On a <see cref="Robotron2084.Core.RomFrameTimer">beat</see> its speed changes and it flickers, and it
    ///     moves when it is time to.
    /// </summary>
    /// <param name="gameTime">Not used. The spark counts <see cref="Robotron2084.Core.ArcadeClock.PortTicksPerSecond">ticks</see>.</param>
    /// <param name="field">The playfield, whose walls the spark stays inside.</param>
    public void Update(GameTime gameTime, PlayField field)
    {
        if (!this.IsAlive()) return;

        if (_lifeTimer.Tick(_lifeRomFrames))
        {
            LifeState = EntityLifeState.Dead;
            return;
        }

        if (_beatTimer.Tick(SparkTuning.BeatIntervalRomFrames)) ActOnBeat();
        if (_moveTimer.Tick(ArcadeClock.MoveIntervalRomFrames)) MoveOnce(field);
    }

    /// <summary>Kills the spark at once. A laser does this.</summary>
    public void Kill()
    {
        if (!this.IsAlive()) return;

        LifeState = EntityLifeState.Dead;
    }

    /// <summary>Which of the flicker animation frames is showing, counting from 0.</summary>
    /// <remarks>ROM: there are 4 flicker animation frames, and the spark shows the next one on each <see cref="Robotron2084.Core.RomFrameTimer">beat</see>.</remarks>
    internal int GetAnimationFrameIndex()
    {
        return _animationFrameIndex;
    }

    /// <summary>
    ///     What the spark does on its <see cref="Robotron2084.Core.RomFrameTimer">beat</see>. Its speed changes a little, which is what makes its path curve, and it shows
    ///     its next flicker animation frame.
    /// </summary>
    /// <remarks>ROM: <c>SPARK</c>, which adds <c>PD2</c>/<c>PD4</c> to the speed and moves <c>OPICT</c> on.</remarks>
    private void ActOnBeat()
    {
        _velocitySubpixels = new IntVector2(
            _velocitySubpixels.X + _accelerationSubpixels.X,
            _velocitySubpixels.Y + _accelerationSubpixels.Y);
        _animationFrameIndex = (_animationFrameIndex + 1) % SpriteSet.SparkAnimationFrameCount;
    }

    /// <summary>Makes one move, by whole pixels. The fraction of a pixel left over is kept for the next move.</summary>
    /// <param name="field">The playfield, whose walls the spark stays inside.</param>
    private void MoveOnce(PlayField field)
    {
        _positionRemainderSubpixels = new IntVector2(
            _positionRemainderSubpixels.X + _velocitySubpixels.X,
            _positionRemainderSubpixels.Y + _velocitySubpixels.Y);

        var stepX = _positionRemainderSubpixels.X / ScreenSize.SubpixelsPerPixel;
        var stepY = _positionRemainderSubpixels.Y / ScreenSize.SubpixelsPerPixel;
        _positionRemainderSubpixels = new IntVector2(
            _positionRemainderSubpixels.X - stepX * ScreenSize.SubpixelsPerPixel,
            _positionRemainderSubpixels.Y - stepY * ScreenSize.SubpixelsPerPixel);

        MoveBy(field, stepX, stepY);
    }

    /// <summary>
    ///     Makes one move. The sideways part is made only if it keeps the spark inside the playfield, and the same goes
    ///     for the up-or-down part.
    /// </summary>
    /// <param name="field">The playfield, whose walls the spark stays inside.</param>
    /// <param name="stepX">How many pixels to go left or right on this move.</param>
    /// <param name="stepY">How many pixels to go up or down on this move.</param>
    private void MoveBy(PlayField field, int stepX, int stepY)
    {
        // A safety limit on how far the spark can go in one move.
        stepX = Math.Clamp(stepX, -SparkTuning.SparkMaxSpeed, SparkTuning.SparkMaxSpeed);
        stepY = Math.Clamp(stepY, -SparkTuning.SparkMaxSpeed, SparkTuning.SparkMaxSpeed);

        // The sideways move is made only if it keeps the spark inside the playfield. The same goes for the up-or-down move. The spark does not bounce.
        var inner = field.Wall.PlayfieldBounds;
        var x = _position.X;
        var y = _position.Y;
        if (_position.X + stepX >= inner.X && _position.X + stepX + Size <= inner.Right) x += stepX;

        if (_position.Y + stepY >= inner.Y && _position.Y + stepY + Size <= inner.Bottom) y += stepY;

        _position = new IntVector2(x, y);
    }
}
