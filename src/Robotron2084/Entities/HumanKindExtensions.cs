using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Graphics;
using Robotron2084.Tuning;

namespace Robotron2084.Entities;

/// <summary>What a family member's kind decides: the size of its box and the animation frames it is drawn with.</summary>
public static class HumanKindExtensions
{
    /// <summary>The walk animation frames the member is drawn with (notes §49).</summary>
    /// <param name="kind">The member.</param>
    /// <param name="sprites">The sprite set that holds them.</param>
    public static Texture2D[] GetAnimationFrames(this HumanKind kind, SpriteSet sprites) => kind switch
    {
        HumanKind.Mikey => sprites.MikeyAnimationFrames,
        HumanKind.Mommy => sprites.MommyAnimationFrames,
        _ => sprites.DaddyAnimationFrames,
    };

    /// <summary>The member's collision box in arcade pixels, before scaling.</summary>
    /// <param name="kind">The member.</param>
    public static (int Width, int Height) GetArcadeCollisionSize(this HumanKind kind) => kind switch
    {
        HumanKind.Mikey => CollisionSizes.MikeyCollisionSize,
        HumanKind.Mommy => CollisionSizes.MommyCollisionSize,
        _ => CollisionSizes.DaddyCollisionSize,
    };
}
