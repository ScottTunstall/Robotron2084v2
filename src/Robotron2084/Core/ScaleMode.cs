namespace Robotron2084.Core;

/// <summary>How the canvas is fitted into the window.</summary>
public enum ScaleMode
{
    /// <summary>The largest whole multiple of the canvas that fits — the crisp default for pixel art.</summary>
    Integer,

    /// <summary>The exact uniform fraction, so the canvas fills as much of the window as its shape allows.</summary>
    Fill,
}
