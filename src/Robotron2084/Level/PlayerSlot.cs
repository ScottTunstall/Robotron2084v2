using Robotron2084.Input;
using Robotron2084.Tuning;

namespace Robotron2084.Level;

/// <summary>
///     Everything the game remembers about one player between waves: their score, their lives, their wave, and what was
///     left on their field when they died.
/// </summary>
/// <remarks>
///     <list type="bullet">
///         <item>
///             Original source: <c>RRG23.ASM</c> <c>PLDATA</c> (player 1's block) and
///             <c>ZP2SCR</c> (player 2's), chosen by <c>PLINDX</c>
///         </item>
///         <item>Disassembly: not separately labelled</item>
///     </list>
///     In a two-player game each player keeps their
///     own score, lives and wave while the other plays. When a life is lost the turn passes to the other
///     player, unless they have no lives left, and they carry on from their own wave (<c>RRG23.ASM</c>
///     <c>PLE1B</c>).</item>
///     </list>
/// </remarks>
/// <param name="Number">1 or 2. It is printed after "PLAYER " when two people play.</param>
/// <param name="PlayerInputSource">
///     This player's controls. Each player has their own, so a second player need not share the first
///     player's.
/// </param>
/// <param name="Lives">The lives the player has, counting the one in play.</param>
/// <param name="Wave">The wave the player is on. Only they move it on.</param>
public sealed record PlayerSlot(int Number, IPlayerInputSource PlayerInputSource, int Lives, int Wave)
{
    /// <summary>The wave the player is on. Only they move it on.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRG23.ASM</c> <c>PWAV</c>.</item>
    ///         <item>Disassembly: not separately labelled.</item>
    ///     </list>
    /// </remarks>
    public int Wave { get; set; } = Wave;

    /// <summary>The player's score.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRF.ASM</c> <c>ZP1SCR</c> and <c>ZP2SCR</c>.</item>
    ///         <item>Disassembly: <c>p1_score</c> and <c>p2_score</c>.</item>
    ///     </list>
    /// </remarks>
    public int Score { get; set; }

    /// <summary>The lives the player has, counting the one in play.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRG23.ASM</c> <c>PLAS</c>.</item>
    ///         <item>Disassembly: not separately labelled.</item>
    ///     </list>
    /// </remarks>
    public int Lives { get; set; } = Lives;

    /// <summary>
    ///     What was left on the player's field when they last died, to start their next life with. It is null when their
    ///     wave has not started or has just been cleared.
    /// </summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>
    ///             Original source: <c>RRG23.ASM</c> <c>PENEMY</c>, the list that <c>PLSAV</c> keeps and
    ///             <c>PLRES</c> brings back.
    ///         </item>
    ///         <item>Disassembly: not separately labelled.</item>
    ///     </list>
    /// </remarks>
    public WaveParameters? SavedWaveParameters { get; set; }

    /// <summary>True while the player can still be given a turn.</summary>
    public bool HasLives()
    {
        return Lives > 0;
    }

    /// <summary>
    ///     The spare lives: the lives the player has besides the one in play. With three lives, the player sees two spare ones
    ///     during their first.
    /// </summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRG23.ASM</c>, the count that <c>MANDSV</c> draws.</item>
    ///         <item>Disassembly: <c>p1_men</c> and <c>p2_men</c>.</item>
    ///     </list>
    /// </remarks>
    public int GetSpareLives()
    {
        return Math.Max(0, Lives - 1);
    }

    /// <summary>How many spare-life icons are drawn on the screen: the smaller of the spare lives and the room there is for icons.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRG23.ASM</c> <c>MANDSV</c>, which stops at <c>#$07</c>.</item>
    ///         <item>Disassembly: not separately labelled.</item>
    ///     </list>
    /// </remarks>
    public int GetDisplayedSpareLives()
    {
        return Math.Min(GetSpareLives(), HudLayout.HudMaxSpareLives);
    }
}
