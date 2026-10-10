namespace RobotronSoundPlayer.Tests;

/// <summary>Lines two recordings up in time and measures where their levels differ.</summary>
internal static class LevelComparison
{
    /// <summary>Compares two recordings over the time both cover.</summary>
    /// <param name="expected">One board's runs.</param>
    /// <param name="actual">The other board's runs.</param>
    /// <returns>How much, and for how long at most, the levels differed.</returns>
    public static LevelDisagreement Compare(List<LevelRun> expected, List<LevelRun> actual)
    {
        var expectedEnds = EndsOf(expected);
        var actualEnds = EndsOf(actual);
        var end = Math.Min(expectedEnds[^1], actualEnds[^1]);
        long[] moments =
            [.. expectedEnds.Concat(actualEnds).Where(time => time < end).Append(0).Append(end).Distinct().Order()];
        long disagreeing = 0;
        long longest = 0;
        for (var i = 0; i + 1 < moments.Length; i++)
        {
            var length = moments[i + 1] - moments[i];
            if (LevelAt(expected, expectedEnds, moments[i]) != LevelAt(actual, actualEnds, moments[i]))
            {
                disagreeing += length;
                longest = Math.Max(longest, length);
            }
        }

        return new LevelDisagreement(disagreeing, end, longest);
    }

    /// <summary>The moment each run ends, counted from the start of the recording.</summary>
    private static long[] EndsOf(List<LevelRun> runs)
    {
        var ends = new long[runs.Count];
        long time = 0;
        for (var i = 0; i < runs.Count; i++)
        {
            time += runs[i].Cycles;
            ends[i] = time;
        }

        return ends;
    }

    /// <summary>The level a recording held at a moment.</summary>
    private static byte LevelAt(List<LevelRun> runs, long[] ends, long moment)
    {
        var found = Array.BinarySearch(ends, moment);
        return runs[found >= 0 ? found + 1 : ~found].Level;
    }
}
