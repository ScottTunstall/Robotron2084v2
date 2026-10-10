using Microsoft.Xna.Framework;

namespace Robotron2084.Tuning;

/// <summary>
///     The ROM's per-wave colour and sprite tables (RRG23 `GTWCOL`), each repeating every ten waves, and the fallback
///     wall palette.
/// </summary>
public static class WavePaletteTables
{
    /// <summary>How many animation frames make up one electrode variant (alive + 2 shrivel frames).</summary>
    public const int ElectrodeAnimationFramesPerVariant = 3;

    // Wall
    public const int WallStepDurationMilliseconds = 150;

    /// <summary>Default 4-colour cyan cycle, dim to bright — never fully dark (spec-stated rule).</summary>
    public static readonly Color[] DefaultWallPalette =
    {
        new(0, 80, 80),
        new(0, 140, 140),
        new(0, 200, 200),
        new(0, 255, 255)
    };

    /// <summary>
    ///     The LASER-WALL COLLIDE colour per wave — the FOURTH of RRG23.ASM's per-wave
    ///     tables (`GTWCOL`: `LDA 30,U / STA LASCOL`), indexed by (wave-1) mod 10.
    ///     RRF.ASM names the variable: `LASCOL RMB 1 LASER WALL COLLIDE COLOR` — it is
    ///     NOT the laser's own colour; it is the FLARE painted where a laser runs off
    ///     the playfield. `LASDIH`/`LASDIV` paint the end pixel(s) in LASCOL for 2
    ///     frames, then in WALCOL for 1 frame, then leave the wall colour.
    ///     Use <see cref="GetLaserWallSlot" />.
    ///     Decoded: wave 1/3/5/6/7/10 = $99 = slot 9 = $FF = WHITE, 2 = $00 = slot 0 =
    ///     BLACK (that wave's flare is invisible — faithful), 4 = $66 = slot 6 = $38 =
    ///     GREEN, 8 = $11 = slot 1 = $07 = RED, 9 = $AA = slot 10 = a CYCLING slot.
    /// </summary>
    public static readonly byte[] LaserWallSlotByWaveMod10 =
    [
        0x99, 0x00, 0x99, 0x66, 0x99, 0x99, 0x99, 0x11, 0xAA, 0x99
    ];

    // Electrode
    // Electrode animation frames per wave — RRG23.ASM `GTWCOL` ("GET WALL COLOR",
    // which sets all four per-wave values): `LDU #WCTAB / LDA PWAV,X / DECA`,
    // wrap the wave into 1..10 (`GTWL CMPA #9 / BLS / SUBA #10`), then
    // `LDB 20,U / LDX PSTP1 / ABX / STX PSTANI` — the POST IMAGE offset table
    // (the third of the wave colour tables) with $10 bytes per variant of
    // 3 animation frames. So the electrode's variant is WAVE-DEPENDENT and repeats
    // every 10 waves: offsets $00,$10,$20,$30,$40,$50,$70,$80,$00,$60 →
    // families 0,1,2,3,4,5,7,8,0,6.
    // The 27 electrode animation frames in sprite-list order are 9 families x 3 frames
    // — frame 0 = alive, frames 1-2 = the shrivel steps (RRP8 `PKPROC`).
    public static readonly int[] ElectrodeVariantByWaveMod10 = [0, 1, 2, 3, 4, 5, 7, 8, 0, 6];

    /// <summary>
    ///     The POST COLOUR per wave — the SECOND of RRG23.ASM's four per-wave tables
    ///     (`GTWCOL`: `LDA 10,U / STA PSTCOL`), indexed by (wave-1) mod 10 like the
    ///     rest. These are PALETTE SLOTS written in the arcade's doubled-nibble form
    ///     ($XX = two pixels of slot X, because the video is 4bpp with 2 pixels per
    ///     byte and a SOLID fill needs both nibbles equal) — so the value's LOW NIBBLE
    ///     is the slot. Use <see cref="GetElectrodeSlot" />.
    ///     Decoded: wave 1 = $FF = slot 15, 2 = $EE = 14, 3 = $BB = 11, 4 = $DD = 13,
    ///     7 = $11 = slot 1 = RED (the ROM's own CRTAB: slot 1 = $07), 10 = $AA = 10.
    ///     The ROM makes the electrode a SOLID silhouette in this slot's live colour
    ///     (`PSTKON` → `OPON1` → `MPCTON`, blitter op $1A), so slots 10-15 — the
    ///     colour-cycling ones — make that wave's posts cycle (notes §47).
    /// </summary>
    public static readonly byte[] ElectrodeSlotByWaveMod10 =
    [
        0xFF, 0xEE, 0xBB, 0xDD, 0xEE, 0xFF, 0x11, 0xBB, 0xDD, 0xAA
    ];

    /// <summary>
    ///     The WALL/BORDER colour per wave — the FIRST of RRG23.ASM's four per-wave
    ///     tables (`GTWCOL`: `LDA ,U / STA WALCOL`), indexed by (wave-1) mod 10. Same
    ///     doubled-nibble form as <see cref="ElectrodeSlotByWaveMod10" /> ($XX = two pixels
    ///     of slot X, because the video is 4bpp and the ROM's `BORDER` fills the
    ///     border by storing ONE byte per step, which must be solid) — so the value's
    ///     LOW NIBBLE is the slot. Use <see cref="GetWallSlot" />.
    ///     Decoded against the ROM's CRTAB defaults (see <c>GamePalette.DefaultSlotValues</c>):
    ///     wave 1 = $22 = slot 2 = $17 = ORANGE, 2 = $55 = slot 5 = $3F = YELLOW,
    ///     3 = $11 = slot 1 = $07 = RED, 4 = $EE = slot 14 = a CYCLING slot,
    ///     5 = $77 = slot 7 = $C0 = BLUE, 6 = $33 = slot 3 = $C7 = MAGENTA,
    ///     7 = $44 = slot 4 = $1F = YELLOW (dimmer), 8 = $88 = slot 8 = $A4 = PURPLE,
    ///     9 = $00 = slot 0 = $00 = **BLACK** (the wave-9 border is genuinely
    ///     invisible — the ROM means it), 10 = $CC = slot 12 = a CYCLING slot.
    ///     VERIFIED 2026-09-17 against `ref/original-source/RRG23.ASM` `BORDER`
    ///     (`LDA WALCOL` → `STA ,X+` … `STA (XMAX-XMIN)*$100+$200,X`, one byte per
    ///     step, so the border is a SOLID fill of that slot's live colour).
    ///     CAVEAT: the ROM's colour processes also re-write slots during play, so the
    ///     border may shift shade within a wave — that machinery is NOT modelled yet
    ///     (see notes §63).
    /// </summary>
    public static readonly byte[] WallSlotByWaveMod10 =
    [
        0x22, 0x55, 0x11, 0xEE, 0x77, 0x33, 0x44, 0x88, 0x00, 0xCC
    ];

    /// <summary>The laser-vs-wall flare palette slot for a wave (RRG23 `LASCOL`; wraps every 10 waves).</summary>
    public static int GetLaserWallSlot(int wave)
    {
        return LaserWallSlotByWaveMod10[(wave - 1) % LaserWallSlotByWaveMod10.Length] & 0x0F;
    }

    /// <summary>
    ///     The electrode variant for a wave (RRG23 `GTWCOL`; the ROM
    ///     wraps the wave into 1..10, so the pattern repeats every 10 waves).
    /// </summary>
    public static int GetElectrodeVariant(int wave)
    {
        return ElectrodeVariantByWaveMod10[(wave - 1) % ElectrodeVariantByWaveMod10.Length];
    }

    /// <summary>The electrode palette slot for a wave (RRG23 `GTWCOL`; wraps every 10 waves).</summary>
    public static int GetElectrodeSlot(int wave)
    {
        return ElectrodeSlotByWaveMod10[(wave - 1) % ElectrodeSlotByWaveMod10.Length] & 0x0F;
    }

    /// <summary>The wall/border palette slot for a wave (RRG23 `WALCOL`; wraps every 10 waves).</summary>
    public static int GetWallSlot(int wave)
    {
        return WallSlotByWaveMod10[(wave - 1) % WallSlotByWaveMod10.Length] & 0x0F;
    }
}
