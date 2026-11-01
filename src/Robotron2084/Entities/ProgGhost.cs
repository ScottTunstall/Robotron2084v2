using Robotron2084.Core;

namespace Robotron2084.Entities;

/// <summary>One ghost: a place the prog has left, and the animation frame the prog was showing there.</summary>
/// <remarks>The arcade draws a ghost once and never draws it again, so a ghost keeps the animation frame it started with.</remarks>
internal readonly record struct ProgGhost(IntVector2 Position, int AnimationFrameIndex);
