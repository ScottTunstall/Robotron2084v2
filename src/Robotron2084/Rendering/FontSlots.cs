namespace Robotron2084.Rendering;

/// <summary>
/// Arcade palette-slot classification for font drawing (notes §39):
/// slots 10-15 are the colour-cycling slots (advanced by the six colour
/// processes, §4.12). In the arcade, a blit whose blitter_mask colour is a
/// cycling slot cycles with the live palette — so the port draws glyphs in
/// those slots through the colour-cycle effect with marker-baked variants,
/// while static slots (0-9) are plain tints.
/// </summary>
public static class FontSlots
{
    public const int FirstCyclingSlot = 10;

    public const int LastCyclingSlot = 15;

    public static bool IsCycling(int slot) => slot is >= FirstCyclingSlot and <= LastCyclingSlot;
}
