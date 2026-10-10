using Microsoft.Xna.Framework.Graphics;

namespace Robotron2084.Entities;

/// <summary>An entity that can hand over the animation frame it is showing. The appear and explosion effects need it.</summary>
/// <seealso cref="Hulk"/>
/// <remarks>ROM: the appear routines (<c>APSTV</c>/<c>APSTZ</c>) and the explosion routines
/// (<c>EXSTV</c>/<c>EXSTZ</c>) both copy an object's animation frame pointer (<c>OPICT</c>); notes §35.5.</remarks>
public interface IAnimationFrameSource
{
    /// <summary>The animation frame this entity is showing right now.</summary>
    Texture2D GetCurrentAnimationFrame();
}
