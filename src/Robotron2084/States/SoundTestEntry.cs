namespace Robotron2084.States;

/// <summary>One thing the sound test page can play: a line of its list.</summary>
/// <remarks>
///     <list type="bullet">
///         <item>
///             The first 63 are the board's sounds (<see cref="Audio.BoardSounds" />), played by sending the number.
///         </item>
///         <item>
///             The next is the transporter, which is not one sound number but a routine on the main board that sends a
///             number over and over, so it has no number of its own.
///         </item>
///         <item>
///             The rest are speech lines (port only), spoken by name on the speech voice, which the board never cuts off.
///         </item>
///     </list>
/// </remarks>
/// <param name="Heading">The page's first line, in the large font: "SOUND" and the number, or the routine's name.</param>
/// <param name="Name">The name the source gives it (<c>HBDV</c>, <c>TRSPRC</c>).</param>
/// <param name="Description">The words shown under the name.</param>
/// <param name="SoundNumber">The sound number to send, or null for the transporter and the speech lines.</param>
/// <param name="SpeechLineName">The name of the speech line to speak, or null for anything that is not speech.</param>
internal sealed record SoundTestEntry(
    string Heading,
    string Name,
    string Description,
    int? SoundNumber,
    string? SpeechLineName = null);
