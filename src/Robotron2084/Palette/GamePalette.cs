using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Graphics;

namespace Robotron2084.Palette;

/// <summary>
///     The colours on the screen: sixteen palette entries, each holding one colour. Entries 0 to 9 stay the same for the
///     whole game, and entries 10 to 15 keep changing colour.
/// </summary>
/// <remarks>
///     <para>
///         The entries that keep changing are moved on by <see cref="PaletteAnimator" /> (notes §4.12). The sprite images
///         store six marker colours in place of those six entries, and the colour-cycle effect swaps each marker for the
///         entry's colour at that moment.
///     </para>
///     <list type="bullet">
///         <item>
///             <description>
///                 Original source: <c>RRS22.ASM</c> <c>CRTAB</c> (the colours the palette starts with) and the colour processes
///                 that change entries 10 to 15.
///             </description>
///         </item>
///         <item>
///             <description>
///                 Disassembly: <c>asm/robomame.asm</c> <c>colorpalette2</c> (<c>$DA51</c>).
///             </description>
///         </item>
///     </list>
/// </remarks>
public sealed class GamePalette
{
    /// <summary>
    ///     The six marker colours the sprite images use in place of entries 10 to 15. Every marker is different from every
    ///     other colour in the palette, so the colour-cycle effect can tell which entry a pixel means.
    /// </summary>
    public static readonly byte[] CyclingPaletteIndexMarkers =
    [
        0xC4, 0xF4, 0xCC, 0x81, 0x45, 0x2F
    ];

    /// <summary>The sixteen colours the palette starts with, one for each entry.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item><description>Original source: <c>RRS22.ASM</c> <c>CRTAB</c>.</description></item>
    ///         <item><description>Disassembly: <c>asm/robomame.asm</c> <c>colorpalette2</c> (<c>$DA51</c>).</description></item>
    ///     </list>
    /// </remarks>
    public static readonly byte[] DefaultPaletteValues =
    [
        0x00, 0x07, 0x17, 0xC7, 0x1F, 0x3F, 0x38, 0xC0,
        0xA4, 0xFF, 0x38, 0x17, 0xCC, 0x81, 0x81, 0x07
    ];

    private readonly byte[] _paletteValues = (byte[])DefaultPaletteValues.Clone();
    private readonly bool[] _suspendedPaletteIndices = new bool[16];

    /// <summary>Gets the colour an entry shows on the screen.</summary>
    /// <param name="paletteIndex">Which entry, from 0 to 15.</param>
    public Color GetColour(int paletteIndex)
    {
        return RobotronColour.CreateFromByte(_paletteValues[paletteIndex]);
    }

    /// <summary>Gets the colour code an entry holds.</summary>
    /// <param name="paletteIndex">Which entry, from 0 to 15.</param>
    public int GetPaletteIndexValue(int paletteIndex)
    {
        return _paletteValues[paletteIndex];
    }

    /// <summary>Says whether an entry's colour process is stopped.</summary>
    /// <param name="paletteIndex">Which entry, from 0 to 15.</param>
    public bool IsPaletteIndexSuspended(int paletteIndex)
    {
        return _suspendedPaletteIndices[paletteIndex];
    }

    /// <summary>Lets an entry's colour process run again.</summary>
    /// <param name="paletteIndex">Which entry, from 0 to 15.</param>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>
    ///             <description>
    ///                 Original source: <c>RRX7.ASM</c> <c>PDTH2</c> calls <c>COLST</c> (the vector in <c>RRF.ASM</c> to
    ///                 <c>COLSTV</c> in <c>RRS22.ASM</c>), which starts the colour processes again.
    ///             </description>
    ///         </item>
    ///         <item><description>Disassembly: not separately labelled.</description></item>
    ///     </list>
    /// </remarks>
    public void ResumePaletteIndex(int paletteIndex)
    {
        _suspendedPaletteIndices[paletteIndex] = false;
    }

    /// <summary>Gives an entry a colour code.</summary>
    /// <param name="paletteIndex">Which entry, from 0 to 15.</param>
    /// <param name="value">The colour code to give it.</param>
    public void SetPaletteIndexValue(int paletteIndex, byte value)
    {
        _paletteValues[paletteIndex] = value;
    }

    /// <summary>Stops an entry's colour process, so that it neither moves on nor writes.</summary>
    /// <param name="paletteIndex">Which entry, from 0 to 15.</param>
    /// <remarks>
    ///     <para>
    ///         The player's death does this to the DECAY process and then colours entry 12 itself. <see cref="ResumePaletteIndex" />
    ///         lets the process run again afterwards.
    ///     </para>
    ///     <list type="bullet">
    ///         <item>
    ///             <description>
    ///                 Original source: <c>RRX7.ASM</c> <c>PDTH2</c>, the line <c>JSR KILL OFF DECAY</c>.
    ///             </description>
    ///         </item>
    ///         <item><description>Disassembly: not separately labelled.</description></item>
    ///     </list>
    /// </remarks>
    public void SuspendPaletteIndex(int paletteIndex)
    {
        _suspendedPaletteIndices[paletteIndex] = true;
    }

    /// <summary>Gives the colour-cycle effect the colours that entries 10 to 15 hold now.</summary>
    /// <param name="effect">The colour-cycle effect to give them to.</param>
    /// <remarks>
    ///     The effect takes six separate values, named <c>Live10</c> to <c>Live15</c>, because its shader parser does not
    ///     accept an array (notes §34).
    /// </remarks>
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
