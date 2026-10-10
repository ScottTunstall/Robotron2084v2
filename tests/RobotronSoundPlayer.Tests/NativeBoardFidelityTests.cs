using Robotron2084.Audio;
using Robotron2084.Audio.Synthesis;
using Robotron2084.Tests;
using RobotronSoundPlayer.Emulation;
using Xunit;

namespace RobotronSoundPlayer.Tests;

/// <summary>
///     The game's own sound board, rebuilt from the sound ROM's source, against the emulated board running the
///     real ROM. The ROM is not in git, so these skip when it is missing (notes §130).
/// </summary>
public class NativeBoardFidelityTests
{
    /// <summary>How long the boards run before the first sound number, as the renderer's warm-up does: a tenth of a second.</summary>
    private const int WarmUpCycles = SoundBoard.ClockHertz / 10;

    /// <summary>How long a single sound number is compared for: four seconds.</summary>
    private const long SoundNumberCycles = SoundBoard.ClockHertz * 4L;

    /// <summary>How long a game sound is compared for, in port ticks: six seconds.</summary>
    private const int GameSoundTicks = 6 * 60;

    /// <summary>
    ///     The longest the two boards may disagree at a stretch: one instruction. The emulated processor
    ///     finishes the instruction it is in before it answers a sound number, while the game's board answers at
    ///     once; the longest instruction the program uses is <c>JSR</c> to a full address.
    /// </summary>
    private const long LongestInstructionCycles = 9;

    /// <summary>
    ///     The most of the time the boards may disagree, from 0 to 1: the noise sounds change level every few dozen
    ///     cycles, so a few cycles' lag shows.
    /// </summary>
    private const double MostDisagreement = 0.03;

    /// <summary>Every sound number the board has (the sound test page's list), which includes every one the game sends.</summary>
    public static TheoryData<int> GameSoundNumbers => [.. BoardSounds.All.Select(sound => sound.Number)];

    /// <summary>Every sound the game plays, by its name in the player's catalog.</summary>
    public static TheoryData<string> GameSoundNames => [.. new SoundCatalog().GameSounds.Select(sound => sound.Name)];

    [Theory]
    [MemberData(nameof(GameSoundNumbers))]
    public void EachSoundNumber_ChangesToTheSameLevels_AtTheSameMoments_AsTheRealRom(int soundNumber)
    {
        var emulated = new EmulatedSoundBoard(RomFiles.ReadOrSkip(RomFiles.SoundRom));
        var native = new SoundBoard();

        var expected = PlayFromCold(emulated, soundNumber);
        var actual = PlayFromCold(native, soundNumber);

        // The last run is cut wherever the recording stops, which an emulated instruction can overrun.
        Assert.Equal(expected.SkipLast(1), actual.SkipLast(1));
    }

    [Theory]
    [MemberData(nameof(GameSoundNames))]
    public void EachGameSound_PlayedThroughTheSequencer_DiffersFromTheRealRom_ByNoMoreThanAnInstruction(string name)
    {
        var rom = RomFiles.ReadOrSkip(RomFiles.SoundRom);

        var expected = PlayThroughSequencer(new EmulatedSoundBoard(rom), name);
        var actual = PlayThroughSequencer(new SoundBoard(), name);
        var disagreement = LevelComparison.Compare(expected, actual);

        Assert.InRange(disagreement.LongestCycles, 0, LongestInstructionCycles);
        Assert.InRange(disagreement.Share, 0, MostDisagreement);
    }

    private static List<LevelRun> PlayFromCold(ISoundBoard board, int soundNumber)
    {
        LevelRecorder.Skip(board, WarmUpCycles);
        board.SendSoundNumber(soundNumber);
        return LevelRecorder.Record(board, SoundNumberCycles);
    }

    private static List<LevelRun> PlayThroughSequencer(ISoundBoard board, string name)
    {
        LevelRecorder.Skip(board, WarmUpCycles);
        var recorder = new TickedBoardRecorder(board);
        var engine = new SoundEngine(recorder);
        var request = new SoundCatalog().Find(name)!;
        request.Start(engine);
        for (var tick = 0; tick < GameSoundTicks; tick++)
        {
            engine.Tick();
            request.Tick(engine);
        }

        return recorder.Runs;
    }
}
