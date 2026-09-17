using Microsoft.Xna.Framework;
using Robotron2084.Core;
using Robotron2084.Entities;
using Robotron2084.Level;
using Robotron2084.Tuning;
using Xunit;

namespace Robotron2084.Tests;

/// <summary>
/// The spheroid's and the quark's DEATH BURST (notes §64) — the ROM's
/// `CIRKP`/`CIRKV`, which is NOT the strip explosion: the enemy's own pictures
/// play as a solid silhouette in a palette slot, then its "1000" picture is
/// displayed down-right of where it died.
/// </summary>
public sealed class ScoreBurstTests
{
    private static readonly GameTime Tick16 = new(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16));

    private static void Advance(ScoreBurst burst, int ticks)
    {
        for (int i = 0; i < ticks; i++)
        {
            // The burst reads neither the field nor the clock — it counts ticks.
            burst.Update(Tick16, field: null!);
        }
    }

    [Fact]
    public void TheBurstShowsTheEnemysPictures_FromTwo_UpToTheRomCount()
    {
        var bounds = new Rectangle(100, 200, 16, 15);
        ScoreBurst burst = ScoreBurst.ForSpheroid(bounds);

        Assert.Equal(ScoreBurst.FirstBurstFrameIndex, burst.FrameIndex);
        Assert.False(burst.ShowingPoints);

        // One tick at a time, recording every change: the 6ths accumulator makes a
        // step land on a 2- or 3-tick boundary depending on its phase, so batching
        // ticks would alias the sequence.
        var seen = new List<int>();
        for (int i = 0; i < 60 && !burst.ShowingPoints; i++)
        {
            int before = burst.FrameIndex;
            Advance(burst, 1);
            if (burst.FrameIndex != before)
            {
                seen.Add(burst.FrameIndex);
            }
        }

        // Pictures 2..7 for the spheroid: `LDA #7` IS the last picture's index,
        // and because the ROM tests the countdown BEFORE drawing, the last step
        // draws nothing — so the showing ends there.
        Assert.Equal([3, 4, 5, 6, 7], seen);
        Assert.Equal(ScoreBurst.FirstBurstFrameIndex, seen[0] - 1); // it started on 2
        Assert.True(burst.ShowingPoints);
        Assert.Equal(GameplayConstants.ScoreBurstPointsSteps, burst.PointsStepsRemaining);
    }

    [Fact]
    public void EachStepTakesTwoRomFrames_NotTwoPortTicks()
    {
        // `NAP 2` = 2 ROM frames = 12 sixths, and a port tick is 5 sixths, so a
        // step lands every 2-3 ticks (2.4). PortTicks(2) would also be 2 here,
        // but the accumulator is the repo's rule for short ROM delays (§52).
        ScoreBurst burst = ScoreBurst.ForSpheroid(new Rectangle(0, 0, 16, 15));

        // Picture 2 is shown from the kill itself; each step is 2 ROM frames.
        Assert.Equal(ScoreBurst.FirstBurstFrameIndex, burst.FrameIndex);
        Advance(burst, 2);
        Assert.Equal(2, burst.FrameIndex); // 10 sixths: not yet
        Advance(burst, 1);
        Assert.Equal(3, burst.FrameIndex); // 15 sixths: stepped, 3 left over
        Advance(burst, 1);
        Assert.Equal(3, burst.FrameIndex);
        Advance(burst, 1);
        Assert.Equal(4, burst.FrameIndex);
    }

    [Fact]
    public void ThePointsValueShowsForThirtySteps_ThenTheBurstIsGone()
    {
        ScoreBurst burst = ScoreBurst.ForSpheroid(new Rectangle(0, 0, 16, 15));

        // 36 steps in total (6 burst + 30 points) x 12 sixths = 432 sixths, and
        // 5 sixths accrue a tick => 86 ticks leave it alive, the 87th kills it.
        Advance(burst, 86);
        Assert.Equal(EntityLifeState.Alive, burst.LifeState);
        Assert.True(burst.ShowingPoints);
        Advance(burst, 1);
        Assert.Equal(EntityLifeState.Dead, burst.LifeState);
    }

    [Fact]
    public void ThePointsValueSits_OneColumnRight_FiveRowsDown()
    {
        var bounds = new Rectangle(100, 200, 16, 15);
        ScoreBurst burst = ScoreBurst.ForQuark(bounds);

        // The ROM adds #$0105 to the blitter's column:row destination.
        Assert.Equal(
            new Rectangle(
                bounds.X + ScreenSize.Scaled(GameplayConstants.ScoreBurstPointsOffsetXSpecPixels),
                bounds.Y + ScreenSize.Scaled(GameplayConstants.ScoreBurstPointsOffsetYSpecPixels),
                bounds.Width,
                bounds.Height),
            burst.PointsBounds);
    }

    [Fact]
    public void TheRomColours_AreSlotsTenFifteenAndThirteen()
    {
        // `LDD #$FFAA`: $AA = slot 10 (the current player's score slot) for the
        // dying silhouette and $FF = slot 15 for the points value; the quark's
        // `LDD #$DDDD` uses slot 13 for both. All three cycle with the palette.
        ScoreBurst spheroid = ScoreBurst.ForSpheroid(new Rectangle(0, 0, 16, 15));
        Assert.Equal(10, spheroid.BurstSlot);
        Assert.Equal(15, spheroid.PointsSlot);

        ScoreBurst quark = ScoreBurst.ForQuark(new Rectangle(0, 0, 16, 15));
        Assert.Equal(13, quark.BurstSlot);
        Assert.Equal(13, quark.PointsSlot);
    }

    [Fact]
    public void TheQuarkBurst_ShowsItsLastPicture_IndexEight()
    {
        // The quark has NINE pictures (SQP0..SQP8) and `LDA #8`, so its burst
        // reaches index 8 — one further than the spheroid's.
        ScoreBurst burst = ScoreBurst.ForQuark(new Rectangle(0, 0, 16, 15));
        int last = burst.FrameIndex;
        while (!burst.ShowingPoints && last < 20)
        {
            Advance(burst, 3);
            last = burst.FrameIndex;
        }

        Assert.True(burst.ShowingPoints);
        Assert.Equal(GameplayConstants.ScoreBurstQuarkCount, last);
    }

    private static PlayField CreateField(int spheroids = 0, int quarks = 0)
    {
        var parameters = new LevelParameters(
            1,
            GruntCount: 0,
            HulkCount: 0,
            SpheroidCount: spheroids,
            QuarkCount: quarks,
            ElectrodeCount: 0,
            MaxEnforcersPerSpheroid: 0,
            MaxTanksPerQuark: 0,
            EnemySpeedBonus: 0);

        return new PlayField(parameters, new FakeInputSource(), PlayFieldSpawnTests.InnerBounds, new WallColorCycle(), new Random(7), startingLives: 3);
    }

    /// <summary>Drains the wave-start appear chain so only kill effects remain.</summary>
    private static void Settle(PlayField field)
    {
        for (int i = 0; i < 30; i++)
        {
            field.Update(Tick16);
        }

        Assert.Empty(field.Explosions);
    }

    [Fact]
    public void ShootingASpheroid_PlaysTheBurst_AndNoStripExplosion()
    {
        PlayField field = CreateField(spheroids: 1);
        Settle(field);

        Spheroid spheroid = field.Spheroids[0];
        IntVector2 aim = new(spheroid.Bounds.Center.X, spheroid.Bounds.Y - 6);
        Assert.True(field.PlayerLasers.TryFire(aim, Direction8.Down, out PlayerLaser? laser));
        Assert.NotNull(laser);
        field.Update(Tick16);

        Assert.Equal(EntityLifeState.Dead, spheroid.LifeState);
        Assert.Single(field.ScoreBursts);
        Assert.Empty(field.Explosions); // CIRKP, not EXST
        Assert.Equal(ScoreValues.Spheroid, field.Score.Score); // both burst paths score $0210 = 1000
    }

    [Fact]
    public void ShootingAQuark_PlaysTheBurst_AndNoStripExplosion()
    {
        PlayField field = CreateField(quarks: 1);
        Settle(field);

        Quark quark = field.Quarks[0];
        IntVector2 aim = new(quark.Bounds.Center.X, quark.Bounds.Y - 6);
        Assert.True(field.PlayerLasers.TryFire(aim, Direction8.Down, out PlayerLaser? laser));
        Assert.NotNull(laser);
        field.Update(Tick16);

        Assert.Equal(EntityLifeState.Dead, quark.LifeState);
        Assert.Single(field.ScoreBursts);
        Assert.Empty(field.Explosions);
    }
}
