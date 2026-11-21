namespace Robotron2084.Audio.Models;

/// <summary>How much a line matters: a line that matters more stops one that matters less, so it is heard at once.</summary>
public enum SpeechPriority
{
    /// <summary>A line that waits its turn behind anything already being said, such as a taunt.</summary>
    Normal = 0,

    /// <summary>A line about something important, such as the player being killed. It is not kept waiting behind a normal line.</summary>
    Important = 1
}
