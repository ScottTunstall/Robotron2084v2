using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Robotron2084.Tuning;

namespace Robotron2084.Audio;

/// <summary>Speaks lines from the content pipeline, on its own <see cref="SoundEffectInstance" />, apart from the sound board.</summary>
/// <remarks>
///     <list type="bullet">
///         <item>Port only: the arcade has no speech sound chip, so this is not in the arcade game.</item>
///         <item>
///             A line is a sound file in a folder of <c>Content/Speech/</c> (GORF or Berzerk). The pipeline builds it, and
///             <see cref="SpeechLinePaths" /> lists them. A line's name is the file's name without the extension, in
///             capitals.
///         </item>
///         <item>A line that has no file is skipped: the voice does not speak and is free for the next line.</item>
///         <item>The voice is not played through the sound board, so nothing sent to the board can stop it.</item>
///     </list>
/// </remarks>
public sealed class SoundEffectSpeechVoice : ISpeechVoice, IDisposable
{
    private readonly Dictionary<string, SoundEffect> _soundEffectsByLineName = new();
    private SoundEffectInstance? _soundEffectInstance;

    /// <summary>Loads every speech line the pipeline built.</summary>
    /// <param name="contentManager">The content manager that loads the built files.</param>
    /// <exception cref="NoAudioHardwareException">The computer has no sound output.</exception>
    public SoundEffectSpeechVoice(ContentManager contentManager)
    {
        foreach (var linePath in SpeechLinePaths.All)
        {
            var lineName = Path.GetFileName(linePath).ToUpperInvariant();
            _soundEffectsByLineName[lineName] = contentManager.Load<SoundEffect>(linePath);
        }
    }

    /// <summary>Stops the voice. The content manager frees the lines.</summary>
    public void Dispose()
    {
        _soundEffectInstance?.Dispose();
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
