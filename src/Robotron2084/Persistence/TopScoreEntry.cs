namespace Robotron2084.Persistence;

/// <summary>
/// The arcade's "GOD" entry (notes §98.2): the operator's own top score and the
/// name that goes with it (<c>GODSCR</c>/<c>GODINT</c> — up to <see
/// cref="HighScoreTable.TopNameLength"/> characters, entered from the service
/// mode's "ENTER GODS NAME"). The port seeds it with the ROM's factory value
/// ("WILLY ELKTRIX", 151782 — RRTESTC's <c>DEFHSR</c>/<c>DEFGOD</c>) because the
/// operator side is deliberately out of scope (D-019).
/// </summary>
public sealed record TopScoreEntry(string Name, int Score);
