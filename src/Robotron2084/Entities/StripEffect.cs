using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Level;
using Robotron2084.Tuning;

namespace Robotron2084.Entities;

/// <summary>The effect where a dying creature's sprite breaks into strips and fans apart, or a new robot's sprite shrinks into view.</summary>
/// <remarks>
/// <list type="bullet">
/// <item>Original source: <c>RRX7.ASM</c>/<c>RRHX4.ASM</c>/<c>RRDX2.ASM</c> (the death explosion) and <c>RRG23.ASM</c>, routine <c>APPEAR</c> (the shrinking appear)</item>
/// <item>Disassembly: <c>asm/robomame.asm</c> at <c>$5C1F</c> (<c>MAKE_ENEMY_EXPLODE</c>) and <c>$473F</c> (<c>CREATE_DIRECTIONAL_EXPLOSION</c>)</item>
/// </list>
/// </remarks>
public sealed class StripEffect : IEntity
{
    private readonly Func<Texture2D> _animationFrameOf;
    private readonly StripFanAxis _axis;
    private readonly Rectangle _bounds;
    private readonly StripClip _clip;
    private readonly StripEffectKind _kind;
    private readonly int _slope;          // the diagonal lean: -1 / 0 / +1 (ROM: SLOPE)
    private int _frames;
    private int _sizer;                   // the spacing accumulator; its high byte is this frame's step (ROM: YSIZER)
    private int _timer;                  // Counts up to the next ROM frame: 5 per tick, 6 per arcade frame (notes §52, §67.4)

    /// <summary>Builds one record; the two static factories below are the only callers.</summary>
    private StripEffect(
        Func<Texture2D> animationFrameOf,
        Rectangle bounds,
        StripEffectKind kind,
        StripFanAxis axis,
        int slope,
        StripClip clip)
    {
        _animationFrameOf = animationFrameOf;
        _bounds = bounds;
        _kind = kind;
        _axis = axis;
        _slope = slope;
        _clip = clip;

        // The spacing accumulator starts small for an explosion ("1 unit is the minimum") or large
        // for an appear, so it can shrink back down; an explosion also gets a frame count.
        _sizer = kind == StripEffectKind.Explode
            ? StripExplosionTuning.ExplosionStartSizer
            : StripExplosionTuning.AppearStartSizer;

        _frames = StripExplosionTuning.ExplosionFrames;
    }

    /// <summary>The dead entity's own box.</summary>
    public Rectangle Bounds => _bounds;

    /// <summary>Alive for the record's life: a fixed frame count, or until an appear's size would reach 1.</summary>
    public EntityLifeState LifeState { get; private set; } = EntityLifeState.Alive;

    /// <summary>The dead entity's top-left, which is also where the fan is centred (its middle).</summary>
    public IntVector2 Position => new(_bounds.X, _bounds.Y);

    /// <summary>The axis the pieces fly along: Rows for a vertical fan, Columns for a horizontal one (test hook).</summary>
    /// <remarks>The ROM calls these the vertical family (rows) and the horizontal family (columns).</remarks>
    internal StripFanAxis Axis => _axis;

    /// <summary>Explode or Appear (test hook).</summary>
    internal StripEffectKind Kind => _kind;

    /// <summary>The diagonal lean, -1 / 0 / +1 (test hook — see <see cref="FanForShot"/>).</summary>
    internal int Slope => _slope;

    /// <summary>The current spacing (the sizer's high byte) — test hook.</summary>
    internal int Spacing => Math.Max(1, _sizer >> 8);

    /// <summary>Starts an appear: the same record with the size running down, so the strips converge.</summary>
    /// <param name="source">The object materialising; its current animation frame is used.</param>
    /// <param name="bounds">The rect the strips are laid out in.</param>
    /// <param name="axis">Which way the sprite is cut: rows or columns.</param>
    /// <param name="slope">The diagonal lean, -1 / 0 / +1.</param>
    /// <param name="clip">The playfield interior that strips are dropped outside of.</param>
    /// <returns>The new appear record.</returns>
    /// <remarks>ROM: RRG23.ASM's <c>APPEAR</c> makes one of these per frame for each robot.</remarks>
    public static StripEffect CreateAppear(IAnimationFrameSource source, Rectangle bounds, StripFanAxis axis, int slope, StripClip clip)
        => new(() => source.GetCurrentAnimationFrame(), bounds, StripEffectKind.Appear, axis, slope, clip);

    /// <summary>Starts an appear for a plain picture rather than an entity: the logo's letters on the attract pages.</summary>
    /// <param name="animationFrame">The picture materialising.</param>
    /// <param name="bounds">The rect the strips are laid out in.</param>
    /// <param name="axis">Which way the picture is cut: rows or columns.</param>
    /// <param name="clip">The area strips are dropped outside of.</param>
    /// <returns>The new appear record.</returns>
    /// <remarks>Original source: <c>RRLOG.ASM</c> <c>WDONE1</c>, which asks the attract-mode appear (<c>AMAP</c>) for each letter.</remarks>
    internal static StripEffect CreateAppear(Texture2D animationFrame, Rectangle bounds, StripFanAxis axis, StripClip clip)
        => new(() => animationFrame, bounds, StripEffectKind.Appear, axis, 0, clip);

    /// <summary>Starts the explosion for a killed object; the killing shot picks the axis and lean.</summary>
    /// <param name="dead">The object being exploded; its animation frame and explosion bounds are used.</param>
    /// <param name="direction">The killing shot's direction, or null for a kill with no laser.</param>
    /// <param name="clip">The playfield interior that strips are dropped outside of.</param>
    /// <returns>The new explosion record.</returns>
    /// <remarks>ROM: the "make an enemy explode" entry point, which dispatches to its straight or
    /// directional setup. The rect is the object's position with the size of the sprite it points at,
    /// which can be bigger than its collision box.</remarks>
    public static StripEffect CreateExplosion(IExplodable dead, Direction8? direction, StripClip clip)
    {
        (StripFanAxis axis, int slope) = FanForShot(direction);
        return new StripEffect(() => dead.GetCurrentAnimationFrame(), dead.ExplosionBounds, StripEffectKind.Explode, axis, slope, clip);
    }

    /// <summary>Draws the frame's strips, each from its own row or column of the dead entity's sprite.</summary>
    /// <param name="spriteBatch">The batch to draw into.</param>
    public void Draw(SpriteBatch spriteBatch)
    {
        if (!this.IsAlive())
        {
            return;
        }

        Texture2D animationFrame = _animationFrameOf();

        // The sprite's width is in pixels and its height in rows; do not scale them back down (see Layout).
        int spriteWidth = animationFrame.Width;
        int spriteRows = animationFrame.Height;

        foreach (Strip strip in Layout(spriteWidth, spriteRows))
        {
            // Sources are in texture pixels; destinations are in screen pixels (pixel x SpecScale).
            Rectangle source = _axis == StripFanAxis.Rows
                ? new Rectangle(0, strip.SourceIndex, spriteWidth, 1)
                : new Rectangle(strip.SourceIndex, 0, 1, spriteRows);

            Rectangle dest = _axis == StripFanAxis.Rows
                ? new Rectangle(
                    strip.X * ScreenSize.SpecScale,
                    strip.Y * ScreenSize.SpecScale,
                    spriteWidth * ScreenSize.SpecScale,
                    ScreenSize.SpecScale)
                : new Rectangle(
                    strip.X * ScreenSize.SpecScale,
                    strip.Y * ScreenSize.SpecScale,
                    ScreenSize.SpecScale,
                    spriteRows * ScreenSize.SpecScale);

            spriteBatch.Draw(animationFrame, dest, source, Color.White);
        }
    }

    /// <summary>One ROM frame of the record's life.</summary>
    /// <param name="gameTime">Unused — the record is stepped once per ROM frame.</param>
    /// <param name="field">Unused; kept for the update call shape.</param>
    public void Update(GameTime gameTime, PlayField? field = null)
    {
        if (!this.IsAlive())
        {
            return;
        }

        // One step per ROM frame, not one per tick (see the remarks).
        _timer += ArcadeClock.UnitsPerPortTick;
        if (_timer < ArcadeClock.UnitsPerRomFrame)
        {
            return;
        }

        _timer -= ArcadeClock.UnitsPerRomFrame;

        if (_kind == StripEffectKind.Explode)
        {
            // Count the frame down; at zero the explosion is gone.
            if (--_frames <= 0)
            {
                LifeState = EntityLifeState.Dead;
                return;
            }

            _sizer += StripExplosionTuning.SizerStep;
            return;
        }

        // Shrink the spacing; the record dies once it would fall to 1 or less.
        int next = _sizer - StripExplosionTuning.SizerStep;
        if ((next >> 8) <= 1)
        {
            LifeState = EntityLifeState.Dead;
            return;
        }

        _sizer = next;
    }

    /// <summary>Maps a killing shot's direction to the fan axis and lean it produces.</summary>
    /// <param name="direction">The killing shot's direction, or null for a kill with no laser.</param>
    /// <returns>The fan axis and the lean: -1, 0 or +1.</returns>
    /// <remarks>ROM: RRX7.ASM's explosion-style routine, reached from "make an enemy explode". The
    /// engine is named for the axis the pieces MOVE, which is ACROSS the shot, not along it: a pure
    /// vertical shot uses the columns split (they fly apart horizontally), a pure horizontal shot or no
    /// direction at all uses the rows split, and a diagonal shot uses the rows split with the halves
    /// leaning opposite ways. These two branches are easy to swap by mistake.</remarks>
    internal static (StripFanAxis Axis, int Slope) FanForShot(Direction8? direction) => direction switch
    {
        // A pure vertical shot → cut into columns.
        Direction8.Up or Direction8.Down => (StripFanAxis.Columns, 0),

        // A pure horizontal shot, and every non-laser kill → cut into rows.
        Direction8.Left or Direction8.Right or null => (StripFanAxis.Rows, 0),

        // The diagonals: the row split, leaning.
        Direction8.UpLeft or Direction8.DownRight => (StripFanAxis.Rows, -1),
        Direction8.UpRight or Direction8.DownLeft => (StripFanAxis.Rows, 1),
        _ => (StripFanAxis.Rows, 0),
    };

    /// <summary>Where a sprite sits when drawn into the bounds, in pixels and rows.</summary>
    /// <param name="bounds">The entity's bounds, in screen pixels.</param>
    /// <param name="spriteWidth">The sprite's width in pixels.</param>
    /// <param name="spriteRows">The sprite's height in rows.</param>
    /// <returns>The top-left the sprite is drawn at, in pixels and rows.</returns>
    internal static (int Left, int Top) SpritePlacement(
        Rectangle bounds, int spriteWidth, int spriteRows)
    {
        int boundsWidth = bounds.Width / ScreenSize.SpecScale;
        int boundsRows = bounds.Height / ScreenSize.SpecScale;

        return (
            (bounds.X / ScreenSize.SpecScale) + ((boundsWidth - spriteWidth) / 2),
            (bounds.Y / ScreenSize.SpecScale) + ((boundsRows - spriteRows) / 2));
    }

    /// <summary>The strips for the current frame (pixels and rows), pure so the shape is unit-testable.</summary>
    /// <param name="spriteWidth">The dead sprite's width in pixels.</param>
    /// <param name="spriteRows">The dead sprite's height in rows.</param>
    /// <returns>The strips to draw this frame, in draw order.</returns>
    /// <remarks>ROM: RRX7.ASM/RRHX4.ASM/RRDX2.ASM's strip-layout logic:
    ///
    /// <code>
    /// YSIZE  = YSIZER >> 8                     ; this frame's step (1, 2, 3, …)
    /// base   = YCENT − YSIZE*YOF + YSIZE/2     ; the FIRST segment's screen row
    /// segment i: row = base + i*YSIZE          ; each further one steps DOWN
    /// </code>
    ///
    /// At step 1 the offset term cancels, so the base is the sprite's top row and frame 0 reconstructs
    /// the sprite exactly — the ROM's own "1 unit is the minimum" rule. One fan opens both ways at
    /// once: the base climbs while the segments march down, so the fan tears UP and DOWN. The fixed
    /// point is the sprite's MIDDLE, so the halves are mirrored — the same strips and the same reach
    /// each way — and a diagonal shot leans them opposite ways (a chevron). The same maths runs on
    /// columns for a vertical shot. The middle anchor matches the ROM's own centring logic, the
    /// sprite's top plus half its height, and keeps both halves equal; anchoring at the collision
    /// point instead is lopsided, because a shot strikes the sprite's near edge. A strip outside the
    /// playfield is DROPPED, not clamped.</remarks>
    internal IReadOnlyList<Strip> Layout(int spriteWidth, int spriteRows)
    {
        bool rows = _axis == StripFanAxis.Rows;
        int extent = rows ? spriteRows : spriteWidth;

        var strips = new List<Strip>(extent);
        int spacing = _sizer >> 8;
        if (spacing < 1)
        {
            spacing = 1;
        }

        // The fan's fixed point is the sprite's middle, derived from the sprite's own extent.
        int split = extent / 2;

        // The sprite is drawn centred in the bounds, so the fan must start from its own top-left.
        (int spriteLeft, int spriteTop) = SpritePlacement(_bounds, spriteWidth, spriteRows);

        // The fixed point's own screen row/column.
        int centre = (rows ? spriteTop : spriteLeft) + split;

        // One unit is ONE pixel of the sprite along the fan axis, for BOTH families: counting the
        // horizontal family in byte columns (2 px) would fly it off at twice the ROM's rate.
        int step = spacing;

        // The ROM's base: centre minus (size x offset) plus half a step. The "obscure bug" guard
        // (no half step when the offset is zero) is kept, though a middle-anchored fan never has one.
        int half = split == 0 ? 0 : (spacing >> 1);
        int fanBase = centre - (spacing * split) + half;

        // The diagonal lean is half the current step, signed by the shot's diagonal: strip i shifts
        // sideways in proportion to its distance from the split, so the two halves lean opposite ways.
        // The lean is measured in COLUMNS of the sprite the ROM cuts up, which is a pixel distance;
        // scaling it by SpecScale instead made the chevron open wider as the render scale rose.
        int drift = _slope * ((spacing >> 1) * ScreenSize.ArcadePixelsPerColumn);

        for (int i = 0; i < extent; i++)
        {
            int along = fanBase + (i * step);
            int lateral = (i - split) * drift;

            int x = rows ? spriteLeft + lateral : along;
            int y = rows ? along : spriteTop + lateral;

            if (IsInside(x, y, spriteWidth, spriteRows))
            {
                strips.Add(new Strip(i, x, y));
            }
        }

        return strips;
    }

    /// <summary>True when the strip lies inside the clip rectangle; a strip outside is dropped.</summary>
    /// <remarks>Matches the ROM's own per-strip clip check.</remarks>
    private bool IsInside(int x, int y, int spriteWidth, int spriteRows)
    {
        if (_axis == StripFanAxis.Rows)
        {
            return x >= _clip.MinX && x + spriteWidth <= _clip.MaxX && y >= _clip.MinY && y < _clip.MaxY;
        }

        return y >= _clip.MinY && y + spriteRows <= _clip.MaxY && x >= _clip.MinX && x < _clip.MaxX;
    }
}
