using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.States.SpriteTest;
using Xunit;

namespace Robotron2084.Tests.States;

/// <summary>The SPRITE TEST page's animator: how long each frame shows, and what turning and standing still do.</summary>
public sealed class SpriteTestAnimatorTests
{
    private static readonly Texture2D[] ThreeFrames = new Texture2D[3];

    [Fact]
    public void EachFrameIsShownForItsHold_ThenTheNextOne()
    {
        var cycle = SpriteTestCycle.CreateFromTicks(ThreeFrames, 3);
        var animator = new SpriteTestAnimator(SpriteTestEntry.CreateUnsteerable("TEST", cycle), new Random(1));

        var frames = new List<int>();
        for (var tick = 0; tick < 10; tick++)
        {
            frames.Add(animator.FrameIndex);
            animator.Tick(null);
        }

        Assert.Equal([0, 0, 0, 1, 1, 1, 2, 2, 2, 0], frames);
    }

    [Fact]
    public void AHoldInArcadeFrames_IsKeptExactlyOverManyTicks()
    {
        // Four arcade frames are 24 clock units, and a tick is 5, so the frame changes every 4.8 ticks on average.
        var cycle = SpriteTestCycle.CreateFromArcadeFrames(ThreeFrames, 4);
        var animator = new SpriteTestAnimator(SpriteTestEntry.CreateUnsteerable("TEST", cycle), new Random(1));
        var changes = 0;
        var previous = animator.FrameIndex;
        var ticks = ArcadeClock.PortTicksPerSecond * 2;
        for (var tick = 0; tick < ticks; tick++)
        {
            animator.Tick(null);
            if (animator.FrameIndex != previous) changes++;
            previous = animator.FrameIndex;
        }

        Assert.Equal(ArcadeClock.ArcadeFramesPerSecond * 2 / 4, changes);
    }

    [Fact]
    public void ARandomHold_IsTheFramesHoldTimesANumberFromOneToTheMost()
    {
        // The first hold is 1 + 2 = 3 times one tick, and the next 1 + 0 = 1 time.
        var cycle = new SpriteTestCycle(
            [new SpriteTestFrame(ThreeFrames[0], ArcadeClock.UnitsPerPortTick), new SpriteTestFrame(ThreeFrames[1], ArcadeClock.UnitsPerPortTick)],
            3);
        var animator = new SpriteTestAnimator(SpriteTestEntry.CreateUnsteerable("TEST", cycle), new ScriptedRandom(2, 0, 0, 0));

        var frames = new List<int>();
        for (var tick = 0; tick < 5; tick++)
        {
            frames.Add(animator.FrameIndex);
            animator.Tick(null);
        }

        Assert.Equal([0, 0, 0, 1, 0], frames);
    }

    [Fact]
    public void TurningStartsTheNewWalkFromItsFirstFrame()
    {
        var left = SpriteTestCycle.CreateFromTicks(ThreeFrames, 1);
        var right = SpriteTestCycle.CreateFromTicks(ThreeFrames, 1);
        var entry = new SpriteTestEntry("TEST", direction => direction == Direction8.Right ? right : left, true);
        var animator = new SpriteTestAnimator(entry, new Random(1));

        animator.Tick(Direction8.Left);
        animator.Tick(Direction8.Left);
        Assert.Equal(2, animator.FrameIndex);

        animator.Tick(Direction8.Right);

        Assert.Equal(0, animator.FrameIndex);
    }

    [Fact]
    public void ASpriteWithNothingToShowWhenNoWayIsHeld_StaysOnItsFrame()
    {
        var walk = SpriteTestCycle.CreateFromTicks(ThreeFrames, 1);
        var entry = new SpriteTestEntry("TEST", direction => direction is null ? null : walk, true);
        var animator = new SpriteTestAnimator(entry, new Random(1));
        animator.Tick(Direction8.Down);
        Assert.Equal(1, animator.FrameIndex);

        for (var tick = 0; tick < 10; tick++) animator.Tick(null);

        Assert.Equal(1, animator.FrameIndex);
    }
}
