using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Level;
using Robotron2084.Rendering;
using Robotron2084.Tuning;

namespace Robotron2084.Entities;

/// <summary>
/// The death effect a killed <see cref="Spheroid"/> or <see cref="Quark"/> leaves behind — these two
/// enemies do not get the strip <see cref="Explosion"/> the other robots get; they get this two-phase
/// effect instead. <b>Phase 1</b>: the enemy's own shape flashes through its remaining animation
/// pictures, each one drawn as a single flat "silhouette" colour rather than its normal art (a quick
/// strobe effect). <b>Phase 2</b>: the enemy is now gone, and a "1000" points picture fades in a little
/// below and to the right of where it died, then disappears. Both phases are drawn in one of the
/// palette's colour-cycling slots (slots 10-15, whose actual RGB value keeps changing every frame — see
/// notes §58), so the effect visibly shimmers rather than staying a fixed colour.
/// </summary>
/// <remarks>
/// <para>
/// The ROM gives both enemies the exact same two-phase death process, just entered from different
/// places: the spheroid's kill routine and the quark's kill routine both hand off into one shared
/// routine that runs the burst-then-points sequence (ROM: RRC11.ASM's <c>CIRKIL</c>/<c>CIRKP</c> for the
/// spheroid, RRTK4.ASM's <c>SQKIL</c>/<c>CIRKV</c> for the quark — the quark's kill jumps straight into
/// the spheroid's routine partway through, past its own parameter setup). Both do:
/// </para>
/// <list type="number">
/// <item><b>the burst:</b> erase the enemy's current picture, step to the next picture in its
/// animation, and redraw it as a flat, solid-colour silhouette instead of its normal art — a strobing
/// countdown of 7 frames for the spheroid / 8 frames for the quark, at one frame every 2 ROM frames (see
/// the "beat"/ROM-frame glossary on <see cref="IEntity"/>). The very first frame of the countdown
/// appears immediately at the moment of death, and the last frame of the countdown only erases and
/// shows nothing further — so what's actually drawn on screen is one fewer picture than the frame count
/// (count-1 pictures shown).</item>
/// <item><b>the points value:</b> once the burst finishes, the enemy is gone and a "1000" points
/// picture fades in a little below and to the right of the death spot, stays for 30 steps of 2 ROM
/// frames each, then disappears too.</item>
/// </list>
/// <para>
/// Both phases are drawn in one of the palette's colour-cycling slots (10-15), so they shimmer rather
/// than sit at a flat colour, matching the disassembly's own note that these reuse "the same colour as
/// the player score": the spheroid's dying silhouette uses the current player's score colour (slot 10,
/// notes §58) and its points value uses slot 15; the quark uses slot 13 for both phases.
/// </para>
/// <para>
/// The "1000" picture drawn here is byte-for-byte the same picture the rescue marker shows for a
/// player's first rescue (<c>SpriteSet.RescueScoreDisplays[0]</c>) — the ROM points at the very same
/// picture data for both. Both enemies award 1000 points when killed this way.
/// </para>
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
    private int _timer;                     // fixed-point ROM-frame-to-port-tick accumulator; see the glossary on IEntity
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

        // The ROM's first burst frame appears at the instant of death: the enemy's
        // live picture is erased and the next one drawn right away. So the port
        // starts already showing that second picture, and has count-1 steps left
        // to take — the very last of those steps only erases, showing nothing.
        _remaining = count - 1;
        _pointsBounds = new Rectangle(
            bounds.X + ScreenSize.Scaled(GameplayConstants.ScoreBurstPointsOffsetXSpecPixels),
            bounds.Y + ScreenSize.Scaled(GameplayConstants.ScoreBurstPointsOffsetYSpecPixels),
            bounds.Width,
            bounds.Height);
    }

    /// <summary>Creates the burst a killed spheroid leaves: silhouette in the player's score colour, points in slot 15.</summary>
    /// <param name="bounds">Where the spheroid was drawn when it died; the silhouette is drawn there and the points just below and right of it.</param>
    /// <remarks>ROM: <c>CIRKP</c> — silhouette in the score colour (slot 10), points in slot 15.</remarks>
    public static ScoreBurst ForSpheroid(Rectangle bounds) => new(
        frames: static sprites => sprites.SpheroidFrames,
        points: static sprites => sprites.RescueScoreDisplays[0],
        count: GameplayConstants.ScoreBurstSpheroidCount,
        burstSlot: GameplayConstants.ScoreBurstSpheroidBurstSlot,
        pointsSlot: GameplayConstants.ScoreBurstSpheroidPointsSlot,
        bounds);

    /// <summary>Creates the burst a killed quark leaves: both phases in the same slot-13 colour.</summary>
    /// <param name="bounds">Where the quark was drawn when it died.</param>
    /// <remarks>ROM: <c>CIRKV</c> — both phases share the same colour, slot 13.</remarks>
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

    /// <summary>The steps left of the "1000" display (its 30-step countdown).</summary>
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

        // The arcade waits 2 ROM frames between steps. `_timer` is this project's
        // fixed-point accumulator (in units of 1/5 of a port tick — see the terminology
        // glossary on IEntity) that converts that ROM-frame delay to an exact number of
        // port ticks without drift or floating point (notes §52).
        _timer += 5;
        if (_timer < GameplayConstants.ScoreBurstRomFramesPerStep * 6)
        {
            return;
        }

        _timer -= GameplayConstants.ScoreBurstRomFramesPerStep * 6;

        if (!_showingPoints)
        {
            // Count down the burst frames; the very last one only erases and shows
            // nothing, which is why count-1 pictures actually appear on screen.
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
            // The points picture is drawn as a flat solid colour too, same as the burst.
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
