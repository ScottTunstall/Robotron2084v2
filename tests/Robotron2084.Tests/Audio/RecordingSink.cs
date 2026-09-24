using Robotron2084.Audio;
using Xunit;

namespace Robotron2084.Tests.Audio;

/// <summary>
/// A recording sink for sequencer tests (notes §36.2): captures every
/// PlayNote(note, ticks) call in order.
/// </summary>
internal sealed class RecordingSink : IAudioSink
{
    public readonly List<(int Note, int Ticks)> Calls = [];

    public void PlayNote(int note, int ticks) => Calls.Add((note, ticks));
    public void Tick() { }
}
