using Robotron2084.Audio.Synthesis;
using Robotron2084.Core.Models;
using Robotron2084.Core;
using Robotron2084.Entities.Extensions;
using Robotron2084.Entities.Models;
using Robotron2084.Events.Models;
using Robotron2084.Events;
using Robotron2084.Palette;
using Robotron2084.Persistence.Models;
using Robotron2084.Persistence;
using Robotron2084.Score;
using Robotron2084.States;
using Robotron2084.Tests.Events;
using Robotron2084.Tuning;
using System.Text;
using Xunit;

namespace Robotron2084.Tests;

/// <summary>What the code review of 21 November 2026 asked for in the main code, one check for each thing that was changed.</summary>
public sealed class CodeReviewFixesTests
{
    private static readonly GameEvent AnEvent = new(GameEventKind.FamilyMemberRescued, IntVector2.Zero);

    [Fact]
    public void AGorfsDropLimitCannotBeZero_BecauseTheNumberOfDropsIsPickedUpToIt()
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new GorfDropParams(0));
    }

    [Fact]
    public void AListenerThatIsSubscribedTwiceIsToldOnce()
    {
        var hub = new GameEventHub();
        var listener = new RecordingGameEvents();

        hub.Subscribe(listener);
        hub.Subscribe(listener);
        hub.Raise(AnEvent);

        Assert.Equal(1, hub.ListenerCount);
        Assert.Single(listener.Events);
    }

    [Fact]
    public void AListenerThatUnsubscribesIsNotToldAgain()
    {
        var hub = new GameEventHub();
        var listener = new RecordingGameEvents();
        hub.Subscribe(listener);

        hub.Unsubscribe(listener);
        hub.Raise(AnEvent);

        Assert.Empty(listener.Events);
        Assert.Equal(0, hub.ListenerCount);
    }

    [Fact]
    public void AListenerAddedWhileAnEventIsPassedOnDoesNotBreakIt_AndHearsTheNextEvent()
    {
        var hub = new GameEventHub();
        var latecomer = new RecordingGameEvents();
        hub.Subscribe(new SubscribingListener(hub, latecomer));

        hub.Raise(AnEvent);
        Assert.Empty(latecomer.Events);

        hub.Raise(AnEvent);
        Assert.Single(latecomer.Events);
    }

    [Fact]
    public void ThePatternsInTheWaveTableDataAreAllThere()
    {
        Assert.NotEmpty(WaveTableData.Patterns);
    }

    [Fact]
    public void AScoreCannotGoDownOrBeNegative()
    {
        var scoreBoard = new ScoreBoard(0);

        _ = Assert.Throws<ArgumentOutOfRangeException>(() => scoreBoard.Add(-1));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new ScoreBoard(0, -1));
    }

    [Fact]
    public void AScoreStaysAtTheBiggestNumberInsteadOfWrappingRound()
    {
        var scoreBoard = new ScoreBoard(int.MaxValue - 5);

        _ = scoreBoard.Add(1000);

        Assert.Equal(int.MaxValue, scoreBoard.Score);
    }

    [Fact]
    public void ThePaletteRefusesAnEntryItDoesNotHave()
    {
        var palette = new GamePalette();

        _ = Assert.Throws<ArgumentOutOfRangeException>(() => palette.GetColour(16));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => palette.SetPaletteIndexValue(-1, 0));
    }

    [Fact]
    public void ThePaletteTablesAreReadOnly()
    {
        Assert.IsNotType<byte[]>(GamePalette.DefaultPaletteValues);
        Assert.IsNotType<byte[]>(PaletteAnimator.LaserTab);
    }

    [Fact]
    public void AWaveBelowOneHasNoPaletteEntry()
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => WavePaletteTables.GetWallPaletteIndex(0));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => WavePaletteTables.GetElectrodeVariant(-3));
    }

    [Fact]
    public void ANegativeLimitIsRefusedByTheArcadePick_AndALimitOfZeroGivesOne()
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => ArcadeRandom.PickUpTo(new Random(1), -1));
        Assert.Equal(1, ArcadeRandom.PickUpTo(new Random(1), 0));
    }

    [Fact]
    public void TheStateManagerRefusesANullState()
    {
        _ = Assert.Throws<ArgumentNullException>(() => new GameStateManager(null!));
    }

    [Fact]
    public void AnAttractStepOutsideTheSequenceIsRefused()
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => AttractNavigation.CreateState(-1, null!));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => AttractNavigation.CreateState(AttractNavigation.StepCount, null!));
    }

    [Fact]
    public void AnUnknownFamilyMemberKindIsRefused_NotTakenForDaddy()
    {
        var unknownKind = (FamilyMemberKind)123;

        _ = Assert.Throws<ArgumentOutOfRangeException>(() => unknownKind.GetArcadeCollisionSize());
    }

    [Fact]
    public void ASavedFileIsReplacedWhole_AndNoTemporaryFileIsLeftBehind()
    {
        var folder = Path.Combine(Path.GetTempPath(), "Robotron2084Tests-" + Guid.NewGuid());
        var path = Path.Combine(folder, "test.ini");
        try
        {
            AtomicFile.WriteAllText(path, "first", new UTF8Encoding(false));
            AtomicFile.WriteAllText(path, "second", new UTF8Encoding(false));

            Assert.Equal("second", File.ReadAllText(path));
            Assert.Equal([path], Directory.GetFiles(folder));
        }
        finally
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }
    }

    [Fact]
    public void AppDataPathsRefusesAFileNameWithAFolderInIt()
    {
        _ = Assert.Throws<ArgumentException>(() => AppDataPaths.GetFilePath(@"..\outside.json"));
        _ = Assert.Throws<ArgumentException>(() => AppDataPaths.GetFilePath(" "));
    }

    [Fact]
    public void AHandEditedHighScoreFileIsMadeFitToUse()
    {
        var table = HighScoreTable.CreateFromSaved(
            new TopScoreEntry("A name that is far longer than twenty three characters", 99_999_999),
            [new HighScoreEntry("AB", 500), null!, new HighScoreEntry(null!, -5), new HighScoreEntry("LONGER", 900)]);

        Assert.Equal(HighScoreTable.TopNameLength, table.TopScoreEntry.Name.Length);
        Assert.Equal(HighScoreTable.MaxScore, table.TopScoreEntry.Score);
        Assert.All(table.AllTime, entry => Assert.Equal(HighScoreTable.InitialsLength, entry.Initials.Length));
        Assert.Equal(900, table.AllTime[0].Score);
        Assert.Equal(500, table.AllTime[1].Score);
        Assert.All(table.AllTime, entry => Assert.InRange(entry.Score, 0, HighScoreTable.MaxScore));
    }

    private sealed class SubscribingListener(GameEventHub hub, IGameEventListener latecomer) : IGameEventListener
    {
        public void Handle(GameEvent gameEvent)
        {
            hub.Subscribe(latecomer);
        }
    }
}
