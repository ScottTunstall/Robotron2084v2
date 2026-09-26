namespace Robotron2084.Tuning;

/// <summary>The sizes of every entity's collision box, in arcade or spec pixels as each constant says.</summary>
public static class CollisionSizes
{
    /// <summary>
    /// ROM `SHLP1 FCB 4,7` — the shell's picture is 4 BYTES wide (a byte is 2 px)
    /// by 7 rows = **8x7 px**, and the ROM bounds and collides a projectile against
    /// the picture it is showing (notes §53). The port used the spec's square 4x4
    /// missile box while drawing a wrongly-sized 14x16 extraction into it.
    /// </summary>
    public static readonly (int Width, int Height) TankShellCollisionSize = (8, 7);

    // Playfield layout
    public const int PlayfieldMarginSpecPixels = 20;

    // The spec's 16x16 entity box is not a collision box — see the
    // per-entity *CollisionSize table below. Still used for the spawn-candidate grid
    // (the largest entity box is 16 spec-px wide) and the picture factory
    // pattern size.
    public const int EntitySizeSpecPixels = 16; // spec-stated (16x16 entities)

    public const int MissileSizeSpecPixels = 4; // spec-stated (4x4 lasers/missiles)

    public const int WallThicknessSpecPixels = 4; // spec-stated ("4px width" wall)

    // The ROM (COL0V, RRS22 ~1038) intersects each object's PICTURE
    // dimensions — ADDD OBJW,U / ADDD [OPICT,X] — not a fixed square: the
    // collision box IS the picture. Sizes are the live-frame dimensions from
    // docs/sprite-map.md, in arcade px; apply ScreenSize.Scaled at the use
    // site. Lasers/sparks keep the spec's 4x4 box (spec-stated).
    public static readonly (int Width, int Height) PlayerCollisionSize = (8, 12);

    public static readonly (int Width, int Height) GruntCollisionSize = (10, 13);

    public static readonly (int Width, int Height) HulkCollisionSize = (14, 16);

    public static readonly (int Width, int Height) SpheroidCollisionSize = (16, 15);

    public static readonly (int Width, int Height) EnforcerCollisionSize = (10, 11);

    public static readonly (int Width, int Height) QuarkCollisionSize = (16, 15);

    public static readonly (int Width, int Height) TankCollisionSize = (14, 16);

    public static readonly (int Width, int Height) ElectrodeCollisionSize = (10, 9);

    public static readonly (int Width, int Height) MomCollisionSize = (8, 14);

    public static readonly (int Width, int Height) DadCollisionSize = (10, 13);

    public static readonly (int Width, int Height) MikeyCollisionSize = (6, 11);

    public static readonly (int Width, int Height) SkullCollisionSize = (12, 11);

    // Brain picture (notes (18) RRB10 decode): 7 bytes x 16 rows =
    // 14x16 px; a prog re-draws its converted human's animation frames and keeps that
    // human's per-kind box (see HumanKindExtensions.ArcadeCollisionSize); cruise missile =
    // the ROM's "FAT PHONY GUY" box (the 6x6 head inset 1 px = 4x4).
    public static readonly (int Width, int Height) BrainCollisionSize = (14, 16);

    /// <summary>
    /// ROM CMMOV: `SUBD #$0101 / STD OBJX,X` — the FAT collision box, `CMPIC
    /// FCB 3,4` = 3 BYTES x 4 rows = **6x4 px**, whose top-left sits ONE COLUMN
    /// and ONE ROW up-left of the missile's true coordinate. The port used a 4x4
    /// box with no offset. (The box is collision-only: `CMPIC`/`CMP1` are never
    /// blitted — notes §49.)
    /// </summary>
    public static readonly (int Width, int Height) CruiseMissileCollisionSize = (6, 4);

    /// <summary>ROM `SUBD #$0101`: the fat box sits -1 column / -1 row from the true coordinate.</summary>
    public const int CruiseMissileBoxOffsetColumns = -1;

    /// <summary>ROM `SUBD #$0101`: the row component of the fat box offset.</summary>
    public const int CruiseMissileBoxOffsetRows = -1;

    /// <summary>
    /// ROM PGXPIC: `FCB 6,16` — the prog's PHONY burst card, 6 BYTES x 16 rows =
    /// 12x16 px. PRGKIL swaps the object's picture descriptor to this card and then
    /// calls the ordinary `EXST`, so the card is what the strip explosion shatters,
    /// and `EXSTV` sizes its record from the picture — see
    /// <see cref="Entities.Prog.ExplosionBounds"/> (notes §90).
    /// </summary>
    public static readonly (int Width, int Height) ProgBurstSize = (12, 16);
}
