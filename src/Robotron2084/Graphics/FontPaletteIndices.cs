namespace Robotron2084.Graphics;

/// <summary>
///     Arcade palette index classification for font drawing (notes §39):
///     palette indices 10-15 are the colour-cycling palette indices (advanced by the six colour
///     processes, §4.12). In the arcade, a blit whose blitter_mask colour is a
///     cycling palette index cycles with the live palette — so the port draws glyphs in
///     those palette indices through the colour-cycle effect with marker-baked variants,
///     while static palette indices (0-9) are plain tints.
/// </summary>
public static class FontPaletteIndices
{
    public const int FirstCyclingPaletteIndex = 10;

    public const int LastCyclingPaletteIndex = 15;

    public static bool IsCycling(int paletteIndex)
    {
        return paletteIndex is >= FirstCyclingPaletteIndex and <= LastCyclingPaletteIndex;
    }
}
