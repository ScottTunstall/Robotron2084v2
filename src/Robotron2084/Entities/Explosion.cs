using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Level;
using Robotron2084.Rendering;
using Robotron2084.Tuning;

namespace Robotron2084.Entities;

/// <summary>The shared strip death effect: the dying sprite's rows or columns fan apart.</summary>
/// <remarks>ROM: the enemy-death and directional-explosion routines plus their shared setup and
/// per-frame layout code (RRX7.ASM/RRHX4.ASM/RRDX2.ASM; notes §61). An explosion runs a fixed number
/// of frames and its spacing grows by a fixed step each frame, so the spacing runs 1,2,3,…; an appear
/// (<see cref="Kind.Appear"/>, ROM: RRG23.ASM's <c>APPEAR</c>) starts large and shrinks, ending when
/// the size would reach 1. One fan opens UP and DOWN at once from the picture's MIDDLE, so both halves
/// carry half the strips and reach equally far; a diagonal shot leans them opposite ways (a chevron).
/// The port keeps the arcade's units: X in art pixels, Y in rows, one unit of spacing is ONE PIXEL
/// along the fan axis in both families, and a strip outside the clip is dropped rather than scaled. The
/// dead entity's frame is resolved at draw time, so this class holds no texture. Timers count 5 per
/// tick and 6 per arcade frame, so an interval of N frames is due at 6 x N.</remarks>
public sealed class Explosion : IEntity
{
    /// <summary>Explode = the spacing grows (the fan opens); Appear = it shrinks (it converges).</summary>
    public enum Kind
    {
        Explode,
        Appear,
    }

    private readonly Func<SpriteSet, Texture2D> _art;
    private readonly Rectangle _bounds;
    private readonly Kind _kind;
    private readonly StripFanAxis _axis;
    private readonly int _slope;          // the diagonal lean: -1 / 0 / +1 (ROM: SLOPE)
    private readonly StripClip _clip;
    private int _sizer;                   // the spacing accumulator; its high byte is this frame's step (ROM: YSIZER)
    private int _frames;
    private int _timer;                  // Counts up to the next ROM frame: 5 per tick, 6 per arcade frame (notes §52, §67.4)

    /// <summary>Builds one record; the two static factories below are the only callers.</summary>
    private Explosion(
        Func<SpriteSet, Texture2D> art,
        Rectangle bounds,
        Kind kind,
        StripFanAxis axis,
        int slope,
        StripClip clip)
    {
        _art = art;
        _bounds = bounds;
        _kind = kind;
        _axis = axis;
        _slope = slope;
        _clip = clip;

        // The spacing accumulator starts small for an explosion ("1 unit is the minimum") or large
        // for an appear, so it can shrink back down; an explosion also gets a frame count.
        _sizer = kind == Kind.Explode
            ? GameplayConstants.StripExplosionStartSizer
            : GameplayConstants.StripAppearStartSizer;

        _frames = GameplayConstants.StripExplosionFrames;
    }

    /// <summary>Starts the explosion for a killed object; the killing shot picks the axis and lean.</summary>
    /// <param name="dead">The object being exploded; its art and explosion bounds are used.</param>
    /// <param name="direction">The killing shot's direction, or null for a kill with no laser.</param>
    /// <param name="clip">The playfield interior that strips are dropped outside of.</param>
    /// <returns>The new explosion record.</returns>
    /// <remarks>ROM: the "make an enemy explode" entry point, which dispatches to its straight or
    /// directional setup. The rect is the object's position with the size of the picture it points at,
    /// which can be bigger than its collision box.</remarks>
    public static Explosion StartExplosion(IExplodable dead, Direction8? direction, StripClip clip)
    {
        (StripFanAxis axis, int slope) = Dispatch(direction);
        return Start(dead.CurrentFrameArt, dead.ExplosionBounds, Kind.Explode, axis, slope, clip);
    }

    /// <summary>Starts an appear: the same record with the size running down, so the strips converge.</summary>
    /// <param name="source">The object materialising; its current art is used.</param>
    /// <param name="bounds">The rect the strips are laid out in.</param>
    /// <param name="axis">Which way the sprite is cut: rows or columns.</param>
    /// <param name="slope">The diagonal lean, -1 / 0 / +1.</param>
    /// <param name="clip">The playfield interior that strips are dropped outside of.</param>
    /// <returns>The new appear record.</returns>
    /// <remarks>ROM: RRG23.ASM's <c>APPEAR</c> makes one of these per frame for each robot.</remarks>
    public static Explosion StartAppear(IArtSource source, Rectangle bounds, StripFanAxis axis, int slope, StripClip clip)
        => Start(source.CurrentFrameArt, bounds, Kind.Appear, axis, slope, clip);

    /// <summary>Shared construction, with the fan's fixed point at the picture's MIDDLE.</summary>
    /// <param name="art">Resolves the picture to cut up, at draw time.</param>
    /// <param name="bounds">The rect the strips are laid out in.</param>
    /// <param name="kind">Explode (the spacing grows) or Appear (it shrinks).</param>
    /// <param name="axis">Which way the sprite is cut: rows or columns.</param>
    /// <param name="slope">The diagonal lean, -1 / 0 / +1.</param>
    /// <param name="clip">The playfield interior that strips are dropped outside of.</param>
    /// <returns>The new record.</returns>
    /// <remarks>The middle anchor matches the ROM's own centring logic, the picture's top plus half its
    /// height, and keeps both halves equal. Anchoring at the collision point instead is lopsided,
    /// because a shot strikes the sprite's near edge. Either way the sprite reconstructs exactly at
    /// step 1 ("1 unit is the minimum"), so don't revert this without checking.</remarks>
    private static Explosion Start(
        Func<SpriteSet, Texture2D> art,
        Rectangle bounds,
        Kind kind,
        StripFanAxis axis,
        int slope,
        StripClip clip)
        => new(art, bounds, kind, axis, slope, clip);

    /// <summary>Maps a killing shot's direction to the fan axis and lean it produces.</summary>
    /// <param name="direction">The killing shot's direction, or null for a kill with no laser.</param>
    /// <returns>The fan axis and the lean: -1, 0 or +1.</returns>
    /// <remarks>ROM: RRX7.ASM's explosion-style routine, reached from "make an enemy explode". The
    /// engine is named for the axis the pieces MOVE, which is ACROSS the shot, not along it: a pure
    /// vertical shot uses the columns split (they fly apart horizontally), a pure horizontal shot or no
    /// direction at all uses the rows split, and a diagonal shot uses the rows split with the halves
    /// leaning opposite ways. These two branches are easy to swap by mistake.</remarks>
    internal static (StripFanAxis Axis, int Slope) Dispatch(Direction8? direction) => direction switch
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

    /// <summary>The dead entity's top-left, which is also where the fan is centred (its middle).</summary>
    public IntVector2 Position => new(_bounds.X, _bounds.Y);

    /// <summary>The dead entity's own box.</summary>
    public Rectangle Bounds => _bounds;

    /// <summary>Alive for the record's life: a fixed frame count, or until an appear's size would reach 1.</summary>
    public EntityLifeState LifeState { get; private set; } = EntityLifeState.Alive;

    /// <summary>Explode or Appear (test hook).</summary>
    internal Kind Mode => _kind;

    /// <summary>The current spacing (the sizer's high byte) — test hook.</summary>
    internal int Spacing => Math.Max(1, _sizer >> 8);

    /// <summary>The axis the pieces fly along: Rows for a vertical fan, Columns for a horizontal one (test hook).</summary>
    /// <remarks>The ROM calls these the vertical family (rows) and the horizontal family (columns).</remarks>
    internal StripFanAxis Axis => _axis;

    /// <summary>The diagonal lean, -1 / 0 / +1 (test hook — see <see cref="Dispatch"/>).</summary>
    internal int Slope => _slope;

    /// <summary>One ROM frame of the record's life.</summary>
    /// <param name="gameTime">Unused — the record is stepped once per ROM frame.</param>
    /// <param name="field">Unused; kept for the update call shape.</param>
    public void Update(GameTime gameTime, PlayField? field = null)
    {
        if (LifeState != EntityLifeState.Alive)
        {
            return;
        }

        // One step per ROM frame, not one per tick (see the remarks).
        _timer += 5;
        if (_timer < 6)
        {
            return;
        }

        _timer -= 6;

        if (_kind == Kind.Explode)
        {
            // Count the frame down; at zero the explosion is gone.
            if (--_frames <= 0)
            {
                LifeState = EntityLifeState.Dead;
                return;
            }

            _sizer += GameplayConstants.StripSizerStep;
            return;
        }

        // Shrink the spacing; the record dies once it would fall to 1 or less.
        int next = _sizer - GameplayConstants.StripSizerStep;
        if ((next >> 8) <= 1)
        {
            LifeState = EntityLifeState.Dead;
            return;
        }

        _sizer = next;
    }

    /// <summary>Draws the frame's strips, each from its own row or column of the dead entity's picture.</summary>
    /// <param name="spriteBatch">The batch to draw into.</param>
    /// <param name="sprites">The shared sprite set.</param>
    public void Draw(SpriteBatch spriteBatch, SpriteSet sprites)
    {
        if (LifeState != EntityLifeState.Alive)
        {
            return;
        }

        Texture2D art = _art(sprites);

        // art.Width is art pixels and art.Height is rows; do not scale them back down (see Layout).
        int widthArt = art.Width;
        int heightRows = art.Height;

        foreach (Strip strip in Layout(widthArt, heightRows))
        {
            // Sources are in texture pixels; destinations are in port pixels (art x SpecScale).
            Rectangle source = _axis == StripFanAxis.Rows
                ? new Rectangle(0, strip.SourceIndex, widthArt, 1)
                : new Rectangle(strip.SourceIndex, 0, 1, heightRows);

            Rectangle dest = _axis == StripFanAxis.Rows
                ? new Rectangle(
                    strip.X * ScreenSize.SpecScale,
                    strip.Y * ScreenSize.SpecScale,
                    widthArt * ScreenSize.SpecScale,
                    ScreenSize.SpecScale)
                : new Rectangle(
                    strip.X * ScreenSize.SpecScale,
                    strip.Y * ScreenSize.SpecScale,
                    ScreenSize.SpecScale,
                    heightRows * ScreenSize.SpecScale);

            spriteBatch.Draw(art, dest, source, Color.White);
        }
    }

    /// <summary>Where a picture sits when drawn into the bounds, in art pixels/rows.</summary>
    /// <param name="bounds">The entity's bounds, in port pixels.</param>
    /// <param name="textureWidth">The picture's width in art pixels.</param>
    /// <param name="textureHeight">The picture's height in rows.</param>
    /// <returns>The picture's extent and the top-left it is drawn at, in art pixels/rows.</returns>
    internal static (int WidthArt, int HeightRows, int Left, int Top) PicturePlacement(
        Rectangle bounds, int textureWidth, int textureHeight)
    {
        int boundsWidth = bounds.Width / ScreenSize.SpecScale;
        int boundsHeight = bounds.Height / ScreenSize.SpecScale;

        return (
            textureWidth,
            textureHeight,
            (bounds.X / ScreenSize.SpecScale) + ((boundsWidth - textureWidth) / 2),
            (bounds.Y / ScreenSize.SpecScale) + ((boundsHeight - textureHeight) / 2));
    }

    /// <summary>The strips for the current frame (art px / rows), pure so the shape is unit-testable.</summary>
    /// <param name="widthArt">The dead picture's width in art pixels.</param>
    /// <param name="heightRows">The dead picture's height in rows.</param>
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
    /// point is the picture's MIDDLE, so the halves are mirrored — the same strips and the same reach
    /// each way — and a diagonal shot leans them opposite ways (a chevron). The same maths runs on
    /// columns for a vertical shot. A strip outside the playfield is DROPPED, not clamped.</remarks>
    internal IReadOnlyList<Strip> Layout(int widthArt, int heightRows)
    {
        bool rows = _axis == StripFanAxis.Rows;
        int extent = rows ? heightRows : widthArt;

        var strips = new List<Strip>(extent);
        int spacing = _sizer >> 8;
        if (spacing < 1)
        {
            spacing = 1;
        }

        // The fan's fixed point is the picture's middle, derived from the art's own extent.
        int split = extent / 2;

        // The art is drawn centred in the bounds, so the fan must start from the art's own top-left.
        (int _, int _, int spriteLeft, int spriteTop) = PicturePlacement(_bounds, widthArt, heightRows);

        // The fixed point's own screen row/column.
        int centre = (rows ? spriteTop : spriteLeft) + split;

        // One unit is ONE pixel of the picture along the fan axis, for BOTH families: counting the
        // horizontal family in byte columns (2 px) would fly it off at twice the ROM's rate.
        const int unit = 1;
        int step = spacing * unit;

        // The ROM's base: centre minus (size x offset) plus half a step. The "obscure bug" guard
        // (no half step when the offset is zero) is kept, though a middle-anchored fan never has one.
        int half = split == 0 ? 0 : (spacing >> 1) * unit;
        int fanBase = centre - (spacing * split) + half;

        // The diagonal lean is half the current step, signed by the shot's diagonal: strip i shifts
        // sideways in proportion to its distance from the split, so the two halves lean opposite ways.
        // The lean is measured in COLUMNS of the picture the ROM cuts up, which is an art-pixel distance;
        // scaling it by SpecScale instead made the chevron open wider as the render scale rose.
        int drift = _slope * ((spacing >> 1) * GameplayConstants.ArcadePixelsPerColumn);

        for (int i = 0; i < extent; i++)
        {
            int along = fanBase + (i * step);
            int lateral = (i - split) * drift;

            int x = rows ? spriteLeft + lateral : along;
            int y = rows ? along : spriteTop + lateral;

            if (IsInside(x, y, widthArt, heightRows))
            {
                strips.Add(new Strip(i, x, y));
            }
        }

        return strips;
    }

    /// <summary>True when the strip lies inside the clip rectangle; a strip outside is dropped.</summary>
    /// <remarks>Matches the ROM's own per-strip clip check.</remarks>
    private bool IsInside(int x, int y, int widthArt, int heightRows)
    {
        if (_axis == StripFanAxis.Rows)
        {
            return x >= _clip.MinX && x + widthArt <= _clip.MaxX && y >= _clip.MinY && y < _clip.MaxY;
        }

        return y >= _clip.MinY && y + heightRows <= _clip.MaxY && x >= _clip.MinX && x < _clip.MaxX;
    }
}
