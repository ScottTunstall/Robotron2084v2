using Robotron2084.AttractMode;
using Robotron2084.Level.Attract;
using Robotron2084.States;
using Xunit;

namespace Robotron2084.Tests.States;

/// <summary>The order of the attract sequence's screens, for the keys that step through it by hand (port only).</summary>
public sealed class AttractNavigationTests
{
    [Fact]
    public void TheSequence_IsTheTitleTwice_TheStoryPages_TheDemo_AndTheHighScores()
    {
        Assert.Equal(0, AttractNavigation.TitleArcadePaneStep);
        Assert.Equal(1, AttractNavigation.TitlePortPaneStep);
        Assert.Equal(2, AttractNavigation.FirstStoryPageStep);
        Assert.Equal(AttractNavigation.FirstStoryPageStep + AttractNavigation.StoryPageCount, AttractNavigation.DemoStep);
        Assert.Equal(AttractNavigation.DemoStep + 1, AttractNavigation.HighScoresStep);
        Assert.Equal(AttractNavigation.HighScoresStep + 1, AttractNavigation.StepCount);
    }

    [Fact]
    public void Next_VisitsEveryStepOnceAndComesBackToTheFirst()
    {
        var visited = new List<int>();
        var step = AttractNavigation.TitleArcadePaneStep;
        for (var count = 0; count < AttractNavigation.StepCount; count++)
        {
            visited.Add(step);
            step = AttractNavigation.GetNeighbourStep(step, 1);
        }

        Assert.Equal(Enumerable.Range(0, AttractNavigation.StepCount), visited);
        Assert.Equal(AttractNavigation.TitleArcadePaneStep, step);
    }

    [Fact]
    public void Previous_UndoesNext_AndWrapsFromTheFirstStepToTheHighScores()
    {
        for (var step = 0; step < AttractNavigation.StepCount; step++)
        {
            var next = AttractNavigation.GetNeighbourStep(step, 1);
            Assert.Equal(step, AttractNavigation.GetNeighbourStep(next, -1));
        }

        Assert.Equal(AttractNavigation.HighScoresStep,
            AttractNavigation.GetNeighbourStep(AttractNavigation.TitleArcadePaneStep, -1));
        Assert.Equal(AttractNavigation.TitleArcadePaneStep,
            AttractNavigation.GetNeighbourStep(AttractNavigation.HighScoresStep, 1));
    }

    [Fact]
    public void TheStoryPageCount_IsTheNumberOfPagesTheStoryReallyHas()
    {
        var script = AttractStoryline.Create(AttractMovieData.Histo);
        var attractMovie = new AttractMovie(script, new Random(3));
        for (var page = 1; page <= AttractNavigation.StoryPageCount; page++) attractMovie.SkipToNextPage();

        Assert.Equal(AttractNavigation.StoryPageCount, attractMovie.AttractPageMachine.PagesStarted);
        Assert.False(attractMovie.IsFinished());

        attractMovie.SkipToNextPage();

        Assert.True(attractMovie.IsFinished()); // there is no eighth page
    }
}
