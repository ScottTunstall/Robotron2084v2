using Robotron2084.Audio;

namespace RobotronSoundPlayer;

/// <summary>One of the game's own sounds: a table from <see cref="SoundTables"/>, played through the sequencer as the game plays it.</summary>
/// <param name="name">The table's name in <see cref="SoundTables"/>.</param>
/// <param name="sequence">The table.</param>
internal sealed class SoundTableRequest(string name, SoundSequence sequence) : ISoundRequest
{
    /// <summary>The table's name in <see cref="SoundTables"/>.</summary>
    public string Name => name;

    /// <summary>The sound numbers the table sends, and its priority.</summary>
    public string Description
    {
        get
        {
            IEnumerable<string> sends = sequence.Entries.Select(entry => $"${entry.SoundNumber:X2}");
            return $"sends {string.Join(" ", sends)}, priority ${sequence.Priority:X2}";
        }
    }

    /// <summary>Asks the sequencer for the table, heard in the middle.</summary>
    /// <param name="engine">The sequencer that sends sound numbers to the board.</param>
    public void Start(SoundEngine engine) => engine.Play(sequence, 0f);

    /// <summary>Does nothing: the sequencer moves the table on by itself.</summary>
    /// <param name="engine">The sequencer that sends sound numbers to the board.</param>
    public void Tick(SoundEngine engine)
    {
    }
}
