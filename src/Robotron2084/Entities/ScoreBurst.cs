using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Level;
using Robotron2084.Rendering;
using Robotron2084.Tuning;

namespace Robotron2084.Entities;

/// <summary>
/// The spheroid's and the quark's DEATH BURST — the arcade's own death sequence
/// for those two enemies, which is NOT the strip explosion (notes §64).
///
/// The ROM gives both the same process: the spheroid's kill (<c>CIRKIL</c>) starts
/// <c>CIRKP</c> = "DRAW_SPHEROID_IN_DEATH_THROES" at $12F1, and the quark's kill
/// (<c>SQKIL</c>) starts <c>CIRKV</c>, which is literally
/// <c>$1143: JMP $12FA</c> — the same routine entered just past the parameter
/// setup. Both do:
///
/// <list type="number">
/// <item><b>the burst:</b> erase the enemy's current picture, advance its
/// animation pointer by one descriptor, and draw the NEXT picture as a SOLID
/// SILHOUETTE (the blitter's "solid and transparent blit" with <c>$2D</c> set to
/// the remap colour) — a countdown of <c>$07</c> (spheroid) / <c>$08</c> (quark)
/// steps at <c>NAP 2</c> each. The first iteration runs at the kill ITSELF (so
/// picture 2 appears immediately) and the countdown is tested with <c>BEQ</c>
/// BEFORE the draw, so the LAST step only erases: the pictures shown are 2..count,
/// i.e. count-1 of them. The count is the last picture's index, which is why the
/// spheroid (8 pictures) uses 7 and the quark (9 pictures) uses 8.</item>
/// <item><b>the points value:</b> the enemy is gone, and the "1000" picture is
/// drawn at the death position + 1 column / +5 rows (<c>ADDD #$0105</c> on the
/// blitter's column:row destination) for <c>LDA #$1E</c> = 30 steps of 2 frames,
/// then erased.</item>
/// </list>
///
/// Colours (<c>LDD #$FFAA</c> / <c>LDD #$DDDD</c> — a colour per phase, and the
/// disassembly's own comment says "the same colour as the player score"): the
/// spheroid's dying silhouette is <c>$AA</c> = SLOT 10 (the current player's score
/// slot, notes §58) and its points value <c>$FF</c> = SLOT 15; the quark uses
/// <c>$DD</c> = SLOT 13 for BOTH. All three are colour-CYCLING slots (10-15), so
/// both effects shimmer with the live palette.
///
/// The "1000" picture is <c>P1KD</c>, the word at ROM $000C in the constant block
/// at $0000-$001F (3-byte JMP trampolines HULKST/HUMST/CKLIM/SKULL followed by
/// 2-byte picture pointers): P1KD = $0485, a 6-byte x 5-row picture whose data is
/// at $0499 — byte-for-byte the "1000" the rescue marker uses
/// (<c>SpriteSet.RescueScoreDisplays[0]</c>). Both enemies score $0210 = 1000.
/// </summary>
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

    /// <summary>ROM `CIRKP` (`LDD #$FFAA`): dying silhouette in $AA = slot 10, points in $FF = slot 15.</summary>
    public static ScoreBurst ForSpheroid(Rectangle bounds) => new(
        frames: static sprites => sprites.SpheroidFrames,
        points: static sprites => sprites.RescueScoreDisplays[0],
        count: GameplayConstants.ScoreBurstSpheroidCount,
        burstSlot: GameplayConstants.ScoreBurstSpheroidBurstSlot,
        pointsSlot: GameplayConstants.ScoreBurstSpheroidPointsSlot,
        bounds);

    /// <summary>ROM `CIRKV` (`LDD #$DDDD`): both phases in $DD = slot 13.</summary>
    public static ScoreBurst ForQuark(Rectangle bounds) => new(
        frames: static sprites => sprites.QuarkFrames,
        points: static sprites => sprites.RescueScoreDisplays[0],
        count: GameplayConstants.ScoreBurstQuarkCount,
        burstSlot: GameplayConstants.ScoreBurstQuarkBurstSlot,
        pointsSlot: GameplayConstants.ScoreBurstQuarkPointsSlot,
        bounds);

    public IntVector2 Position => new(_bounds.X, _bounds.Y);

    public Rectangle Bounds => _bounds;

    public EntityLifeState LifeState { get; private set; } = EntityLifeState.Alive;

    /// <summary>The picture the burst is currently drawing (valid while <see cref="ShowingPoints"/> is false).</summary>
    internal int FrameIndex => _frameIndex;

    /// <summary>True once the burst has finished and the "1000" is showing.</summary>
    internal bool ShowingPoints => _showingPoints;

    /// <summary>The steps left of the "1000" display (the ROM's `LDA #$1E` countdown).</summary>
    internal int PointsStepsRemaining => _pointsStepsRemaining;

    internal int BurstSlot => _burstSlot;

    internal int PointsSlot => _pointsSlot;

    internal Rectangle PointsBounds => _pointsBounds;

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
