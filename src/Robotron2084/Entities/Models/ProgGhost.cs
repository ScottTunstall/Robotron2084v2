using Robotron2084.Core;
using Robotron2084.Core.Models;

namespace Robotron2084.Entities.Models;

/// <summary>One ghost: a place the prog has left, and the animation frame the prog was showing there.</summary>
/// <remarks>The arcade draws a ghost once and never draws it again, so a ghost keeps the animation frame it started with.</remarks>
/// <param name="Position">The top-left corner of the place the prog left.</param>
/// <param name="AnimationFrameIndex">The animation frame the prog was showing there.</param>
internal readonly record struct ProgGhost(IntVector2 Position, int AnimationFrameIndex);
