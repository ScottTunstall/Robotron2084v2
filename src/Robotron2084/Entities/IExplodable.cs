using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Rendering;

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
}
