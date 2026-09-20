using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Level;
using Robotron2084.Rendering;
using Robotron2084.Tuning;

namespace Robotron2084.Entities;

/// <summary>
/// The two-part death effect a killed SPHEROID or QUARK leaves behind — not the strip explosion the
/// other robots get. First the enemy's silhouette flashes through its remaining animation frames as a
/// flat colour; then the enemy is gone and its "1000" points picture appears a little below and right
/// of where it died. Both parts use colour-cycling palette slots, so the effect shimmers with the
/// live palette.
/// </summary>
/// <remarks>
/// The ROM gives both enemies the same process: the spheroid's kill (<c>CIRKIL</c>) starts <c>CIRKP</c>
/// = "DRAW_SPHEROID_IN_DEATH_THROES" at $12F1, and the quark's kill (<c>SQKIL</c>) starts <c>CIRKV</c>,
/// which is literally <c>$1143: JMP $12FA</c> — the same routine entered just past the parameter setup.
/// Both do:
///
/// <list type="number">
/// <item><b>the burst:</b> erase the enemy's current picture, advance its animation pointer by one
/// descriptor, and draw the NEXT picture as a SOLID SILHOUETTE (the blitter's "solid and transparent
/// blit" with <c>$2D</c> set to the remap colour) — a countdown of <c>$07</c> (spheroid) / <c>$08</c>
/// (quark) steps at <c>NAP 2</c> each. The first iteration runs at the kill ITSELF (so picture 2
/// appears immediately) and the countdown is tested with <c>BEQ</c> BEFORE the draw, so the LAST step
/// only erases: the pictures shown are 2..count, i.e. count-1 of them. The count is the last picture's
/// index, which is why the spheroid (8 pictures) uses 7 and the quark (9 pictures) uses 8.</item>
/// <item><b>the points value:</b> the enemy is gone, and the "1000" picture is drawn at the death
/// position + 1 column / +5 rows (<c>ADDD #$0105</c> on the blitter's column:row destination) for
/// <c>LDA #$1E</c> = 30 steps of 2 frames, then erased.</item>
/// </list>
///
/// Colours (<c>LDD #$FFAA</c> / <c>LDD #$DDDD</c> — a colour per phase, and the disassembly's own
/// comment says "the same colour as the player score"): the spheroid's dying silhouette is <c>$AA</c> =
/// SLOT 10 (the current player's score slot, notes §58) and its points value <c>$FF</c> = SLOT 15; the
/// quark uses <c>$DD</c> = SLOT 13 for BOTH. All three are colour-CYCLING slots (10-15).
///
/// The "1000" picture is <c>P1KD</c>, the word at ROM $000C in the constant block at $0000-$001F
/// (3-byte JMP trampolines HULKST/HUMST/CKLIM/SKULL followed by 2-byte picture pointers): P1KD = $0485,
/// a 6-byte x 5-row picture whose data is at $0499 — byte for byte the "1000" the rescue marker uses
/// (<c>SpriteSet.RescueScoreDisplays[0]</c>). Both enemies score $0210 = 1000.
/// </remarks>
public sealed class ScoreBurst : IEntity
{
    private readonly Func<SpriteSet, Texture2D[]> _frames;
    private readonly Func<SpriteSet, Texture2D> _points;
    private readonly int _count;        // the ROM's countdown = the last picture's index
    private readonly int _burstSlot;
    private readonly int _pointsSlot;
    private readonly Rectangle _bounds;      // where the enemy was drawn
    private readonly Rectangle _pointsBounds;
    private int _fifths;                     // exact 6th-of-a-tick accumulator (see §52)
    private int _frameIndex = FirstBurstFrameIndex;
    private int _remaining;
    private bool _showingPoints;
    private int _pointsStepsRemaining;

    /// <summary>The first picture the burst shows (the ROM starts one past the live frame).</summary>
    internal const int FirstBurstFrameIndex = 2;

    /// <summary>Builds one burst — both static factories funnel through here.</summary>
    private ScoreBurst(
        Func<SpriteSet, Texture2D[]> frames,
        Func<SpriteSet, Texture2D> points,
        int count,
        int burstSlot,
        int pointsSlot,
        Rectangle bounds)
    {
        _frames = frames;
        _points = points;
        _count = count;
        _burstSlot = burstSlot;
        _pointsSlot = pointsSlot;
        _bounds = bounds;

        // The ROM's FIRST iteration runs at the kill itself: it erases the live
        // picture and draws picture 2, then sleeps `NAP 2`. So the port starts
        // SHOWING picture 2, and has count-1 steps left to take (the last of
        // which only erases — the countdown is tested before the draw).
        _remaining = count - 1;
        _pointsBounds = new Rectangle(
            bounds.X + ScreenSize.Scaled(GameplayConstants.ScoreBurstPointsOffsetXSpecPixels),
            bounds.Y + ScreenSize.Scaled(GameplayConstants.ScoreBurstPointsOffsetYSpecPixels),
            bounds.Width,
            bounds.Height);
    }

    /// <summary>Creates the burst a killed spheroid leaves: silhouette in the player's score colour, points in slot 15.</summary>
    /// <param name="bounds">Where the spheroid was drawn when it died; the silhouette is drawn there and the points just below and right of it.</param>
    /// <remarks>ROM `CIRKP` (`LDD #$FFAA`): silhouette <c>$AA</c> = slot 10, points <c>$FF</c> = slot 15.</remarks>
    public static ScoreBurst ForSpheroid(Rectangle bounds) => new(
        frames: static sprites => sprites.SpheroidFrames,
        points: static sprites => sprites.RescueScoreDisplays[0],
        count: GameplayConstants.ScoreBurstSpheroidCount,
        burstSlot: GameplayConstants.ScoreBurstSpheroidBurstSlot,
        pointsSlot: GameplayConstants.ScoreBurstSpheroidPointsSlot,
        bounds);

    /// <summary>Creates the burst a killed quark leaves: both phases in the same slot-13 colour.</summary>
    /// <param name="bounds">Where the quark was drawn when it died.</param>
    /// <remarks>ROM `CIRKV` (`LDD #$DDDD`): both phases use <c>$DD</c> = slot 13.</remarks>
    public static ScoreBurst ForQuark(Rectangle bounds) => new(
        frames: static sprites => sprites.QuarkFrames,
        points: static sprites => sprites.RescueScoreDisplays[0],
        count: GameplayConstants.ScoreBurstQuarkCount,
        burstSlot: GameplayConstants.ScoreBurstQuarkBurstSlot,
        pointsSlot: GameplayConstants.ScoreBurstQuarkPointsSlot,
        bounds);

    /// <summary>The dead enemy's top-left corner; the burst is drawn at the size it died at.</summary>
    public IntVector2 Position => new(_bounds.X, _bounds.Y);

    /// <summary>The dead enemy's own box, which is also the box the burst draws in.</summary>
    public Rectangle Bounds => _bounds;

    /// <summary>Alive for both phases (silhouette, then points), then Dead.</summary>
    public EntityLifeState LifeState { get; private set; } = EntityLifeState.Alive;

    /// <summary>The picture the burst is currently drawing (valid while <see cref="ShowingPoints"/> is false).</summary>
    internal int FrameIndex => _frameIndex;

    /// <summary>True once the burst has finished and the "1000" is showing.</summary>
    internal bool ShowingPoints => _showingPoints;

    /// <summary>The steps left of the "1000" display (the ROM's `LDA #$1E` countdown).</summary>
    internal int PointsStepsRemaining => _pointsStepsRemaining;

    /// <summary>The burst's palette slot (test hook — a cycling slot, so it shimmers).</summary>
    internal int BurstSlot => _burstSlot;

    /// <summary>The points picture's palette slot (test hook).</summary>
    internal int PointsSlot => _pointsSlot;

    /// <summary>Where the points picture is drawn: the death spot + 1 column / +5 rows (test hook).</summary>
    internal Rectangle PointsBounds => _pointsBounds;

    /// <summary>
    /// Advances the effect on its 2-frame clock: one silhouette picture per step, and — when the
    /// countdown runs out — the switch to the points picture and its own countdown.
    /// </summary>
    /// <param name="gameTime">Unused — the steps are counted in ROM frames.</param>
    /// <param name="field">Unused — the effect touches nothing on the playfield.</param>
    public void Update(GameTime gameTime, PlayField field)
    {
        if (LifeState != EntityLifeState.Alive)
        {
            return;
        }

        // `NAP 2` in the ROM. Accumulate in exact 6ths so a 2-frame delay is not
        // one of the 17-20% -fast short delays the port's PortTicks() rounding
        // produces (notes §52): 2 ROM frames = 12 sixths, and a port tick is 5.
        _fifths += 5;
        if (_fifths < GameplayConstants.ScoreBurstRomFramesPerStep * 6)
        {
            return;
        }

        _fifths -= GameplayConstants.ScoreBurstRomFramesPerStep * 6;

        if (!_showingPoints)
        {
            // ROM: DEC PD6 / BEQ (points) — the LAST step only erases, so the
            // pictures shown are 2..count (count-1 of them).
            if (--_remaining <= 0)
            {
                _showingPoints = true;
                _pointsStepsRemaining = GameplayConstants.ScoreBurstPointsSteps;
                return;
            }

            _frameIndex++;
            return;
        }

        if (--_pointsStepsRemaining <= 0)
        {
            LifeState = EntityLifeState.Dead;
        }
    }

    /// <summary>Draws the current phase: the solid silhouette, or the solid "1000" once the enemy is gone.</summary>
    /// <param name="spriteBatch">The batch to draw into.</param>
    /// <param name="sprites">The shared sprite set, which holds the enemy frames and the points picture.</param>
    public void Draw(SpriteBatch spriteBatch, SpriteSet sprites)
    {
        if (LifeState != EntityLifeState.Alive)
        {
            return;
        }

        if (_showingPoints)
        {
            // The points value is a SOLID single-colour blit too ("solid and
            // transparent blit" with $2D = the phase colour).
            sprites.DrawSpriteSolid(spriteBatch, _points(sprites), _pointsBounds, sprites.SlotColor(_pointsSlot));
            return;
        }

        Texture2D[] frames = _frames(sprites);
        if (_frameIndex < frames.Length)
        {
            sprites.DrawSpriteSolid(spriteBatch, frames[_frameIndex], _bounds, sprites.SlotColor(_burstSlot));
        }
    }
}
