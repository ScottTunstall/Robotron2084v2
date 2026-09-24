using Robotron2084.Rendering;

namespace Robotron2084.Tests;

/// <summary>The one artwork-free sprite set the whole suite shares.</summary>
internal static class TestSprites
{
    /// <summary>The shared set; see <see cref="NoSpriteSource"/> for what it does and does not hold.</summary>
    public static readonly SpriteSet Shared = new(new NoSpriteSource());
}
