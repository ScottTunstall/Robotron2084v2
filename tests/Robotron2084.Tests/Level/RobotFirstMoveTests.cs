using Microsoft.Xna.Framework;
using Robotron2084.Core;
using Robotron2084.Entities;
using Robotron2084.Level;
using Xunit;

namespace Robotron2084.Tests;

/// <summary>
/// When each kind of robot makes its first move after the game goes live (notes §143). No robot sees `CLR STATUS` at once:
/// each routine sleeps and looks again (`BITA #$7F`), at its own interval, and its looks are counted from the frame the
/// wave was set up, because `MKPRCV` runs a new process on the frame it is made.
/// <list type="bullet">
/// <item>Grunts (`RRP8.ASM` `ROBOT`, R5 $39B7): a look every 2 frames, then `NAP 10,ROB0`.</item>
/// <item>Hulks (`RRH11.ASM` `HULK`, $0030): a look every 8 frames, then the first step at once.</item>
/// <item>Brains (`RRB10.ASM` `BRAIN`, $1BD8): a look every 4 frames, then `NAP 12,BRNL`.</item>
/// <item>Tanks (`RRTK4.ASM` `TANK`, $4D8B): a look every 15 frames, then the first beat at once.</item>
/// </list>
/// </summary>
public sealed class RobotFirstMoveTests
{
    private static GameTime Frame() => new(TimeSpan.Zero, TimeSpan.FromTicks(TimeSpan.TicksPerSecond / 60));

    private static PlayField CreateField(LevelParameters parameters) =>
        new PlayFieldBuilder().WithParameters(parameters).WithInput(new FakeInputSource()).WithSeed(7).Build();

    private static void TickToRomFrame(PlayField field, ref int ticks, int romFrame)
    {
        while (ticks < ArcadeClock.ToPortTicksRoundedUp(romFrame))
        {
            field.Update(Frame());
            ticks++;
        }
    }

    [Theory]
    [InlineData(2, 10, 78)] // a grunt: the look on frame 68, then 10
    [InlineData(8, 0, 72)] // a hulk: the look on frame 72
    [InlineData(4, 12, 80)] // a brain: the look on frame 68, then 12
    [InlineData(15, 0, 75)] // a tank: the look on frame 75
    public void TheWaitToTheFirstBeat_RunsFromTheFirstLookThatFindsTheGameLive(int pollRomFrames, int napRomFrames, int firstBeatRomFrame)
    {
        // Fifteen robots: the game goes live on ROM frame 68, which is tick 82, and that tick starts at 405 clock units.
        var sequence = new WaveStartSequence(15, isBrainWave: false);
        for (int tick = 1; tick <= 82; tick++)
        {
            Assert.False(sequence.HasJustGoneLive());
            sequence.Update();
        }

        Assert.True(sequence.HasJustGoneLive());
        Assert.Equal(ArcadeClock.ToClockUnits(firstBeatRomFrame) - 405, sequence.GetClockUnitsToFirstBeat(pollRomFrames, napRomFrames));

        sequence.Update();
        Assert.False(sequence.HasJustGoneLive());
    }

    [Theory]
    [InlineData(15, 78)] // live on frame 68, which is a frame the grunts look on
    [InlineData(14, 78)] // live on frame 67: the next look is on 68
    [InlineData(16, 80)] // live on frame 69: the next look is on 70
    public void TheGrunts_TakeTheirFirstStep_TenFramesAfterTheirFirstLook(int grunts, int firstStepRomFrame)
    {
        // A move delay of 1 makes every grunt step on every pass, so the first pass is the first step.
        PlayField field = CreateField(new LevelParameters(LevelNumber: 1, GruntCount: grunts, GruntMoveDelay: 1));
        IntVector2[] starts = [.. field.Entities.Grunts.Select(grunt => grunt.Position)];
        int ticks = 0;

        TickToRomFrame(field, ref ticks, firstStepRomFrame - 1);
        while (ticks < ArcadeClock.ToPortTicksRoundedUp(firstStepRomFrame) - 1)
        {
            field.Update(Frame());
            ticks++;
        }

        Assert.Equal(starts, field.Entities.Grunts.Select(grunt => grunt.Position));

        field.Update(Frame());
        Assert.All(field.Entities.Grunts, grunt => Assert.True(grunt.SteppedThisUpdate));
    }

    [Fact]
    public void AHulk_TakesItsFirstStep_OnItsFirstLookAfterTheGameGoesLive()
    {
        // One hulk: the player appears on ROM frame 44 and the game is live on 54. The hulk looks on frames 0, 8, 16
        // and so on, so its first look after that is on frame 56, and it steps on that look.
        PlayField field = CreateField(new LevelParameters(LevelNumber: 1, HulkCount: 1));
        Hulk hulk = field.Entities.Hulks[0];
        IntVector2 start = hulk.Position;
        int ticks = 0;

        TickToRomFrame(field, ref ticks, 55);
        Assert.True(field.IsLive());
        Assert.Equal(start, hulk.Position);

        TickToRomFrame(field, ref ticks, 56);
        Assert.NotEqual(start, hulk.Position);
    }

    [Fact]
    public void ABrain_TakesItsFirstBeat_TwelveFramesAfterItsFirstLook()
    {
        // A brain wave is live on ROM frame 160, which is a frame the brains look on, so the first beat is on 172.
        // A brain has no target until its first beat (BRNL0 resolves it).
        PlayField field = CreateField(new LevelParameters(LevelNumber: 5, BrainCount: 1, MikeyCount: 1));
        Brain brain = field.Entities.Brains[0];
        int ticks = 0;

        TickToRomFrame(field, ref ticks, 171);
        Assert.True(field.IsLive());
        Assert.Null(brain.Target);

        TickToRomFrame(field, ref ticks, 172);
        Assert.NotNull(brain.Target);
    }

    [Fact]
    public void ATankLeftFromTheLastMan_TakesItsFirstBeat_OnItsFirstLookAfterTheGameGoesLive()
    {
        // One tank: the game is live on ROM frame 54. The tank looks on frames 0, 15, 30 and so on, so its first
        // look after that is on frame 60. Its tread moves on by one picture each beat (TANK3).
        PlayField field = CreateField(new LevelParameters(LevelNumber: 1, TankCount: 1));
        Tank tank = field.Entities.Tanks[0];
        int treadAtStart = tank.TreadFrameIndex;
        int ticks = 0;

        TickToRomFrame(field, ref ticks, 59);
        Assert.True(field.IsLive());
        Assert.Equal(treadAtStart, tank.TreadFrameIndex);

        TickToRomFrame(field, ref ticks, 60);
        Assert.NotEqual(treadAtStart, tank.TreadFrameIndex);
    }
}
