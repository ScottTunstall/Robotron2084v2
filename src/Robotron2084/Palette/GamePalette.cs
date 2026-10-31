using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Graphics;

namespace Robotron2084.Palette;

/// <summary>
///     The live 16-index screen palette. Palette indices 0-9 are fixed for the game; palette indices
///     10-15 are the colour-cycling palette indices advanced by <see cref="PaletteAnimator" />
///     (process tables decoded from the original source — arcade-fidelity-notes
///     §4.12). Sprite PNGs store six fixed marker colours for the cycling palette indices
///     (the de-duplicated service values); the colour-cycle shader remaps those
///     markers to the palette indices' current live colours.
/// </summary>
public sealed class GamePalette
{
    /// <summary>
    ///     The six cycling palette index marker colour bytes (palette indices 10-15) exactly as the
    ///     sprite PNGs encode them (RobotronPaletteService de-duplicated values —
    ///     all 16 distinct, so a texel's palette index is unambiguous for the shader).
    /// </summary>
    public static readonly byte[] CyclingPaletteIndexMarkers =
    [
        0xC4, 0xF4, 0xCC, 0x81, 0x45, 0x2F
    ];

    /// <summary>ROM default palette (CRTAB, ROM $DA51) — the initial 16 palette indices.</summary>
    public static readonly byte[] DefaultPaletteValues =
    [
        0x00, 0x07, 0x17, 0xC7, 0x1F, 0x3F, 0x38, 0xC0,
        0xA4, 0xFF, 0x38, 0x17, 0xCC, 0x81, 0x81, 0x07
    ];

    private readonly byte[] _paletteValues = (byte[])DefaultPaletteValues.Clone();
    private readonly bool[] _suspendedPaletteIndices = new bool[16];

    /// <summary>The live RGB of a palette index.</summary>
    public Color GetColour(int paletteIndex)
    {
        return RobotronColour.CreateFromByte(_paletteValues[paletteIndex]);
    }

    /// <summary>The current 8-bit colour code of a palette index (0-15).</summary>
    public int GetPaletteIndexValue(int paletteIndex)
    {
        return _paletteValues[paletteIndex];
    }

    /// <summary>True while a palette index's colour process is stopped.</summary>
    public bool IsPaletteIndexSuspended(int paletteIndex)
    {
        return _suspendedPaletteIndices[paletteIndex];
    }

    /// <summary>Lets a palette index's colour process run again (the ROM's `COLST`).</summary>
    public void ResumePaletteIndex(int paletteIndex)
    {
        _suspendedPaletteIndices[paletteIndex] = false;
    }

    /// <summary>Writes a palette index's current colour code (the colour processes do this).</summary>
    public void SetPaletteIndexValue(int paletteIndex, byte value)
    {
        _paletteValues[paletteIndex] = value;
    }

    /// <summary>
    ///     Freezes a palette index's colour process (the ROM's "KILL OFF DECAY": the player
    ///     death stops the DECAY process and then drives palette index 12 itself). A suspended
    ///     palette index's process neither steps nor writes; <see cref="ResumePaletteIndex" /> is the
    ///     ROM's `COLST`, which recreates the colour processes afterwards.
    /// </summary>
    public void SuspendPaletteIndex(int paletteIndex)
    {
        _suspendedPaletteIndices[paletteIndex] = true;
    }

    /// <summary>
    ///     Pushes the live colours of the six cycling palette indices (10-15) into the
    ///     colour-cycle effect's named uniforms <c>Live10</c>..<c>Live15</c>
    ///     (notes §34: six named uniforms — the TPGParser rejects array syntax).
    /// </summary>
    public void UpdateEffectColours(Effect effect)
    {
        for (var paletteIndex = 10; paletteIndex <= 15; paletteIndex++)
        {
            var colour = GetColour(paletteIndex);
            effect.Parameters[$"Live{paletteIndex}"]
                .SetValue(new Vector4(colour.R / 255f, colour.G / 255f, colour.B / 255f, 1f));
        }
    }
}
