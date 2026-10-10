namespace Robotron2084.States;

/// <summary>One thing the sound test page can play: a line of its list.</summary>
/// <remarks>
/// Every entry but the last is one of the board's sounds (<see cref="Audio.BoardSounds"/>), played by sending its
/// number. The last is the transporter, which is not one sound number but a routine on the main board that sends
/// a number over and over, so it has no number of its own.
/// </remarks>
/// <param name="Heading">The page's first line, in the large font: "SOUND" and the number, or the routine's name.</param>
/// <param name="Name">The name the source gives it (<c>HBDV</c>, <c>TRSPRC</c>).</param>
/// <param name="Description">The words shown under the name.</param>
/// <param name="SoundNumber">The sound number to send, or null for the transporter.</param>
internal sealed record SoundTestEntry(string Heading, string Name, string Description, int? SoundNumber);
