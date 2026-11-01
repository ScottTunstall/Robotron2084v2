using Microsoft.Xna.Framework;
using Robotron2084.Core;
using Robotron2084.Level;
using Xunit;

namespace Robotron2084.Tests;

/// <summary>
///     When each kind of robot makes its first move after the game goes live (notes §143). No robot sees `CLR STATUS` at
///     once:
///     each routine sleeps and looks again (`BITA #$7F`), at its own interval, and its looks are counted from the frame
///     the
///     wave was set up, because `MKPRCV` runs a new process on the frame it is made.
///     <list type="bullet">
///         <item>Grunts (`RRP8.ASM` `ROBOT`, R5 $39B7): a look every 2 frames, then `NAP 10,ROB0`.</item>
///         <item>Hulks (`RRH11.ASM` `HULK`, $0030): a look every 8 frames, then the first step at once.</item>
///         <item>Brains (`RRB10.ASM` `BRAIN`, $1BD8): a look every 4 frames, then `NAP 12,BRNL`.</item>
///         <item>Tanks (`RRTK4.ASM` `TANK`, $4D8B): a look every 15 frames, then the first beat at once.</item>
///     </list>
/// </summary>
public sealed class RobotFirstMoveTests
{
    private static GameTime OneTick()
    {
        return new GameTime(TimeSpan.Zero, TimeSpan.FromTicks(TimeSpan.TicksPerSecond / 60));
    }

    private static PlayField CreateField(WaveParameters waveParameters)
    {
        return new PlayFieldBuilder().WithParameters(waveParameters).WithInput(new FakeInputSource()).WithSeed(7).Build();
    }

    private static int TickToArcadeFrame(PlayField playField, int ticks, int arcadeFrame)
    {
        while (ticks < ArcadeClock.ToPortTicksRoundedUp(arcadeFrame))
        {
            playField.Update(OneTick());
            ticks++;
        }

        return ticks;
    }

    [Theory]
    [InlineData(2, 10, 78)] // a grunt: the look on arcade frame 68, then 10
    [InlineData(8, 0, 72)] // a hulk: the look on frame 72
    [InlineData(4, 12, 80)] // a brain: the look on arcade frame 68, then 12
    [InlineData(15, 0, 75)] // a tank: the look on frame 75
    public void TheWaitToTheFirstBeat_RunsFromTheFirstLookThatFindsTheGameLive(int pollArcadeFrames, int napArcadeFrames,
        int firstBeatArcadeFrame)
    {
        // Fifteen robots: the game goes live on fiftieth of a second 68, which is tick 82, and that tick starts at 405 clock units.
        var waveStartSequence = new WaveStartSequence(15, false);
        for (var tick = 1; tick <= 82; tick++)
        {
            Assert.False(waveStartSequence.HasJustGoneLive());
            waveStartSequence.Update();
        }

        Assert.True(waveStartSequence.HasJustGoneLive());
        Assert.Equal(ArcadeClock.ToClockUnits(firstBeatArcadeFrame) - 405,
            waveStartSequence.GetClockUnitsToFirstBeat(pollArcadeFrames, napArcadeFrames));

        waveStartSequence.Update();
        Assert.False(waveStartSequence.HasJustGoneLive());
    }

    [Theory]
    [InlineData(15, 78)] // live on arcade frame 68, which is an arcade frame the grunts look on
    [InlineData(14, 78)] // live on frame 67: the next look is on 68
    [InlineData(16, 80)] // live on frame 69: the next look is on 70
    public void TheGrunts_TakeTheirFirstStep_TenFramesAfterTheirFirstLook(int grunts, int firstStepArcadeFrame)
    {
        // A move delay of 1 makes every grunt step on every pass, so the first pass is the first step.
        var playField = CreateField(new WaveParameters(1, grunts, GruntMoveDelay: 1));
        IntVector2[] starts = [.. playField.FieldEntities.Grunts.Select(grunt => grunt.Position)];
        var ticks = 0;

        ticks = TickToArcadeFrame(playField, ticks, firstStepArcadeFrame - 1);
        while (ticks < ArcadeClock.ToPortTicksRoundedUp(firstStepArcadeFrame) - 1)
        {
            playField.Update(OneTick());
            ticks++;
        }

        Assert.Equal(starts, playField.FieldEntities.Grunts.Select(grunt => grunt.Position));

        playField.Update(OneTick());
        Assert.All(playField.FieldEntities.Grunts, grunt => Assert.True(grunt.SteppedThisUpdate));
    }

    [Fact]
    public void AHulk_TakesItsFirstStep_OnItsFirstLookAfterTheGameGoesLive()
    {
        // One hulk: the player appears on fiftieth of a second 44 and the game is live on 54. The hulk looks on frames 0, 8, 16
        // and so on, so its first look after that is on frame 56, and it steps on that look.
        var playField = CreateField(new WaveParameters(1, HulkCount: 1));
        var hulk = playField.FieldEntities.Hulks[0];
        var start = hulk.Position;
        var ticks = 0;

        ticks = TickToArcadeFrame(playField, ticks, 55);
        Assert.True(playField.IsLive());
        Assert.Equal(start, hulk.Position);

        ticks = TickToArcadeFrame(playField, ticks, 56);
        Assert.NotEqual(start, hulk.Position);
    }

    [Fact]
    public void ABrain_TakesItsFirstBeat_TwelveFramesAfterItsFirstLook()
    {
        // A brain wave is live on fiftieth of a second 160, which is a frame the brains look on, so the first beat is on 172.
        // A brain has no target until its first beat (BRNL0 resolves it).
        var playField = CreateField(new WaveParameters(5, BrainCount: 1, MikeyCount: 1));
        var brain = playField.FieldEntities.Brains[0];
        var ticks = 0;

        ticks = TickToArcadeFrame(playField, ticks, 171);
        Assert.True(playField.IsLive());
        Assert.Null(brain.TargetFamilyMember);

        ticks = TickToArcadeFrame(playField, ticks, 172);
        Assert.NotNull(brain.TargetFamilyMember);
    }

    [Fact]
    public void ATankLeftFromTheLastMan_TakesItsFirstBeat_OnItsFirstLookAfterTheGameGoesLive()
    {
        // One tank: the game is live on fiftieth of a second 54. The tank looks on frames 0, 15, 30 and so on, so its first
        // look after that is on frame 60. Its tread moves on by one picture each beat (TANK3).
        var playField = CreateField(new WaveParameters(1, TankCount: 1));
        var tank = playField.FieldEntities.Tanks[0];
        var treadAtStart = tank.TreadFrameIndex;
        var ticks = 0;

        ticks = TickToArcadeFrame(playField, ticks, 59);
        Assert.True(playField.IsLive());
        Assert.Equal(treadAtStart, tank.TreadFrameIndex);

        ticks = TickToArcadeFrame(playField, ticks, 60);
        Assert.NotEqual(treadAtStart, tank.TreadFrameIndex);
    }
}
