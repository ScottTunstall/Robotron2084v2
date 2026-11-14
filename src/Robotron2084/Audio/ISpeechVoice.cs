namespace Robotron2084.Audio;

/// <summary>One voice that speaks one line at a time, and keeps going until the line is finished.</summary>
public interface ISpeechVoice
{
    /// <summary>Gets whether the voice is still speaking a line.</summary>
    bool IsSpeaking();

    /// <summary>Starts speaking a line, raised or lowered by a number of octaves. The caller only asks while the voice is not speaking.</summary>
    /// <param name="lineName">The name of the line to speak.</param>
    /// <param name="pitchOctaves">How far to raise (more than 0) or lower (less than 0) the line, in octaves, from -1 to 1. It plays faster when raised and slower when lowered.</param>
    void Speak(string lineName, float pitchOctaves);
}
