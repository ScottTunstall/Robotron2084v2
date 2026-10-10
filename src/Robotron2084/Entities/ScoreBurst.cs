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
///     It has no beat. The <see cref="PlayField" /> calls <see cref="Update" /> on every tick, through
///     <see cref="FieldEntities" /> and <see cref="PlayField.UpdateEntity" />. The one time it does not is during the
///     short freeze just after the player is killed. <see cref="_stepTimer" /> gathers the ticks until it is time for
///     the next step (see <see cref="ArcadeClock" />).
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
    /// <summary>
    ///     The place of the first animation frame the burst shows. It is the starting value of
    ///     <see cref="_animationFrameIndex" />.
    /// </summary>
    internal const int FirstBurstAnimationFrameIndex = 2;

    private readonly Texture2D[] _animationFrames;
    private readonly Rectangle _bounds;

    // Where the points are drawn: a little to the right of the dead enemy and below it.

    private readonly Texture2D _pointsSprite;
    private readonly SpriteSet _sprites;
    private int _animationFrameIndex = FirstBurstAnimationFrameIndex;
    private int _burstStepsRemaining;
    private int _stepTimer; // Counts up to the burst's next step (see ArcadeClock).

    /// <summary>Makes one burst. <see cref="CreateForQuark" /> and <see cref="CreateForSpheroid" /> both use it.</summary>
    /// <param name="sprites">The shared sprite set.</param>
    /// <param name="frames">The dead enemy's animation frames, which the burst is drawn from.</param>
    /// <param name="pointsSprite">The sprite of the points number.</param>
    /// <param name="count">How many steps the burst takes, counting the one that ends it.</param>
    /// <param name="burstSlot">The palette slot the burst is drawn in.</param>
    /// <param name="pointsSlot">The palette slot the points are drawn in.</param>
    /// <param name="bounds">The box the enemy took up when it died.</param>
    private ScoreBurst(
        SpriteSet sprites,
        Texture2D[] frames,
        Texture2D pointsSprite,
        int count,
        int burstSlot,
        int pointsSlot,
        Rectangle bounds)
    {
        _sprites = sprites;
        _animationFrames = frames;
        _pointsSprite = pointsSprite;
        BurstSlot = burstSlot;
        PointsSlot = pointsSlot;
        _bounds = bounds;

        // The first burst animation frame is already showing, so there is one step fewer left to take.
        _burstStepsRemaining = count - 1;
        PointsBounds = new Rectangle(
            bounds.X + ScreenSize.ToPortPixelsFromArcadePixels(ScoreBurstTuning.PointsOffsetXArcadePixels),
            bounds.Y + ScreenSize.ToPortPixelsFromArcadePixels(ScoreBurstTuning.PointsOffsetYArcadePixels),
            bounds.Width,
            bounds.Height);
    }

    /// <summary>
    ///     Which animation frame the burst is showing. It only means something until the points are shown (see
    ///     <see cref="ShowingPoints" />).
    /// </summary>
    internal int AnimationFrameIndex => _animationFrameIndex;

    /// <summary>The palette slot the burst is drawn in. Its colour keeps changing, so the burst shimmers. Tests use this.</summary>
    internal int BurstSlot { get; }

    /// <summary>Where the points are drawn: a little to the right of the dead enemy and below it. Tests use this.</summary>
    internal Rectangle PointsBounds { get; }

    /// <summary>The palette slot the points are drawn in. Tests use this.</summary>
    internal int PointsSlot { get; }

    /// <summary>How many more steps the points are shown for.</summary>
    internal int PointsStepsRemaining { get; private set; }

    /// <summary>True once the burst is over and the points are showing.</summary>
    internal bool ShowingPoints { get; private set; }

    /// <summary>The box the dead enemy took up, which is also the box the burst is drawn in.</summary>
    public Rectangle GetBounds()
    {
        return _bounds;
    }

    /// <summary>Alive while the burst, and then the points, are shown. Dead after that.</summary>
    public EntityLifeState LifeState { get; private set; } = EntityLifeState.Alive;

    /// <summary>Where the dead enemy's top-left corner was. The burst is drawn there, at the enemy's size.</summary>
    public IntVector2 Position => new(_bounds.X, _bounds.Y);

    /// <summary>Draws the burst as a shape filled with one colour. Once the burst is over, it draws the points the same way.</summary>
    /// <param name="spriteBatch">What the burst is drawn with.</param>
    public void Draw(SpriteBatch spriteBatch)
    {
        if (!this.IsAlive()) return;

        if (ShowingPoints)
        {
            _sprites.Blitter.DrawSpriteSolid(spriteBatch, _pointsSprite, PointsBounds,
                _sprites.Blitter.GetSlotColour(PointsSlot));
            return;
        }

        if (_animationFrameIndex < _animationFrames.Length)
            _sprites.Blitter.DrawSpriteSolid(spriteBatch, _animationFrames[_animationFrameIndex], _bounds,
                _sprites.Blitter.GetSlotColour(BurstSlot));
    }

    /// <summary>
    ///     Runs one tick. On each step the burst shows its next animation frame. When the burst is over the points are
    ///     shown, and then the whole thing is gone.
    /// </summary>
    /// <param name="gameTime">Not used. The burst counts ticks.</param>
    /// <param name="field">Not used.</param>
    public void Update(GameTime gameTime, PlayField field)
    {
        if (!this.IsAlive()) return;

        // Wait until it is time for the next step (see ArcadeClock).
        _stepTimer += ArcadeClock.UnitsPerPortTick;
        if (_stepTimer < ArcadeClock.ToClockUnits(ScoreBurstTuning.RomFramesPerStep)) return;

        _stepTimer -= ArcadeClock.ToClockUnits(ScoreBurstTuning.RomFramesPerStep);

        if (!ShowingPoints)
        {
            // When the burst's steps run out, the burst goes and the points are shown in its place.
            if (--_burstStepsRemaining <= 0)
            {
                ShowingPoints = true;
                PointsStepsRemaining = ScoreBurstTuning.PointsSteps;
                return;
            }

            _animationFrameIndex++;
            return;
        }

        if (--PointsStepsRemaining <= 0) LifeState = EntityLifeState.Dead;
    }

    /// <summary>Makes the burst that a killed quark leaves. The burst and the points are drawn in the same palette slot.</summary>
    /// <param name="sprites">The shared sprite set.</param>
    /// <param name="bounds">Where the quark was drawn when it died.</param>
    public static ScoreBurst CreateForQuark(SpriteSet sprites, Rectangle bounds)
    {
        return new ScoreBurst(
            sprites,
            sprites.QuarkAnimationFrames,
            sprites.RescueScoreDisplays[0],
            ScoreBurstTuning.QuarkCount,
            ScoreBurstTuning.QuarkBurstSlot,
            ScoreBurstTuning.QuarkPointsSlot,
            bounds);
    }

    /// <summary>
    ///     Makes the burst that a killed spheroid leaves. The burst is drawn in one palette slot and the points in
    ///     another.
    /// </summary>
    /// <param name="sprites">The shared sprite set.</param>
    /// <param name="bounds">Where the spheroid was drawn when it died.</param>
    public static ScoreBurst CreateForSpheroid(SpriteSet sprites, Rectangle bounds)
    {
        return new ScoreBurst(
            sprites,
            sprites.SpheroidAnimationFrames,
            sprites.RescueScoreDisplays[0],
            ScoreBurstTuning.SpheroidCount,
            ScoreBurstTuning.SpheroidBurstSlot,
            ScoreBurstTuning.SpheroidPointsSlot,
            bounds);
    }
}
