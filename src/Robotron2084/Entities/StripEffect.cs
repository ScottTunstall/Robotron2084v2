using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Level;
using Robotron2084.Tuning;

namespace Robotron2084.Entities;

/// <summary>The effect where a sprite is cut into strips. When something is killed, the strips fly apart. When something appears, the strips start far apart and close up to make the sprite.</summary>
/// <remarks>
/// It has no beat. The <see cref="PlayField"/> calls <see cref="Update"/> on every tick, through
/// <see cref="FieldEntities"/> and <see cref="PlayField.UpdateEntity"/>. The one time it does not is during the
/// short freeze just after the player is killed. <see cref="_romFrameTimer"/> gathers the ticks until it is time
/// for the effect's next step. It takes a step 50 times a second (see <see cref="ArcadeClock"/>).
///
/// <list type="bullet">
/// <item>Original source: <c>RRX7.ASM</c>/<c>RRHX4.ASM</c>/<c>RRDX2.ASM</c> (the explosion) and
/// <c>RRG23.ASM</c>, routine <c>APPEAR</c> (the appear)</item>
/// <item>Disassembly: <c>asm/robomame.asm</c> at <c>$5C1F</c> (<c>MAKE_ENEMY_EXPLODE</c>) and <c>$473F</c>
/// (<c>CREATE_DIRECTIONAL_EXPLOSION</c>)</item>
/// </list>
/// </remarks>
public sealed class StripEffect : IEntity
{
    private readonly Func<Texture2D> _getAnimationFrame;

    /// <summary>Gets the box of the thing that is appearing, so that the appear can stay with it as it moves. It is null when the effect stays where it started.</summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>RRX7.ASM</c> <c>AWRIT0</c>, <c>LDD PX / STA UL,Y</c> ("SCROLL EM"), which
    /// moves an appear to the player once the game is live.</item>
    /// <item>Disassembly: <c>$5D57</c> to <c>$5D61</c>.</item>
    /// </list>
    /// </remarks>
    private readonly Func<Rectangle>? _getFollowedBounds;

    private readonly StripFanAxis _axis;

    /// <summary>Works out which strip the other strips close in on or fly away from. It is given how many strips the sprite has, and gives back a strip counted from the top or the left. If it is null, the middle strip is used.</summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>RRX7.ASM</c> <c>YOF</c>, and <c>RRHX4.ASM</c> <c>XOF</c>.</item>
    /// <item>Disassembly: the byte 5 bytes from the start of a record, as at <c>$5BEF</c>.</item>
    /// </list>
    /// </remarks>
    private readonly Func<int, int>? _getCentreStripIndex;

    /// <summary>How much the gap between the strips shrinks on each step, when the effect is an appear. It is measured the way <see cref="_spacingAccumulator"/> is.</summary>
    private readonly int _appearSizerStep;

    /// <summary>The smallest gap at which an appear is still drawn. When the gap would be smaller than this, the appear is over.</summary>
    private readonly int _smallestAppearSpacing;

    private Rectangle _bounds;
    private readonly StripClip _clip;
    private readonly StripEffectKind _kind;
    private readonly int _slope;          // How the strips lean. 0 is no lean. +1 puts each strip further right than the strip above it, and -1 puts it further left (ROM: SLOPE).
    private int _romFramesRemaining;
    private int _spacingAccumulator;                   // 256 times the gap between the strips, so that the gap can change by less than a whole pixel at a time (ROM: YSIZER).
    private int _romFrameTimer;                  // Counts up to the effect's next step (see ArcadeClock; notes §52, §67.4).

    /// <summary>True once an appear has taken its first step. The arcade draws nothing for an appear until then.</summary>
    private bool _hasAppearBeenDrawn;

    /// <summary>Makes one effect. Only the <c>Create</c> methods below use it.</summary>
    /// <param name="getAnimationFrame">Gets the animation frame the strips are cut from.</param>
    /// <param name="bounds">The box the sprite is drawn in.</param>
    /// <param name="kind">Whether it is an explosion or an appear.</param>
    /// <param name="axis">Which way the sprite is cut: into rows or into columns.</param>
    /// <param name="slope">How the strips lean: -1, 0 or +1.</param>
    /// <param name="clip">The inside of the playfield. A strip outside it is not drawn.</param>
    /// <param name="getCentreStripIndex">Works out which strip the others close in on or fly away from. If it is null, the middle strip is used.</param>
    /// <param name="startClockUnits">How far into the wait for its first step the effect starts, in clock units.</param>
    /// <param name="isClosedUpAtTheEnd">True for an appear that is drawn with its strips fully closed up before it ends.</param>
    /// <param name="getFollowedBounds">Gets the box of the thing that is appearing, so that the appear can stay with it. It is null when the effect stays where it started.</param>
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

        // How fast an appear closes up. The diagonal routine closes it twice as fast as the other two routines.
        StripEngine engine = GetEngine();
        _appearSizerStep = engine == StripEngine.Diagonal ? StripExplosionTuning.SizerStep : StripExplosionTuning.SlowAppearSizerStep;
        _smallestAppearSpacing = isClosedUpAtTheEnd || engine == StripEngine.Horizontal
            ? StripExplosionTuning.SmallestClosedAppearSpacing
            : StripExplosionTuning.SmallestAppearSpacing;

        // The gap between the strips starts small for an explosion, which then spreads out. It starts large for an appear, which then closes up.
        _spacingAccumulator = kind == StripEffectKind.Explode
            ? StripExplosionTuning.ExplosionStartSizer
            : StripExplosionTuning.AppearStartSizer;

        _romFramesRemaining = StripExplosionTuning.ExplosionFrames;
    }

    /// <summary>The box of the thing that is exploding or appearing.</summary>
    public Rectangle GetBounds() => _bounds;

    /// <summary>Alive while the effect runs. An explosion runs for a set number of steps. An appear runs until its strips have closed up.</summary>
    public EntityLifeState LifeState { get; private set; } = EntityLifeState.Alive;

    /// <summary>Where the top-left corner of the effect's box is.</summary>
    public IntVector2 Position => new(_bounds.X, _bounds.Y);

    /// <summary>Which way the sprite is cut: into rows, which move up and down, or into columns, which move left and right. Tests use this.</summary>
    /// <remarks>The ROM calls the code for rows "vertical" and the code for columns "horizontal".</remarks>
    internal StripFanAxis Axis => _axis;

    /// <summary>Whether this is an explosion or an appear. Tests use this.</summary>
    internal StripEffectKind Kind => _kind;

    /// <summary>How the strips lean: -1, 0 or +1 (see <see cref="GetFanForShot"/>). Tests use this.</summary>
    internal int Slope => _slope;

    /// <summary>The gap between the strips now, in the sprite's own pixels. It is never less than 1. Tests use this.</summary>
    internal int GetSpacing() => Math.Max(1, _spacingAccumulator >> 8);

    /// <summary>Makes an appear for an entity. The strips start far apart and close up to make the sprite.</summary>
    /// <param name="source">The thing that is appearing. The animation frame it is showing is used.</param>
    /// <param name="bounds">The box the sprite is drawn in.</param>
    /// <param name="axis">Which way the sprite is cut: into rows or into columns.</param>
    /// <param name="slope">How the strips lean: -1, 0 or +1.</param>
    /// <param name="clip">The inside of the playfield. A strip outside it is not drawn.</param>
    /// <param name="getCentreStripIndex">Works out which strip the others close in on. It is given how many strips the sprite has, and gives back a strip counted from the top or the left. If it is null, the middle strip is used.</param>
    /// <param name="startClockUnits">How far into the wait for its first step the effect starts, in clock units (see <see cref="ArcadeClock"/>). This makes the first step come at the right time. It is below zero when the effect has to wait longer than usual for its first step.</param>
    /// <returns>The new appear.</returns>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>RRG23.ASM</c> <c>APPEAR</c>, which makes one of these 50 times a second;
    /// <c>RRX7.ASM</c> <c>APSTV</c>, <c>RRHX4.ASM</c> <c>HAPSTV</c> and <c>RRDX2.ASM</c> <c>APSTZ</c></item>
    /// <item>Disassembly: <c>$5BC6</c> (the vertical routine), <c>$F066</c> (the horizontal one) and
    /// <c>$46E6</c> (the diagonal one)</item>
    /// </list>
    /// </remarks>
    public static StripEffect CreateAppear(IAnimationFrameSource source, Rectangle bounds, StripFanAxis axis, int slope, StripClip clip, Func<int, int>? getCentreStripIndex = null, int startClockUnits = 0)
        => new(() => source.GetCurrentAnimationFrame(), bounds, StripEffectKind.Appear, axis, slope, clip, getCentreStripIndex, startClockUnits);

    /// <summary>Makes an appear that stays with the thing that is appearing as that thing moves. The player's appear does this, because it is still running when the game goes live and the player can walk.</summary>
    /// <typeparam name="T">The kind of thing that is appearing.</typeparam>
    /// <param name="source">The thing that is appearing. Its animation frame and its box are looked up again as the effect runs.</param>
    /// <param name="axis">Which way the sprite is cut: into rows or into columns.</param>
    /// <param name="slope">How the strips lean: -1, 0 or +1.</param>
    /// <param name="clip">The inside of the playfield. A strip outside it is not drawn.</param>
    /// <param name="centreStripIndex">Which strip the others close in on, counted from the top or the left.</param>
    /// <param name="startClockUnits">How far into the wait for its first step the effect starts, in clock units.</param>
    /// <returns>The new appear.</returns>
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

    /// <summary>Makes an appear for a sprite that is not an entity. The letters of the logo on the attract pages use it.</summary>
    /// <param name="animationFrame">The sprite that is appearing.</param>
    /// <param name="bounds">The box the sprite is drawn in.</param>
    /// <param name="axis">Which way the sprite is cut: into rows or into columns.</param>
    /// <param name="clip">The area strips may be drawn in. A strip outside it is not drawn.</param>
    /// <returns>The new appear.</returns>
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

    /// <summary>Makes the explosion for something that has been killed. The way the killing laser was going decides how the sprite is cut and how the strips lean.</summary>
    /// <param name="dead">The thing that was killed. Its animation frame and its explosion box are used.</param>
    /// <param name="direction">The way the killing laser was going, or null if it was not killed by a laser.</param>
    /// <param name="clip">The inside of the playfield. A strip outside it is not drawn.</param>
    /// <returns>The new explosion.</returns>
    /// <remarks>ROM: the "make an enemy explode" routine, which goes on to the code for a straight explosion or for a
    /// leaning one. The box is at the object's position and is the size of the sprite the object shows, which can be
    /// bigger than the box used to tell what the object touches.</remarks>
    public static StripEffect CreateExplosion(IExplodable dead, Direction8? direction, StripClip clip)
    {
        (StripFanAxis axis, int slope) = GetFanForShot(direction);
        return new StripEffect(() => dead.GetCurrentAnimationFrame(), dead.GetExplosionBounds(), StripEffectKind.Explode, axis, slope, clip);
    }

    /// <summary>Draws the strips. Each strip is one row or one column of the sprite.</summary>
    /// <param name="spriteBatch">What the strips are drawn with.</param>
    public void Draw(SpriteBatch spriteBatch)
    {
        if (!this.IsAlive() || !IsDrawn())
        {
            return;
        }

        Texture2D animationFrame = _getAnimationFrame();

        // LayOutStrips works in the sprite's own pixels, so it is given the sprite's own width and height.
        int spriteWidth = animationFrame.Width;
        int spriteRows = animationFrame.Height;

        foreach (Strip strip in LayOutStrips(spriteWidth, spriteRows))
        {
            // Each strip is one row or one column of the sprite, drawn SpecScale times bigger on the screen.
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

    /// <summary>Runs one tick. When a step is due, an explosion's strips move further apart and an appear's strips move closer together.</summary>
    /// <param name="gameTime">Not used. The effect counts ticks.</param>
    /// <param name="field">Not used.</param>
    public void Update(GameTime gameTime, PlayField? field = null)
    {
        if (!this.IsAlive())
        {
            return;
        }

        // The effect takes a step 50 times a second, as it did in the arcade. A tick comes 60 times a second, so it does not step on every tick (see ArcadeClock).
        _romFrameTimer += ArcadeClock.UnitsPerPortTick;
        if (_romFrameTimer < ArcadeClock.UnitsPerRomFrame)
        {
            return;
        }

        _romFrameTimer -= ArcadeClock.UnitsPerRomFrame;

        if (_kind == StripEffectKind.Explode)
        {
            // When the explosion's time runs out, it is over. Until then, the strips move further apart on each step.
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
    /// <returns>The horizontal routine when the sprite is cut into columns. The diagonal routine when it is cut into rows that lean. The vertical routine when it is cut into rows that do not lean.</returns>
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

    /// <summary>Takes one step of an appear, which makes the gap between the strips a little smaller. The strips are only redrawn when the gap has shrunk by a whole pixel. The appear is over when the gap would be smaller than the smallest its routine draws.</summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Original source: <c>AWRITE</c> in <c>RRX7.ASM</c>, <c>RRHX4.ASM</c> and <c>RRDX2.ASM</c>:
    /// <c>CMPA YSIZER,Y</c> ("CHANGE?") skips the step when the whole number of rows has not changed, and the
    /// tests after <c>AWRIT1</c> end the appear</item>
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

    /// <summary>Works out how a sprite is cut and how its strips lean, from the way the killing laser was going.</summary>
    /// <param name="direction">The way the killing laser was going, or null if it was not killed by a laser.</param>
    /// <returns>Which way the sprite is cut, and how the strips lean: -1, 0 or +1.</returns>
    /// <remarks>ROM: RRX7.ASM's routine that picks the kind of explosion. The strips fly apart across the path of the
    /// laser, not along it. A laser going straight up or down cuts the sprite into columns, which fly apart sideways.
    /// A laser going straight left or right, or a kill with no laser, cuts it into rows, which fly apart up and down.
    /// A diagonal laser cuts it into rows that lean. It is easy to get the first two the wrong way round.</remarks>
    internal static (StripFanAxis Axis, int Slope) GetFanForShot(Direction8? direction) => direction switch
    {
        // A laser going straight up or straight down cuts the sprite into columns.
        Direction8.Up or Direction8.Down => (StripFanAxis.Columns, 0),

        // A laser going straight left or straight right cuts the sprite into rows. So does a kill that was not made by a laser.
        Direction8.Left or Direction8.Right or null => (StripFanAxis.Rows, 0),

        // A laser going diagonally cuts the sprite into rows, and the rows lean (see _slope).
        Direction8.UpLeft or Direction8.DownRight => (StripFanAxis.Rows, -1),
        Direction8.UpRight or Direction8.DownLeft => (StripFanAxis.Rows, 1),
        _ => (StripFanAxis.Rows, 0),
    };

    /// <summary>Works out where a sprite's top-left corner goes when the sprite is drawn in the middle of a box.</summary>
    /// <param name="bounds">The box, in port pixels.</param>
    /// <param name="spriteWidth">How wide the sprite is, in its own pixels.</param>
    /// <param name="spriteRows">How tall the sprite is, in its own rows.</param>
    /// <returns>Where the sprite's top-left corner goes, in arcade pixels across and rows down.</returns>
    internal static (int Left, int Top) GetSpritePlacement(
        Rectangle bounds, int spriteWidth, int spriteRows)
    {
        int boundsWidth = bounds.Width / ScreenSize.SpecScale;
        int boundsRows = bounds.Height / ScreenSize.SpecScale;

        return (
            (bounds.X / ScreenSize.SpecScale) + ((boundsWidth - spriteWidth) / 2),
            (bounds.Y / ScreenSize.SpecScale) + ((boundsRows - spriteRows) / 2));
    }

    /// <summary>Works out where every strip goes on this step. It changes nothing, so tests can check the shape.</summary>
    /// <param name="spriteWidth">How wide the sprite is, in its own pixels.</param>
    /// <param name="spriteRows">How tall the sprite is, in its own rows.</param>
    /// <returns>The strips to draw on this step, in the order they are drawn.</returns>
    /// <remarks>ROM: how RRX7.ASM, RRHX4.ASM and RRDX2.ASM place the strips:
    ///
    /// <code>
    /// YSIZE  = YSIZER >> 8                     ; the gap on this step (1, 2, 3, …)
    /// base   = YCENT − YSIZE*YOF + YSIZE/2     ; the screen row of the first strip
    /// strip i:   row = base + i*YSIZE          ; each strip after it is one gap further down
    /// </code>
    ///
    /// When the gap is 1, the first strip is on the sprite's top row and every strip touches the next, so the sprite
    /// looks whole. This is the ROM's own "1 unit is the minimum" rule. As the gap grows, the first strip moves up
    /// while the later strips move down, so the sprite tears apart both upwards and downwards. The strips spread from
    /// the middle of the sprite, so both halves spread the same distance. A diagonal shot also makes the strips lean.
    /// The same sums are done on columns for a shot going up or down. Spreading from the middle is what the ROM does:
    /// it uses the sprite's top plus half its height. Spreading from the point the laser hit would look lopsided,
    /// because a laser hits the near edge of the sprite. A strip outside the playfield is left out. It is not moved
    /// back inside.</remarks>
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

        // The strips spread out from one chosen strip, or from the middle strip when none was chosen.
        int split = GetCentreStripIndex(extent);

        // The sprite is drawn in the middle of its box, so this finds where the sprite's own top-left corner is.
        (int spriteLeft, int spriteTop) = GetSpritePlacement(_bounds, spriteWidth, spriteRows);

        // Where the chosen strip is on the screen: its row, or its column when the sprite is cut into columns.
        int centre = (fansByRows ? spriteTop : spriteLeft) + split;

        // The gap is counted in the sprite's own pixels, for rows and for columns alike. Counting a column as 2 pixels wide would make the strips fly apart twice as fast as in the arcade.
        int step = spacing;

        // Where the first strip goes. The arcade has a bug here, and it is kept: when the chosen strip is the very first strip, the half gap is left out.
        int half = split == 0 ? 0 : (spacing >> 1);
        int fanBase = centre - (spacing * split) + half;

        // How far each strip slides to the side for a diagonal shot. A strip slides further the further it is from the chosen strip, and strips on opposite sides of the chosen strip slide opposite ways.
        // The slide is not scaled up with the screen, because then the strips would slide too far.
        int drift = _slope * ((spacing >> 1) * ScreenSize.ArcadePixelsPerByte);

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

    /// <summary>Says whether a strip is inside the area strips may be drawn in. A strip outside it is not drawn.</summary>
    /// <param name="x">Where the strip's left edge is.</param>
    /// <param name="y">Where the strip's top edge is.</param>
    /// <param name="spriteWidth">How wide the sprite is, in its own pixels.</param>
    /// <param name="spriteRows">How tall the sprite is, in its own rows.</param>
    /// <remarks>The ROM makes the same check on each strip.</remarks>
    private bool IsInside(int x, int y, int spriteWidth, int spriteRows)
    {
        if (_axis == StripFanAxis.Rows)
        {
            return x >= _clip.MinX && x + spriteWidth <= _clip.MaxX && y >= _clip.MinY && y < _clip.MaxY;
        }

        return y >= _clip.MinY && y + spriteRows <= _clip.MaxY && x >= _clip.MinX && x < _clip.MaxX;
    }
}
