namespace Robotron2084.Audio;

/// <summary>
///     One line of a sound table: send this sound number to the sound board this many times, waiting
///     this long each time.
/// </summary>
/// <remarks>
///     <list type="bullet">
///         <item>
///             Original source: <c>RRS22.ASM</c>, the table format above routine <c>SNDLDV</c>:
///             <c>REPCNT,SNDTMR(16MSEC),SND#</c>.
///         </item>
///         <item>Disassembly: <c>asm/robomame.asm</c> at <c>$D3E0</c> (the sequencer that reads these lines).</item>
///     </list>
///     A table ends at the first line whose repeat count is 0; the port stores tables already trimmed.
/// </remarks>
/// <param name="Repetitions">How many times the sound number is sent (<c>REPCNT</c>).</param>
/// <param name="LengthVblanks">How many vblanks each send lasts before the next (<c>SNDTMR</c>).</param>
/// <param name="SoundNumber">Which sound the sound board plays (<c>SND#</c>).</param>
public readonly record struct SoundEntry(byte Repetitions, byte LengthVblanks, byte SoundNumber);
