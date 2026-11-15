namespace Robotron2084.Audio.Models;

/// <summary>
///     A decoded ROM sound: the request <see cref="Priority" /> (the byte the ROM
///     call sites point at) and the (dur, len, note) entries that follow it.
///     <see cref="RomTableAddress" /> is the table address in
///     <c>ref/rom/robotron64k.bin</c>, for provenance.
/// </summary>
/// <param name="Priority">The request's priority: a sound can only be cut off by one whose priority is as high or higher.</param>
/// <param name="RomTableAddress">Where the table starts in the sound ROM image.</param>
/// <param name="Entries">The rows of the table, in the order they are played.</param>
public sealed record SoundSequence(int Priority, int RomTableAddress, SoundTableRow[] Entries);
