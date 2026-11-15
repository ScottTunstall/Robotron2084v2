using Robotron2084.Audio;
using Robotron2084.Level.Models;
using static Robotron2084.Audio.Models.BerzerkWord;

namespace Robotron2084.Level;

/// <summary>
///     What the Berzerk robot says as a wave starts: "INTRUDER ALERT INTRUDER ALERT", at the start of the game's first wave
///     with <see cref="WaveParams.EnableBerzerkSpeak" /> on, and never again in that game. Port only.
/// </summary>
/// <remarks>
///     Saying it only puts the words in the speech queue. The words were loaded when the game started, and they play on
///     the sound system's own thread, so speaking costs the game's frame almost nothing.
/// </remarks>
public static class WaveStartSpeech
{
    /// <summary>Says "INTRUDER ALERT INTRUDER ALERT" if this wave is the game's first with Berzerk speech on.</summary>
    /// <param name="gameSession">The game, which remembers whether it has been said.</param>
    /// <param name="waveParams">The wave that is starting.</param>
    public static void Say(GameSession gameSession, WaveParams waveParams)
    {
        if (gameSession.HasSaidIntruderAlert || !waveParams.EnableBerzerkSpeak) return;

        gameSession.HasSaidIntruderAlert = true;
        Speech.SayBerzerk(Intruder, Alert, Pause, Intruder, Alert);
    }
}
