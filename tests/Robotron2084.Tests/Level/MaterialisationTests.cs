using Microsoft.Xna.Framework;
using Robotron2084.Core;
using Robotron2084.Entities;
using Robotron2084.Level;
using Robotron2084.Tuning;
using Xunit;

namespace Robotron2084.Tests.Level;

/// <summary>
///     How the robots are brought on at the start of a wave (notes §62, §143), from `RRG23.ASM` `APPEAR` (R5 $28FE to
///     $2962).
///     The loop makes one pass a fiftieth of a second (`NAP 1,APL`, $2949). Each pass starts the appear effect of the next
///     robot on the
///     robot list `RPTR`, and every fourth one is a column fan (`ANDA #3 / CMPA #3`, $291C). The robots are off meanwhile
///     (`ROBOFF`), and the loop draws robot j whole from pass 32 + j on (`CMPA #32`, $2930; `APREF`, $2965).
/// </summary>
public class MaterialisationTests
{
    private static readonly Rectangle InnerBounds = PlayFieldBuilder.DefaultBounds;

    private static PlayField CreateField(int grunts, int hulks = 0, int spheroids = 0, int quarks = 0, int tanks = 0)
    {
        var waveParameters = new WaveParameters(
            1,
            grunts,
            HulkCount: hulks,
            SpheroidCount: spheroids,
            QuarkCount: quarks,
            ElectrodeCount: 0,
            MaxEnforcersPerSpheroid: 1,
            MaxTanksPerQuark: 1,
            EnemySpeedBonus: 0,
            TankCount: tanks);

        return new PlayFieldBuilder().WithParameters(waveParameters).WithBounds(InnerBounds).WithSeed(1234).Build();
    }

    private static void Advance(PlayField playField, int ticks)
    {
        for (var i = 0; i < ticks; i++)
            playField.Update(new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16)));
    }

    [Fact]
    public void OnlyTheRobotsOnTheArcadesRobotList_AreBroughtOnByTheAppearLoop()
    {
        // GETROB puts grunts, hulks, brains and tanks on RPTR. Spheroids and quarks are made with MKPROB and go on the
        // object list OPTR (RRC11 CIRST, RRTK4 SQSTV), which APPEAR never walks, so they are on the screen from the start.
        var playField = CreateField(3, 2, 1, 1);

        Assert.Equal(5, playField.GetPendingAppearCount());
        Assert.All(playField.FieldEntities.Grunts, grunt => Assert.True(playField.IsMaterialising(grunt)));
        Assert.All(playField.FieldEntities.Hulks, hulk => Assert.True(playField.IsMaterialising(hulk)));
        Assert.False(playField.IsMaterialising(playField.FieldEntities.Spheroids[0]));
        Assert.False(playField.IsMaterialising(playField.FieldEntities.Quarks[0]));
    }

    [Fact]
    public void TheLoopMeetsTheRobots_InTheOppositeOrderToTheOneTheArcadeSetsThemUpIn()
    {
        // PLS0A sets up the hulks, then the tanks, then the grunts ($2831 to $2840), and GETRBV puts each new robot at
        // the head of RPTR. So the loop meets the last grunt made first, and the hulks last.
        var playField = CreateField(2, 1, tanks: 1);
        var appearBounds = new List<Rectangle>();

        while (appearBounds.Count < 4)
        {
            Advance(playField, 1);
            if (playField.FieldEntities.Explosions.Count > appearBounds.Count)
                appearBounds.Add(playField.FieldEntities.Explosions[^1].GetBounds());
        }

        Rectangle[] expected =
        [
            playField.FieldEntities.Grunts[1].GetBounds(),
            playField.FieldEntities.Grunts[0].GetBounds(),
            playField.FieldEntities.Tanks[0].GetBounds(),
            playField.FieldEntities.Hulks[0].GetBounds()
        ];
        Assert.Equal(expected, appearBounds);
    }

    [Fact]
    public void AMaterialisingRobot_DoesNotAct()
    {
        // The ROM holds the robots OFF (ROBOFF) through the appear sequence, so a
        // robot that has not finished assembling must not move or fire.
        var playField = CreateField(1);
        var grunt = playField.FieldEntities.Grunts[0];
        var start = grunt.Position;

        Advance(playField, 3);

        Assert.True(playField.IsMaterialising(grunt));
        Assert.Equal(start, grunt.Position);
    }

    [Fact]
    public void OneAppearStarts_OnEachArcadeFrame_NotOnEachPortTick()
    {
        // `NAP 1,APL` (R5 $2949): one robot's appear a fiftieth of a second, the first at once. A fiftieth of a second is 6/5 of a port
        // tick, so robot k starts on tick ceil(1.2 x (k - 1)), and the 7th tick starts none.
        var playField = CreateField(8);

        Advance(playField, 1);
        Assert.Equal(7, playField.GetPendingAppearCount()); // fiftieth of a second 0, on the first tick
        Assert.Single(playField.FieldEntities.Explosions);
        Assert.Equal(StripEffectKind.Appear, playField.FieldEntities.Explosions[0].Kind);

        Advance(playField, 5);
        Assert.Equal(2, playField.GetPendingAppearCount()); // ticks 2 to 6 start robots 2 to 6

        Advance(playField, 1);
        Assert.Equal(2, playField.GetPendingAppearCount()); // tick 7 is still fiftieth of a second 5

        Advance(playField, 1);
        Assert.Equal(1, playField.GetPendingAppearCount()); // tick 8 reaches fiftieth of a second 6
    }

    [Fact]
    public void EveryFourthRobot_UsesTheHorizontalColumnFan()
    {
        // `LDA PD,U / ANDA #3 / CMPA #3 / BNE AP1 / JSR HAPST` — the fourth and the eighth robots get the horizontal
        // (column) fan, the rest the row fan.
        var playField = CreateField(8);

        // One appear starts on each fiftieth of a second, so some ticks start none: note each one as it starts.
        var axes = new List<StripFanAxis>();
        while (axes.Count < 8)
        {
            Advance(playField, 1);
            if (playField.FieldEntities.Explosions.Count > axes.Count) axes.Add(playField.FieldEntities.Explosions[^1].Axis);
        }

        Assert.Equal(StripFanAxis.Rows, axes[0]);
        Assert.Equal(StripFanAxis.Rows, axes[2]);
        Assert.Equal(StripFanAxis.Columns, axes[3]);
        Assert.Equal(StripFanAxis.Columns, axes[7]);
    }

    [Fact]
    public void ARobotWhoseStripRoutineHasNoRecordFree_GetsNoEffect_AndTheLoopDoesNotWaitForIt()
    {
        // The horizontal routine has two records (RRHX4 HXINV, R5 $F01D: the loop that links them runs once). Robots 4
        // and 8 take them, and they are still in use when robot 12's turn comes. HAPST then makes nothing, and
        // `AP2 STX PD2,U` ($292A) moves the loop on to robot 13 on the next pass all the same.
        var playField = CreateField(13);

        Advance(playField, ArcadeClock.ToPortTicksRoundedUp(11)); // pass 12, on fiftieth of a second 11

        Assert.Equal(1, playField.GetPendingAppearCount());
        Assert.Equal(11, playField.FieldEntities.Explosions.Count);
        Assert.Equal(StripEffectTuning.HorizontalPoolSize,
            playField.FieldEntities.Explosions.Count(effect => effect.Axis == StripFanAxis.Columns));

        Advance(playField, 1); // tick 15 reaches fiftieth of a second 12: pass 13

        Assert.Equal(0, playField.GetPendingAppearCount());
        Assert.Equal(12, playField.FieldEntities.Explosions.Count);
        Assert.Equal(StripFanAxis.Rows, playField.FieldEntities.Explosions[^1].Axis);
    }

    [Fact]
    public void TheAppearEffect_IsLaidOutInTheRobotsOwnBox()
    {
        var playField = CreateField(1);
        var grunt = playField.FieldEntities.Grunts[0];

        Advance(playField, 1);

        var appear = playField.FieldEntities.Explosions[0];
        Assert.Equal(grunt.GetBounds(), appear.GetBounds());
    }

    [Fact]
    public void TheStripsCloseInOnARow_ThatIsAsFarDownTheSpriteAsTheRobotIsDownTheScreen()
    {
        // APCENT ($29B5): the centre row is OBJH x OBJY / 256 rows below the sprite's top. The arcade's playfield
        // runs from row 24 (YMIN) to row 234 (YMAX), so a 13-row grunt at the top closes in on row 1 and one at the
        // bottom on row 11.
        Rectangle atTop = new(InnerBounds.X + 100, InnerBounds.Y, 20, 26);
        Rectangle atBottom = new(InnerBounds.X + 100, InnerBounds.Bottom, 20, 26);

        Assert.Equal(1, WaveMaterialisation.GetCentreRowFinder(atTop, InnerBounds)(13));
        Assert.Equal(11, WaveMaterialisation.GetCentreRowFinder(atBottom, InnerBounds)(13));
    }

    [Fact]
    public void TheStripsCloseInOnAColumn_ThatIsAsFarAcrossTheSpriteAsTheRobotIsAcrossTheScreen()
    {
        // APCENT doubles the column to a pixel (held at 255) and takes OBJW x that / 256 columns. The arcade's playfield
        // runs from column 7 (XMIN) to column $8F (XMAX), and a column is two pixels: a sprite 5 columns wide at the
        // left closes in on its first pixel column and one at the right on its ninth.
        Rectangle atLeft = new(InnerBounds.X, InnerBounds.Y + 100, 20, 26);
        Rectangle atRight = new(InnerBounds.Right, InnerBounds.Y + 100, 20, 26);

        Assert.Equal(0, WaveMaterialisation.GetCentreColumnFinder(atLeft, InnerBounds)(10));
        Assert.Equal(8, WaveMaterialisation.GetCentreColumnFinder(atRight, InnerBounds)(10));
    }

    [Fact]
    public void ARobotIsDrawnWhole_ThirtyTwoPassesAfterItsAppearStarts()
    {
        // The first robot's appear starts on pass 1. Its vertical effect is over on fiftieth of a second 29, and APREF draws the
        // robot itself from pass 33, which is fiftieth of a second 32. Draw-time behaviour cannot be tested (there is no graphics
        // device), so this pins the flag that the field's DrawEntity reads.
        var playField = CreateField(1);
        var grunt = playField.FieldEntities.Grunts[0];

        Advance(playField, ArcadeClock.ToPortTicksRoundedUp(29) - 1);
        Assert.Single(playField.FieldEntities.Explosions);

        Advance(playField, 1);
        Assert.Empty(playField.FieldEntities.Explosions);
        Assert.True(playField.IsMaterialising(grunt));

        Advance(playField, ArcadeClock.ToPortTicksRoundedUp(32) - ArcadeClock.ToPortTicksRoundedUp(29) - 1);
        Assert.True(playField.IsMaterialising(grunt));

        Advance(playField, 1);
        Assert.False(playField.IsMaterialising(grunt));
    }

    [Fact]
    public void SpheroidsAndQuarks_StandStill_UntilTheGameIsLive()
    {
        // With STATUS bit 3 set the arcade does not add a motion object's velocity (RRS22 OPRC80, `BITA #8 / BNE O80`),
        // and each of their routines holds its drop timer (`TST STATUS`, RRC11 CIRCLE and RRTK4 SQUARE).
        var playField = CreateField(0, spheroids: 2, quarks: 2);
        IntVector2[] spheroidStarts = [.. playField.FieldEntities.Spheroids.Select(spheroid => spheroid.Position)];
        IntVector2[] quarkStarts = [.. playField.FieldEntities.Quarks.Select(quark => quark.Position)];

        Advance(playField, WaveStartTicks.UntilLive(playField) - 1);

        Assert.Equal(spheroidStarts, playField.FieldEntities.Spheroids.Select(spheroid => spheroid.Position));
        Assert.Equal(quarkStarts, playField.FieldEntities.Quarks.Select(quark => quark.Position));
        Assert.Empty(playField.FieldEntities.Enforcers);
    }
}
