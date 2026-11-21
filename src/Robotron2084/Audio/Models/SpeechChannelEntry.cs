namespace Robotron2084.Audio.Models;

/// <summary>One thing waiting on the speech channel: a line to speak, or a silence to wait out.</summary>
/// <param name="LineName">The name of the line to speak, or null for a silence.</param>
/// <param name="PitchOctaves">How far to raise or lower the line, in octaves (<see cref="ISpeechVoice.Speak" />).</param>
/// <param name="VolumeShare">How loud the line is, as a share of the speech's volume (<see cref="ISpeechVoice.Speak" />).</param>
/// <param name="SilenceTicks">
///     For a silence, how long it lasts, in <see cref="Robotron2084.Core.ArcadeClock.PortTicksPerSecond">ticks</see>.
/// </param>
public readonly record struct SpeechChannelEntry(string? LineName, float PitchOctaves, float VolumeShare, int SilenceTicks);
