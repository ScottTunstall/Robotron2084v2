using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Rendering;

namespace Robotron2084.Entities;

/// <summary>
/// Anything whose current animation frame can be copied by the appear and explosion
/// effects — see <see cref="Explosion"/>. Those effects need the art and nothing else:
/// not the entity's behaviour, and not even its type.
/// </summary>
/// <remarks>
/// The arcade's engines (the appear, <c>APSTV</c> in RRX7.ASM and <c>APSTZ</c> in
/// RRDX2.ASM — "START AN APPEAR", notes §35.5; the explosion, <c>EXSTV</c>/<c>EXSTZ</c>)
/// do not care what an object is either, only which picture it is showing: the object
/// record holds a picture pointer (<c>OPICT</c>) and the effect blits those pixels.
///
/// This is a separate interface from <see cref="IExplodable"/> because the two are not
/// the same set of entities. The INDESTRUCTIBLE HULK never shatters, but it does
/// materialise at the start of a wave: the appear engine walks the robot list and creates
/// an appear record for every robot.
/// </remarks>
public interface IArtSource
{
    /// <summary>The texture frame this entity is showing right now.</summary>
    /// <param name="sprites">The shared sprite set, which holds the entity's frames.</param>
    /// <returns>The texture for the entity's current animation frame.</returns>
    Texture2D CurrentFrameArt(SpriteSet sprites);
}
