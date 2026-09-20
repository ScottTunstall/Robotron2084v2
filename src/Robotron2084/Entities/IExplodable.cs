using Microsoft.Xna.Framework;

namespace Robotron2084.Entities;

/// <summary>
/// Implemented by entities that shatter into a fragmenting <see cref="Explosion"/> on death
/// (rather than just vanishing, or leaving a marker like <see cref="ScoreBurst"/>). The
/// playfield builds the explosion from the entity's current picture via
/// <see cref="IArtSource.CurrentFrameArt"/>, so the flying pieces are recognisably the enemy
/// that just died.
/// </summary>
/// <remarks>
/// ROM: <c>EXSTV</c> (RRX7.ASM) / <c>EXSTZ</c> (RRDX2.ASM) slice the dying object's picture
/// into strips that fly apart based on where in the picture each strip came from (notes §35),
/// using whatever picture the object was showing at the moment of death.
/// </remarks>
public interface IExplodable : IEntity, IArtSource
{
    /// <summary>
    /// The screen rectangle the explosion is built at. Defaults to <see cref="IEntity.Bounds"/>,
    /// which is correct for almost every entity; override only if the death picture's size
    /// differs from the normal collision box.
    /// </summary>
    /// <remarks>
    /// ROM: the explosion's top-left is the object's position, and its size comes from whatever
    /// picture the object is currently showing — the default matches this whenever the picture
    /// and collision box are the same size. Exception: PROG's `PRGKIL` swaps in the 12x16
    /// `PGXPIC` card without moving `OBJX/OBJY`, so its explosion is that card's rectangle at
    /// the prog's corner (see <see cref="Prog"/>).
    /// </remarks>
    Rectangle ExplosionBounds => Bounds;
}
