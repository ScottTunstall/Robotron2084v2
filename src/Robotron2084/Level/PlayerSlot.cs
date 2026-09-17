using Robotron2084.Input;
using Robotron2084.Tuning;

namespace Robotron2084.Level;

/// <summary>
/// ONE player's persistent session state — the ROM's per-player block
/// (<c>PLDATA</c> for player 1, <c>ZP2SCR</c> for player 2, selected by
/// <c>PLINDX</c>/<c>PLDX</c>): the score (4 BCD bytes), the next-free-man
/// threshold, the lives counter (<c>PLAS</c>), the wave (<c>PWAV</c>) and the
/// frozen enemy list.
///
/// Robotron 2084 alternates turns: each player keeps their own score, lives and
/// wave across the other player's turn, and a death hands the turn over (ROM
/// RRG23 <c>PLE1B</c>: <c>EORA #3</c> to the other player, skipping anyone with
/// no men left, then <c>PLSTRT</c> resumes THEIR wave).
/// </summary>
public sealed class PlayerSlot
{
    public PlayerSlot(int number, IPlayerInputSource input, int lives, int wave)
    {
        Number = number;
        Input = input;
        Lives = lives;
        Wave = wave;
    }

    /// <summary>1 or 2 — the ROM prints it after "PLAYER " (string 103).</summary>
    public int Number { get; }

    /// <summary>
    /// This player's controls. Per-slot so a future SIMULTANEOUS two-player mode
    /// can give player 2 their own source; in the arcade's alternating game both
    /// slots just hold the same device.
    /// </summary>
    public IPlayerInputSource Input { get; }

    /// <summary>ROM <c>PWAV</c> — this player's own wave number (only they advance it).</summary>
    public int Wave { get; set; }

    /// <summary>ROM <c>ZP1SCR</c>/<c>ZP2SCR</c>.</summary>
    public int Score { get; set; }

    /// <summary>
    /// Men remaining INCLUDING the life in play (ROM <c>PLAS</c>: <c>START1</c>
    /// loads it from the CMOS "ships per credit", and <c>PLSTRT</c> does
    /// <c>DEC PLAS,X</c> as each new life begins).
    /// </summary>
    public int Lives { get; set; }

    /// <summary>ROM <c>SAVCNT</c> — humans rescued during the current life; cleared on death.</summary>
    public int Rescues { get; set; }

    /// <summary>True while this player can still be given a turn.</summary>
    public bool HasMen => Lives > 0;

    /// <summary>
    /// The SPARE men the HUD draws (ROM <c>p1_men</c>/<c>p2_men</c> = the lives
    /// counter read AFTER the life in play was decremented) — with 3 ships you
    /// see 2 icons during your first life.
    /// </summary>
    public int SpareMen => System.Math.Max(0, Lives - 1);

    /// <summary>
    /// How many mini man icons the HUD draws: the spare men, capped at SEVEN
    /// (ROM MANDSV: <c>CMPA #$07 / BLS / LDA #$07</c> — "MAX OF 7").
    /// </summary>
    public int DisplayedMen => System.Math.Min(SpareMen, GameplayConstants.HudMaxMen);
}
