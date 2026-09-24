using Robotron2084.Core;

namespace Robotron2084.Level.Attract;

/// <summary>
/// One object in the attract movie (notes §95.3): a picture, a position, a
/// velocity, and (for the walking characters) a walker state. The movie's
/// objects are the ROM's `OBJ` blocks — plain data; the process that drives one
/// lives in <see cref="AttractObjectMachine"/>.
///
/// Coordinates are the ROM's: <see cref="X"/> counts 1/256 COLUMNS and
/// <see cref="Y"/> counts 1/256 ROWS, so the integer column is
/// <c>X / 256</c> (one column = 2 arcade px) and the integer row is
/// <c>Y / 256</c>. That is the ROM's `OX16`/`OY16` pair, which packs into its
/// screen address as `column*256 + row`.
/// </summary>
public sealed class MovieObject
{
    public MovieObject(MovieDescriptor? descriptor, int imageIndex, int x, int y)
    {
        Descriptor = descriptor;
        ImageIndex = imageIndex;
        X = x;
        Y = y;
    }

    /// <summary>The picture set. Null for the movie's laser bolts (see <see cref="IsLaser"/>).</summary>
    public MovieDescriptor? Descriptor { get; set; }

    /// <summary>A left/right laser bolt fired by LFIRE/RFIRE — drawn as the laser, killed on its timer.</summary>
    public bool IsLaser { get; set; }

    /// <summary>Which way a laser bolt travels: true = right (RFIRE).</summary>
    public bool LaserRight { get; set; }

    /// <summary>ROM frames a laser bolt stays alive (LFIRE/RFIRE's first operand).</summary>
    public int LaserFramesLeft { get; set; }

    public int ImageIndex { get; set; }

    /// <summary>X in 1/256 columns (256 = one column = 2 arcade px).</summary>
    public int X { get; set; }

    /// <summary>Y in 1/256 rows (256 = one row = 1 arcade px).</summary>
    public int Y { get; set; }

    /// <summary>Velocity in 1/256 columns per ROM frame (the ROM's OXV).</summary>
    public int XVelocity { get; set; }

    /// <summary>Velocity in 1/256 rows per ROM frame (the ROM's OYV).</summary>
    public int YVelocity { get; set; }

    /// <summary>
    /// Whether the object is on the ROM's object LIST — HIB clears it (the object
    /// keeps its identity but is neither drawn nor moved) and REBORN sets it
    /// again. MONO hides its object for the same reason.
    /// </summary>
    public bool OnList { get; set; } = true;

    /// <summary>The object's process(es) have all ended.</summary>
    public bool Dead { get; set; }

    /// <summary>MONO: the box is filled in this palette slot (0 = no box).</summary>
    public int MonoBoxSlot { get; set; }

    /// <summary>MONO: the object is drawn as a solid silhouette in this palette slot.</summary>
    public int MonoImageSlot { get; set; }

    /// <summary>MONO: draw the BRAIN inside the box too (the ROM's `DMAON` "brain in the square").</summary>
    public bool MonoBrain { get; set; }

    /// <summary>MONO is running (the renderer draws the box + solid silhouette).</summary>
    public bool MonoActive { get; set; }

    /// <summary>RPROG: the shake's row offset, in rows (the ROM pokes `OBJY` directly).</summary>
    public int ShakeRowOffset { get; set; }

    /// <summary>The object's drawn position as arcade pixels — the ROM's `OBJX`/`OBJY`.</summary>
    public int Column => X >> 8;

    public int Row => (Y >> 8) + ShakeRowOffset;

    public int ArcadeX => Column * ScreenSize.ArcadePixelsPerColumn;

    public int ArcadeY => Row;
}
