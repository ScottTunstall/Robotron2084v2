using Microsoft.Xna.Framework;

namespace Robotron2084.Entities;

/// <summary>An entity that breaks into flying strips when it is killed (see <see cref="StripEffect"/>).</summary>
/// <remarks>ROM: <c>EXSTV</c> (RRX7.ASM) and <c>EXSTZ</c> (RRDX2.ASM) cut the dying object's sprite
/// into strips that fly apart. They use the sprite it was showing when it died (notes §35).</remarks>
public interface IExplodable : IEntity, IAnimationFrameSource
{
    /// <summary>The box the explosion starts in. It is the same as <see cref="IEntity.GetBounds()"/> unless the entity shows a different sprite when it dies.</summary>
    /// <remarks>ROM: the object's position, with the size of its current animation frame. The prog is different:
    /// its <c>PRGKIL</c> swaps in the 12x16 <c>PGXPIC</c> card where it stands.</remarks>
    Rectangle GetExplosionBounds() => GetBounds();
}
