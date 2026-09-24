namespace Robotron2084.Level;

/// <summary>One player's finished-game score: the value the end-game ceremony offers to the high score table, with the player number the initials screen prints.</summary>
/// <remarks>ROM: RRTESTC's <c>ENDGAM</c> reads <c>ZP1SCR</c>/<c>ZP2SCR</c> and passes the number in <c>CURPLR</c>. The ROM offers player 1 and then player 2; the port offers the highest score first (notes §98).</remarks>
/// <param name="PlayerNumber">1 or 2 — the ROM's <c>CURPLR</c>, printed after "PLAYER ".</param>
/// <param name="Score">The player's final score (ROM <c>ZP1SCR</c>/<c>ZP2SCR</c>).</param>
public readonly record struct FinalScore(int PlayerNumber, int Score);
