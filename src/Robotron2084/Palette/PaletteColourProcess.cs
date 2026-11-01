namespace Robotron2084.Palette;

/// <summary>One of the six colour processes.</summary>
internal sealed class PaletteColourProcess
{
    /// <summary>How many fiftieths of a second pass between one cycle of the process and the next.</summary>
    public int ArcadeFramesPerCycle = 1;

    /// <summary>What the process's timer has gathered so far, in clock units.</summary>
    public int ClockUnits;

    /// <summary>The place in the colour table the process is at.</summary>
    public int ColourIndex;

    /// <summary>The colours the process goes through.</summary>
    public byte[] ColourTable = [];

    /// <summary>The palette entry the process writes.</summary>
    public int PaletteIndex;
}
