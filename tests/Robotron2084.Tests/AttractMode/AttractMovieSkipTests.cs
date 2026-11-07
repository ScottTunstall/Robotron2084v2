using Robotron2084.AttractMode;
using Robotron2084.Level.Attract;
using Xunit;

namespace Robotron2084.Tests.AttractMode;

/// <summary>Skipping on to the next page of the story (port only, a development key; notes §97).</summary>
public sealed class AttractMovieSkipTests
{
    /// <summary>How many pages the story has: each starts by clearing the text of the one before.</summary>
    private const int PageCount = 7;

    private static AttractMovie CreateMovie()
    {
        return new AttractMovie(AttractStoryline.Create(AttractMovieData.Histo), new Random(3));
    }

    [Fact]
    public void EachSkip_StartsExactlyTheNextPage()
    {
        var attractMovie = CreateMovie();
        Assert.Equal(0, attractMovie.AttractPageMachine.PagesStarted);

        for (var page = 1; page <= PageCount; page++)
        {
            attractMovie.SkipToNextPage();

            Assert.Equal(page, attractMovie.AttractPageMachine.PagesStarted);
        }
    }

    [Fact]
    public void SkippingFromTheLastPage_RunsTheMovieToItsEnd()
    {
        var attractMovie = CreateMovie();
        for (var page = 1; page <= PageCount; page++) attractMovie.SkipToNextPage();

        attractMovie.SkipToNextPage();

        Assert.True(attractMovie.IsFinished());
    }

    [Fact]
    public void ASkippedToPage_HasItsOwnTextAndTheEarlierPagesTextIsGone()
    {
        var attractMovie = CreateMovie();
        attractMovie.SkipToNextPage();
        attractMovie.SkipToNextPage();
        attractMovie.SkipToNextPage();

        // Three skips land on the family page, which has cleared the introduction's words before it.
        var textOnScreen = new string(attractMovie.AttractPageMachine.TextCells.Select(cell => cell.Character).ToArray());
        Assert.DoesNotContain("INSPIRED", textOnScreen);
        Assert.DoesNotContain("ROBOTRONS", textOnScreen);
    }

    [Fact]
    public void ASkip_ThrowsAwayTheExplosionsOfThePagesItPassed()
    {
        var attractMovie = CreateMovie();
        for (var page = 1; page <= PageCount; page++) attractMovie.SkipToNextPage();

        Assert.Empty(attractMovie.AttractObjectMachine.DrainExplosions());
    }
}
