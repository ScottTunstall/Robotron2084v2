using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Graphics;
using Robotron2084.Tuning;

namespace Robotron2084.Entities;

/// <summary>What depends on which family member it is: how big it is, and the animation frames it is drawn with.</summary>
public static class HumanKindExtensions
{
    /// <summary>The walk animation frames the family member is drawn with (notes §49).</summary>
    /// <param name="kind">Which family member.</param>
    /// <param name="textureSet">The texture set that holds them.</param>
    public static Texture2D[] GetAnimationFrames(this HumanKind kind, TextureSet textureSet)
    {
        return kind switch
        {
            HumanKind.Mikey => textureSet.MikeyAnimationFrameTextures,
            HumanKind.Mommy => textureSet.MommyAnimationFrameTextures,
            _ => textureSet.DaddyAnimationFrameTextures
        };
    }

    /// <summary>How big the family member is, in arcade pixels.</summary>
    /// <param name="kind">Which family member.</param>
    public static (int Width, int Height) GetArcadeCollisionSize(this HumanKind kind)
    {
        return kind switch
        {
            HumanKind.Mikey => CollisionSizes.MikeyCollisionSize,
            HumanKind.Mommy => CollisionSizes.MommyCollisionSize,
            _ => CollisionSizes.DaddyCollisionSize
        };
    }
}
