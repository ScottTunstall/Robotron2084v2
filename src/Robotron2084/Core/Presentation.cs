using Microsoft.Xna.Framework;

namespace Robotron2084.Core;

/// <summary>How the canvas is fitted into the window.</summary>
public enum ScaleMode
{
    /// <summary>The largest whole multiple of the canvas that fits — the crisp default for pixel art.</summary>
    Integer,

    /// <summary>The exact uniform fraction, so the canvas fills as much of the window as its shape allows.</summary>
    Fill,
}

/// <summary>Fits the canvas into a window's client area: one scale for both axes, centred, black bars where it does not fit.</summary>
/// <remarks>Port-only: the arcade's canvas IS its screen, so it has no fit to choose. The canvas is never
/// stretched — it keeps the shape <see cref="ScreenSize.SpecWidth"/>:<see cref="ScreenSize.SpecHeight"/>
/// gives it — and a client area smaller than the canvas crops it rather than scaling below 1x. Pure, so
/// any client size can be checked without a graphics device.</remarks>
/// <seealso cref="ScreenSize"/>
public static class Presentation
{
    /// <summary>The scale mode the next F8 press selects.</summary>
    public static ScaleMode Next(ScaleMode mode) =>
        mode == ScaleMode.Integer ? ScaleMode.Fill : ScaleMode.Integer;

    /// <summary>The uniform scale the canvas is drawn at, for a client area of any size.</summary>
    /// <remarks><see cref="ScaleMode.Integer"/> is the largest whole multiple that fits and never less
    /// than 1, so a window smaller than the canvas crops it; <see cref="ScaleMode.Fill"/> is the exact
    /// fraction of the limiting axis. A client area with no area at all falls back to 1, so nothing here
    /// can divide by zero.</remarks>
    public static float ScaleFor(int clientWidth, int clientHeight, ScaleMode mode)
    {
        if (clientWidth <= 0 || clientHeight <= 0)
        {
            return 1f;
        }

        if (mode == ScaleMode.Integer)
        {
            return ScreenSize.MaxIntegerScale(clientWidth, clientHeight);
        }

        return Math.Min((float)clientWidth / ScreenSize.Width, (float)clientHeight / ScreenSize.Height);
    }

    /// <summary>The canvas's destination rect in the client area: scaled uniformly and centred.</summary>
    /// <remarks>The leftover on the wider axis is the black bars. The rect starts off-screen (negative)
    /// when the canvas is larger than the client area, which crops it evenly.</remarks>
    public static Rectangle CanvasDestination(int clientWidth, int clientHeight, ScaleMode mode)
    {
        float scale = ScaleFor(clientWidth, clientHeight, mode);
        int width = Math.Max(1, (int)MathF.Round(ScreenSize.Width * scale));
        int height = Math.Max(1, (int)MathF.Round(ScreenSize.Height * scale));
        return new Rectangle((clientWidth - width) / 2, (clientHeight - height) / 2, width, height);
    }
}
