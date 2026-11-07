using Microsoft.Xna.Framework.Audio;
using Robotron2084.Tuning;

namespace Robotron2084.Audio;

/// <summary>Speaks lines from WAV files, on its own <see cref="SoundEffectInstance" />, apart from the sound board.</summary>
/// <remarks>
///     <list type="bullet">
///         <item>Port only: the arcade has no speech sound chip, so this is not in the arcade game.</item>
///         <item>
///             A line is a WAV file in the <see cref="SpeechFolderName" /> folder beside the game. Its name is the file's
///             name without the extension, in capitals. The files are 16-bit PCM.
///         </item>
///         <item>A line that has no file is skipped: the voice does not speak and is free for the next line.</item>
///         <item>The voice is not played through the sound board, so nothing sent to the board can stop it.</item>
///     </list>
/// </remarks>
public sealed class SoundEffectSpeechVoice : ISpeechVoice, IDisposable
{
    /// <summary>The name of the folder, beside the game, that holds the WAV files.</summary>
    public const string SpeechFolderName = "Speech";

    private readonly Dictionary<string, SoundEffect> _soundEffectsByLineName = new();
    private SoundEffectInstance? _soundEffectInstance;

    /// <summary>Loads every WAV file in the speech folder.</summary>
    /// <param name="speechFolderPath">The full path of the folder. A folder that does not exist holds no lines.</param>
    /// <exception cref="NoAudioHardwareException">The computer has no sound output.</exception>
    public SoundEffectSpeechVoice(string speechFolderPath)
    {
        if (!Directory.Exists(speechFolderPath)) return;

        foreach (var wavPath in Directory.GetFiles(speechFolderPath, "*.wav"))
        {
            var lineName = Path.GetFileNameWithoutExtension(wavPath).ToUpperInvariant();
            using var wavStream = File.OpenRead(wavPath);
            _soundEffectsByLineName[lineName] = SoundEffect.FromStream(wavStream);
        }
    }

    /// <summary>Stops the voice and frees the lines.</summary>
    public void Dispose()
    {
        _soundEffectInstance?.Dispose();
        foreach (var soundEffect in _soundEffectsByLineName.Values) soundEffect.Dispose();
    }

    /// <inheritdoc />
    public bool IsSpeaking()
    {
        return _soundEffectInstance?.State == SoundState.Playing;
    }

    /// <inheritdoc />
    public void Speak(string lineName)
    {
        var hasLine = _soundEffectsByLineName.TryGetValue(lineName.ToUpperInvariant(), out var soundEffect);
        if (!hasLine || soundEffect is null) return;

        _soundEffectInstance?.Dispose();
        _soundEffectInstance = soundEffect.CreateInstance();
        _soundEffectInstance.Volume = SoundTuning.SpeechVolume;
        _soundEffectInstance.Play();
    }
}
