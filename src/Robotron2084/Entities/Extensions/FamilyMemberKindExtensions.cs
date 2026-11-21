using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Entities.Models;
using Robotron2084.Graphics;
using Robotron2084.Tuning;

namespace Robotron2084.Entities.Extensions;

/// <summary>What depends on which family member it is: how big it is, and the animation frames it is drawn with.</summary>
public static class FamilyMemberKindExtensions
{
    /// <summary>Gets the walk animation frames the family member is drawn with.</summary>
    /// <remarks>See notes §49.</remarks>
    /// <param name="kind">Which family member.</param>
    /// <param name="textureSet">The texture set that holds them.</param>
    /// <exception cref="ArgumentOutOfRangeException">The kind is not one of the three family members.</exception>
    public static Texture2D[] GetAnimationFrames(this FamilyMemberKind kind, TextureSet textureSet)
    {
        return kind switch
        {
            FamilyMemberKind.Mikey => textureSet.MikeyAnimationFrameTextures,
            FamilyMemberKind.Mommy => textureSet.MommyAnimationFrameTextures,
            FamilyMemberKind.Daddy => textureSet.DaddyAnimationFrameTextures,
            _ => throw CreateUnknownKindException(kind)
        };
    }

    /// <summary>How big the family member is, in arcade pixels.</summary>
    /// <param name="kind">Which family member.</param>
    /// <exception cref="ArgumentOutOfRangeException">The kind is not one of the three family members.</exception>
    public static (int Width, int Height) GetArcadeCollisionSize(this FamilyMemberKind kind)
    {
        return kind switch
        {
            FamilyMemberKind.Mikey => CollisionSizes.MikeyCollisionSize,
            FamilyMemberKind.Mommy => CollisionSizes.MommyCollisionSize,
            FamilyMemberKind.Daddy => CollisionSizes.DaddyCollisionSize,
            _ => throw CreateUnknownKindException(kind)
        };
    }

    /// <summary>Makes the exception for a kind that is not one of the three family members.</summary>
    /// <param name="kind">The kind.</param>
    private static ArgumentOutOfRangeException CreateUnknownKindException(FamilyMemberKind kind)
    {
        return new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown family member kind.");
    }
}
