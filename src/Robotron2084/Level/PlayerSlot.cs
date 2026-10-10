using Robotron2084.Input;
using Robotron2084.Tuning;

namespace Robotron2084.Level;

/// <summary>Everything the game remembers about one player between waves: their score, their men, their wave, and what was left on their field when they died.</summary>
/// <remarks>
/// <list type="bullet">
/// <item>Original source: <c>RRG23.ASM</c> <c>PLDATA</c> (player 1's block) and <c>ZP2SCR</c> (player 2's), chosen by <c>PLINDX</c></item>
/// <item>Disassembly: not separately labelled</item>
/// </list>
/// In a two-player game each player keeps their own score, men and wave while the other plays. When a man is lost the turn passes to the other
/// player, unless they have no men left, and they carry on from their own wave (<c>RRG23.ASM</c> <c>PLE1B</c>).
/// </remarks>
/// <param name="Number">1 or 2. It is printed after "PLAYER " when two people play.</param>
/// <param name="Input">This player's controls. Each player has their own, so a second player need not share the first player's.</param>
/// <param name="Lives">The men the player has, counting the one in play.</param>
/// <param name="Wave">The wave the player is on. Only they move it on.</param>
public sealed record PlayerSlot(int Number, IPlayerInputSource Input, int Lives, int Wave)
{
    /// <summary>The wave the player is on. Only they move it on.</summary>
    /// <remarks>Original source: <c>RRG23.ASM</c> <c>PWAV</c>. Disassembly: not separately labelled.</remarks>
    public int Wave { get; set; } = Wave;

    /// <summary>The player's score.</summary>
    /// <remarks>Original source: <c>RRF.ASM</c> <c>ZP1SCR</c> and <c>ZP2SCR</c>. Disassembly: <c>p1_score</c> and <c>p2_score</c>.</remarks>
    public int Score { get; set; }

    /// <summary>The men the player has, counting the one in play.</summary>
    /// <remarks>Original source: <c>RRG23.ASM</c> <c>PLAS</c>. Disassembly: not separately labelled.</remarks>
    public int Lives { get; set; } = Lives;

    /// <summary>What was left on the player's field when they last died, to start their next man with. It is null when their wave has not started or has just been cleared.</summary>
    /// <remarks>Original source: <c>RRG23.ASM</c> <c>PENEMY</c>, the list that <c>PLSAV</c> keeps and <c>PLRES</c> brings back. Disassembly: not separately labelled.</remarks>
    public LevelParameters? SavedWaveParameters { get; set; }

    /// <summary>True while the player can still be given a turn.</summary>
    public bool HasMen() => Lives > 0;

    /// <summary>The spare men: the men the player has besides the one in play. With three men, the player sees two spare ones during their first.</summary>
    /// <remarks>Original source: <c>RRG23.ASM</c>, the count that <c>MANDSV</c> draws. Disassembly: <c>p1_men</c> and <c>p2_men</c>.</remarks>
    public int GetSpareMen() => System.Math.Max(0, Lives - 1);

    /// <summary>How many little men are drawn on the screen: the spare men, though never more than the screen has room for.</summary>
    /// <remarks>Original source: <c>RRG23.ASM</c> <c>MANDSV</c>, which stops at <c>#$07</c>. Disassembly: not separately labelled.</remarks>
    public int GetDisplayedMen() => System.Math.Min(GetSpareMen(), HudLayout.HudMaxMen);
}
