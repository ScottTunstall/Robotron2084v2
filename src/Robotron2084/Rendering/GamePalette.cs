using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Robotron2084.Rendering;

/// <summary>
/// The live 16-slot screen palette. Slots 0-9 are fixed for the game; slots
/// 10-15 are the colour-cycling slots advanced by <see cref="PaletteAnimator"/>
/// (process tables decoded from the original source — arcade-fidelity-notes
/// §4.12). Sprite PNGs store six fixed marker colours for the cycling slots
/// (the de-duplicated service values); the colour-cycle shader remaps those
/// markers to the slots' current live colours.
/// </summary>
public sealed class GamePalette
{
    /// <summary>ROM default palette (CRTAB, ROM $DA51) — the initial 16 slots.</summary>
    public static readonly byte[] DefaultSlots =
    [
        0x00, 0x07, 0x17, 0xC7, 0x1F, 0x3F, 0x38, 0xC0,
        0xA4, 0xFF, 0x38, 0x17, 0xCC, 0x81, 0x81, 0x07,
    ];

    /// <summary>
    /// The six cycling-slot marker colour bytes (slots 10-15) exactly as the
    /// sprite PNGs encode them (RobotronPaletteService de-duplicated values —
    /// all 16 distinct, so a texel's slot is unambiguous for the shader).
    /// </summary>
    public static readonly byte[] CyclingSlotMarkers =
    [
        0xC4, 0xF4, 0xCC, 0x81, 0x45, 0x2F,
    ];

    private readonly byte[] _slots = (byte[])DefaultSlots.Clone();
    private readonly bool[] _suspended = new bool[16];

    /// <summary>The current 8-bit colour code of a slot (0-15).</summary>
    public int SlotValue(int slot) => _slots[slot];

    /// <summary>Writes a slot's current colour code (the colour processes do this).</summary>
    public void SetSlot(int slot, byte value) => _slots[slot] = value;

    /// <summary>
    /// Freezes a slot's colour process (the ROM's "KILL OFF DECAY": the player
    /// death stops the DECAY process and then drives slot 12 itself). A suspended
    /// slot's process neither steps nor writes; <see cref="ResumeSlot"/> is the
    /// ROM's `COLST`, which recreates the colour processes afterwards.
    /// </summary>
    public void SuspendSlot(int slot) => _suspended[slot] = true;

    /// <summary>Lets a slot's colour process run again (the ROM's `COLST`).</summary>
    public void ResumeSlot(int slot) => _suspended[slot] = false;

    /// <summary>True while a slot's colour process is stopped.</summary>
    public bool IsSlotSuspended(int slot) => _suspended[slot];

    /// <summary>The live RGB of a slot.</summary>
    public Color Color(int slot) => RobotronColor.FromByte(_slots[slot]);

    /// <summary>
    /// Pushes the live colours of the six cycling slots (10-15) into the
    /// colour-cycle effect's named uniforms <c>Live10</c>..<c>Live15</c>
    /// (notes §34: six named uniforms — the TPGParser rejects array syntax).
    /// </summary>
    public void UpdateEffectColors(Effect effect)
    {
        for (int slot = 10; slot <= 15; slot++)
        {
            Color c = Color(slot);
            effect.Parameters[$"Live{slot}"].SetValue(new Vector4(c.R / 255f, c.G / 255f, c.B / 255f, 1f));
        }
    }
}
