using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Robotron2084.Audio.Models;
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
    /// <summary>The start of the path of every Berzerk word.</summary>
    private const string BerzerkFolderStart = "Speech/" + SpeechFolders.Berzerk + "/";

    /// <summary>The extension of a file the content pipeline built.</summary>
    private const string BuiltFileExtension = ".xnb";

    /// <summary>The Berzerk words, which this voice made itself and so must free.</summary>
    private readonly List<SoundEffect> _ownSoundEffects = [];

    private readonly Dictionary<string, SoundEffect> _soundEffectsByLineName = new();
    private SoundEffectInstance? _soundEffectInstance;

    /// <summary>
    ///     Loads every speech line the pipeline built. Each Berzerk word has the silence around it cut off, so the words of
    ///     a sentence follow each other closely, as the arcade's speech chip says them.
    /// </summary>
    /// <param name="contentManager">The content manager that loads the built files.</param>
    /// <exception cref="NoAudioHardwareException">The computer has no sound output.</exception>
    /// <exception cref="InvalidDataException">A Berzerk word's built file cannot be read.</exception>
    public SoundEffectSpeechVoice(ContentManager contentManager)
    {
        foreach (var linePath in SpeechLinePaths.All)
        {
            var lineName = Path.GetFileName(linePath).ToUpperInvariant();
            var isBerzerkWord = linePath.StartsWith(BerzerkFolderStart, StringComparison.Ordinal);
            _soundEffectsByLineName[lineName] = isBerzerkWord
                ? CreateTrimmedSoundEffect(contentManager, linePath)
                : contentManager.Load<SoundEffect>(linePath);
        }
    }

    /// <summary>Stops the voice and frees the Berzerk words. The content manager frees the other lines.</summary>
    public void Dispose()
    {
        _soundEffectInstance?.Dispose();
        foreach (var soundEffect in _ownSoundEffects) soundEffect.Dispose();
    }

    /// <inheritdoc />
    public bool IsSpeaking()
    {
        return _soundEffectInstance?.State == SoundState.Playing;
    }

    /// <inheritdoc />
    public void Speak(string lineName, float pitchOctaves, float volumeShare)
    {
        var hasLine = _soundEffectsByLineName.TryGetValue(lineName.ToUpperInvariant(), out var soundEffect);
        if (!hasLine || soundEffect is null) return;

        _soundEffectInstance?.Dispose();
        _soundEffectInstance = soundEffect.CreateInstance();
        _soundEffectInstance.Volume = SoundTuning.SpeechVolume * volumeShare;
        _soundEffectInstance.Pitch = pitchOctaves;
        _soundEffectInstance.Play();
    }

    /// <inheritdoc />
    public void Stop()
    {
        _soundEffectInstance?.Stop();
    }

    /// <summary>Makes a line from its built file, with the silence around it cut off.</summary>
    /// <param name="contentManager">The content manager, whose folder holds the built files.</param>
    /// <param name="linePath">The line's path in that folder, without the extension.</param>
    /// <returns>The line, which this voice frees.</returns>
    /// <exception cref="InvalidDataException">The built file cannot be read.</exception>
    private SoundEffect CreateTrimmedSoundEffect(ContentManager contentManager, string linePath)
    {
        PcmSound pcmSound;
        using (var stream = TitleContainer.OpenStream(Path.Combine(contentManager.RootDirectory, linePath + BuiltFileExtension)))
        {
            pcmSound = XnbSoundEffectReader.Read(stream)
                .GetTrimmed(SoundTuning.BerzerkWordSilenceLevel, SoundTuning.BerzerkWordSilenceMarginMilliseconds);
        }

        SoundEffect soundEffect = new(pcmSound.Samples, pcmSound.SampleRate, pcmSound.Channels);
        _ownSoundEffects.Add(soundEffect);
        return soundEffect;
    }
}
