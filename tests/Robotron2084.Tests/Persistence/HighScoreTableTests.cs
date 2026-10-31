using Robotron2084.Persistence;
using Xunit;

namespace Robotron2084.Tests.Persistence;

/// <summary>
///     The arcade's high score table (notes §98) — RRTESTC's CMOS lists and its own
///     factory tables, and the ENDGAM score rules (`GODCHK` → `TODCHK`/`ALLCHK`).
/// </summary>
public sealed class HighScoreTableTests
{
    [Fact]
    public void FactoryTable_IsTheRomsOwnDefaultTable()
    {
        var highScoreTable = HighScoreTable.CreateWithFactoryScores();

        // DEFHSR/DEFGOD: the operator's top entry is "WILLY ELKTRIX" at 151782.
        Assert.Equal("WILLY ELKTRIX", highScoreTable.TopScoreEntry.Name);
        Assert.Equal(151782, highScoreTable.TopScoreEntry.Score);

        // TODTAB: ten entries, highest first, and exactly the ROM's names/scores.
        Assert.Equal(10, highScoreTable.Today.Count);
        Assert.Equal([("DRJ", 52127), ("LED", 50218), ("EPJ", 41255)],
            highScoreTable.Today.Take(3).Select(e => (e.GetDisplayName(), e.Score)));
        Assert.Equal(("CJM", 24110), (highScoreTable.Today[^1].GetDisplayName(), highScoreTable.Today[^1].Score));

        // CMSCOR: the full 36 rows, the factory ones in order and the rest blank.
        Assert.Equal(36, highScoreTable.AllTime.Count);
        Assert.Equal(("VID", 122145), (highScoreTable.AllTime[0].GetDisplayName(), highScoreTable.AllTime[0].Score));
        Assert.Equal(("CNS", 12755), (highScoreTable.AllTime[21].GetDisplayName(), highScoreTable.AllTime[21].Score));
        Assert.All(highScoreTable.AllTime.Skip(22), entry => Assert.Equal(HighScoreEntry.BlankHighScoreEntry, entry));
    }

    [Fact]
    public void Submit_InsertInOrder_AndDropsTheLowest()
    {
        var highScoreTable = HighScoreTable.CreateWithFactoryScores();

        var result = highScoreTable.Submit(40000, "ABC");

        Assert.False(result.BecomesTop);
        Assert.True(result.EnteredToday);
        Assert.True(result.EnteredAllTime); // the all-time table's tail is blanks, so 40000 beats its 36th

        // It lands after JER (41250) and before KID (31920): highest first, and the
        // today's list keeps exactly its ten rows.
        Assert.Equal(40000, highScoreTable.Today[4].Score);
        Assert.Equal(10, highScoreTable.Today.Count);
    }

    [Fact]
    public void Submit_ABigScore_EntersBothListsWithoutTruncatingThem()
    {
        var highScoreTable = HighScoreTable.CreateWithFactoryScores();

        // 30000 beats today's lowest (CJM 24110) AND the all-time table's blank tail.
        var result = highScoreTable.Submit(30000, "ABC");

        Assert.True(result.EnteredToday);
        Assert.True(result.EnteredAllTime);
        Assert.Equal(10, highScoreTable.Today.Count);
        Assert.Equal(36, highScoreTable.AllTime.Count);
        Assert.Contains(highScoreTable.Today, e => e.Score == 30000);
        Assert.Contains(highScoreTable.AllTime, e => e.Score == 30000);

        // A score that beats only the blanks enters the all-time list alone.
        Assert.True(highScoreTable.Submit(20000, "ABC").EnteredAllTime);
    }

    [Fact]
    public void Submit_BeatingTheTop_BecomesTheTopAndPushesTheOldOneDown()
    {
        var highScoreTable = HighScoreTable.CreateWithFactoryScores();

        var result = highScoreTable.Submit(200000, "ACE");

        Assert.True(result.BecomesTop);
        Assert.Equal(200000, highScoreTable.TopScoreEntry.Score);
        Assert.Equal("ACE", highScoreTable.TopScoreEntry.Name);

        // The old "GOD" entry drops into the all-time list, and the list stays full.
        Assert.Equal(("WIL", 151782), (highScoreTable.AllTime[0].GetDisplayName(), highScoreTable.AllTime[0].Score));
        Assert.Equal(36, highScoreTable.AllTime.Count);
    }

    [Fact]
    public void Submit_AScoreThatBeatsNothing_ChangesNothing()
    {
        var highScoreTable = HighScoreTable.CreateWithFactoryScores();

        // Zero beats nothing, not even the blank rows the full table ends with.
        var result = highScoreTable.Submit(0, "ABC");

        Assert.False(result.BecomesTop);
        Assert.False(result.EnteredToday);
        Assert.False(result.EnteredAllTime);
    }

    [Fact]
    public void Qualifies_IsTheRomsTodchkAndAllchk()
    {
        var highScoreTable = HighScoreTable.CreateWithFactoryScores();

        // Zero beats nothing: TODAY's lowest is CJM 24110 and the all-time tail is blank (NULSCR).
        Assert.False(highScoreTable.Qualifies(0));
        Assert.False(highScoreTable.QualifiesForToday(HighScoreTable.FactoryToday[^1].Score));
        Assert.True(highScoreTable.Qualifies(25000));
        Assert.True(highScoreTable.QualifiesForToday(25000));
        Assert.True(highScoreTable.QualifiesForAllTime(25000));

        // Beating the top entry counts as qualifying on its own (GODCHK).
        Assert.True(highScoreTable.QualifiesForAllTime(highScoreTable.TopScoreEntry.Score + 1));
    }

    [Fact]
    public void Submit_CapsHowManyAllTimeEntriesShareOneSetOfInitials()
    {
        var highScoreTable = HighScoreTable.CreateWithFactoryScores();

        // The all-time tail is blank, so five "ABC" scores fit and the maximum never applies.
        for (var i = 0; i < HighScoreTable.MaxAllTimeInitials; i++)
        {
            var entered = highScoreTable.Submit(26000 + i, "ABC");
            Assert.True(entered.EnteredAllTime);
            Assert.False(entered.ReachedEntriesMaximum);
        }

        // A sixth that cannot beat the lowest of the full set is turned away, and the page says why (SETBOT/GETHM4).
        var turnedAway = highScoreTable.Submit(25999, "ABC");

        Assert.True(turnedAway.ReachedEntriesMaximum);
        Assert.False(turnedAway.EnteredAllTime);
        Assert.DoesNotContain(highScoreTable.AllTime, e => e.Score == 25999);

        // One that does beat it replaces it — the set stays five strong.
        var replaced = highScoreTable.Submit(26100, "ABC");

        Assert.True(replaced.ReachedEntriesMaximum);
        Assert.True(replaced.EnteredAllTime);
        Assert.Equal(HighScoreTable.MaxAllTimeInitials, highScoreTable.AllTime.Count(e => e.Initials == "ABC"));
        Assert.DoesNotContain(highScoreTable.AllTime, e => e.Score == 26000);
    }

    [Fact]
    public void Store_RoundTripsTheAllTimeListAndTheTop_AndSeedsTodayFromTheRom()
    {
        var path = Path.Combine(Path.GetTempPath(), $"robotron-hs-{Guid.NewGuid():N}.json");
        try
        {
            var saved = HighScoreTable.CreateWithFactoryScores();
            saved.Submit(200000, "ACE"); // a new top (and the old top drops into the list)
            saved.Submit(12345, "ABC"); // today's only
            HighScoreStore.Save(path, saved);

            var loaded = HighScoreStore.Load(path);

            Assert.Equal(200000, loaded.TopScoreEntry.Score);
            Assert.Contains(loaded.AllTime, e => e.Score == 151782);

            // TODAY'S is not saved: the ROM reloads it from TODTAB at power-up (CKHS).
            Assert.Equal(HighScoreTable.FactoryToday.Select(e => (e.GetDisplayName(), e.Score)),
                loaded.Today.Select(e => (e.GetDisplayName(), e.Score)));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Store_WithNoFile_YieldsTheFactoryTable()
    {
        var missing = Path.Combine(Path.GetTempPath(), $"robotron-hs-{Guid.NewGuid():N}.json");
        Assert.Equal(151782, HighScoreStore.Load(missing).TopScoreEntry.Score);
    }

    [Fact]
    public void Store_WithAnUnreadableJsonFile_ThrowsNamingTheFile()
    {
        var broken = Path.Combine(Path.GetTempPath(), $"robotron-hs-{Guid.NewGuid():N}.json");
        File.WriteAllText(broken, "{ not json at all");
        try
        {
            var exception = Assert.Throws<PersistenceException>(() => HighScoreStore.Load(broken));

            Assert.Contains(broken, exception.Message); // the user must be told WHICH file failed
        }
        finally
        {
            File.Delete(broken);
        }
    }
}
