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
        var gorf = SpeechFolders.GetLineNames(SpeechFolders.Gorf);
        var berzerk = SpeechFolders.GetLineNames(SpeechFolders.Berzerk);

        Assert.Contains("GOTYOU", gorf);
        Assert.DoesNotContain("GOTYOU", berzerk);
        Assert.Contains("INTRUDER", berzerk);
        Assert.DoesNotContain("INTRUDER", gorf);
    }

    [Fact]
    public void EveryLineIsOnExactlyOnePage()
    {
        var onPages = SpeechFolders.GetLineNames(SpeechFolders.Gorf).Count +
                      SpeechFolders.GetLineNames(SpeechFolders.Berzerk).Count;

        Assert.Equal(SpeechLinePaths.All.Length, onPages);
    }

    [Fact]
    public void APageForAFolderThatHasNoLinesHasNone()
    {
        Assert.Empty(SpeechFolders.GetLineNames("Nobody"));
    }
}
