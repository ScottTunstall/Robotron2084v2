namespace Robotron2084.Audio;

/// <summary>
/// One entry of a ROM sound table (notes §36.2, R5 $D3E0): play
/// <see cref="Note"/> <see cref="Dur"/> times, each repetition sounding for
/// <see cref="Len"/> vblanks. A table ends at the first entry whose
/// <see cref="Dur"/> is 0 (the port passes pre-trimmed tables).
/// </summary>
public readonly record struct SoundEntry(byte Dur, byte Len, byte Note);
