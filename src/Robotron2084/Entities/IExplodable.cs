using Microsoft.Xna.Framework;

namespace Robotron2084.Entities;

/// <summary>An entity that shatters into a strip <see cref="Explosion"/> when it dies.</summary>
/// <remarks>ROM: <c>EXSTV</c> (RRX7.ASM) / <c>EXSTZ</c> (RRDX2.ASM) slice the dying object's
/// picture into strips that fly apart, using the picture it showed at the moment of death (notes §35).</remarks>
public interface IExplodable : IEntity, IArtSource
{
    /// <summary>The box the explosion is built at; <see cref="IEntity.Bounds"/> unless the death picture differs.</summary>
    /// <remarks>ROM: position plus the current picture's size — except PROG, whose <c>PRGKIL</c>
    /// swaps in the 12x16 <c>PGXPIC</c> card where it stands.</remarks>
    Rectangle ExplosionBounds => Bounds;
}
