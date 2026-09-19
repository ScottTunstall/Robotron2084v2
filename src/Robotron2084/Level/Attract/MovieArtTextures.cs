using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Rendering;

namespace Robotron2084.Level.Attract;

/// <summary>
/// Maps a movie object's (art, ROM image index) to the port's texture, including
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
public static class MovieArtTextures
{
    private static readonly int[] HulkImages = [0, 1, 2, 6, 7, 8, 3, 4, 5];

    private static readonly int[] EnforcerImages = [1, 2, 3, 4, 5, 0];

    private static readonly int[] GruntImages = [0, 1, 0];

    /// <summary>The texture a movie object draws, or null when there is none.</summary>
    public static Texture2D? Resolve(SpriteSet sprites, MovieArt art, int imageIndex)
    {
        Texture2D[]? frames = Frames(sprites, art);
        if (frames is null || frames.Length == 0)
        {
            return art switch
            {
                MovieArt.Skull => sprites.Skull,
                MovieArt.Cruise => sprites.AttractCruise,
                _ => null,
            };
        }

        int[]? remap = art switch
        {
            MovieArt.Hulk => HulkImages,
            MovieArt.Enforcer => EnforcerImages,
            MovieArt.Grunt => GruntImages,
            _ => null,
        };

        int index = imageIndex;
        if (remap is not null)
        {
            index = index >= 0 && index < remap.Length ? remap[index] : 0;
        }

        return frames[index % frames.Length];
    }

    private static Texture2D[]? Frames(SpriteSet sprites, MovieArt art) => art switch
    {
        MovieArt.Mummy => sprites.MomFrames,
        MovieArt.Daddy => sprites.DadFrames,
        MovieArt.Mikey => sprites.MikeyFrames,
        MovieArt.Hulk => sprites.HulkFrames,
        MovieArt.Brain => sprites.BrainFrames,
        MovieArt.Grunt => sprites.GruntFrames,
        MovieArt.Enforcer => sprites.EnforcerFrames,
        MovieArt.Player => sprites.PlayerFrames,
        MovieArt.Quark => sprites.QuarkFrames,
        MovieArt.Spheroid => sprites.SpheroidFrames,
        MovieArt.TankGrow => sprites.TankGrowFrames,
        MovieArt.Tank => sprites.TankFrames,
        MovieArt.Points => sprites.RescueScoreDisplays,
        MovieArt.Posts => sprites.PostFrames,
        _ => null,
    };
}
