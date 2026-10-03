using Microsoft.Xna.Framework;
using Robotron2084.Core;
using Robotron2084.Graphics;
using Xunit;

namespace Robotron2084.Tests;

/// <summary>The big ROBOTRON letters coming into view one at a time (ROM <c>RRLOG.ASM</c> <c>WDONE</c>).</summary>
public sealed class WordmarkAppearTests
{
    private static WordmarkAppear CreateAppear(int letterCount) => new(
        null!,
        null!,
        new Point(200, 29),
        [.. Enumerable.Range(0, letterCount).Select(index => new LetterSpan(index * 25, 20))],
        new Point(0, 0));

    [Fact]
    public void TheLetters_AreTheRunsOfColumnsThatHaveSomethingDrawnInThem()
    {
        bool[] columns = [false, true, true, false, false, true, false, true, true, true];

        Assert.Equal(
            [new LetterSpan(1, 2), new LetterSpan(5, 1), new LetterSpan(7, 3)],
            WordmarkAppear.FindLetters(columns));
    }

    [Fact]
    public void ALetterStartsEveryEightRomFrames_BeginningWithTheFirstAtOnce()
    {
        WordmarkAppear appear = CreateAppear(9);
        Assert.Equal(1, Started(appear, ticks: 1));

        appear = CreateAppear(9);
        Assert.Equal(1, Started(appear, ArcadeClock.ToPortTicksRoundedUp(7)));

        appear = CreateAppear(9);
        Assert.Equal(2, Started(appear, ArcadeClock.ToPortTicksRoundedUp(8)));

        appear = CreateAppear(9);
        Assert.Equal(9, Started(appear, ArcadeClock.ToPortTicksRoundedUp(8 * 8)));
    }

    [Fact]
    public void TheNextThingGoesUp_ThirtyTwoRomFramesAfterTheLastLetterStarts()
    {
        WordmarkAppear appear = CreateAppear(9);
        int ticks = ArcadeClock.ToPortTicksRoundedUp((8 * 8) + 0x20);

        for (int tick = 0; tick < ticks - 1; tick++)
        {
            appear.Update();
        }

        Assert.False(appear.IsFinished);
        appear.Update();
        Assert.True(appear.IsFinished);
    }

    [Fact]
    public void AWordWithNoLetters_IsFinishedAtOnce() => Assert.True(CreateAppear(0).IsFinished);

    private static int Started(WordmarkAppear appear, int ticks)
    {
        for (int tick = 0; tick < ticks; tick++)
        {
            appear.Update();
        }

        return appear.StartedLetterCount;
    }
}
