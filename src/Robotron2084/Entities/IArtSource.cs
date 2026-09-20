using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Rendering;

namespace Robotron2084.Entities;

/// <summary>An entity that hands over the picture it is showing, for the appear and explosion effects.</summary>
/// <seealso cref="Hulk"/>
/// <remarks>ROM: the appear engine (<c>APSTV</c>/<c>APSTZ</c>) and the explosion engine
/// (<c>EXSTV</c>/<c>EXSTZ</c>) both copy an object's picture pointer (<c>OPICT</c>); notes §35.5.</remarks>
public interface IArtSource
{
    /// <summary>The texture frame this entity is showing right now.</summary>
    /// <param name="sprites">The shared sprite set.</param>
    /// <returns>The texture for the entity's current animation frame.</returns>
    Texture2D CurrentFrameArt(SpriteSet sprites);
}
