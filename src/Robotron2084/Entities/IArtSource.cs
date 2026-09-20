using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Rendering;

namespace Robotron2084.Entities;

/// <summary>
/// A minimal "give me your current picture" contract, implemented by any entity whose art can
/// be reused by the "appear" effect (an enemy materialising at the start of a wave) or the
/// "explosion" effect (see <see cref="IExplodable"/>), without those effects needing to know
/// anything else about the entity.
/// </summary>
/// <remarks>
/// ROM: the appear engine (<c>APSTV</c>/<c>APSTZ</c>, notes §35.5) and the explosion engine
/// (<c>EXSTV</c>/<c>EXSTZ</c>) both just copy an object's picture pointer (<c>OPICT</c>). This
/// is a separate interface from <see cref="IExplodable"/> because the two entity sets differ:
/// the indestructible hulk implements only this one, since it still needs to appear but never
/// explodes.
/// </remarks>
public interface IArtSource
{
    /// <summary>The texture frame this entity is showing right now.</summary>
    /// <param name="sprites">The shared sprite set, which holds the entity's frames.</param>
    /// <returns>The texture for the entity's current animation frame.</returns>
    Texture2D CurrentFrameArt(SpriteSet sprites);
}
