namespace Robotron2084.Tuning;

/// <summary>The player's speed, death sequence, invincibility, fire rate and lives.</summary>
public static class PlayerTuning
{
    // Scoring — per-kill point values live in Level/ScoreValues.cs
    public const int HitStopTicks = 10;

    public const int InvincibilityFlickerHiddenTicks = 2;

    public const int InvincibilityFlickerVisibleTicks = 4;

    /// <summary>
    /// The palette slot the laser sprites are drawn in: every lit nibble of the ROM's four laser sprites
    /// (R5 $35C1-$35DC) is <c>$A</c>, the LASER FLASH entry (RRS22 <c>LF</c>), which flashes white and a random
    /// colour of <c>COLTAB</c> every few frames.
    /// </summary>
    public const int LaserSlot = 10;

    // Player laser
    public const int LaserSpeed = 12;

    /// <summary>
    /// A press fires immediately (the arcade's rising edge) and HOLDING the fire
    /// button re-fires every this many ticks; the 3-laser SLOT cap (LaserSlots)
    /// is the real binding limit.
    /// </summary>
    public const int PlayerAutoFireTicks = 12;

    public const int PlayerDeathColourRomFrames = 6;

    public const int PlayerDeathFadeRomFrames = 4;

    public const int PlayerDeathFadeSlot = 0x0C;

    // PLAYER DEATH (notes §66) — RRX7.ASM `PDTHV` ("GENIES BITCHEN 330AM PDEATH").
    // The player is drawn as a SOLID silhouette ($99 = slot 9) for 2 frames and
    // then in a RANDOM colour from PDCTAB ($00,$11,$33,$77 -> slots 0,1,3,7) for
    // 6 frames, repeated `LDA #10` = 10 times; then the colour processes are
    // restarted, the DECAY process (slot 12) is STOPPED, and the fade table is
    // written into slot 12 one byte per `NAP 4` while the player keeps being drawn
    // solid in slot 12 — the trailing $00 ends it and erases the player.
    // Total: 10 x (2 + 6) = 80 frames of flash, then 7 gaps of 4 frames across the
    // 8-byte fade table = 108 fiftieths of a second (129.6 port ticks).
    public const int PlayerDeathFlashIterations = 10;

    public const int PlayerDeathWhiteRomFrames = 2;

    public const int PlayerDeathWhiteSlot = 0x09;

    public const int PlayerInvincibilityTicks = 120;

    // Player
    // R5 MOVE_PLAYER ($2FD0) deltas: vertical = 1 arcade px/tick; horizontal
    // = 0.5 arcade px/tick (X is 15.1 fixed point: FF/01 -> ASRA/RORB half
    // step). Diagonals apply BOTH, unnormalised. Scaled to the port's
    // 640x400 screen (3.33x / 2.5x) and rounded: 2 / 3 screen px per port
    // tick ~= arcade field-crossing times (6.4s X / 2.67s Y).
    public const int PlayerSpeedX = 2;

    public const int PlayerSpeedY = 3;
    // $99

    // $CC = slot 12 (the DECAY slot)

    public const int StartingLevelNumber = 1;

    // Session / geometry values taken from spec.txt
    public const int StartingLives = 3;

    /// <summary>ROM `PD2TAB`: the slot-12 fade, FF F6 AD A4 5B 52 09 00 — the last byte ends it.</summary>
    public static readonly byte[] PlayerDeathFadeValues = [0xFF, 0xF6, 0xAD, 0xA4, 0x5B, 0x52, 0x09, 0x00];

    /// <summary>ROM `PDCTAB`: FCB $00,$11,$33,$77 — doubled-nibble slots 0, 1, 3, 7.</summary>
    public static readonly int[] PlayerDeathFlashSlots = [0x00, 0x01, 0x03, 0x07];

    /// <summary>
    /// TEMPORARY playtest aid: the player cannot be
    /// killed at all, so new robot types (brains/progs/missiles) can be
    /// playtested across whole waves. TURN THIS OFF (and re-verify the
    /// gates) once the gameplay is confirmed working.
    /// </summary>
    public static bool PlayerInvincibleForTesting => true; // flip to false when playtesting is done (a property, not const, so the guard below doesn't fold to unreachable)

    // spec-stated ("the PLAYER is awarded 3 lives")

    // spec-stated ("assigned level 1")
}
