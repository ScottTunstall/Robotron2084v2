namespace Robotron2084.Audio;

/// <summary>
/// One entry of a ROM sound table (notes §36.2, R5 $D3E0): play
/// <see cref="Note"/> <see cref="Repetitions"/> times, each repetition sounding for
/// <see cref="LengthVblanks"/> vblanks. A table ends at the first entry whose
/// <see cref="Repetitions"/> is 0 (the port passes pre-trimmed tables).
/// </summary>
/// <param name="Repetitions">How many times the note is sounded (the ROM's DUR byte).</param>
/// <param name="LengthVblanks">How many vblanks each repetition lasts (the ROM's LEN byte).</param>
/// <param name="Note">The note's pitch byte.</param>
public readonly record struct SoundEntry(byte Repetitions, byte LengthVblanks, byte Note);
