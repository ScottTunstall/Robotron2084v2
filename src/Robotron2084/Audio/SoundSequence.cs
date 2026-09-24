namespace Robotron2084.Audio;

/// <summary>
/// A decoded ROM sound: the request <see cref="Priority"/> (the byte the ROM
/// call sites point at) and the (dur, len, note) entries that follow it.
/// <see cref="RomTableAddress"/> is the table address in
/// <c>ref/rom/robotron64k.bin</c>, for provenance.
/// </summary>
public sealed record SoundSequence(int Priority, int RomTableAddress, SoundEntry[] Entries);
