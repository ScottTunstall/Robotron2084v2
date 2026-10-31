using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Graphics;
using Robotron2084.Level;
using Robotron2084.Tuning;

namespace Robotron2084.Entities;

/// <summary>The flashing shape and floating score number that appear for a moment when you kill a spheroid or a quark.</summary>
/// <seealso cref="Spheroid" />
/// <seealso cref="Quark" />
/// <remarks>
///     It has no <see cref="Robotron2084.Core.ArcadeFrameTimer">beat</see>. <see cref="_stepArcadeFrameTimer" /> says when it is time for the next step.
///     <list type="bullet">
///         <item>
///             Original source: <c>RRC11.ASM</c>, routine <c>CIRKIL</c>/<c>CIRKP</c> (spheroid) and
///             <c>RRTK4.ASM</c>, routine <c>SQKIL</c>/<c>CIRKV</c> (quark)
///         </item>
///         <item>Disassembly: <c>asm/robomame.asm</c> at <c>$131E</c> (<c>DRAW_SPHEROID_POINTS_VALUE</c>)</item>
///     </list>
/// </remarks>
public sealed class ScoreBurst : IEntity
{
    /// <summary>The place of the first animation frame the burst shows.</summary>
    internal const int FirstBurstAnimationFrameIndex = 2;

    private readonly Texture2D[] _animationFrameTextures;
    private readonly Rectangle _bounds;

    // Where the points are drawn: a little to the right of the dead enemy and below it.

    private readonly Texture2D _pointsTexture;
    private readonly TextureSet _textureSet;
    private readonly ArcadeFrameTimer _stepArcadeFrameTimer = new();
    private int _animationFrameIndex = FirstBurstAnimationFrameIndex;
    private int _burstStepsRemaining;

    /// <summary>Makes one burst. <see cref="CreateForQuark" /> and <see cref="CreateForSpheroid" /> both use it.</summary>
    /// <param name="textureSet">The shared texture set.</param>
    /// <param name="animationFrameTextures">The dead enemy's animation frames, which the burst is drawn from.</param>
    /// <param name="pointsTexture">The sprite of the points number.</param>
    /// <param name="count">How many steps the burst takes, counting the one that ends it.</param>
    /// <param name="burstPaletteIndex">The palette index the burst is drawn in.</param>
    /// <param name="pointsPaletteIndex">The palette index the points are drawn in.</param>
    /// <param name="bounds">The box the enemy took up when it died.</param>
    private ScoreBurst(
        TextureSet textureSet,
        Texture2D[] animationFrameTextures,
        Texture2D pointsTexture,
        int count,
        int burstPaletteIndex,
        int pointsPaletteIndex,
        Rectangle bounds)
    {
        _textureSet = textureSet;
        _animationFrameTextures = animationFrameTextures;
        _pointsTexture = pointsTexture;
        BurstPaletteIndex = burstPaletteIndex;
        PointsPaletteIndex = pointsPaletteIndex;
        _bounds = bounds;

        // The first burst animation frame is already showing, so there is one step fewer left to take.
        _burstStepsRemaining = count - 1;
        PointsBounds = new Rectangle(
            bounds.X + ScreenSize.ToPortPixelsFromArcadePixels(ScoreBurstTuning.PointsOffsetXArcadePixels),
            bounds.Y + ScreenSize.ToPortPixelsFromArcadePixels(ScoreBurstTuning.PointsOffsetYArcadePixels),
            bounds.Width,
            bounds.Height);
    }

    /// <summary>Alive while the burst, and then the points, are shown. Dead after that.</summary>
    public EntityLifeState LifeState { get; private set; } = EntityLifeState.Alive;

    /// <summary>Where the dead enemy's top-left corner was. The burst is drawn there, at the enemy's size.</summary>
    public IntVector2 Position => new(_bounds.X, _bounds.Y);

    /// <summary>
    ///     Which animation frame the burst is showing. It only means something until the points are shown (see
    ///     <see cref="IsVisible" />).
    /// </summary>
    internal int AnimationFrameIndex => _animationFrameIndex;

    /// <summary>The palette index the burst is drawn in. Its colour keeps changing, so the burst shimmers.</summary>
    internal int BurstPaletteIndex { get; }

    /// <summary>Where the points are drawn: a little to the right of the dead enemy and below it.</summary>
    internal Rectangle PointsBounds { get; }

    /// <summary>The palette index the points are drawn in.</summary>
    internal int PointsPaletteIndex { get; }

    /// <summary>How many more steps the points are shown for.</summary>
    internal int PointsStepsRemaining { get; private set; }

    /// <summary>True once the burst shape is over and the number is showing. (The burst shape itself is on screen before that.)</summary>
    internal bool IsVisible { get; private set; }

    /// <summary>Makes the burst that a killed quark leaves. The burst and the points are drawn in the same palette index.</summary>
    /// <param name="textureSet">The shared texture set.</param>
    /// <param name="bounds">Where the quark was drawn when it died.</param>
    public static ScoreBurst CreateForQuark(TextureSet textureSet, Rectangle bounds)
    {
        return new ScoreBurst(
            textureSet,
            textureSet.QuarkAnimationFrameTextures,
            textureSet.RescueBonusAnimationFrameTextures[0],
            ScoreBurstTuning.QuarkCount,
            ScoreBurstTuning.QuarkBurstPaletteIndex,
            ScoreBurstTuning.QuarkPointsPaletteIndex,
            bounds);
    }

    /// <summary>
    ///     Makes the burst that a killed spheroid leaves. The burst is drawn in one palette index and the points in
    ///     another.
    /// </summary>
    /// <param name="textureSet">The shared texture set.</param>
    /// <param name="bounds">Where the spheroid was drawn when it died.</param>
    public static ScoreBurst CreateForSpheroid(TextureSet textureSet, Rectangle bounds)
    {
        return new ScoreBurst(
            textureSet,
            textureSet.SpheroidAnimationFrameTextures,
            textureSet.RescueBonusAnimationFrameTextures[0],
            ScoreBurstTuning.SpheroidCount,
            ScoreBurstTuning.SpheroidBurstPaletteIndex,
            ScoreBurstTuning.SpheroidPointsPaletteIndex,
            bounds);
    }

    /// <summary>Draws the burst as a shape filled with one colour. Once the burst is over, it draws the points the same way.</summary>
    /// <param name="spriteBatch">What the burst is drawn with.</param>
    public void Draw(SpriteBatch spriteBatch)
    {
        if (!this.IsAlive()) return;

        if (IsVisible)
        {
            _textureSet.BlitterDraw.DrawSpriteSolid(spriteBatch, _pointsTexture, PointsBounds,
                _textureSet.BlitterDraw.GetPaletteIndexColour(PointsPaletteIndex));
            return;
        }

        if (_animationFrameIndex < _animationFrameTextures.Length)
            _textureSet.BlitterDraw.DrawSpriteSolid(spriteBatch, _animationFrameTextures[_animationFrameIndex], _bounds,
                _textureSet.BlitterDraw.GetPaletteIndexColour(BurstPaletteIndex));
    }

    /// <summary>The box the dead enemy took up, which is also the box the burst is drawn in.</summary>
    public Rectangle GetBounds()
    {
        return _bounds;
    }

    /// <summary>
    ///     Runs one <see cref="Robotron2084.Core.ArcadeClock.PortTicksPerSecond">tick</see>. On each step the burst shows
    ///     its next animation frame; when it is over the score number is shown, and then the whole thing is gone.
    /// </summary>
    /// <param name="gameTime">Not used. The burst counts <see cref="Robotron2084.Core.ArcadeClock.PortTicksPerSecond">ticks</see>.</param>
    /// <param name="playField">Not used.</param>
    public void Update(GameTime gameTime, PlayField playField)
    {
        if (!this.IsAlive()) return;

        if (!_stepArcadeFrameTimer.Tick(ScoreBurstTuning.StepIntervalArcadeFrames)) return;

        if (!IsVisible)
        {
            // When the burst's steps run out, the burst goes and the points are shown in its place.
            if (--_burstStepsRemaining <= 0)
            {
                IsVisible = true;
                PointsStepsRemaining = ScoreBurstTuning.PointsSteps;
                return;
            }

            _animationFrameIndex++;
            return;
        }

        if (--PointsStepsRemaining <= 0) LifeState = EntityLifeState.Dead;
    }
}
