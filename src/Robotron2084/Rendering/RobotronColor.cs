using Microsoft.Xna.Framework;

namespace Robotron2084.Rendering;

/// <summary>
/// Robotron 8-bit colour byte (BBGGGRRR) to RGB conversion.
/// Ported verbatim from WmsGfxSpriteEditor's
/// <c>RobotronPaletteService</c> (Shared/Palettes), which credits Sean
/// Riddle's Williams ripper algorithm
/// (https://www.seanriddle.com/ripper.html). No 6809/Williams hardware
/// modelling — plain RGB (arcade-fidelity-notes §0.6).
/// Accepts ANY 8-bit code, not just the 16 default palette entries (the
/// colour-cycle processes write intermediate codes at runtime).
/// </summary>
public static class RobotronColor
{
    /// <summary>Converts a Robotron colour byte (BBGGGRRR) to its RGB value.</summary>
    public static Color FromByte(byte value)
    {
        // The red component is stored in bits 0-2.
        int red = (value & 0x07) << 1;
        if (red > 6)
        {
            red++;
        }

        // The green component is stored in bits 3-5.
        int green = (value & 0x38) >> 2;
        if (green > 6)
        {
            green++;
        }

        // The blue component is stored in the top 2 bits.
        int blue = ((value & 0xC0) >> 6) * 5;

        return new Color(
            (byte)Math.Min(255, red << 4),
            (byte)Math.Min(255, green << 4),
            (byte)Math.Min(255, blue << 4));
    }
}
