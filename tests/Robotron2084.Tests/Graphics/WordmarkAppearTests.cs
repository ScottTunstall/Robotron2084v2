using Microsoft.Xna.Framework;
using Robotron2084.Core;
using Robotron2084.Graphics;
using Xunit;

namespace Robotron2084.Tests.Graphics;

/// <summary>The big ROBOTRON letters coming into view one at a time (ROM <c>RRLOG.ASM</c> <c>WDONE</c>).</summary>
public sealed class WordmarkAppearTests
{
    private static WordmarkAppear CreateAppear(int letterCount)
    {
        return new WordmarkAppear(
            null!,
            null!,
            new Point(200, 29),
            [.. Enumerable.Range(0, letterCount).Select(index => new LetterSpan(index * 25, 20))],
            new Point(0, 0));
    }

    [Fact]
    public void TheLetters_AreTheRunsOfColumnsThatHaveSomethingDrawnInThem()
    {
        bool[] columns = [false, true, true, false, false, true, false, true, true, true];

        Assert.Equal(
            [new LetterSpan(1, 2), new LetterSpan(5, 1), new LetterSpan(7, 3)],
            WordmarkAppear.FindLetters(columns));
    }

    [Fact]
    public void ALetterStartsEveryEightArcadeFrames_BeginningWithTheFirstAtOnce()
    {
        var wordmarkAppear = CreateAppear(9);
        Assert.Equal(1, Started(wordmarkAppear, 1));

        wordmarkAppear = CreateAppear(9);
        Assert.Equal(1, Started(wordmarkAppear, ArcadeClock.ToPortTicksRoundedUp(7)));

        wordmarkAppear = CreateAppear(9);
        Assert.Equal(2, Started(wordmarkAppear, ArcadeClock.ToPortTicksRoundedUp(8)));

        wordmarkAppear = CreateAppear(9);
        Assert.Equal(9, Started(wordmarkAppear, ArcadeClock.ToPortTicksRoundedUp(8 * 8)));
    }

    [Fact]
    public void TheNextThingGoesUp_ThirtyTwoArcadeFramesAfterTheLastLetterStarts()
    {
        var wordmarkAppear = CreateAppear(9);
        var ticks = ArcadeClock.ToPortTicksRoundedUp(8 * 8 + 0x20);

        for (var tick = 0; tick < ticks - 1; tick++) wordmarkAppear.Update();

        Assert.False(wordmarkAppear.IsFinished());
        wordmarkAppear.Update();
        Assert.True(wordmarkAppear.IsFinished());
    }

    [Fact]
    public void AWordWithNoLetters_IsFinishedAtOnce()
    {
        Assert.True(CreateAppear(0).IsFinished());
    }

    private static int Started(WordmarkAppear wordmarkAppear, int ticks)
    {
        for (var tick = 0; tick < ticks; tick++) wordmarkAppear.Update();

        return wordmarkAppear.GetStartedLetterCount();
    }
}
