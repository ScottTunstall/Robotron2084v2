using Robotron2084.Audio;
using Robotron2084.States;
using Xunit;

namespace Robotron2084.Tests.States;

/// <summary>The speech test pages' lines: each page has the lines of one robot.</summary>
public sealed class SpeechTestStateTests
{
    [Fact]
    public void TheGorfPageHasTheGorfLines_AndTheBerzerkPageHasTheBerzerkLines()
    {
        var gorf = SpeechTestState.GetLineNames(SpeechTestState.GorfFolder);
        var berzerk = SpeechTestState.GetLineNames(SpeechTestState.BerzerkFolder);

        Assert.Contains("GOTYOU", gorf);
        Assert.DoesNotContain("GOTYOU", berzerk);
        Assert.Contains("INTRUDER", berzerk);
        Assert.DoesNotContain("INTRUDER", gorf);
    }

    [Fact]
    public void EveryLineIsOnExactlyOnePage()
    {
        var onPages = SpeechTestState.GetLineNames(SpeechTestState.GorfFolder).Count +
                      SpeechTestState.GetLineNames(SpeechTestState.BerzerkFolder).Count;

        Assert.Equal(SpeechLinePaths.All.Length, onPages);
    }

    [Fact]
    public void APageForAFolderThatHasNoLinesHasNone()
    {
        Assert.Empty(SpeechTestState.GetLineNames("Nobody"));
    }
}
