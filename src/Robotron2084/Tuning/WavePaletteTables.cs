using Microsoft.Xna.Framework;

namespace Robotron2084.Tuning;

/// <summary>
///     The per-wave colour and sprite tables from the arcade, each repeating every ten waves, and the fallback wall
///     palette.
/// </summary>
/// <remarks>
///     <list type="bullet">
///         <item>
///             Original source: <c>RRG23.ASM</c> <c>GTWCOL</c> ("GET WALL COLOR"), which sets all four per-wave values:
///             <c>LDU #WCTAB / LDA PWAV,X / DECA</c>, then wraps the wave into 1 to 10 (<c>GTWL CMPA #9 / BLS / SUBA #10</c>)
///         </item>
///         <item>
///             Disassembly: <c>asm/robomame.asm</c> <c>SET_BORDER_WALL_COLOUR</c> (<c>$2A21</c>), with the tables
///             <c>BORDER_WALL_COLOUR_TABLE</c> (<c>$2A4B</c>), <c>ELECTRODE_COLOUR_TABLE</c> (<c>$2A55</c>),
///             <c>ELECTRODE_ANIMATION_FRAME_METADATA_OFFSET_TABLE</c> (<c>$2A5F</c>) and
///             <c>LASER_SPLASH_DAMAGE_COLOUR_TABLE</c> (<c>$2A69</c>)
///         </item>
///     </list>
///     The colour values are palette slots in the arcade's doubled-nibble form: <c>$XX</c> is two pixels of palette index
///     X, because the video is 4 bits a pixel with two pixels in each byte, and a solid fill needs both nibbles equal. The
///     low nibble of a value is therefore the palette index.
/// </remarks>
public static class WavePaletteTables
{
    /// <summary>How many animation frames make up one electrode variant: the electrode alive, and two shrivel frames.</summary>
    public const int ElectrodeAnimationFramesPerVariant = 3;

    /// <summary>How long the wall stays on each colour of its palette, in milliseconds.</summary>
    /// <remarks>Port only: used with <see cref="DefaultWallPalette" />.</remarks>
    public const int WallStepDurationMilliseconds = 150;

    /// <summary>The wall's cycle of four colours, from dim to bright, that is used when no wave palette applies.</summary>
    /// <remarks>The arcade spec's rule is that the wall is never fully dark.</remarks>
    public static readonly Color[] DefaultWallPalette =
    {
        new(0, 80, 80),
        new(0, 140, 140),
        new(0, 200, 200),
        new(0, 255, 255)
    };

    /// <summary>The electrode's colour for each wave, counting from wave 1 and repeating every ten waves.</summary>
    /// <remarks>
    ///     The second of the four per-wave tables (<c>LDA 10,U / STA PSTCOL</c>). Use
    ///     <see cref="GetElectrodePaletteIndex" />. Decoded: wave 1 is <c>$FF</c>, palette index 15; wave 2 is <c>$EE</c>,
    ///     14; wave 3 is <c>$BB</c>, 11; wave 4 is <c>$DD</c>, 13; wave 7 is <c>$11</c>, palette index 1, which is red
    ///     (the ROM's own <c>CRTAB</c> has palette index 1 as <c>$07</c>); wave 10 is <c>$AA</c>, 10. The ROM makes the
    ///     electrode a solid silhouette in the live colour of this palette index (<c>PSTKON</c>, <c>OPON1</c> and
    ///     <c>MPCTON</c>, blitter op <c>$1A</c>), so palette indices 10 to 15, the colour-cycling ones, make that wave's
    ///     posts cycle (notes §47).
    /// </remarks>
    public static readonly byte[] ElectrodePaletteIndexByWaveMod10 =
    [
        0xFF, 0xEE, 0xBB, 0xDD, 0xEE, 0xFF, 0x11, 0xBB, 0xDD, 0xAA
    ];

    /// <summary>The electrode shape for each wave, counting from wave 1 and repeating every ten waves.</summary>
    /// <remarks>
    ///     The third of the four per-wave tables, the post image offset table: <c>LDB 20,U / LDX PSTP1 / ABX / STX
    ///     PSTANI</c>, with <c>$10</c> bytes for each variant of three animation frames. So the offsets are <c>$00</c>,
    ///     <c>$10</c>, <c>$20</c>, <c>$30</c>, <c>$40</c>, <c>$50</c>, <c>$70</c>, <c>$80</c>, <c>$00</c> and <c>$60</c>,
    ///     which are the families 0, 1, 2, 3, 4, 5, 7, 8, 0 and 6. The 27 electrode animation frames in sprite-list order
    ///     are nine families of three: frame 0 is alive and frames 1 and 2 are the shrivel steps (<c>RRP8.ASM</c>
    ///     <c>PKPROC</c>).
    /// </remarks>
    public static readonly int[] ElectrodeVariantByWaveMod10 = [0, 1, 2, 3, 4, 5, 7, 8, 0, 6];

    /// <summary>The colour of the flash where a laser runs off the playfield, for each wave, counting from wave 1 and repeating every ten waves.</summary>
    /// <remarks>
    ///     The fourth of the four per-wave tables (<c>LDA 30,U / STA LASCOL</c>). <c>RRF.ASM</c> names the variable:
    ///     <c>LASCOL RMB 1 LASER WALL COLLIDE COLOR</c>. It is not the laser's own colour. <c>LASDIH</c> and <c>LASDIV</c>
    ///     paint the end pixels in <c>LASCOL</c> for two frames, then in <c>WALCOL</c> for one frame, and then leave the
    ///     wall colour. Use <see cref="GetLaserWallPaletteIndex" />. Decoded: waves 1, 3, 5, 6, 7 and 10 are <c>$99</c>,
    ///     palette index 9, which is <c>$FF</c> white; wave 2 is <c>$00</c>, palette index 0, black, so that wave's flash
    ///     is invisible, as in the arcade; wave 4 is <c>$66</c>, palette index 6, which is <c>$38</c> green; wave 8 is
    ///     <c>$11</c>, palette index 1, which is <c>$07</c> red; wave 9 is <c>$AA</c>, palette index 10, a colour-cycling
    ///     one.
    /// </remarks>
    public static readonly byte[] LaserWallPaletteIndexByWaveMod10 =
    [
        0x99, 0x00, 0x99, 0x66, 0x99, 0x99, 0x99, 0x11, 0xAA, 0x99
    ];

    /// <summary>The wall's colour for each wave, counting from wave 1 and repeating every ten waves.</summary>
    /// <remarks>
    ///     The first of the four per-wave tables (<c>LDA ,U / STA WALCOL</c>). Use <see cref="GetWallPaletteIndex" />.
    ///     It is in the same form as <see cref="ElectrodePaletteIndexByWaveMod10" />. The ROM's <c>BORDER</c> (checked
    ///     against <c>RRG23.ASM</c>: <c>LDA WALCOL</c> then <c>STA ,X+</c> up to <c>STA (XMAX-XMIN)*$100+$200,X</c>)
    ///     stores one byte for each step, so the border is a solid fill of the live colour of that palette index.
    ///     Decoded against the ROM's <c>CRTAB</c> defaults (see <c>GamePalette.DefaultPaletteValues</c>): wave 1 is
    ///     <c>$22</c>, palette index 2, which is <c>$17</c> orange; wave 2 is <c>$55</c>, 5, <c>$3F</c> yellow; wave 3
    ///     is <c>$11</c>, 1, <c>$07</c> red; wave 4 is <c>$EE</c>, 14, a colour-cycling one; wave 5 is <c>$77</c>, 7,
    ///     <c>$C0</c> blue; wave 6 is <c>$33</c>, 3, <c>$C7</c> magenta; wave 7 is <c>$44</c>, 4, <c>$1F</c> a dimmer
    ///     yellow; wave 8 is <c>$88</c>, 8, <c>$A4</c> purple; wave 9 is <c>$00</c>, 0, <c>$00</c> black, so the wave-9
    ///     border is invisible, as the ROM means it; wave 10 is <c>$CC</c>, 12, a colour-cycling one. The ROM's colour
    ///     processes also write palette indices during play, so the border may change shade within a wave (notes §63).
    /// </remarks>
    public static readonly byte[] WallPaletteIndexByWaveMod10 =
    [
        0x22, 0x55, 0x11, 0xEE, 0x77, 0x33, 0x44, 0x88, 0x00, 0xCC
    ];

    /// <summary>Gets the electrode's palette index for a wave. The table repeats every ten waves.</summary>
    /// <param name="wave">The wave number, counting from 1.</param>
    public static int GetElectrodePaletteIndex(int wave)
    {
        return ElectrodePaletteIndexByWaveMod10[(wave - 1) % ElectrodePaletteIndexByWaveMod10.Length] & 0x0F;
    }

    /// <summary>Gets the electrode shape for a wave. The table repeats every ten waves.</summary>
    /// <param name="wave">The wave number, counting from 1.</param>
    public static int GetElectrodeVariant(int wave)
    {
        return ElectrodeVariantByWaveMod10[(wave - 1) % ElectrodeVariantByWaveMod10.Length];
    }

    /// <summary>Gets the palette index of the flash where a laser meets the wall, for a wave. The table repeats every ten waves.</summary>
    /// <param name="wave">The wave number, counting from 1.</param>
    public static int GetLaserWallPaletteIndex(int wave)
    {
        return LaserWallPaletteIndexByWaveMod10[(wave - 1) % LaserWallPaletteIndexByWaveMod10.Length] & 0x0F;
    }

    /// <summary>Gets the wall's palette index for a wave. The table repeats every ten waves.</summary>
    /// <param name="wave">The wave number, counting from 1.</param>
    public static int GetWallPaletteIndex(int wave)
    {
        return WallPaletteIndexByWaveMod10[(wave - 1) % WallPaletteIndexByWaveMod10.Length] & 0x0F;
    }
}
