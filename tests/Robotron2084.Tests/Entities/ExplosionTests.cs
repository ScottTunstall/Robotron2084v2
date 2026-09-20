using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Entities;
using Robotron2084.Level;
using Robotron2084.Rendering;
using Robotron2084.Tuning;
using Xunit;

namespace Robotron2084.Tests;

/// <summary>
/// The ROM's explosion engine (notes §35.5, §61, §67, §69, §71) — the sources'
/// `EXSTZ`/`APGO`/`WRITE` and the disassembly's `CREATE_EXPLOSION` ($F0D7) /
/// `CREATE_DIRECTIONAL_EXPLOSION` ($473F): the sprite's rows (or columns) are the
/// segments, the first one is placed at `YCENT − YSIZE*YOF + YSIZE/2` and the rest
/// step away from it by the spacing, so ONE fan opens up and down (or left and
/// right) at the same time; the spacing is the ROM's own 16-bit accumulator ($0100
/// start, +$0100 a frame); and a strip outside the playfield is DROPPED.
/// </summary>
public sealed class ExplosionTests
{
    private static readonly StripClip Clip = new(MinX: 20, MaxX: 300, MinY: 20, MaxY: 180);

    /// <summary>An 8x12-art-pixel picture at art (50,100); its collision box is the same extent in port px.</summary>
    private static readonly Rectangle Sprite = new(
        ScreenSize.Scaled(SpriteLeft),
        ScreenSize.Scaled(SpriteTop),
        ScreenSize.Scaled(WidthArt),
        ScreenSize.Scaled(HeightRows));
    private const int WidthArt = 8;
    private const int HeightRows = 12;
    private const int SpriteLeft = 50;
    private const int SpriteTop = 100;

    private sealed class FakeDead : IExplodable
    {
        public FakeDead(Rectangle bounds) => Bounds = bounds;

        public Rectangle Bounds { get; }

        public IntVector2 Position => new(Bounds.X, Bounds.Y);

        public EntityLifeState LifeState => EntityLifeState.Dead;

        public void Update(GameTime gameTime, PlayField field)
        {
        }

        public void Draw(SpriteBatch spriteBatch, SpriteSet sprites)
        {
        }

        // Draw-time only; the lifecycle/geometry tests never draw.
        public Texture2D CurrentFrameArt(SpriteSet sprites)
            => throw new NotSupportedException("no texture in unit tests");
    }

    private static Explosion NewExplosion(Direction8? direction = null, Rectangle? sprite = null) =>
        Explosion.StartExplosion(
            new FakeDead(sprite ?? Sprite),
            direction: direction,
            clip: Clip);

    /// <summary>Ticks the record forward one ROM FRAME at a time (the clock is 1.2 ticks).</summary>
    private static void Steps(Explosion explosion, int frames)
    {
        int target = explosion.Spacing + frames;
        int guard = 0;
        while ((explosion.Spacing < target || target < 1) && guard++ < 200)
        {
            explosion.Update(new GameTime(), null!);
        }
    }

    /// <summary>Advances whole frames, then lays the strips out.</summary>
    private static IReadOnlyList<Strip> Strips(Explosion explosion, int frames = 0)
    {
        Steps(explosion, frames);
        return explosion.Layout(WidthArt, HeightRows);
    }

    [Fact]
    public void TheFirstFrame_ReconstructsTheSpriteExactly()
    {
        // YSIZER starts at $0100, "1 UNIT IS MIN": spacing 1, so the base is the
        // sprite's own top row (the YSIZE*YOF term cancels) and the strips ARE the
        // sprite, row for row — the ROM's own frame-0 sanity check.
        Explosion explosion = NewExplosion(Direction8.Left);

        IReadOnlyList<Strip> strips = Strips(explosion);

        Assert.Equal(HeightRows, strips.Count);
        for (int row = 0; row < HeightRows; row++)
        {
            Assert.Equal(SpriteLeft, strips[row].X);
            Assert.Equal(SpriteTop + row, strips[row].Y);
            Assert.Equal(row, strips[row].SourceIndex);
        }
    }

    [Fact]
    public void TheFanOpensUpAndDownAtOnce_AndIsMirroredAboutThePicturesMiddle()
    {
        // ROM RRX7 `WRITE`: base = YCENT − YSIZE*YOF + YSIZE/2, and each further
        // segment steps DOWN by YSIZE. The base climbs faster than the segments march
        // away from it, so ONE fan covers both sides at the same time — the author's
        // "it's meant to be up AND down at the same time".
        //
        // Its fixed point is the picture's MIDDLE (the ROM's NWCENT centre invariant):
        // the author's follow-up (notes §73) — *"one side of the explosion is not
        // mirrored on the other side ... the half going UP is bigger than the half
        // going DOWN"* — is exactly what anchoring at the hit gives, because a laser
        // strikes the sprite's near edge. Half the strips and the same reach each way.
        Explosion explosion = NewExplosion(Direction8.Left); // horizontal shot -> rows

        IReadOnlyList<Strip> strips = Strips(explosion, frames: 2); // spacing 3

        Assert.Equal(HeightRows, strips.Count);

        // split = 12/2 = 6, centre = 100 + 6 = 106, base = 106 − 3*6 + (3>>1) = 89.
        int expectedBase = SpriteTop + (HeightRows / 2) - (3 * (HeightRows / 2)) + 1;
        Assert.Equal(expectedBase, strips[0].Y);
        Assert.True(strips[0].Y < SpriteTop, "the fan must reach above the sprite");

        // Every next segment is one spacing further down, so the far end is well
        // below the sprite's bottom row as well: up AND down.
        for (int i = 1; i < strips.Count; i++)
        {
            Assert.Equal(strips[i - 1].Y + 3, strips[i].Y);
        }

        Assert.Equal(expectedBase + (HeightRows - 1) * 3, strips[^1].Y);
        Assert.True(strips[^1].Y > SpriteTop + HeightRows - 1, "the fan must reach below the sprite");

        // MIRRORED: the reach above the picture equals the reach below it.
        int reachUp = SpriteTop - strips[0].Y;
        int reachDown = strips[^1].Y - (SpriteTop + HeightRows - 1);
        Assert.Equal(reachUp, reachDown);
    }

    [Fact]
    public void AVerticalShot_IsMirroredLeftAndRightToo()
    {
        // The same mirror rule on the column axis (the H family, a vertical shot):
        // the author's *"the explosion half going LEFT is bigger than ... RIGHT"*.
        Explosion explosion = NewExplosion(Direction8.Up);

        IReadOnlyList<Strip> strips = Strips(explosion, frames: 2); // spacing 3

        Assert.Equal(WidthArt, strips.Count);
        Assert.All(strips, s => Assert.Equal(SpriteTop, s.Y));

        int reachLeft = SpriteLeft - strips[0].X;
        int reachRight = strips[^1].X - (SpriteLeft + WidthArt - 1);
        Assert.Equal(reachLeft, reachRight);
    }

    [Fact]
    public void TheFanStartsFromTheCentredArt_NotTheBoundsCorner()
    {
        // A picture smaller than its collision box is drawn CENTRED in it
        // (SpriteSet.CentredIn), so the fan has to start from the ART's own top-left.
        // At spacing 1 the fan IS the picture, so strip 0 lands exactly where the art
        // sits: BOUNDS 11x15 art px holding an 8x12 picture, so the art starts one
        // pixel in on both axes (notes §75).
        Rectangle bounds = new(
            ScreenSize.Scaled(SpriteLeft),
            ScreenSize.Scaled(SpriteTop),
            ScreenSize.Scaled(WidthArt + 3),
            ScreenSize.Scaled(HeightRows + 3));

        var explosion = Explosion.StartExplosion(
            new FakeDead(bounds),
            direction: Direction8.Left,
            clip: Clip);

        IReadOnlyList<Strip> strips = Strips(explosion);

        Assert.Equal(HeightRows, strips.Count);
        Assert.Equal(SpriteLeft + 1, strips[0].X);
        Assert.Equal(SpriteTop + 1, strips[0].Y);
        Assert.Equal(SpriteTop + 1 + (HeightRows - 1), strips[^1].Y);

        // And the placement helper agrees with the draw path's own convention: a
        // texture's dimensions ARE the picture's extent in art pixels.
        Assert.Equal(
            (WidthArt, HeightRows, SpriteLeft + 1, SpriteTop + 1),
            Explosion.PicturePlacement(bounds, WidthArt, HeightRows));
    }

    [Fact]
    public void TheSpacingGrowsOnTheRomsAccumulator_NotALinearGuess()
    {
        // ROM WRITE: YSIZER += $100 a frame, so the gap between neighbouring segments
        // is 1, 2, 3, ... — the ROM's curve, not a guess.
        Explosion explosion = NewExplosion(Direction8.Left);

        for (int spacing = 1; spacing <= 8; spacing++)
        {
            IReadOnlyList<Strip> strips = Strips(explosion, frames: spacing == 1 ? 0 : 1);

            Assert.Equal(HeightRows, strips.Count);
            Assert.Equal(spacing, strips[1].Y - strips[0].Y);
        }
    }

    [Fact]
    public void ADiagonalShot_LeansTheTwoHalvesOppositeWays()
    {
        // ROM RRDX2 — the DIAGONAL engine (notes §74). `XSIZE = ±(YSIZE>>1)` in columns
        // with the sign from SLOPE (`LDB SLOPE,Y / BPL APGG1 / NEGA`), and the strips are
        // placed from `XCENT − YOFF*XSIZE` adding `XSIZE` each: strip i's lateral is
        // `(i − YOF) * XSIZE`, measured from the fan's FIXED POINT. The rows above it
        // lean one way and the rows below it the other — a MIRRORED chevron. Leaning the
        // whole fan one way sheared it off to one side, which is the author's "one side
        // of the explosion is not mirrored on the other side".
        Explosion right = NewExplosion(Direction8.UpRight); // slope +1

        IReadOnlyList<Strip> strips = Strips(right, frames: 3); // spacing 4 -> drift ±4

        Assert.Equal(HeightRows, strips.Count);

        // split = 12/2 = 6 and drift = +((4>>1) * 2) = 4 art px a strip, so the strip AT
        // the fixed point does not move and its neighbours move opposite ways.
        Assert.Equal(SpriteLeft - (6 * 4), strips[0].X);   // topmost: six steps left
        Assert.Equal(SpriteLeft, strips[6].X);             // the fixed point: no lateral
        Assert.Equal(SpriteLeft + (5 * 4), strips[11].X);  // bottom: five steps right

        Assert.True(strips[0].X < SpriteLeft, "the upper half must lean left");
        Assert.True(strips[^1].X > SpriteLeft, "the lower half must lean the other way");

        // The other diagonal mirrors it: the same chevron, the other way round.
        Explosion left = NewExplosion(Direction8.UpLeft); // slope -1
        IReadOnlyList<Strip> mirrored = Strips(left, frames: 3);

        Assert.Equal(HeightRows, mirrored.Count);
        Assert.Equal(SpriteLeft + (6 * 4), mirrored[0].X);
        Assert.Equal(SpriteLeft, mirrored[6].X);
        Assert.Equal(SpriteLeft - (5 * 4), mirrored[11].X);
    }

    [Fact]
    public void AStraightHorizontalShot_DoesNotLeanAtAll()
    {
        Explosion explosion = NewExplosion(Direction8.Left);

        Assert.All(Strips(explosion, frames: 3), s => Assert.Equal(SpriteLeft, s.X));
    }

    [Fact]
    public void AStripOutsideThePlayfield_IsDropped_NotMoved()
    {
        // The sprite sits on the TOP wall, so the leading part of the fan leaves the
        // playfield and those strips are DROPPED (the ROM's clip passes).
        var onTheWall = new Rectangle(
            ScreenSize.Scaled(SpriteLeft), ScreenSize.Scaled(20), ScreenSize.Scaled(WidthArt), ScreenSize.Scaled(HeightRows));
        Explosion explosion = NewExplosion(Direction8.Left, sprite: onTheWall);

        IReadOnlyList<Strip> strips = Strips(explosion, frames: 2); // spacing 3

        Assert.True(strips.Count < HeightRows, "the strips above the wall should be gone");
        Assert.All(strips, s => Assert.True(s.Y >= Clip.MinY, $"strip {s.Y} is above the playfield"));
        Assert.All(strips, s => Assert.True(s.Y < Clip.MaxY, $"strip {s.Y} is below the playfield"));
    }

    [Fact]
    public void AVerticalShot_FansTheColumnsSideways()
    {
        // A VERTICAL shot is the H engine (RRHX4 `WRITE`, the exact mirror of RRX7):
        // the base COLUMN is `XCENT − XSIZE*XOF + XSIZE/2` and the segments step RIGHT
        // by the spacing, so the base moves left while they march right — the fan opens
        // left AND right, and every strip keeps the sprite's own row.
        Explosion explosion = NewExplosion(Direction8.Up);
        int collisionColumn = SpriteLeft + 4; // the picture's middle (notes §73)

        IReadOnlyList<Strip> strips = Strips(explosion, frames: 1); // spacing 2

        Assert.Equal(WidthArt, strips.Count);
        Assert.All(strips, s => Assert.Equal(SpriteTop, s.Y)); // no vertical displacement

        // A unit of spacing is ONE PIXEL in this family too (RRHX4 counts pixel
        // columns: `ASLA` "DOUBLE FOR PIXEL WIDTH", `DECA` "NO NEED TO DO ZEROS ON
        // LEFT COLUMN" — notes §72), so base = 54 − 2*4 + (2>>1) = 47 and the
        // segments step one pixel each: 47, 49, 51, …
        Assert.Equal(collisionColumn - (2 * 4) + (2 >> 1), strips[0].X);
        for (int i = 1; i < strips.Count; i++)
        {
            Assert.Equal(strips[i - 1].X + 2, strips[i].X);
        }

        Assert.True(strips[0].X < collisionColumn, "the fan must reach left of the collision");
        Assert.True(strips[^1].X > collisionColumn, "the fan must reach right of it");
    }

    [Fact]
    public void AVerticalShotAtOneUnit_AlsoReconstructsTheSpriteExactly()
    {
        // Both families honour the ROM's "1 UNIT IS MIN": at spacing 1 the fan IS the
        // sprite, pixel for pixel. The H family only did this once its unit was the
        // ROM's pixel (notes §72) — with byte columns its first frame was already
        // twice the sprite's width, and the author saw the fan leave the playfield far
        // too early ("the vertical explosion works sometimes, but doesn't last very
        // long").
        Explosion explosion = NewExplosion(Direction8.Up);

        IReadOnlyList<Strip> strips = Strips(explosion);

        Assert.Equal(WidthArt, strips.Count);
        for (int column = 0; column < WidthArt; column++)
        {
            Assert.Equal(SpriteTop, strips[column].Y);
            Assert.Equal(SpriteLeft + column, strips[column].X);
            Assert.Equal(column, strips[column].SourceIndex);
        }
    }

    [Fact]
    public void Dispatch_MatchesTheRomLaserDirectionRules()
    {
        // RRX7 `EXSTV`: `LDA LASDIR / BNE EXST1 / JSR HEXST` — a shot with NO
        // horizontal component (a straight VERTICAL shot) takes the HORIZONTAL
        // explosion; `EXST1 LDB LASDIR+1 / BEQ EXST1A` — "NO Y COMPONENT, STRAIGHT
        // VERTICAL" — a straight HORIZONTAL shot takes the VERTICAL explosion; both
        // components non-zero → the diagonal engine. The name is the axis the pieces
        // MOVE (across the shot), which is why these look "swapped" at first glance.
        Assert.Equal((StripFanAxis.Columns, 0), Explosion.Dispatch(Direction8.Up));
        Assert.Equal((StripFanAxis.Columns, 0), Explosion.Dispatch(Direction8.Down));

        Assert.Equal((StripFanAxis.Rows, 0), Explosion.Dispatch(Direction8.Left));
        Assert.Equal((StripFanAxis.Rows, 0), Explosion.Dispatch(Direction8.Right));

        // No laser direction at all: the port's non-laser kills, which the ROM sends
        // through HVEXV with LASDIR = $0100 — that lands on EXST1A, the VERTICAL one.
        Assert.Equal((StripFanAxis.Rows, 0), Explosion.Dispatch(null));

        // Diagonals: the row split, leaning by SLOPE = ~(vertical ^ horizontal).
        Assert.Equal((StripFanAxis.Rows, -1), Explosion.Dispatch(Direction8.UpLeft));
        Assert.Equal((StripFanAxis.Rows, -1), Explosion.Dispatch(Direction8.DownRight));
        Assert.Equal((StripFanAxis.Rows, 1), Explosion.Dispatch(Direction8.UpRight));
        Assert.Equal((StripFanAxis.Rows, 1), Explosion.Dispatch(Direction8.DownLeft));
    }

    [Fact]
    public void Appear_ConvergesAndFreesItselfWhenTheSizeReachesOne()
    {
        // APSTZ/AWRITE: YSIZER starts at $1000 and shrinks $100 a frame, and the
        // record dies when the step would fall to 1 or less — so it draws steps
        // 15,14,...,2: fourteen draws, the mirror of an explosion.
        Explosion appear = Explosion.StartAppear(
            new FakeDead(new Rectangle(100, 200, 16, 24)),
            new Rectangle(100, 200, 16, 24),
            StripFanAxis.Rows,
            slope: 0,
            clip: Clip);

        Assert.Equal(Explosion.Kind.Appear, appear.Mode);

        int ticks = 0;
        while (appear.LifeState == EntityLifeState.Alive && ticks < 60)
        {
            appear.Update(new GameTime(), null!);
            ticks++;
        }

        Assert.Equal(EntityLifeState.Dead, appear.LifeState);

        // 15 ROM FRAMES of work (14 draws and the call that frees the record), which
        // the 6/5 clock spreads over 18 port ticks: 15 × 6 sixths / 5 a tick.
        Assert.Equal(18, ticks);
    }

    [Fact]
    public void StartExplosion_TakesTheRomsDispatch()
    {
        var dead = new FakeDead(new Rectangle(100, 200, 16, 24));

        Explosion diagonal = Explosion.StartExplosion(dead, Direction8.DownRight, Clip);
        Explosion vertical = Explosion.StartExplosion(dead, Direction8.Up, Clip);
        Explosion horizontal = Explosion.StartExplosion(dead, Direction8.Left, Clip);

        Assert.Equal(Explosion.Kind.Explode, diagonal.Mode);
        Assert.Equal((StripFanAxis.Rows, -1), (diagonal.Axis, diagonal.Slope));

        // A vertical shot is the HORIZONTAL explosion (columns), a horizontal shot the
        // VERTICAL one (rows) — see Explosion.Dispatch and notes §69.
        Assert.Equal((StripFanAxis.Columns, 0), (vertical.Axis, vertical.Slope));
        Assert.Equal((StripFanAxis.Rows, 0), (horizontal.Axis, horizontal.Slope));

        // And every one of them anchors at the picture's MIDDLE, not at the hit
        // (the ROM's NWCENT centre invariant — notes §73), which the two mirror
        // tests above pin from both axes.
    }

    [Fact]
    public void LaserKillsEnforcer_SpawnsExplosionImmediately_NoBlink()
    {
        // RRC11 ENFKIL: `JSR KILOFP` (object and process gone, image off) then
        // `JSR EXST` (explode) — the enforcer has NO death animation. The port
        // used to play a 2-second blink; the author reported it on 2026-09-16
        // ("enforcers shouldn't flash when hit"), the same defect class as the
        // grunt in §44.
        PlayField field = CreateEmptyField();
        IntVector2 spot = new(field.Wall.PlayfieldBounds.X + 100, field.Wall.PlayfieldBounds.Y + 100);
        field.SpawnEnforcer(spot);
        Enforcer enforcer = field.Enforcers[0];

        IntVector2 laserOrigin = new(spot.X + 6, spot.Y - 12);
        Assert.True(field.PlayerLasers.TryFire(laserOrigin, Direction8.Down, out _));
        field.Update(new GameTime());

        Assert.Equal(EntityLifeState.Dead, enforcer.LifeState); // no Dying state at all
        Assert.Single(field.Explosions);
        Assert.Equal(0, field.EnforcerCount);
    }

    [Fact]
    public void LaserKillsElectrode_SpawnsNOExplosion_ItJustShrivels()
    {
        // RRP8 PSTKIL: a non-player kill does `KILPST` + `DMAOFF` + `MAKP
        // PKPROC` — the post's 3-frame SHRIVEL, with NO `EXST` call, so nothing
        // bursts. The author reported the port's shatter explosion on 2026-09-16:
        // "electrodes shouldn't explode when hit, they shrivel."
        PlayField field = CreateEmptyField();
        IntVector2 spot = new(field.Wall.PlayfieldBounds.X + 100, field.Wall.PlayfieldBounds.Y + 100);
        var electrode = new Electrode(spot);
        field.AddElectrode(electrode);

        IntVector2 laserOrigin = new(spot.X + 6, spot.Y - 12);
        Assert.True(field.PlayerLasers.TryFire(laserOrigin, Direction8.Down, out _));
        field.Update(new GameTime());

        Assert.Empty(field.Explosions);
        Assert.Equal(EntityLifeState.Dying, electrode.LifeState); // the shrivel is running
    }

    [Fact]
    public void GruntOnElectrode_OnlyTheGruntShatters()
    {
        // RRP8 ROBKIL explodes the grunt (`JSR EXST`); the post it walked into
        // shrivels instead (PSTKIL, above) — one explosion, not two.
        PlayField field = CreateEmptyField();
        IntVector2 spot = new(field.Wall.PlayfieldBounds.X + 100, field.Wall.PlayfieldBounds.Y + 100);
        var electrode = new Electrode(spot);
        field.AddElectrode(electrode);
        field.AddGrunt(new Grunt(spot, speedBonus: 0)); // standing on the electrode

        field.Update(new GameTime());

        Assert.Single(field.Explosions); // the grunt only
        Assert.Equal(StripFanAxis.Rows, field.Explosions[0].Axis);
        Assert.Equal(0, field.Explosions[0].Slope); // non-directional shatter (notes §35)
        Assert.Equal(EntityLifeState.Dying, electrode.LifeState);
    }

    [Fact]
    public void AtMostTenConcurrentRecords_TheRomsSharedPool()
    {
        // RRDX2's `EX` region holds TEN records (`RMB ((10-1)*EXSIZE)`) and
        // explosions and appears take from the SAME free list, so an eleventh is
        // refused outright (the caller's kill still stands).
        PlayField field = CreateEmptyField();
        Rectangle bounds = field.Wall.PlayfieldBounds;

        for (int i = 0; i < GameplayConstants.StripMaxConcurrent + 4; i++)
        {
            IntVector2 spot = new(bounds.X + 16 + (i * 12), bounds.Y + 60);
            field.AddGrunt(new Grunt(spot, speedBonus: 0));
            field.AddElectrode(new Electrode(spot));
        }

        field.Update(new GameTime());

        Assert.Equal(GameplayConstants.StripMaxConcurrent, field.Explosions.Count);
    }

    private static PlayField CreateEmptyField()
    {
        var parameters = new LevelParameters(
            1,
            GruntCount: 0,
            HulkCount: 0,
            SpheroidCount: 0,
            QuarkCount: 0,
            ElectrodeCount: 0,
            MaxEnforcersPerSpheroid: 1,
            MaxTanksPerQuark: 1,
            EnemySpeedBonus: 0);

        return new PlayField(parameters, new FakeInputSource(), PlayFieldSpawnTests.InnerBounds, new WallColorCycle(), new Random(99), startingLives: 3);
    }
}
