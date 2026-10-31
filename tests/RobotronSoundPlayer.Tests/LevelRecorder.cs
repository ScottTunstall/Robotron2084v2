using Robotron2084.Audio;

namespace RobotronSoundPlayer.Tests;

/// <summary>
///     Runs a sound board and writes down its output as runs of one level, so two boards can be compared change for
///     change.
/// </summary>
internal static class LevelRecorder
{
    /// <summary>Runs a board for a while and records each level it holds and for how long.</summary>
    /// <param name="soundBoard">The board.</param>
    /// <param name="cycles">How long to run it, in clock cycles.</param>
    /// <returns>The runs, in order; two runs in a row never share a level.</returns>
    public static List<LevelRun> Record(ISoundBoard soundBoard, long cycles)
    {
        var runs = new List<LevelRun>();
        for (long elapsed = 0; elapsed < cycles;)
        {
            var level = soundBoard.OutputLevel;
            var ran = soundBoard.Run((int)Math.Min(int.MaxValue, cycles - elapsed));
            elapsed += ran;
            if (runs.Count > 0 && runs[^1].Level == level)
            {
                runs[^1] = runs[^1] with { Cycles = runs[^1].Cycles + ran };
                continue;
            }

            runs.Add(new LevelRun(level, ran));
        }

        return runs;
    }

    /// <summary>Runs a board for a while, throwing away what it plays.</summary>
    /// <param name="soundBoard">The board.</param>
    /// <param name="cycles">How long to run it, in clock cycles.</param>
    public static void Skip(ISoundBoard soundBoard, long cycles)
    {
        for (long elapsed = 0; elapsed < cycles;) elapsed += soundBoard.Run((int)Math.Min(int.MaxValue, cycles - elapsed));
    }
}
