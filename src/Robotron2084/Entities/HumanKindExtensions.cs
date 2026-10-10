using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Graphics;
using Robotron2084.Tuning;

namespace Robotron2084.Entities;

/// <summary>What depends on which family member it is: how big it is, and the animation frames it is drawn with.</summary>
public static class HumanKindExtensions
{
    /// <summary>The walk animation frames the family member is drawn with (notes §49).</summary>
    /// <param name="kind">Which family member.</param>
    /// <param name="sprites">The sprite set that holds them.</param>
    public static Texture2D[] GetAnimationFrames(this HumanKind kind, SpriteSet sprites) => kind switch
    {
        HumanKind.Mikey => sprites.MikeyAnimationFrames,
        HumanKind.Mommy => sprites.MommyAnimationFrames,
        _ => sprites.DaddyAnimationFrames,
    };

    /// <summary>How big the family member is, in arcade pixels. This size is used to tell what the family member touches.</summary>
    /// <param name="kind">Which family member.</param>
    public static (int Width, int Height) GetArcadeCollisionSize(this HumanKind kind) => kind switch
    {
        HumanKind.Mikey => CollisionSizes.MikeyCollisionSize,
        HumanKind.Mommy => CollisionSizes.MommyCollisionSize,
        _ => CollisionSizes.DaddyCollisionSize,
    };
}
