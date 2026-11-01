namespace Robotron2084.Tuning;

/// <summary>The player's speed, death sequence, invincibility, fire rate and lives.</summary>
/// <remarks>
///     Per-kill point values live in <c>Level/ScoreValues.cs</c>, not here.
/// </remarks>
public static class PlayerTuning
{
    /// <summary>
    ///     How many <see cref="Robotron2084.Core.ArcadeClock.PortTicksPerSecond">ticks</see> the playfield stands still for when the
    ///     player is killed.
    /// </summary>
    /// <remarks>Port only.</remarks>
    public const int HitStopTicks = 10;

    /// <summary>
    ///     How many <see cref="Robotron2084.Core.ArcadeClock.PortTicksPerSecond">ticks</see> the player is hidden for in each flicker
    ///     of the invincibility that follows a death.
    /// </summary>
    /// <remarks>Port only.</remarks>
    public const int InvincibilityFlickerHiddenTicks = 2;

    /// <summary>
    ///     How many <see cref="Robotron2084.Core.ArcadeClock.PortTicksPerSecond">ticks</see> the player is shown for in each flicker
    ///     of the invincibility that follows a death.
    /// </summary>
    /// <remarks>Port only.</remarks>
    public const int InvincibilityFlickerVisibleTicks = 4;

    /// <summary>The palette index the laser sprites are drawn in.</summary>
    /// <remarks>
    ///     Disassembly: every lit nibble of the ROM's four laser sprites (R5 <c>$35C1</c> to <c>$35DC</c>) is <c>$A</c>.
    ///     Original source: that is the LASER FLASH entry, <c>RRS22.ASM</c> <c>LF</c>, which flashes white and a random
    ///     colour of <c>COLTAB</c> every few frames.
    /// </remarks>
    public const int LaserPaletteIndex = 10;

    /// <summary>How far a laser goes on each tick, in port pixels.</summary>
    /// <remarks>Port only.</remarks>
    public const int LaserSpeed = 12;

    /// <summary>
    ///     How many <see cref="Robotron2084.Core.ArcadeClock.PortTicksPerSecond">ticks</see> pass between one laser and the next
    ///     while the fire control is held down.
    /// </summary>
    /// <remarks>
    ///     Port only. A press fires at once, as the arcade does on the rising edge of the control. The limit that really
    ///     binds is the number of laser slots (<c>LaserSlots</c>).
    /// </remarks>
    public const int PlayerAutoFireTicks = 12;

    /// <summary>
    ///     How many <see cref="Robotron2084.Core.ArcadeClock.ArcadeFramesPerSecond">fiftieths of a second</see> the player is drawn in
    ///     a random colour for, in each flash of the death.
    /// </summary>
    /// <remarks>Original source: <c>RRX7.ASM</c> <c>PDTHV</c> (notes §66).</remarks>
    public const int PlayerDeathColourArcadeFrames = 6;

    /// <summary>
    ///     How many <see cref="Robotron2084.Core.ArcadeClock.ArcadeFramesPerSecond">fiftieths of a second</see> there are between one
    ///     step of the death fade and the next.
    /// </summary>
    /// <remarks>Original source: <c>RRX7.ASM</c> <c>PDTHV</c>, <c>NAP 4</c> for each byte of the fade table (notes §66).</remarks>
    public const int PlayerDeathFadeArcadeFrames = 4;

    /// <summary>The palette index the player fades out in at the end of the death.</summary>
    /// <remarks>
    ///     Original source: <c>RRX7.ASM</c> <c>PDTHV</c>, colour <c>$CC</c>. This is the DECAY palette index.
    /// </remarks>
    public const int PlayerDeathFadePaletteIndex = 0x0C;

    /// <summary>How many times the flash of the death is repeated.</summary>
    /// <remarks>Original source: <c>RRX7.ASM</c> <c>PDTHV</c>, <c>LDA #10</c> (notes §66).</remarks>
    public const int PlayerDeathFlashIterations = 10;

    /// <summary>
    ///     How many <see cref="Robotron2084.Core.ArcadeClock.ArcadeFramesPerSecond">fiftieths of a second</see> the player is drawn
    ///     as a white silhouette for, at the start of each flash of the death.
    /// </summary>
    /// <remarks>Original source: <c>RRX7.ASM</c> <c>PDTHV</c> (notes §66).</remarks>
    public const int PlayerDeathWhiteArcadeFrames = 2;

    /// <summary>The palette index of the white silhouette that starts each flash of the death.</summary>
    /// <remarks>Original source: <c>RRX7.ASM</c> <c>PDTHV</c>, colour <c>$99</c>.</remarks>
    public const int PlayerDeathWhitePaletteIndex = 0x09;

    /// <summary>
    ///     How many <see cref="Robotron2084.Core.ArcadeClock.PortTicksPerSecond">ticks</see> the player is protected for after coming
    ///     back from a death.
    /// </summary>
    /// <remarks>Port only.</remarks>
    public const int PlayerInvincibilityTicks = 120;

    /// <summary>How far the player goes sideways on each tick, in port pixels.</summary>
    /// <remarks>
    ///     Original source: <c>RRF.ASM</c> <c>PLAYER</c>. Disassembly: <c>asm/robomame.asm</c> <c>MOVE_PLAYER</c>
    ///     (<c>$2FD0</c>). The arcade's sideways step is half its up-and-down step (X is held with one fractional bit, so
    ///     <c>FF</c> and <c>01</c> go through <c>ASRA / RORB</c> to a half step), and diagonals apply both steps
    ///     unchanged. The port scales the arcade's steps to its own screen and rounds, so that the player takes about the
    ///     same time to cross the field as in the arcade.
    /// </remarks>
    public const int PlayerSpeedX = 2;

    /// <summary>How far the player goes up or down on each tick, in port pixels.</summary>
    /// <remarks>See <see cref="PlayerSpeedX" />.</remarks>
    public const int PlayerSpeedY = 3;

    /// <summary>The wave the first game starts at.</summary>
    /// <remarks>The arcade spec says "assigned level 1".</remarks>
    public const int StartingWaveNumber = 1;

    /// <summary>How many lives each player starts a game with.</summary>
    /// <remarks>The arcade spec says "the PLAYER is awarded 3 lives".</remarks>
    public const int StartingLives = 3;

    /// <summary>
    ///     The values the death fade writes into the palette index it fades in, one after another. The last one ends the
    ///     fade.
    /// </summary>
    /// <remarks>Original source: <c>RRX7.ASM</c> <c>PD2TAB</c>: <c>FF F6 AD A4 5B 52 09 00</c>.</remarks>
    public static readonly byte[] PlayerDeathFadeValues = [0xFF, 0xF6, 0xAD, 0xA4, 0x5B, 0x52, 0x09, 0x00];

    /// <summary>The palette indices the death flash picks its random colour from, one at random for each flash.</summary>
    /// <remarks>
    ///     Original source: <c>RRX7.ASM</c> <c>PDCTAB</c>, <c>FCB $00,$11,$33,$77</c>, which are doubled-nibble palette
    ///     indices 0, 1, 3 and 7.
    /// </remarks>
    public static readonly int[] PlayerDeathFlashPaletteIndices = [0x00, 0x01, 0x03, 0x07];

    /// <summary>
    ///     A playtest aid: when true the player cannot be killed at all, so that new robot types can be playtested across
    ///     whole waves. It is a property, not a constant, so that the guard on it does not fold to unreachable code.
    /// </summary>
    /// <remarks>
    ///     This is meant to be false once the gameplay is confirmed working.
    /// </remarks>
    public static bool PlayerInvincibleForTesting => true;
}
