using Microsoft.Xna.Framework;

namespace Robotron2084.Entities;

/// <summary>
/// Implemented by entities that shatter into an explosion (RRX7 — notes 35)
/// when destroyed. <see cref="IArtSource.CurrentFrameArt"/> returns the art frame the
/// entity is CURRENTLY showing, so the explosion shatters the exact image the
/// player saw (the ROM stores the dying object's animation-frame pointer,
/// PICPTR, in the explosion slot).
/// </summary>
public interface IExplodable : IEntity, IArtSource
{
    /// <summary>
    /// The rectangle the explosion record copies. `EXSTV`/`EXSTZ` take the
    /// upper-left from the OBJECT (`UL = OBJX/OBJY`) and the W/H from the
    /// PICTURE DESCRIPTOR it is pointing at (`LDD ,X` — the descriptor starts
    /// `FCB width,height`), so the record's rect is the ART's own size at the
    /// object's corner. For every entity whose picture IS its collision box
    /// that is exactly <see cref="IEntity.Bounds"/>, which is why this
    /// defaults to it.
    ///
    /// A PROG is the one exception: `PRGKIL` swaps its picture descriptor to
    /// the 12×16 `PGXPIC` card and leaves `OBJX/OBJY` alone, so its explosion
    /// is the CARD's rect at the prog's corner — see <see cref="Prog"/>.
    /// </summary>
    Rectangle ExplosionBounds => Bounds;
}
