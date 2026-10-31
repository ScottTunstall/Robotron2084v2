using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Graphics;

namespace Robotron2084.AttractMode;

/// <summary>
///     Maps a movie object's (animation, ROM animation frame index) to the port's texture, including
///     the three animation frames whose ROM table lists them in a different order to the
///     port's files (notes §96.4).
///     The port's sprite PNGs are stored in ROM-ADDRESS order, and for most of the
///     movie's descriptors the ROM's animation frame table is in that same order — so the
///     animation frame index is the file index. Three are not:
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
///     </list>
/// </summary>
public static class MovieAnimationFrames
{
    private static readonly int[] EnforcerAnimationFrameOrder = [1, 2, 3, 4, 5, 0];
    private static readonly int[] GruntAnimationFrameOrder = [0, 1, 0];
    private static readonly int[] HulkAnimationFrameOrder = [0, 1, 2, 6, 7, 8, 3, 4, 5];

    /// <summary>The texture a movie object draws, or null when there is none.</summary>
    public static Texture2D? Resolve(SpriteSet spriteSet, MovieAnimation animation, int animationFrameIndex)
    {
        var frames = GetAnimationFrames(spriteSet, animation);
        if (frames is null || frames.Length == 0)
            return animation switch
            {
                MovieAnimation.Skull => spriteSet.SkullSprite,
                MovieAnimation.Cruise => spriteSet.AttractCruiseSprite,
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

    private static Texture2D[]? GetAnimationFrames(SpriteSet spriteSet, MovieAnimation animation)
    {
        return animation switch
        {
            MovieAnimation.Mommy => spriteSet.MommyAnimationFrames,
            MovieAnimation.Daddy => spriteSet.DaddyAnimationFrames,
            MovieAnimation.Mikey => spriteSet.MikeyAnimationFrames,
            MovieAnimation.Hulk => spriteSet.HulkAnimationFrames,
            MovieAnimation.Brain => spriteSet.BrainAnimationFrames,
            MovieAnimation.Grunt => spriteSet.GruntAnimationFrames,
            MovieAnimation.Enforcer => spriteSet.EnforcerAnimationFrames,
            MovieAnimation.Player => spriteSet.PlayerAnimationFrames,
            MovieAnimation.Quark => spriteSet.QuarkAnimationFrames,
            MovieAnimation.Spheroid => spriteSet.SpheroidAnimationFrames,
            MovieAnimation.TankGrow => spriteSet.TankGrowAnimationFrames,
            MovieAnimation.Tank => spriteSet.TankAnimationFrames,
            MovieAnimation.Points => spriteSet.RescueScoreDisplays,
            MovieAnimation.Electrodes => spriteSet.AttractElectrodeAnimationFrames,
            _ => null
        };
    }
}
