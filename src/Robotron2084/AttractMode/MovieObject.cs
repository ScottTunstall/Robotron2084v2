using Robotron2084.Core;

namespace Robotron2084.AttractMode;

/// <summary>
/// One object in the attract movie (notes §95.3): a sprite, a position, a
/// velocity, and (for the walking characters) a walker state. The movie's
/// objects are the ROM's `OBJ` blocks — plain data; the process that drives one
/// lives in <see cref="AttractObjectMachine"/>.
///
/// Coordinates are the ROM's: <see cref="XSubpixels"/> counts 1/256 COLUMNS and
/// <see cref="YSubpixels"/> counts 1/256 ROWS, so the integer column is
/// <c>XSubpixels / 256</c> (one column = 2 arcade px) and the integer row is
/// <c>YSubpixels / 256</c>. That is the ROM's `OX16`/`OY16` pair, which packs into its
/// screen address as `column*256 + row`.
/// </summary>
public sealed record MovieObject
{
    /// <summary>Creates an object showing one animation frame at the given ROM coordinates.</summary>
    public MovieObject(MovieDescriptor? descriptor, int animationFrameIndex, int xSubpixels, int ySubpixels)
    {
        Descriptor = descriptor;
        AnimationFrameIndex = animationFrameIndex;
        XSubpixels = xSubpixels;
        YSubpixels = ySubpixels;
    }

    /// <summary>The sprite set. Null for the movie's laser bolts (see <see cref="IsLaser"/>).</summary>
    public MovieDescriptor? Descriptor { get; set; }

    /// <summary>A left/right laser bolt fired by LFIRE/RFIRE — drawn as the laser, killed on its timer.</summary>
    public bool IsLaser { get; set; }

    /// <summary>ROM frames a laser bolt stays alive (LFIRE/RFIRE's first operand).</summary>
    public int LaserRomFramesLeft { get; set; }

    /// <summary>Which animation frame of the descriptor's animation the object is showing.</summary>
    public int AnimationFrameIndex { get; set; }

    /// <summary>X in 1/256 columns (256 = one column = 2 arcade px).</summary>
    public int XSubpixels { get; set; }

    /// <summary>Y in 1/256 rows (256 = one row = 1 arcade px).</summary>
    public int YSubpixels { get; set; }

    /// <summary>Velocity in 1/256 columns per ROM frame (the ROM's OXV).</summary>
    public int XVelocitySubpixels { get; set; }

    /// <summary>Velocity in 1/256 rows per ROM frame (the ROM's OYV).</summary>
    public int YVelocitySubpixels { get; set; }

    /// <summary>
    /// Whether the object is on the ROM's object LIST — HIB clears it (the object
    /// keeps its identity but is neither drawn nor moved) and REBORN sets it
    /// again. MONO hides its object for the same reason.
    /// </summary>
    public bool IsOnList { get; set; } = true;

    /// <summary>The object's process(es) have all ended.</summary>
    public bool IsDead { get; set; }

    /// <summary>MONO: the box is filled in this palette slot (0 = no box).</summary>
    public int MonoBoxSlot { get; set; }

    /// <summary>MONO: the object is drawn as a solid silhouette in this palette slot.</summary>
    public int MonoSilhouetteSlot { get; set; }

    /// <summary>MONO: draw the BRAIN inside the box too (the ROM's `DMAON` "brain in the square").</summary>
    public bool ShowsMonoBrain { get; set; }

    /// <summary>MONO is running (the renderer draws the box + solid silhouette).</summary>
    public bool IsMonoActive { get; set; }

    /// <summary>RPROG: the shake's row offset, in rows (the ROM pokes `OBJY` directly).</summary>
    public int ShakeRowOffset { get; set; }

    /// <summary>The object's drawn position as arcade pixels — the ROM's `OBJX`/`OBJY`.</summary>
    /// <summary>The integer column (the ROM's `OBJX`).</summary>
    public int Column => XSubpixels >> 8;

    /// <summary>The integer row, including the shake (the ROM's `OBJY`).</summary>
    public int Row => (YSubpixels >> 8) + ShakeRowOffset;

    /// <summary>The column as arcade pixels.</summary>
    public int ArcadeX => Column * ScreenSize.ArcadePixelsPerColumn;

    /// <summary>The row as arcade pixels.</summary>
    public int ArcadeY => Row;
}
