namespace Robotron2084.Palette;

/// <summary>One of the page's colour processes.</summary>
internal sealed class HighScoreColourProcess
{
    /// <summary>How many fiftieths of a second pass between one cycle of the process and the next.</summary>
    public int ArcadeFramesPerCycle;

    /// <summary>What the process's timer has gathered so far, in clock units.</summary>
    public int ClockUnits;

    /// <summary>The place in the colour table the process is at.</summary>
    public int ColourIndex;

    /// <summary>The colours the process goes through.</summary>
    public byte[] ColourTable = [];

    /// <summary>The palette entry the process writes, or <see cref="HighScorePalette.LoopPaletteIndex" /> for the wall's process.</summary>
    public int PaletteIndex;
}
