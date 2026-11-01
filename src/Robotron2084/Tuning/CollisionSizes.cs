namespace Robotron2084.Tuning;

/// <summary>The sizes of every entity's collision box, in arcade pixels or in columns and rows, as each constant says.</summary>
/// <remarks>
///     Original source: <c>RRS22.ASM</c> <c>COL0V</c> (about line 1038). The ROM does not use a fixed square. It
///     intersects each object's own sprite size (<c>ADDD OBJW,U</c> and <c>ADDD [OPICT,X]</c>), so the collision box
///     is the sprite. The sizes here are the sizes of the animation frames that are showing, taken from
///     <c>docs/sprite-map.md</c>, in arcade pixels. Lasers and sparks keep the spec's 4 by 4 box.
/// </remarks>
public static class CollisionSizes
{
    /// <summary>How many columns left of the cruise missile's true coordinate its collision box starts.</summary>
    /// <remarks>Original source: <c>RRB10.ASM</c> <c>CMMOV</c>, <c>SUBD #$0101</c>.</remarks>
    public const int CruiseMissileBoxOffsetColumns = -1;

    /// <summary>How many rows above the cruise missile's true coordinate its collision box starts.</summary>
    /// <remarks>Original source: <c>RRB10.ASM</c> <c>CMMOV</c>, <c>SUBD #$0101</c>.</remarks>
    public const int CruiseMissileBoxOffsetRows = -1;

    /// <summary>The width and height of the grid cell used to place entities and of the pattern the texture factory draws, in arcade pixels.</summary>
    /// <remarks>
    ///     This is the largest entity box in width. It is not a collision box: the spec's 16 by 16 entity box is replaced
    ///     by the per-entity sizes below.
    /// </remarks>
    public const int EntitySizeArcadePixels = 16;

    /// <summary>The width and height of a missile's collision box, in arcade pixels.</summary>
    /// <remarks>The arcade spec gives lasers and missiles a 4 by 4 box.</remarks>
    public const int MissileSizeArcadePixels = 4;

    /// <summary>How far the playfield's edge sits in from the edge of the screen, in arcade pixels.</summary>
    public const int PlayfieldMarginArcadePixels = 20;

    /// <summary>How thick the wall around the playfield is, in arcade pixels.</summary>
    /// <remarks>The arcade spec gives the wall a width of 4 pixels.</remarks>
    public const int WallThicknessArcadePixels = 4;

    /// <summary>The Berzerk robot's animation frame canvas, in arcade pixels.</summary>
    /// <remarks>The Berzerk robot is a new entity, so it has no ROM source.</remarks>
    public static readonly (int Width, int Height) BerzerkRobotCollisionSize = (8, 12);

    /// <summary>The brain's collision box, in arcade pixels.</summary>
    /// <remarks>
    ///     The brain sprite is 7 bytes wide by 16 rows tall (notes §18, <c>RRB10.ASM</c> decode), which is 14 by 16
    ///     pixels. A prog draws the animation frames of the family member it was converted from, and keeps that family
    ///     member's box (see <c>FamilyMemberKindExtensions.ArcadeCollisionSize</c>).
    /// </remarks>
    public static readonly (int Width, int Height) BrainCollisionSize = (14, 16);

    /// <summary>The cruise missile's collision box, in arcade pixels.</summary>
    /// <remarks>
    ///     Original source: <c>RRB10.ASM</c> <c>CMMOV</c>, <c>SUBD #$0101 / STD OBJX,X</c>. The box is the "fat" box
    ///     of <c>CMPIC</c>, <c>FCB 3,4</c>, which is 3 bytes by 4 rows and so 6 by 4 pixels. Its top-left sits one column
    ///     and one row up and to the left of the missile's true coordinate (see
    ///     <see cref="CruiseMissileBoxOffsetColumns" />). The box is for collisions only: <c>CMPIC</c> and <c>CMP1</c>
    ///     are never blitted (notes §49).
    /// </remarks>
    public static readonly (int Width, int Height) CruiseMissileCollisionSize = (6, 4);

    /// <summary>The father's collision box, in arcade pixels.</summary>
    public static readonly (int Width, int Height) DaddyCollisionSize = (10, 13);

    /// <summary>
    ///     How big each shape of electrode is, in arcade pixels, in the order of the shapes. Most are 10 by 9. The thin
    ///     one is 6 by 9, the "2084" one is 18 by 7, and the last is 10 by 10.
    /// </summary>
    /// <remarks>
    ///     Disassembly: the first two bytes of each 16-byte entry from <c>$3B05</c> to <c>$3B85</c>, which are the width
    ///     in bytes (two arcade pixels each) and the height in rows: <c>05 09</c>, and <c>03 09</c> at <c>$3B45</c>,
    ///     <c>09 07</c> at <c>$3B65</c> and <c>05 0A</c> at <c>$3B85</c>.
    /// </remarks>
    public static readonly (int Width, int Height)[] ElectrodeCollisionSizeByVariant =
    [
        (10, 9), (10, 9), (10, 9), (10, 9), (6, 9), (10, 9), (18, 7), (10, 9), (10, 10)
    ];

    /// <summary>The enforcer's collision box, in arcade pixels.</summary>
    public static readonly (int Width, int Height) EnforcerCollisionSize = (10, 11);

    /// <summary>Gorf's collision box, which is the size of its animation frame, in arcade pixels.</summary>
    /// <remarks>Gorf is a new entity, so it has no ROM source.</remarks>
    public static readonly (int Width, int Height) GorfCollisionSize = (15, 20);

    /// <summary>The grunt's collision box, in arcade pixels.</summary>
    public static readonly (int Width, int Height) GruntCollisionSize = (10, 13);

    /// <summary>The hulk's collision box, in arcade pixels.</summary>
    public static readonly (int Width, int Height) HulkCollisionSize = (14, 16);

    /// <summary>The son's collision box, in arcade pixels.</summary>
    public static readonly (int Width, int Height) MikeyCollisionSize = (6, 11);

    /// <summary>The mother's collision box, in arcade pixels.</summary>
    public static readonly (int Width, int Height) MommyCollisionSize = (8, 14);

    /// <summary>The player's collision box, in arcade pixels.</summary>
    public static readonly (int Width, int Height) PlayerCollisionSize = (8, 12);

    /// <summary>The size of the prog's burst card, in arcade pixels.</summary>
    /// <remarks>
    ///     Original source: <c>RRB10.ASM</c> <c>PGXPIC</c>, <c>FCB 6,16</c>: the prog's PHONY burst card, which is 6 bytes
    ///     by 16 rows and so 12 by 16 pixels. <c>PRGKIL</c> swaps the object's sprite descriptor to this card and then
    ///     calls the ordinary <c>EXST</c>, so the card is what the strip explosion shatters, and <c>EXSTV</c> sizes its
    ///     record from the sprite. See <see cref="Robotron2084.Entities.Prog.GetExplosionBounds" /> (notes §90).
    /// </remarks>
    public static readonly (int Width, int Height) ProgBurstSize = (12, 16);

    /// <summary>The quark's collision box, in arcade pixels.</summary>
    public static readonly (int Width, int Height) QuarkCollisionSize = (16, 15);

    /// <summary>The skull's collision box, in arcade pixels.</summary>
    public static readonly (int Width, int Height) SkullCollisionSize = (12, 11);

    /// <summary>The spheroid's collision box, in arcade pixels.</summary>
    public static readonly (int Width, int Height) SpheroidCollisionSize = (16, 15);

    /// <summary>The tank's collision box, in arcade pixels.</summary>
    public static readonly (int Width, int Height) TankCollisionSize = (14, 16);

    /// <summary>The tank shell's collision box, in arcade pixels.</summary>
    /// <remarks>
    ///     Original source: <c>SHLP1</c>, <c>FCB 4,7</c>: the shell's sprite is 4 bytes wide (a byte is two pixels) by
    ///     7 rows, which is 8 by 7 pixels. The ROM bounds and collides a projectile against the animation frame it is
    ///     showing (notes §53).
    /// </remarks>
    public static readonly (int Width, int Height) TankShellCollisionSize = (8, 7);
}
