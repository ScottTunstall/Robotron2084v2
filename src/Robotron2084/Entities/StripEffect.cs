using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Level;
using Robotron2084.Tuning;

namespace Robotron2084.Entities;

/// <summary>The effect where a dying creature's sprite breaks into strips and fans apart, or a new robot's sprite shrinks into view.</summary>
/// <remarks>
/// It has no beat. The <see cref="PlayField"/> calls <see cref="Update"/> on nearly every tick, through
/// <see cref="FieldEntities"/> and <see cref="PlayField.UpdateEntity"/>. <see cref="_romFrameTimer"/> gathers the
/// ticks until it is time for the next ROM frame, and it takes a step every ROM frame (see
/// <see cref="ArcadeClock"/>).
///
/// <list type="bullet">
/// <item>Original source: <c>RRX7.ASM</c>/<c>RRHX4.ASM</c>/<c>RRDX2.ASM</c> (the death explosion) and
/// <c>RRG23.ASM</c>, routine <c>APPEAR</c> (the shrinking appear)</item>
/// <item>Disassembly: <c>asm/robomame.asm</c> at <c>$5C1F</c> (<c>MAKE_ENEMY_EXPLODE</c>) and <c>$473F</c>
/// (<c>CREATE_DIRECTIONAL_EXPLOSION</c>)</item>
/// </list>
/// </remarks>
public sealed class StripEffect : IEntity
{
    private readonly Func<Texture2D> _getAnimationFrame;

    /// <summary>Gets the box of the thing an appear is forming, when the appear must stay with it as it moves; null when the effect stays where it started.</summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>RRX7.ASM</c> <c>AWRIT0</c>, <c>LDD PX / STA UL,Y</c> ("SCROLL EM"), which
    /// moves an appear to the player once the game is live.</item>
    /// <item>Disassembly: <c>$5D57</c> to <c>$5D61</c>.</item>
    /// </list>
    /// </remarks>
    private readonly Func<Rectangle>? _getFollowedBounds;

    private readonly StripFanAxis _axis;

    /// <summary>Works out which strip of the sprite the others close in on or fly away from, counted from the top row or the left column, when it is given how many strips the sprite has; null for the middle one.</summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>RRX7.ASM</c> <c>YOF</c>, and <c>RRHX4.ASM</c> <c>XOF</c>.</item>
    /// <item>Disassembly: the byte at offset <c>5</c> of a record, as at <c>$5BEF</c>.</item>
    /// </list>
    /// </remarks>
    private readonly Func<int, int>? _getCentreStripIndex;

    /// <summary>How much the gap shrinks on each ROM frame, when the effect is an appear.</summary>
    private readonly int _appearSizerStep;

    /// <summary>The smallest gap at which an appear is still drawn. When the gap would fall below it the appear is over.</summary>
    private readonly int _smallestAppearSpacing;

    private Rectangle _bounds;
    private readonly StripClip _clip;
    private readonly StripEffectKind _kind;
    private readonly int _slope;          // the diagonal lean: -1 / 0 / +1 (ROM: SLOPE)
    private int _romFramesRemaining;
    private int _spacingAccumulator;                   // the spacing accumulator; its high byte is this frame's step (ROM: YSIZER)
    private int _romFrameTimer;                  // Counts up to the next ROM frame: 5 per tick, 6 per arcade frame (notes §52, §67.4)

    /// <summary>True once an appear has taken its first step. The arcade draws nothing for an appear until then.</summary>
    private bool _hasAppearBeenDrawn;

    /// <summary>Builds one record; the two static factories below are the only callers.</summary>
    private StripEffect(
        Func<Texture2D> getAnimationFrame,
        Rectangle bounds,
        StripEffectKind kind,
        StripFanAxis axis,
        int slope,
        StripClip clip,
        Func<int, int>? getCentreStripIndex = null,
        int startClockUnits = 0,
        bool isClosedUpAtTheEnd = false,
        Func<Rectangle>? getFollowedBounds = null)
    {
        _getAnimationFrame = getAnimationFrame;
        _bounds = bounds;
        _kind = kind;
        _axis = axis;
        _slope = slope;
        _clip = clip;
        _getCentreStripIndex = getCentreStripIndex;
        _romFrameTimer = startClockUnits;
        _getFollowedBounds = getFollowedBounds;

        // The vertical and the horizontal routines shrink an appear by half a row a frame, and the diagonal one by a whole row.
        StripEngine engine = GetEngine();
        _appearSizerStep = engine == StripEngine.Diagonal ? StripExplosionTuning.SizerStep : StripExplosionTuning.SlowAppearSizerStep;
        _smallestAppearSpacing = isClosedUpAtTheEnd || engine == StripEngine.Horizontal
            ? StripExplosionTuning.SmallestClosedAppearSpacing
            : StripExplosionTuning.SmallestAppearSpacing;

        // The spacing accumulator starts small for an explosion ("1 unit is the minimum") or large
        // for an appear, so it can shrink back down; an explosion also gets a frame count.
        _spacingAccumulator = kind == StripEffectKind.Explode
            ? StripExplosionTuning.ExplosionStartSizer
            : StripExplosionTuning.AppearStartSizer;

        _romFramesRemaining = StripExplosionTuning.ExplosionFrames;
    }

    /// <summary>The dead entity's own box.</summary>
    public Rectangle GetBounds() => _bounds;

    /// <summary>Alive for the record's life: a fixed frame count, or until an appear's size would reach 1.</summary>
    public EntityLifeState LifeState { get; private set; } = EntityLifeState.Alive;

    /// <summary>The dead entity's top-left, which is also where the fan is centred (its middle).</summary>
    public IntVector2 Position => new(_bounds.X, _bounds.Y);

    /// <summary>The axis the pieces fly along: Rows for a vertical fan, Columns for a horizontal one (test hook).</summary>
    /// <remarks>The ROM calls these the vertical family (rows) and the horizontal family (columns).</remarks>
    internal StripFanAxis Axis => _axis;

    /// <summary>Explode or Appear (test hook).</summary>
    internal StripEffectKind Kind => _kind;

    /// <summary>The diagonal lean, -1 / 0 / +1 (test hook — see <see cref="GetFanForShot"/>).</summary>
    internal int Slope => _slope;

    /// <summary>The current spacing (the sizer's high byte) — test hook.</summary>
    internal int GetSpacing() => Math.Max(1, _spacingAccumulator >> 8);

    /// <summary>Starts an appear: the same record with the size running down, so the strips converge.</summary>
    /// <param name="source">The object materialising; its current animation frame is used.</param>
    /// <param name="bounds">The rect the strips are laid out in.</param>
    /// <param name="axis">Which way the sprite is cut: rows or columns.</param>
    /// <param name="slope">The diagonal lean, -1 / 0 / +1.</param>
    /// <param name="clip">The playfield interior that strips are dropped outside of.</param>
    /// <param name="getCentreStripIndex">Works out which row or column the others close in on, counted from the top or the left, when it is given how many rows or columns the sprite has; null for the middle one.</param>
    /// <param name="startClockUnits">How far into a ROM frame the effect starts, in clock units, so that its first step falls on the right ROM frame. It is less than nothing when the first step is more than a tick away.</param>
    /// <returns>The new appear record.</returns>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>RRG23.ASM</c> <c>APPEAR</c>, which makes one of these on each ROM frame;
    /// <c>RRX7.ASM</c> <c>APSTV</c>, <c>RRHX4.ASM</c> <c>HAPSTV</c> and <c>RRDX2.ASM</c> <c>APSTZ</c></item>
    /// <item>Disassembly: <c>$5BC6</c> (the vertical routine), <c>$F066</c> (the horizontal one) and
    /// <c>$46E6</c> (the diagonal one)</item>
    /// </list>
    /// </remarks>
    public static StripEffect CreateAppear(IAnimationFrameSource source, Rectangle bounds, StripFanAxis axis, int slope, StripClip clip, Func<int, int>? getCentreStripIndex = null, int startClockUnits = 0)
        => new(() => source.GetCurrentAnimationFrame(), bounds, StripEffectKind.Appear, axis, slope, clip, getCentreStripIndex, startClockUnits);

    /// <summary>Starts an appear that stays with the thing it is forming as that thing moves. The player's appear is the one that does: it is still running when the game goes live and the player can walk.</summary>
    /// <typeparam name="T">The kind of thing that is appearing.</typeparam>
    /// <param name="source">The thing that is appearing. Its animation frame and its box are read again each time the picture changes.</param>
    /// <param name="axis">Which way the sprite is cut: rows or columns.</param>
    /// <param name="slope">The diagonal lean, -1 / 0 / +1.</param>
    /// <param name="clip">The playfield interior that strips are dropped outside of.</param>
    /// <param name="centreStripIndex">Which row or column the others close in on, counted from the top or the left.</param>
    /// <param name="startClockUnits">How far into a ROM frame the effect starts, in clock units.</param>
    /// <returns>The new appear record.</returns>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>RRX7.ASM</c> <c>AWRIT0</c> ("SCROLL EM"), <c>RRHX4.ASM</c> <c>AWRIT0</c>
    /// ("SCROLL PLAYER APPEAR") and <c>RRDX2.ASM</c> <c>AWRIT0</c>, each of which moves the appear to
    /// the player when <c>STATUS</c> is clear</item>
    /// <item>Disassembly: <c>$5D57</c> to <c>$5D61</c> for the vertical routine</item>
    /// </list>
    /// </remarks>
    public static StripEffect CreateFollowingAppear<T>(T source, StripFanAxis axis, int slope, StripClip clip, int centreStripIndex, int startClockUnits)
        where T : IEntity, IAnimationFrameSource
        => new(() => source.GetCurrentAnimationFrame(), source.GetBounds(), StripEffectKind.Appear, axis, slope, clip, _ => centreStripIndex, startClockUnits, getFollowedBounds: () => source.GetBounds());

    /// <summary>Starts an appear for a plain picture rather than an entity: the logo's letters on the attract pages.</summary>
    /// <param name="animationFrame">The picture materialising.</param>
    /// <param name="bounds">The rect the strips are laid out in.</param>
    /// <param name="axis">Which way the picture is cut: rows or columns.</param>
    /// <param name="clip">The area strips are dropped outside of.</param>
    /// <returns>The new appear record.</returns>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>RRLOG.ASM</c> <c>WDONE1</c>, which asks the attract-mode appear
    /// (<c>AMAP</c>) for each letter. That appear draws the strips closed up before it ends
    /// (<c>RRX7.ASM</c> <c>AWW2</c>, "FORCE SIZE OF 1").</item>
    /// <item>Disassembly: <c>$5B98</c> and <c>$5D7C</c> to <c>$5D85</c>.</item>
    /// </list>
    /// </remarks>
    internal static StripEffect CreateAppear(Texture2D animationFrame, Rectangle bounds, StripFanAxis axis, StripClip clip)
        => new(() => animationFrame, bounds, StripEffectKind.Appear, axis, 0, clip, isClosedUpAtTheEnd: true);

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
        (StripFanAxis axis, int slope) = GetFanForShot(direction);
        return new StripEffect(() => dead.GetCurrentAnimationFrame(), dead.GetExplosionBounds(), StripEffectKind.Explode, axis, slope, clip);
    }

    /// <summary>Draws the frame's strips, each from its own row or column of the dead entity's sprite.</summary>
    /// <param name="spriteBatch">The batch to draw into.</param>
    public void Draw(SpriteBatch spriteBatch)
    {
        if (!this.IsAlive() || !IsDrawn())
        {
            return;
        }

        Texture2D animationFrame = _getAnimationFrame();

        // The sprite's width is in pixels and its height in rows; do not scale them back down (see Layout).
        int spriteWidth = animationFrame.Width;
        int spriteRows = animationFrame.Height;

        foreach (Strip strip in LayOutStrips(spriteWidth, spriteRows))
        {
            // Sources are in texture pixels; destinations are in screen pixels (pixel x SpecScale).
            Rectangle source = _axis == StripFanAxis.Rows
                ? new Rectangle(0, strip.SourceIndex, spriteWidth, 1)
                : new Rectangle(strip.SourceIndex, 0, 1, spriteRows);

            Rectangle destination = _axis == StripFanAxis.Rows
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

            spriteBatch.Draw(animationFrame, destination, source, Color.White);
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
        _romFrameTimer += ArcadeClock.UnitsPerPortTick;
        if (_romFrameTimer < ArcadeClock.UnitsPerRomFrame)
        {
            return;
        }

        _romFrameTimer -= ArcadeClock.UnitsPerRomFrame;

        if (_kind == StripEffectKind.Explode)
        {
            // Count the frame down; at zero the explosion is gone.
            if (--_romFramesRemaining <= 0)
            {
                LifeState = EntityLifeState.Dead;
                return;
            }

            _spacingAccumulator += StripExplosionTuning.SizerStep;
            return;
        }

        StepAppear();
    }

    /// <summary>Says which of the arcade's three strip routines runs this effect.</summary>
    /// <returns>The horizontal routine for a column fan, the diagonal one for a row fan that leans, and the vertical one for a row fan that does not.</returns>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>RRX7.ASM</c> <c>EXSTV</c>, which picks the routine from the laser's
    /// direction.</item>
    /// <item>Disassembly: <c>$5C1F</c>.</item>
    /// </list>
    /// </remarks>
    internal StripEngine GetEngine()
    {
        if (_axis == StripFanAxis.Columns)
        {
            return StripEngine.Horizontal;
        }

        return _slope == 0 ? StripEngine.Vertical : StripEngine.Diagonal;
    }

    /// <summary>Says whether the effect has anything on the screen. An appear has nothing until its first step.</summary>
    internal bool IsDrawn() => _kind == StripEffectKind.Explode || _hasAppearBeenDrawn;

    /// <summary>Takes one ROM frame off an appear's gap. The picture changes only when the whole-row part of the gap changes, and the appear is over when the gap would fall below the smallest that its routine draws.</summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>AWRITE</c> in <c>RRX7.ASM</c>, <c>RRHX4.ASM</c> and <c>RRDX2.ASM</c>:
    /// <c>CMPA YSIZER,Y</c> ("CHANGE?") skips the frame when the row count is the same, and the tests
    /// after <c>AWRIT1</c> end the appear</item>
    /// <item>Disassembly: <c>$5D48</c> to <c>$5D85</c> for the vertical routine</item>
    /// </list>
    /// </remarks>
    private void StepAppear()
    {
        int next = _spacingAccumulator - _appearSizerStep;
        if ((next >> 8) == (_spacingAccumulator >> 8))
        {
            _spacingAccumulator = next;
            return;
        }

        if ((next >> 8) < _smallestAppearSpacing)
        {
            LifeState = EntityLifeState.Dead;
            return;
        }

        _spacingAccumulator = next;
        _hasAppearBeenDrawn = true;
        if (_getFollowedBounds is not null)
        {
            _bounds = _getFollowedBounds();
        }
    }

    /// <summary>Maps a killing shot's direction to the fan axis and lean it produces.</summary>
    /// <param name="direction">The killing shot's direction, or null for a kill with no laser.</param>
    /// <returns>The fan axis and the lean: -1, 0 or +1.</returns>
    /// <remarks>ROM: RRX7.ASM's explosion-style routine, reached from "make an enemy explode". The
    /// engine is named for the axis the pieces MOVE, which is ACROSS the shot, not along it: a pure
    /// vertical shot uses the columns split (they fly apart horizontally), a pure horizontal shot or no
    /// direction at all uses the rows split, and a diagonal shot uses the rows split with the halves
    /// leaning opposite ways. These two branches are easy to swap by mistake.</remarks>
    internal static (StripFanAxis Axis, int Slope) GetFanForShot(Direction8? direction) => direction switch
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
    internal static (int Left, int Top) GetSpritePlacement(
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
    internal IReadOnlyList<Strip> LayOutStrips(int spriteWidth, int spriteRows)
    {
        bool fansByRows = _axis == StripFanAxis.Rows;
        int extent = fansByRows ? spriteRows : spriteWidth;

        var strips = new List<Strip>(extent);
        int spacing = _spacingAccumulator >> 8;
        if (spacing < 1)
        {
            spacing = 1;
        }

        // The fan's fixed point is the strip it was given, or the sprite's middle when it was given none.
        int split = GetCentreStripIndex(extent);

        // The sprite is drawn centred in the bounds, so the fan must start from its own top-left.
        (int spriteLeft, int spriteTop) = GetSpritePlacement(_bounds, spriteWidth, spriteRows);

        // The fixed point's own screen row/column.
        int centre = (fansByRows ? spriteTop : spriteLeft) + split;

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

            int x = fansByRows ? spriteLeft + lateral : along;
            int y = fansByRows ? along : spriteTop + lateral;

            if (IsInside(x, y, spriteWidth, spriteRows))
            {
                strips.Add(new Strip(i, x, y));
            }
        }

        return strips;
    }

    /// <summary>Gets which strip the others close in on or fly away from.</summary>
    /// <param name="extent">How many strips the sprite is cut into.</param>
    /// <returns>The strip the effect was given, or the middle one when it was given none.</returns>
    private int GetCentreStripIndex(int extent) => _getCentreStripIndex?.Invoke(extent) ?? (extent / 2);

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
