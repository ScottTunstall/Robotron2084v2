using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Graphics;
using Robotron2084.Level;
using Robotron2084.Tuning;

namespace Robotron2084.Entities;

/// <summary>The flashing shape and floating score number that appear for a moment when you kill a spheroid or a quark.</summary>
/// <seealso cref="Spheroid"/>
/// <seealso cref="Quark"/>
/// <remarks>
/// It has no beat. The <see cref="PlayField"/> calls <see cref="Update"/> on nearly every tick, through
/// <see cref="FieldEntities"/> and <see cref="PlayField.UpdateEntity"/>. <see cref="_stepTimer"/> gathers the ticks
/// until it is time for the next step (see <see cref="ArcadeClock"/>).
///
/// <list type="bullet">
/// <item>Original source: <c>RRC11.ASM</c>, routine <c>CIRKIL</c>/<c>CIRKP</c> (spheroid) and
/// <c>RRTK4.ASM</c>, routine <c>SQKIL</c>/<c>CIRKV</c> (quark)</item>
/// <item>Disassembly: <c>asm/robomame.asm</c> at <c>$131E</c> (<c>DRAW_SPHEROID_POINTS_VALUE</c>)</item>
/// </list>
/// </remarks>
public sealed class ScoreBurst : IEntity
{
    /// <summary>The place of the first animation frame the burst shows. It is the starting value of <see cref="_animationFrameIndex"/>.</summary>
    internal const int FirstBurstAnimationFrameIndex = 2;

    private readonly Texture2D[] _animationFrames;
    private readonly Rectangle _bounds;
    private readonly int _burstSlot;
    private readonly Texture2D _pointsSprite;

    // where the enemy was drawn
    private readonly Rectangle _pointsBounds;

    private readonly int _pointsSlot;
    private readonly SpriteSet _sprites;
    private int _animationFrameIndex = FirstBurstAnimationFrameIndex;
    private int _pointsStepsRemaining;
    private int _burstStepsRemaining;
    private bool _showingPoints;
    private int _stepTimer;                     // Counts up to the next step: 5 per tick, 6 per arcade frame.

    /// <summary>Builds one burst — both static factories funnel through here.</summary>
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
        _burstSlot = burstSlot;
        _pointsSlot = pointsSlot;
        _bounds = bounds;

        // The first burst animation frame appears at death, so count-1 steps remain; the last only erases.
        _burstStepsRemaining = count - 1;
        _pointsBounds = new Rectangle(
            bounds.X + ScreenSize.ToPortPixels(ScoreBurstTuning.PointsOffsetXSpecPixels),
            bounds.Y + ScreenSize.ToPortPixels(ScoreBurstTuning.PointsOffsetYSpecPixels),
            bounds.Width,
            bounds.Height);
    }

    /// <summary>The dead enemy's own box, which is also the box the burst draws in.</summary>
    public Rectangle GetBounds() => _bounds;

    /// <summary>Alive for both phases (silhouette, then points), then Dead.</summary>
    public EntityLifeState LifeState { get; private set; } = EntityLifeState.Alive;

    /// <summary>The dead enemy's top-left corner; the burst is drawn at the size it died at.</summary>
    public IntVector2 Position => new(_bounds.X, _bounds.Y);

    /// <summary>The animation frame the burst is currently drawing (valid while <see cref="ShowingPoints"/> is false).</summary>
    internal int AnimationFrameIndex => _animationFrameIndex;

    /// <summary>The burst's palette slot (test hook — a cycling slot, so it shimmers).</summary>
    internal int BurstSlot => _burstSlot;

    /// <summary>Where the points sprite is drawn: the death spot + 1 column / +5 rows (test hook).</summary>
    internal Rectangle PointsBounds => _pointsBounds;

    /// <summary>The points sprite's palette slot (test hook).</summary>
    internal int PointsSlot => _pointsSlot;

    /// <summary>The steps left of the "1000" display (its 30-step countdown).</summary>
    internal int PointsStepsRemaining => _pointsStepsRemaining;

    /// <summary>True once the burst has finished and the "1000" is showing.</summary>
    internal bool ShowingPoints => _showingPoints;

    /// <summary>Creates the burst a killed quark leaves: both phases in the same slot-13 colour.</summary>
    /// <param name="sprites">The shared sprite set.</param>
    /// <param name="bounds">Where the quark was drawn when it died.</param>
    public static ScoreBurst CreateForQuark(SpriteSet sprites, Rectangle bounds) => new(
        sprites,
        frames: sprites.QuarkAnimationFrames,
        pointsSprite: sprites.RescueScoreDisplays[0],
        count: ScoreBurstTuning.QuarkCount,
        burstSlot: ScoreBurstTuning.QuarkBurstSlot,
        pointsSlot: ScoreBurstTuning.QuarkPointsSlot,
        bounds: bounds);

    /// <summary>Creates the burst a killed spheroid leaves: silhouette in the player's score colour, points in slot 15.</summary>
    /// <param name="sprites">The shared sprite set.</param>
    /// <param name="bounds">Where the spheroid was drawn when it died.</param>
    public static ScoreBurst CreateForSpheroid(SpriteSet sprites, Rectangle bounds) => new(
        sprites,
        frames: sprites.SpheroidAnimationFrames,
        pointsSprite: sprites.RescueScoreDisplays[0],
        count: ScoreBurstTuning.SpheroidCount,
        burstSlot: ScoreBurstTuning.SpheroidBurstSlot,
        pointsSlot: ScoreBurstTuning.SpheroidPointsSlot,
        bounds: bounds);

    /// <summary>Draws the current phase: the solid silhouette, or the solid "1000" once the enemy is gone.</summary>
    /// <param name="spriteBatch">The batch to draw into.</param>
    public void Draw(SpriteBatch spriteBatch)
    {
        if (!this.IsAlive())
        {
            return;
        }

        if (_showingPoints)
        {
            _sprites.Blitter.DrawSpriteSolid(spriteBatch, _pointsSprite, _pointsBounds, _sprites.Blitter.GetSlotColour(_pointsSlot));
            return;
        }

        if (_animationFrameIndex < _animationFrames.Length)
        {
            _sprites.Blitter.DrawSpriteSolid(spriteBatch, _animationFrames[_animationFrameIndex], _bounds, _sprites.Blitter.GetSlotColour(_burstSlot));
        }
    }

    /// <summary>Advances the effect on its 2-frame clock: one silhouette per step, then the points.</summary>
    /// <param name="gameTime">Unused — the steps are counted in ticks.</param>
    /// <param name="field">Unused.</param>
    public void Update(GameTime gameTime, PlayField field)
    {
        if (!this.IsAlive())
        {
            return;
        }

        // Counts up to the next step: 5 per tick, 6 per arcade frame.
        _stepTimer += ArcadeClock.UnitsPerPortTick;
        if (_stepTimer < ArcadeClock.ToClockUnits(ScoreBurstTuning.RomFramesPerStep))
        {
            return;
        }

        _stepTimer -= ArcadeClock.ToClockUnits(ScoreBurstTuning.RomFramesPerStep);

        if (!_showingPoints)
        {
            // The last burst step only erases, so count-1 animation frames appear.
            if (--_burstStepsRemaining <= 0)
            {
                _showingPoints = true;
                _pointsStepsRemaining = ScoreBurstTuning.PointsSteps;
                return;
            }

            _animationFrameIndex++;
            return;
        }

        if (--_pointsStepsRemaining <= 0)
        {
            LifeState = EntityLifeState.Dead;
        }
    }
}
