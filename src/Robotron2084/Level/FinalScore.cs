namespace Robotron2084.Level;

/// <summary>One player's score at the end of a game, with the player's number. It is what the game offers to the high score table.</summary>
/// <remarks>
/// <list type="bullet">
/// <item>Original source: <c>RRTESTC.ASM</c> <c>ENDGAM</c>, which reads
/// <c>ZP1SCR</c> and <c>ZP2SCR</c> and passes the number in <c>CURPLR</c></item>
/// <item>Disassembly: not separately labelled</item>
/// </list> The arcade offers player 1 and then player 2.
/// The port offers the highest score first (notes §98).</item>
/// </list>
/// </remarks>
/// <param name="PlayerNumber">1 or 2. It is printed after "PLAYER " on the initials screen.</param>
/// <param name="Score">The player's score when the game ended.</param>
public readonly record struct FinalScore(int PlayerNumber, int Score);
