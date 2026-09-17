using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Rendering;

namespace Robotron2084.Entities;

/// <summary>
/// Anything whose CURRENT animation frame can be blitted by the ROM's appear /
/// explosion engines — the ROM stores an object's picture pointer (<c>OPICT</c>)
/// in the record and blits those pixels, so the engine needs the art, not the
/// entity's behaviour.
///
/// It exists separately from <see cref="IExplodable"/> because the two are not
/// the same set: the INDESTRUCTIBLE HULK never shatters, but it does MATERIALISE
/// at a wave start (RRG23's `APPEAR` walks the robot list and creates an appear
/// record for every robot).
/// </summary>
public interface IArtSource
{
    /// <summary>The texture frame currently on screen for this entity.</summary>
    Texture2D CurrentFrameArt(SpriteSet sprites);
}
