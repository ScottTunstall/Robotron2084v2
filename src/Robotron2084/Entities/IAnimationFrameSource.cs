using Microsoft.Xna.Framework.Graphics;

namespace Robotron2084.Entities;

/// <summary>An entity that hands over the animation frame it is showing, for the appear and explosion effects.</summary>
/// <seealso cref="Hulk"/>
/// <remarks>ROM: the appear engine (<c>APSTV</c>/<c>APSTZ</c>) and the explosion engine
/// (<c>EXSTV</c>/<c>EXSTZ</c>) both copy an object's picture pointer (<c>OPICT</c>); notes §35.5.</remarks>
public interface IAnimationFrameSource
{
    /// <summary>The animation frame this entity is showing right now.</summary>
    Texture2D CurrentAnimationFrame { get; }
}
