using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Rendering;

namespace Robotron2084.Level.Attract;

/// <summary>
/// Maps a movie object's (animation, ROM image index) to the port's texture, including
/// the three pictures whose ROM table lists them in a different order to the
/// port's files (notes §96.4).
///
/// The port's sprite PNGs are stored in ROM-ADDRESS order, and for most of the
/// movie's descriptors the ROM's picture table is in that same order — so the
/// image index is the file index. Three are not:
/// <list type="bullet">
/// <item><b>hulk</b> — the table at $0CF9 runs hulk1,hulk2,hulk3,hulk7,hulk8,hulk9,
/// hulk4,hulk5,hulk6, i.e. its LEFT block is files 1-3, its RIGHT block files 7-9
/// and its DOWN/UP block files 4-6 (which is why the playfield's <c>Hulk</c> uses
/// frames 6,7,8 for its right walk);</item>
/// <item><b>enforcer</b> — the table at $18D2 starts at $1921 (the port's
/// Enforcer_2, the first GROW picture) and ends at $18EA (Enforcer_1, the full
/// one the grow-up finishes on);</item>
/// <item><b>grunt</b> — the table at $4063 lists $4073, $40B4, $4073: ROM image 2
/// is the same picture as image 0, so the movie's <c>SETIM 2</c> draws Grunt_1
/// (the port ships a third distinct grunt picture at $40F5, which the movie's
/// table does not use).</item>
/// </list>
/// </summary>
public static class MovieAnimationFrames
{
    private static readonly int[] HulkImages = [0, 1, 2, 6, 7, 8, 3, 4, 5];

    private static readonly int[] EnforcerImages = [1, 2, 3, 4, 5, 0];

    private static readonly int[] GruntImages = [0, 1, 0];

    /// <summary>The texture a movie object draws, or null when there is none.</summary>
    public static Texture2D? Resolve(SpriteSet sprites, MovieAnimation animation, int imageIndex)
    {
        Texture2D[]? frames = Frames(sprites, animation);
        if (frames is null || frames.Length == 0)
        {
            return animation switch
            {
                MovieAnimation.Skull => sprites.Skull,
                MovieAnimation.Cruise => sprites.AttractCruise,
                _ => null,
            };
        }

        int[]? remap = animation switch
        {
            MovieAnimation.Hulk => HulkImages,
            MovieAnimation.Enforcer => EnforcerImages,
            MovieAnimation.Grunt => GruntImages,
            _ => null,
        };

        int index = imageIndex;
        if (remap is not null)
        {
            index = index >= 0 && index < remap.Length ? remap[index] : 0;
        }

        return frames[index % frames.Length];
    }

    private static Texture2D[]? Frames(SpriteSet sprites, MovieAnimation animation) => animation switch
    {
        MovieAnimation.Mummy => sprites.MomFrames,
        MovieAnimation.Daddy => sprites.DadFrames,
        MovieAnimation.Mikey => sprites.MikeyFrames,
        MovieAnimation.Hulk => sprites.HulkFrames,
        MovieAnimation.Brain => sprites.BrainFrames,
        MovieAnimation.Grunt => sprites.GruntFrames,
        MovieAnimation.Enforcer => sprites.EnforcerFrames,
        MovieAnimation.Player => sprites.PlayerFrames,
        MovieAnimation.Quark => sprites.QuarkFrames,
        MovieAnimation.Spheroid => sprites.SpheroidFrames,
        MovieAnimation.TankGrow => sprites.TankGrowFrames,
        MovieAnimation.Tank => sprites.TankFrames,
        MovieAnimation.Points => sprites.RescueScoreDisplays,
        MovieAnimation.Posts => sprites.PostFrames,
        _ => null,
    };
}