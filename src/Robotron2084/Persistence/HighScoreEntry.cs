namespace Robotron2084.Persistence;

/// <summary>
/// One entry of the arcade's high score table (notes §98.2). The ROM stores it in
/// FOURTEEN CMOS NIBBLES (<c>SCRSIZ</c>): three name characters plus a check
/// nibble, then four bytes of packed BCD — a seven-digit score. A "no name" entry
/// is the ROM's <c>NULSCR</c>: three spaces and no score.
/// </summary>
/// <param name="Initials">Up to three characters (<see cref="HighScoreTable.InitialsLength"/>).</param>
/// <param name="Score">The score, as the ROM's seven BCD digits.</param>
public sealed record HighScoreEntry(string Initials, int Score)
{
    /// <summary>ROM `NULSCR` — three spaces and 0: what an unused table row holds.</summary>
    public static HighScoreEntry Blank { get; } = new("   ", 0);

    /// <summary>The name, trimmed for display (the ROM walks its text to the first blank).</summary>
    public string DisplayName => Initials.Trim();
}
