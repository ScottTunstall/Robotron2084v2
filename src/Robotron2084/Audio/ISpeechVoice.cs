namespace Robotron2084.Audio;

/// <summary>One voice that speaks one line at a time, and keeps going until the line is finished.</summary>
public interface ISpeechVoice
{
    /// <summary>Gets whether the voice is still speaking a line.</summary>
    bool IsSpeaking();

    /// <summary>Starts speaking a line. The caller only asks while the voice is not speaking.</summary>
    /// <param name="lineName">The name of the line to speak.</param>
    void Speak(string lineName);
}
