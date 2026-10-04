using Microsoft.Xna.Framework;
using Robotron2084.Core;
using Robotron2084.Entities;
using Robotron2084.Level;
using Xunit;

namespace Robotron2084.Tests;

/// <summary>
/// The wave-start MATERIALISATION (RRG23's `APPEAR`, notes §61.4): the ROM creates
/// one APPEAR record per frame for the objects in the robot list — every fourth
/// with the horizontal (column) fan — while the robots themselves are OFF, so a
/// wave's robots ASSEMBLE instead of appearing.
/// </summary>
public class MaterialisationTests
{
    private static readonly Rectangle InnerBounds = PlayFieldBuilder.DefaultBounds;

    private static PlayField CreateField(int grunts, int hulks = 0, int spheroids = 0, int quarks = 0, int brains = 0)
    {
        var parameters = new LevelParameters(
            1,
            GruntCount: grunts,
            HulkCount: hulks,
            SpheroidCount: spheroids,
            QuarkCount: quarks,
            ElectrodeCount: 0,
            MaxEnforcersPerSpheroid: 1,
            MaxTanksPerQuark: 1,
            EnemySpeedBonus: 0);

        return new PlayFieldBuilder().WithParameters(parameters).WithBounds(InnerBounds).WithSeed(1234).Build();
    }

    private static void Advance(PlayField field, int ticks)
    {
        for (int i = 0; i < ticks; i++)
        {
            field.Update(new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16)));
        }
    }

    [Fact]
    public void EveryWaveStartRobot_IsQueuedForMaterialisation()
    {
        PlayField field = CreateField(grunts: 3, hulks: 2, spheroids: 1, quarks: 1, brains: 1);

        // Every spawned robot is queued and NONE has started assembling yet —
        // they count as assembling from the moment they are queued, because the
        // ROM holds them OFF for the whole sequence. (Brains are a wave-table
        // spawn, so this level has none.)
        int spawned = field.Entities.Grunts.Count + field.Entities.Hulks.Count + field.Entities.Spheroids.Count + field.Entities.Quarks.Count + field.Entities.Brains.Count;
        Assert.Equal(spawned, field.PendingAppearCount);
        Assert.True(field.IsMaterialising(field.Entities.Grunts[0]));
        Assert.True(field.IsMaterialising(field.Entities.Hulks[0]));
        Assert.True(field.IsMaterialising(field.Entities.Spheroids[0]));
        Assert.True(field.IsMaterialising(field.Entities.Quarks[0]));
    }

    [Fact]
    public void TheAppearChain_IsOneRobotPerFrame_AndClearsWhenEachConverges()
    {
        // The ROM makes ONE appear a frame (`LDA #1 / PSHS A ... NAP 1,APL`), and
        // an appear lives 15 calls (14 draws), so the queue drains one per frame
        // while the records converge over the next fourteen.
        PlayField field = CreateField(grunts: 4);

        Advance(field, 1);
        Assert.Equal(3, field.PendingAppearCount);
        Assert.Single(field.Entities.Explosions);
        Assert.Equal(StripEffectKind.Appear, field.Entities.Explosions[0].Kind);

        Advance(field, 3);
        Assert.Equal(0, field.PendingAppearCount);

        // 4 creates + 15 ROM frames per appear (18 ticks) = the last robot is free
        // by tick 34; allow a little slack for the queue draining.
        Advance(field, 22);
        Assert.DoesNotContain(field.Entities.Grunts, g => field.IsMaterialising(g));
        Assert.Empty(field.Entities.Explosions);
    }

    [Fact]
    public void AMaterialisingRobot_DoesNotAct()
    {
        // The ROM holds the robots OFF (ROBOFF) through the appear sequence, so a
        // robot that has not finished assembling must not move or fire.
        PlayField field = CreateField(grunts: 1);
        Grunt grunt = field.Entities.Grunts[0];
        IntVector2 start = grunt.Position;

        Advance(field, 3);

        Assert.True(field.IsMaterialising(grunt));
        Assert.Equal(start, grunt.Position);
    }

    [Fact]
    public void OneAppearStarts_OnEachRomFrame_NotOnEachPortTick()
    {
        // `NAP 1,APL` (R5 $2949): one robot's appear a ROM frame, the first at once. A ROM frame is 6/5 of a port
        // tick, so robot k starts on tick ceil(1.2 x (k - 1)), and the 7th tick starts none.
        PlayField field = CreateField(grunts: 8);

        Advance(field, 1);
        Assert.Equal(7, field.PendingAppearCount); // ROM frame 0, on the first tick

        Advance(field, 5);
        Assert.Equal(2, field.PendingAppearCount); // ticks 2 to 6 start robots 2 to 6

        Advance(field, 1);
        Assert.Equal(2, field.PendingAppearCount); // tick 7 is still ROM frame 5

        Advance(field, 1);
        Assert.Equal(1, field.PendingAppearCount); // tick 8 reaches ROM frame 6
    }

    [Fact]
    public void EveryFourthRobot_UsesTheHorizontalColumnFan()
    {
        // `LDA PD,U / ANDA #3 / CMPA #3 / BNE AP1 / JSR HAPST` — the sequence index
        // 3, 7, 11 ... (every fourth) is the horizontal (column) fan, the rest the
        // row fan.
        PlayField field = CreateField(grunts: 8);

        // One appear starts on each ROM frame, so some ticks start none: note each one as it starts.
        var axes = new List<StripFanAxis>();
        while (axes.Count < 8)
        {
            Advance(field, 1);
            if (field.Entities.Explosions.Count > axes.Count)
            {
                axes.Add(field.Entities.Explosions[^1].Axis);
            }
        }

        Assert.Equal(StripFanAxis.Rows, axes[0]);
        Assert.Equal(StripFanAxis.Rows, axes[2]);
        Assert.Equal(StripFanAxis.Columns, axes[3]);
        Assert.Equal(StripFanAxis.Columns, axes[7]);
    }

    [Fact]
    public void TheAppearConvergesOntoTheRobotsCentre()
    {
        PlayField field = CreateField(grunts: 1);
        Grunt grunt = field.Entities.Grunts[0];

        Advance(field, 1);

        // The record's own bounds are the robot's (the sprite it is blitting), and
        // APCENT gave it the robot's centre as the impact.
        StripEffect appear = field.Entities.Explosions[0];
        Assert.Equal(grunt.Bounds, appear.Bounds);
    }

    [Fact]
    public void AMaterialisingRobot_IsNotDrawnByTheFieldItself()
    {
        // Draw-time behaviour is not unit-testable (no GraphicsDevice), so this
        // pins the flag the field's DrawEntity guard reads: while it is true the
        // robot's own sprite is suppressed and only the appear strips draw it.
        PlayField field = CreateField(grunts: 1);
        Grunt grunt = field.Entities.Grunts[0];

        Assert.True(field.IsMaterialising(grunt));

        Advance(field, 20);
        Assert.False(field.IsMaterialising(grunt));
    }
}
