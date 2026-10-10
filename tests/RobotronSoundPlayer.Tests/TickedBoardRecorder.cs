using Robotron2084.Audio;
using Robotron2084.Audio.Synthesis;

namespace RobotronSoundPlayer.Tests;

/// <summary>
///     Stands where the game's audio sink stands: each port tick it runs a board for one tick's worth of
///     cycles, carrying any overrun into the next tick as the renderer does, and records what the board held.
/// </summary>
/// <param name="board">The board.</param>
internal sealed class TickedBoardRecorder(ISoundBoard board) : IAudioSink
{
    /// <summary>The game's fixed update rate: port ticks a second.</summary>
    private const double PortTicksPerSecond = 60;

    /// <summary>Clock cycles in one port tick.</summary>
    private const double CyclesPerPortTick = SoundBoard.ClockHertz / PortTicksPerSecond;

    private double _overrunCycles;

    /// <summary>Each level the board held, and for how long, in order.</summary>
    public List<LevelRun> Runs { get; } = [];

    /// <summary>Sends a sound number to the board; where it is heard does not matter here.</summary>
    /// <param name="soundNumber">The sound number.</param>
    /// <param name="pan">Not used.</param>
    public void SendSoundNumber(int soundNumber, float pan)
    {
        board.SendSoundNumber(soundNumber);
    }

    /// <summary>Runs the board for one port tick and records it.</summary>
    public void Tick()
    {
        while (_overrunCycles < CyclesPerPortTick)
        {
            var level = board.OutputLevel;
            var ran = board.Run((int)Math.Ceiling(CyclesPerPortTick - _overrunCycles));
            _overrunCycles += ran;
            Add(level, ran);
        }

        _overrunCycles -= CyclesPerPortTick;
    }

    /// <summary>Adds time at a level, joining it to the last run when the level is the same.</summary>
    private void Add(byte level, int cycles)
    {
        if (Runs.Count > 0 && Runs[^1].Level == level)
        {
            Runs[^1] = Runs[^1] with { Cycles = Runs[^1].Cycles + cycles };
            return;
        }

        Runs.Add(new LevelRun(level, cycles));
    }
}
