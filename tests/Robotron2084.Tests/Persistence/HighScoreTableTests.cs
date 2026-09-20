using Robotron2084.Persistence;
using Xunit;

namespace Robotron2084.Tests.Persistence;

/// <summary>
/// The arcade's high score table (notes §98) — RRTESTC's CMOS lists and its own
/// factory tables, and the ENDGAM score rules (`GODCHK` → `TODCHK`/`ALLCHK`).
/// </summary>
public sealed class HighScoreTableTests
{
    [Fact]
    public void FactoryTable_IsTheRomsOwnDefaultTable()
    {
        HighScoreTable table = HighScoreTable.CreateFactory();

        // DEFHSR/DEFGOD: the operator's top entry is "WILLY ELKTRIX" at 151782.
        Assert.Equal("WILLY ELKTRIX", table.Top.Name);
        Assert.Equal(151782, table.Top.Score);

        // TODTAB: ten entries, highest first, and exactly the ROM's names/scores.
        Assert.Equal(10, table.Today.Count);
        Assert.Equal([("DRJ", 52127), ("LED", 50218), ("EPJ", 41255)], table.Today.Take(3).Select(e => (e.DisplayName, e.Score)));
        Assert.Equal(("CJM", 24110), (table.Today[^1].DisplayName, table.Today[^1].Score));

        // CMSCOR: the full 36 rows, the factory ones in order and the rest blank.
        Assert.Equal(36, table.AllTime.Count);
        Assert.Equal(("VID", 122145), (table.AllTime[0].DisplayName, table.AllTime[0].Score));
        Assert.Equal(("CNS", 12755), (table.AllTime[21].DisplayName, table.AllTime[21].Score));
        Assert.All(table.AllTime.Skip(22), entry => Assert.Equal(HighScoreEntry.Blank, entry));
    }

    [Fact]
    public void Submit_InsertInOrder_AndDropsTheLowest()
    {
        HighScoreTable table = HighScoreTable.CreateFactory();

        HighScoreTable.SubmitResult result = table.Submit(40000);

        Assert.False(result.BecomesTop);
        Assert.True(result.EnteredToday);
        Assert.True(result.EnteredAllTime); // the all-time table's tail is blanks, so 40000 beats its 36th

        // It lands after JER (41250) and before KID (31920): highest first, and the
        // today's list keeps exactly its ten rows.
        Assert.Equal(40000, table.Today[4].Score);
        Assert.Equal(10, table.Today.Count);
    }

    [Fact]
    public void Submit_ABigScore_EntersBothListsWithoutTruncatingThem()
    {
        HighScoreTable table = HighScoreTable.CreateFactory();

        // 30000 beats today's lowest (CJM 24110) AND the all-time table's blank tail.
        HighScoreTable.SubmitResult result = table.Submit(30000);

        Assert.True(result.EnteredToday);
        Assert.True(result.EnteredAllTime);
        Assert.Equal(10, table.Today.Count);
        Assert.Equal(36, table.AllTime.Count);
        Assert.Contains(table.Today, e => e.Score == 30000);
        Assert.Contains(table.AllTime, e => e.Score == 30000);

        // A score that beats only the blanks enters the all-time list alone.
        Assert.True(table.Submit(20000).EnteredAllTime);
    }

    [Fact]
    public void Submit_BeatingTheTop_BecomesTheTopAndPushesTheOldOneDown()
    {
        HighScoreTable table = HighScoreTable.CreateFactory();

        HighScoreTable.SubmitResult result = table.Submit(200000);

        Assert.True(result.BecomesTop);
        Assert.Equal(200000, table.Top.Score);
        Assert.Equal(new string(' ', HighScoreTable.InitialsLength), table.Top.Name); // no name entry yet (notes §98.4)

        // The old "GOD" entry drops into the all-time list, and the list stays full.
        Assert.Equal(("WIL", 151782), (table.AllTime[0].DisplayName, table.AllTime[0].Score));
        Assert.Equal(36, table.AllTime.Count);
    }

    [Fact]
    public void Submit_AScoreThatBeatsNothing_ChangesNothing()
    {
        HighScoreTable table = HighScoreTable.CreateFactory();

        // Zero beats nothing, not even the blank rows the full table ends with.
        HighScoreTable.SubmitResult result = table.Submit(0);

        Assert.False(result.BecomesTop);
        Assert.False(result.EnteredToday);
        Assert.False(result.EnteredAllTime);
    }

    [Fact]
    public void Store_RoundTripsTheAllTimeListAndTheTop_AndSeedsTodayFromTheRom()
    {
        string path = Path.Combine(Path.GetTempPath(), $"robotron-hs-{Guid.NewGuid():N}.json");
        try
        {
            HighScoreTable saved = HighScoreTable.CreateFactory();
            saved.Submit(200000);           // a new top (and the old top drops into the list)
            saved.Submit(12345);            // today's only
            HighScoreStore.Save(path, saved);

            HighScoreTable loaded = HighScoreStore.Load(path);

            Assert.Equal(200000, loaded.Top.Score);
            Assert.Contains(loaded.AllTime, e => e.Score == 151782);

            // TODAY'S is not saved: the ROM reloads it from TODTAB at power-up (CKHS).
            Assert.Equal(HighScoreTable.FactoryToday.Select(e => (e.DisplayName, e.Score)),
                loaded.Today.Select(e => (e.DisplayName, e.Score)));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Store_WithNoFileOrABrokenOne_YieldsTheFactoryTable()
    {
        string missing = Path.Combine(Path.GetTempPath(), $"robotron-hs-{Guid.NewGuid():N}.json");
        Assert.Equal(151782, HighScoreStore.Load(missing).Top.Score);

        string broken = Path.Combine(Path.GetTempPath(), $"robotron-hs-{Guid.NewGuid():N}.json");
        File.WriteAllText(broken, "{ not json at all");
        try
        {
            Assert.Equal(HighScoreTable.CreateFactory().AllTime, HighScoreStore.Load(broken).AllTime);
        }
        finally
        {
            File.Delete(broken);
        }
    }
}
