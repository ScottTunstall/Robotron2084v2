using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Graphics;

namespace Robotron2084.AttractMode;

/// <summary>Finds the sprite picture a character in the attract movie is showing.</summary>
/// <remarks>
///     The port's sprite files are kept in the order of the arcade's memory addresses. For most characters the arcade's
///     animation frame table is in that same order, so the animation frame number is the file number. Three are not:
///     <list type="bullet">
///         <item>
///             <b>hulk</b> — the table at $0CF9 runs hulk1,hulk2,hulk3,hulk7,hulk8,hulk9,
///             hulk4,hulk5,hulk6, i.e. its LEFT block is files 1-3, its RIGHT block files 7-9
///             and its DOWN/UP block files 4-6 (which is why the playfield's <c>Hulk</c> uses
///             frames 6,7,8 for its right walk);
///         </item>
///         <item>
///             <b>enforcer</b> — the table at $18D2 starts at $1921 (the port's
///             Enforcer_2, the first GROW animation frame) and ends at $18EA (Enforcer_1, the full
///             one the grow-up finishes on);
///         </item>
///         <item>
///             <b>grunt</b> — the table at $4063 lists $4073, $40B4, $4073: ROM image 2
///             is the same sprite as image 0, so the movie's <c>SETIM 2</c> draws Grunt_1
///             (the port ships a third distinct grunt sprite at $40F5, which the movie's
///             table does not use).
///         </item>
///         <item>Original source: <c>RRSCRIPT.ASM</c> descriptors such as <c>MOMMY</c> and <c>BRAIN</c>, and their animation frame tables (notes §96.4)</item>
///         <item>Disassembly: the unlabelled descriptors from $7E41, and the animation frame tables they point to</item>
///     </list>
/// </remarks>
public static class MovieAnimationFrames
{
    private static readonly int[] EnforcerAnimationFrameOrder = [1, 2, 3, 4, 5, 0];
    private static readonly int[] GruntAnimationFrameOrder = [0, 1, 0];
    private static readonly int[] HulkAnimationFrameOrder = [0, 1, 2, 6, 7, 8, 3, 4, 5];

    /// <summary>The texture a movie object draws, or null when there is none.</summary>
    public static Texture2D? Resolve(TextureSet textureSet, MovieAnimation animation, int animationFrameIndex)
    {
        var frames = GetAnimationFrames(textureSet, animation);
        if (frames is null || frames.Length == 0)
            return animation switch
            {
                MovieAnimation.Skull => textureSet.SkullTexture,
                MovieAnimation.Cruise => textureSet.AttractCruiseTexture,
                _ => null
            };

        var remap = animation switch
        {
            MovieAnimation.Hulk => HulkAnimationFrameOrder,
            MovieAnimation.Enforcer => EnforcerAnimationFrameOrder,
            MovieAnimation.Grunt => GruntAnimationFrameOrder,
            _ => null
        };

        var index = animationFrameIndex;
        if (remap is not null)
        {
            var isInRange = index >= 0 && index < remap.Length;
            if (isInRange) index = remap[index];
            else index = 0;
        }

        return frames[index % frames.Length];
    }

    private static Texture2D[]? GetAnimationFrames(TextureSet textureSet, MovieAnimation animation)
    {
        return animation switch
        {
            MovieAnimation.Mommy => textureSet.MommyAnimationFrameTextures,
            MovieAnimation.Daddy => textureSet.DaddyAnimationFrameTextures,
            MovieAnimation.Mikey => textureSet.MikeyAnimationFrameTextures,
            MovieAnimation.Hulk => textureSet.HulkAnimationFrameTextures,
            MovieAnimation.Brain => textureSet.BrainAnimationFrameTextures,
            MovieAnimation.Grunt => textureSet.GruntAnimationFrameTextures,
            MovieAnimation.Enforcer => textureSet.EnforcerAnimationFrameTextures,
            MovieAnimation.Player => textureSet.PlayerAnimationFrameTextures,
            MovieAnimation.Quark => textureSet.QuarkAnimationFrameTextures,
            MovieAnimation.Spheroid => textureSet.SpheroidAnimationFrameTextures,
            MovieAnimation.TankGrow => textureSet.TankGrowAnimationFrameTextures,
            MovieAnimation.Tank => textureSet.TankAnimationFrameTextures,
            MovieAnimation.Points => textureSet.RescueBonusAnimationFrameTextures,
            MovieAnimation.Electrodes => textureSet.AttractElectrodeAnimationFrameTextures,
            _ => null
        };
    }
}
