using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Level;
using Robotron2084.Rendering;
using Robotron2084.Tuning;

namespace Robotron2084.Entities;

/// <summary>The death effect a killed spheroid or quark leaves — a strobing silhouette, then a "1000".</summary>
/// <seealso cref="Spheroid"/>
/// <seealso cref="Quark"/>
/// <remarks>ROM: RRC11.ASM's <c>CIRKIL</c>/<c>CIRKP</c> (spheroid) and RRTK4.ASM's <c>SQKIL</c>/
/// <c>CIRKV</c> (quark; its kill jumps into the spheroid's routine past the parameter setup). The
/// burst strobes the enemy's remaining pictures as flat silhouettes — 7 frames for the spheroid, 8 for
/// the quark, one every 2 ROM frames — and the first appears at the moment of death, so only count-1
/// pictures are drawn. Then the enemy is gone and a "1000" (byte-for-byte
/// <c>SpriteSet.RescueScoreDisplays[0]</c>) appears a little below-right for 30 steps of 2 frames.
/// Both phases use cycling palette slots, which is why they shimmer: the spheroid's silhouette uses
/// the score colour (slot 10) and its points slot 15; the quark uses slot 13 for both (notes §58).
/// Timers count 5 per tick and 6 per arcade frame, so an interval of N frames is due at 6 x N.</remarks>
public sealed class ScoreBurst : IEntity
{
    private readonly SpriteSet _sprites;
    private readonly Texture2D[] _frames;
    private readonly Texture2D _points;
    private readonly int _burstSlot;
    private readonly int _pointsSlot;
    private readonly Rectangle _bounds;      // where the enemy was drawn
    private readonly Rectangle _pointsBounds;
    private int _timer;                     // Counts up to the next step: 5 per tick, 6 per arcade frame.
    private int _frameIndex = FirstBurstFrameIndex;
    private int _remaining;
    private bool _showingPoints;
    private int _pointsStepsRemaining;

    /// <summary>The first picture the burst shows (the ROM starts one past the live frame).</summary>
    internal const int FirstBurstFrameIndex = 2;

    /// <summary>Builds one burst — both static factories funnel through here.</summary>
    private ScoreBurst(
        SpriteSet sprites,
        Texture2D[] frames,
        Texture2D points,
        int count,
        int burstSlot,
        int pointsSlot,
        Rectangle bounds)
    {
        _sprites = sprites;
        _frames = frames;
        _points = points;
        _burstSlot = burstSlot;
        _pointsSlot = pointsSlot;
        _bounds = bounds;

        // The first burst picture appears at death, so count-1 steps remain; the last only erases.
        _remaining = count - 1;
        _pointsBounds = new Rectangle(
            bounds.X + ScreenSize.Scaled(GameplayConstants.ScoreBurstPointsOffsetXSpecPixels),
            bounds.Y + ScreenSize.Scaled(GameplayConstants.ScoreBurstPointsOffsetYSpecPixels),
            bounds.Width,
            bounds.Height);
    }

    /// <summary>Creates the burst a killed spheroid leaves: silhouette in the player's score colour, points in slot 15.</summary>
    /// <param name="sprites">The shared sprite set.</param>
    /// <param name="bounds">Where the spheroid was drawn when it died.</param>
    public static ScoreBurst ForSpheroid(SpriteSet sprites, Rectangle bounds) => new(
        sprites,
        frames: sprites.SpheroidFrames,
        points: sprites.RescueScoreDisplays[0],
        count: GameplayConstants.ScoreBurstSpheroidCount,
        burstSlot: GameplayConstants.ScoreBurstSpheroidBurstSlot,
        pointsSlot: GameplayConstants.ScoreBurstSpheroidPointsSlot,
        bounds: bounds);

    /// <summary>Creates the burst a killed quark leaves: both phases in the same slot-13 colour.</summary>
    /// <param name="sprites">The shared sprite set.</param>
    /// <param name="bounds">Where the quark was drawn when it died.</param>
    public static ScoreBurst ForQuark(SpriteSet sprites, Rectangle bounds) => new(
        sprites,
        frames: sprites.QuarkFrames,
        points: sprites.RescueScoreDisplays[0],
        count: GameplayConstants.ScoreBurstQuarkCount,
        burstSlot: GameplayConstants.ScoreBurstQuarkBurstSlot,
        pointsSlot: GameplayConstants.ScoreBurstQuarkPointsSlot,
        bounds: bounds);

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

    /// <summary>Advances the effect on its 2-frame clock: one silhouette per step, then the points.</summary>
    /// <param name="gameTime">Unused — the steps are counted in ticks.</param>
    /// <param name="field">Unused.</param>
    public void Update(GameTime gameTime, PlayField field)
    {
        if (LifeState != EntityLifeState.Alive)
        {
            return;
        }

        // Counts up to the next step: 5 per tick, 6 per arcade frame.
        _timer += 5;
        if (_timer < GameplayConstants.ScoreBurstRomFramesPerStep * 6)
        {
            return;
        }

        _timer -= GameplayConstants.ScoreBurstRomFramesPerStep * 6;

        if (!_showingPoints)
        {
            // The last burst step only erases, so count-1 pictures appear.
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
    /// <param name="sprites">The shared sprite set.</param>
    public void Draw(SpriteBatch spriteBatch)
    {
        if (LifeState != EntityLifeState.Alive)
        {
            return;
        }

        if (_showingPoints)
        {
            _sprites.DrawSpriteSolid(spriteBatch, _points, _pointsBounds, _sprites.SlotColor(_pointsSlot));
            return;
        }

        if (_frameIndex < _frames.Length)
        {
            _sprites.DrawSpriteSolid(spriteBatch, _frames[_frameIndex], _bounds, _sprites.SlotColor(_burstSlot));
        }
    }
}
