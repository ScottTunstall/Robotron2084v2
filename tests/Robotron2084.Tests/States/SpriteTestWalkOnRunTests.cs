using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Entities;
using Robotron2084.States.SpriteTest;
using Robotron2084.Tuning;
using Xunit;

namespace Robotron2084.Tests.States;

/// <summary>A sprite's walk onto the SPRITE TEST page: its steps to the left, and Gorf's hops.</summary>
public sealed class SpriteTestWalkOnRunTests
{
    private const int StartX = 640;

    private static readonly Texture2D[] FourFrames = new Texture2D[4];

    [Fact]
    public void ASpriteThatStepsAsItsFrameChanges_TakesTheStepForTheFrameItMovesOnTo_AndNoneBetween()
    {
        // Frames change every tick; steps of 1, 2, 3 and 4 pixels go with frames 0, 1, 2 and 3.
        var entry = SpriteTestEntry.CreateUnsteerable("TEST", SpriteTestCycle.CreateFromTicks(FourFrames, 1));
        var animator = new SpriteTestAnimator(entry, new Random(1));
        var run = new SpriteTestWalkOnRun(new SpriteTestWalkOn([1, 2, 3, 4]), StartX, animator);

        animator.Tick(Direction8.Left);
        run.Tick(animator);
        Assert.Equal(StartX - 2, run.X); // frame 1's step

        animator.Tick(Direction8.Left);
        run.Tick(animator);
        Assert.Equal(StartX - 2 - 3, run.X); // frame 2's step
    }

    [Fact]
    public void ASpriteThatStepsAsItsFrameChanges_StandsStillWhileItsFrameHolds()
    {
        var entry = SpriteTestEntry.CreateUnsteerable("TEST", SpriteTestCycle.CreateFromTicks(FourFrames, 3));
        var animator = new SpriteTestAnimator(entry, new Random(1));
        var run = new SpriteTestWalkOnRun(new SpriteTestWalkOn([5]), StartX, animator);

        animator.Tick(Direction8.Left);
        run.Tick(animator);
        animator.Tick(Direction8.Left);
        run.Tick(animator);
        Assert.Equal(StartX, run.X);

        animator.Tick(Direction8.Left);
        run.Tick(animator);
        Assert.Equal(StartX - 5, run.X);
    }

    [Fact]
    public void ThePlayerWalksOnAtItsSpeedInTheGame_OneStepEveryTick()
    {
        var player = SpriteTestEntries.Create(TestTextures.SharedTextureSet).Single(entry => entry.Name == "PLAYER");
        var animator = new SpriteTestAnimator(player, new Random(1));
        var run = new SpriteTestWalkOnRun(player.WalkOn!, StartX, animator);

        for (var tick = 0; tick < 10; tick++)
        {
            animator.Tick(Direction8.Left);
            run.Tick(animator);
        }

        Assert.Equal(StartX - 10 * PlayerTuning.PlayerSpeedX, run.X);
        Assert.Equal(0, run.Rise);
    }

    [Fact]
    public void GorfHopsOn_RisingAndLandingAsItDoesInTheGame()
    {
        var gorf = SpriteTestEntries.Create(TestTextures.SharedTextureSet).Single(entry => entry.Name == "GORF");
        var animator = new SpriteTestAnimator(gorf, new Random(1));
        var run = new SpriteTestWalkOnRun(gorf.WalkOn!, StartX, animator);
        var highest = 0;
        // The ticks a whole hop takes, rounded up so that its last step is taken.
        var hopClockUnits = GorfTuning.HopSteps * ArcadeClock.ToClockUnits(GorfTuning.BeatIntervalArcadeFrames);
        var ticksForAHop = (hopClockUnits + ArcadeClock.UnitsPerPortTick - 1) / ArcadeClock.UnitsPerPortTick;

        for (var tick = 0; tick < ticksForAHop; tick++)
        {
            animator.Tick(Direction8.Left);
            run.Tick(animator);
            highest = Math.Max(highest, run.Rise);
        }

        Assert.Equal(Gorf.GetHopRisePixels(GorfTuning.HopSteps / 2), highest);
        Assert.Equal(0, run.Rise); // a whole hop ends on the ground
        Assert.Equal(StartX - GorfTuning.HopSteps * GorfTuning.StepPixels, run.X);
    }

    [Fact]
    public void GorfIsOnTheGroundAtTheEndOfEachHop_AndHighestHalfwayThrough()
    {
        Assert.Equal(0, Gorf.GetHopRisePixels(GorfTuning.HopSteps));
        Assert.Equal(0, Gorf.GetHopRisePixels(GorfTuning.HopSteps * 2));
        Assert.Equal(ScreenSize.ToPortPixelsFromArcadePixels(GorfTuning.HopRows), Gorf.GetHopRisePixels(GorfTuning.HopSteps / 2));
        Assert.Equal(Gorf.GetHopRisePixels(1), Gorf.GetHopRisePixels(GorfTuning.HopSteps + 1));
    }

    [Theory]
    [InlineData("PLAYER")]
    [InlineData("MOMMY")]
    [InlineData("DADDY")]
    [InlineData("MIKEY")]
    [InlineData("GRUNT")]
    [InlineData("HULK")]
    [InlineData("BRAIN")]
    [InlineData("PROG MOMMY")]
    [InlineData("BERZERK ROBOT")]
    [InlineData("GORF")]
    public void EverySpriteThatWalksInTheGame_CanWalkOn(string name)
    {
        var entry = SpriteTestEntries.Create(TestTextures.SharedTextureSet).Single(candidate => candidate.Name == name);

        Assert.NotNull(entry.WalkOn);
        Assert.All(entry.WalkOn!.StepPortPixels, step => Assert.True(step > 0, "every step goes left"));
    }
}
