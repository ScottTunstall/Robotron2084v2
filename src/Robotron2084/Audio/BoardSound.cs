namespace Robotron2084.Audio;

/// <summary>One of the sound board's 63 sounds, as the sound ROM's source names it.</summary>
/// <param name="Number">The sound number the main board sends (the original source's <c>SND#</c>), 1 to 63.</param>
/// <param name="Name">The label the source gives the sound's routine or vector (<c>HBDV</c>, <c>CANNON</c>, ...).</param>
/// <param name="Description">The words the source puts beside the label or the sound's data, or empty when it has none.</param>
public sealed record BoardSound(int Number, string Name, string Description);

