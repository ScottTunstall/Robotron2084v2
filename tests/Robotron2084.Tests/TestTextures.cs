using Robotron2084.Graphics;

namespace Robotron2084.Tests;

/// <summary>The one headless texture set the whole suite shares.</summary>
internal static class TestTextures
{
    /// <summary>The shared set; see <see cref="NoTextureSource" /> for what it does and does not hold.</summary>
    public static readonly TextureSet SharedTextureSet = new(new NoTextureSource());
}
