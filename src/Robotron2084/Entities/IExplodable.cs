using Microsoft.Xna.Framework;

namespace Robotron2084.Entities;

/// <summary>
/// Implemented by entities that shatter into an explosion when they are destroyed: the
/// playfield builds an <see cref="Explosion"/> from the entity's art as it takes it off
/// the field.
/// </summary>
/// <remarks>
/// The arcade's explosion engine is <c>EXSTV</c> (RRX7.ASM) and <c>EXSTZ</c> (RRDX2.ASM) —
/// a direction-dispatched strip explosion, notes §35. The engine is handed the frame the
/// entity is showing at the moment it dies, because the ROM copies the dying object's
/// picture pointer into the explosion record: the explosion is built from the image the
/// player actually saw.
/// </remarks>
public interface IExplodable : IEntity, IArtSource
{
    /// <summary>
    /// The rectangle the explosion is built at. Defaults to the entity's own
    /// <see cref="IEntity.Bounds"/>.
    /// </summary>
    /// <remarks>
    /// The ROM takes the upper-left corner from the OBJECT (`UL = OBJX/OBJY`) and the width
    /// and height from the picture descriptor it is pointing at (`LDD ,X` — every descriptor
    /// starts with `FCB width,height`), so the record's rectangle is the ART's own size at
    /// the object's corner. For every entity whose picture is also its collision box that is
    /// exactly <see cref="IEntity.Bounds"/>, which is why this defaults to it.
    ///
    /// The PROG is the one exception: `PRGKIL` swaps its picture descriptor to the 12×16
    /// `PGXPIC` card and leaves `OBJX/OBJY` alone, so its explosion is the CARD's rectangle
    /// at the prog's corner — see <see cref="Prog"/>.
    /// </remarks>
    Rectangle ExplosionBounds => Bounds;
}
