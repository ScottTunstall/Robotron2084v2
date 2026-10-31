using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Palette;
using Robotron2084.Tuning;

namespace Robotron2084.Graphics;

/// <summary>
///     ROM sprite textures loaded from the content pipeline (extracted from
///     ref/rom/robotron64k.bin by tools/SpriteExtractor — definitive source and
///     per-sprite ROM offsets in docs/sprite-map.md), plus runtime-generated
///     textures for the four player laser sprites (ROM data R5 $35BE-$35DC,
///     see the Laser* properties) and the 1x1 wall pixel.
///     Arcade fidelity: animation frames are drawn at PortPixelsPerArcadePixel× arcade pixels (the
///     screen is WidthInArcadePixels by HeightInArcadePixels arcade pixels, each drawn PortPixelsPerArcadePixel port
///     pixels wide) and centred
///     inside the entity's collision box.
/// </summary>
public sealed class TextureSet
{
    /// <summary>
    ///     Number of spark (enforcer bullet) frames — SPKP0..3 in the ROM (notes 32). It is the number of animation
    ///     frames loaded into <see cref="SparkAnimationFrameTextures" />.
    /// </summary>
    public const int SparkAnimationFrameCount = 4;

    /// <summary>
    ///     The electrode sprite variants (nine variants of three, see notes §47). It is the number of animation frames
    ///     loaded into <see cref="ElectrodeAnimationFrameTextures" />.
    /// </summary>
    private const int ElectrodeAnimationFrameCount = 27;

    /// <summary>
    ///     Animation frames in the enforcer's walk cycle. It is the number of animation frames loaded into
    ///     <see cref="EnforcerAnimationFrameTextures" />.
    /// </summary>
    private const int EnforcerAnimationFrameCount = 6;

    /// <summary>
    ///     Animation frames in the grunt's walk cycle. It is the number of animation frames loaded into
    ///     <see cref="GruntAnimationFrameTextures" />.
    /// </summary>
    private const int GruntAnimationFrameCount = 3;

    /// <summary>
    ///     Animation frames in the hulk's walk cycle. It is the number of animation frames loaded into
    ///     <see cref="HulkAnimationFrameTextures" />.
    /// </summary>
    private const int HulkAnimationFrameCount = 9;

    /// <summary>
    ///     Animation frames in the player's walk cycle: 4 directions × 3 frames. It is the number of animation frames
    ///     loaded into <see cref="PlayerAnimationFrameTextures" />.
    /// </summary>
    private const int PlayerAnimationFrameCount = 12;

    /// <summary>
    ///     Animation frames in the quark's spin/drop cycle. It is the number of animation frames loaded into
    ///     <see cref="QuarkAnimationFrameTextures" />.
    /// </summary>
    private const int QuarkAnimationFrameCount = 9;

    /// <summary>
    ///     Glyphs the small font's table carries up to the closing bracket. It is the number of glyphs loaded into
    ///     <see cref="FontSmallTextures" />.
    /// </summary>
    private const int SmallFontGlyphCount = 38;

    /// <summary>
    ///     Animation frames in the spheroid's spin/drop cycle. It is the number of animation frames loaded into
    ///     <see cref="SpheroidAnimationFrameTextures" />.
    /// </summary>
    private const int SpheroidAnimationFrameCount = 8;

    /// <summary>
    ///     Animation frames in the tank's walk cycle. It is the number of animation frames loaded into
    ///     <see cref="TankAnimationFrameTextures" />.
    /// </summary>
    private const int TankAnimationFrameCount = 4;

    /// <summary>
    ///     Animation frames in one walk cycle of a family member or the brain — 4 directions x 3 frames. It is the number
    ///     of animation frames loaded into <see cref="MikeyAnimationFrameTextures" />, and into the animation frames of the other
    ///     family members.
    /// </summary>
    private const int WalkCycleAnimationFrameCount = 12;

    /// <summary>
    ///     Font glyph file-name suffixes in ROM order (notes §38, §96): '0'..'9',
    ///     'A'..'Z', '(', ')', ':' (the large font has the colon; the small
    ///     font stops after the parens), then the LARGE font's punctuation —
    ///     '!', ',', '.', '-' — which the ROM's own table carries between '9' and
    ///     'A' and the story movie's text crawl prints. The punctuation is APPENDED
    ///     so every existing index (and every caller's expectation of it) is
    ///     unchanged.
    /// </summary>
    private static readonly string[] GlyphSuffixes =
    [
        "0", "1", "2", "3", "4", "5", "6", "7", "8", "9",
        "A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L", "M",
        "N", "O", "P", "Q", "R", "S", "T", "U", "V", "W", "X", "Y", "Z",
        "(", ")", "colon", "arrowleft",
        "exclaim", "comma", "period", "hyphen"
    ];

    /// <summary>Loads the game's sprites.</summary>
    /// <param name="textureSource">
    ///     Where the sprites come from — the content pipeline in the game, and nothing at
    ///     all in a headless test.
    /// </param>
    public TextureSet(ITextureSource textureSource)
    {
        PlayerAnimationFrameTextures = textureSource.LoadAll(GetNumberedNames("Sprites/Player", PlayerAnimationFrameCount));
        GruntAnimationFrameTextures = textureSource.LoadAll(GetNumberedNames("Sprites/Grunt", GruntAnimationFrameCount));
        GorfAnimationFrameTextures = textureSource.LoadAll(GetNumberedNames("Sprites/Gorf", 2));
        BerzerkRobotIdleAnimationFrameTextures = textureSource.LoadAll(GetNumberedNames("Sprites/BerzerkRobot_Idle", 6));
        BerzerkRobotWalkRightAnimationFrameTextures = textureSource.LoadAll(GetNumberedNames("Sprites/BerzerkRobot_WalkRight", 2));
        BerzerkRobotWalkLeftAnimationFrameTextures = textureSource.LoadAll(GetNumberedNames("Sprites/BerzerkRobot_WalkLeft", 2));
        BerzerkRobotWalkUpAnimationFrameTextures = textureSource.LoadAll(GetNumberedNames("Sprites/BerzerkRobot_WalkUp", 3));
        BerzerkRobotWalkDownAnimationFrameTextures = textureSource.LoadAll(GetNumberedNames("Sprites/BerzerkRobot_WalkDown", 3));
        HulkAnimationFrameTextures = textureSource.LoadAll(GetNumberedNames("Sprites/Hulk", HulkAnimationFrameCount));
        SpheroidAnimationFrameTextures = textureSource.LoadAll(GetNumberedNames("Sprites/Spheroid", SpheroidAnimationFrameCount));
        EnforcerAnimationFrameTextures = textureSource.LoadAll(GetNumberedNames("Sprites/Enforcer", EnforcerAnimationFrameCount));
        EnforcerTexture = EnforcerAnimationFrameTextures[0];
        QuarkAnimationFrameTextures = textureSource.LoadAll(GetNumberedNames("Sprites/Quark", QuarkAnimationFrameCount));
        TankAnimationFrameTextures = textureSource.LoadAll(GetNumberedNames("Sprites/Tank", TankAnimationFrameCount));

        // ROM MTNKP1..4 (notes §53): the four birth animation frames. Each is a
        // different size, so the drawer reads each texture's own dimensions
        // rather than the tank's collision box.
        TankGrowAnimationFrameTextures = textureSource.LoadAll(GetNumberedNames("Sprites/TankGrow", TankTuning.GrowSteps));
        ElectrodeAnimationFrameTextures = textureSource.LoadAll(GetNumberedNames("Sprites/Electrode", ElectrodeAnimationFrameCount));
        SparkAnimationFrameTextures = textureSource.LoadAll(GetNumberedNames("Sprites/Spark", SparkAnimationFrameCount));
        SkullTexture = textureSource.Load("Sprites/Skull");
        RescueBonusAnimationFrameTextures = textureSource.LoadAll(
        [
            "Sprites/Score_1000",
            "Sprites/Score_2000",
            "Sprites/Score_3000",
            "Sprites/Score_4000",
            "Sprites/Score_5000"
        ]);
        MikeyAnimationFrameTextures = textureSource.LoadAll(GetNumberedNames("Sprites/Mikey", WalkCycleAnimationFrameCount));
        MommyAnimationFrameTextures = textureSource.LoadAll(GetNumberedNames("Sprites/Mommy", WalkCycleAnimationFrameCount));
        DaddyAnimationFrameTextures = textureSource.LoadAll(GetNumberedNames("Sprites/Daddy", WalkCycleAnimationFrameCount));
        BrainAnimationFrameTextures = textureSource.LoadAll(GetNumberedNames("Sprites/Brain", WalkCycleAnimationFrameCount));
        ProgBurstTexture = textureSource.Load("Sprites/ProgBurst");
        TitleWordmarkCoreTexture = textureSource.Load("Sprites/Title_Wordmark_Core");
        TitleWordmarkRimTexture = textureSource.Load("Sprites/Title_Wordmark_Rim");
        Title2084Texture = textureSource.Load("Sprites/Title_2084");
        WilliamsLogoTexture = textureSource.Load("Sprites/WilliamsLogo");

        LaserBarTexture = textureSource.Create(6, 1, TextureFactory.BuildLaserBarPattern(Color.White));
        LaserColumnTexture = textureSource.Create(2, 6, TextureFactory.BuildLaserColumnPattern(Color.White));
        LaserDiagonalMainTexture = textureSource.Create(6, 6, TextureFactory.BuildLaserDiagonalMainPattern(Color.White));
        LaserDiagonalAntiTexture = textureSource.Create(6, 6, TextureFactory.BuildLaserDiagonalAntiPattern(Color.White));
        TankShellTexture = textureSource.Load("Sprites/TankShell");
        AttractCruiseTexture = textureSource.Load("Sprites/AttractCruise");
        AttractElectrodeAnimationFrameTextures = textureSource.LoadAll(
        [
            "Sprites/AttractElectrode_1",
            "Sprites/AttractElectrode_2",
            "Sprites/AttractElectrode_3",
            "Sprites/AttractElectrode_4"
        ]);
        WallPixelTexture = textureSource.CreateSolid(1, 1, Color.White);
        SpareLifeTexture = textureSource.Load("Sprites/SpareLife");
        FontLargeTextures = textureSource.LoadAll(GetGlyphNames("Sprites/Font_L", GlyphSuffixes.Length));
        FontSmallTextures = textureSource.LoadAll(GetGlyphNames("Sprites/Font_S", SmallFontGlyphCount));
        CursorArrowTexture = textureSource.Load("Sprites/Font_S_cursorright");
        BlitterDraw = new BlitterDraw(WallPixelTexture);
        ArcadeText = new ArcadeText(BlitterDraw, FontLargeTextures, FontSmallTextures, SpareLifeTexture);
    }

    /// <summary>
    ///     The attract movie's CRUISE MISSILE — ROM `CRUSB` ($86BA, the 9x2 sprite
    ///     the movie's own CRUSM descriptor points at; notes §95.6). The playfield's
    ///     cruise missile draws itself as solid marks, not a sprite (see <see cref="Robotron2084.Entities.CruiseMissile" />).
    /// </summary>
    public Texture2D AttractCruiseTexture { get; }

    /// <summary>
    ///     The attract movie's four SCORE ELECTRODES — ROM `POSTS` images 12, 0, 4 and 8
    ///     (notes §95.6): the main electrode and the three that fork off it in the POSTER
    ///     script. They are WHITE masks because the movie only ever draws them SOLID,
    ///     through MONO's colour pair.
    /// </summary>
    public Texture2D[] AttractElectrodeAnimationFrameTextures { get; }

    /// <summary>The BerzerkRobot standing still: six frames that cycle (notes §138).</summary>
    public Texture2D[] BerzerkRobotIdleAnimationFrameTextures { get; }

    /// <summary>The BerzerkRobot walking down: three frames, played 1, 2, 3, 2.</summary>
    public Texture2D[] BerzerkRobotWalkDownAnimationFrameTextures { get; }

    /// <summary>The BerzerkRobot walking left: two frames.</summary>
    public Texture2D[] BerzerkRobotWalkLeftAnimationFrameTextures { get; }

    /// <summary>The BerzerkRobot walking right: two frames.</summary>
    public Texture2D[] BerzerkRobotWalkRightAnimationFrameTextures { get; }

    /// <summary>The BerzerkRobot walking up: three frames, played 1, 2, 3, 2.</summary>
    public Texture2D[] BerzerkRobotWalkUpAnimationFrameTextures { get; }

    /// <summary>The blitter operations that draw these sprites.</summary>
    public BlitterDraw BlitterDraw { get; }

    /// <summary>
    ///     The brain's 12 walk frames (4 directions × 3, same layout as
    ///     the family), the prog's phony burst (PGXPIC — solid blit), and the
    ///     cruise missile's two flicker frames (CMPIC/CMP1).
    /// </summary>
    public Texture2D[] BrainAnimationFrameTextures { get; }

    /// <summary>
    ///     The arcade's GAME ADJUSTMENT CURSOR (notes §108.4): the small font's own
    ///     "->" glyph — the 46th entry of the ROM's SMALL_CHARACTER_TABLE (pointer
    ///     `$EC14`: a 7px width byte then 5 rows of 4), which SHOW_GAME_ADJUSTMENT_CURSOR
    ///     (`$71FE`) prints at column `$0C` on the row the move lever is on. The record
    ///     ends exactly where the next table entry's pointer (`$EC29`) begins.
    ///     It is NOT one of <see cref="FontSmallTextures" />'s 38 glyphs — that array stops at
    ///     the parens, because the ROM's table order and the port's differ — so it
    ///     loads on its own (see <c>Content/Sprites/Font_S_cursorright.png</c>, guarded
    ///     against the ROM by <c>tools/verify-fonts.py</c>).
    /// </summary>
    public Texture2D CursorArrowTexture { get; }

    public Texture2D[] DaddyAnimationFrameTextures { get; }

    public Texture2D[] ElectrodeAnimationFrameTextures { get; }

    public Texture2D[] EnforcerAnimationFrameTextures { get; }
    public Texture2D EnforcerTexture { get; }

    /// <summary>
    ///     Arcade font glyphs, extracted from the ROM (notes §38; the offsets come from
    ///     WmsGfxSpriteEditor's font editor): index 0..9 = '0'..'9', 10..35 = 'A'..'Z',
    ///     then '(', ')', and (large only) ':' and 'arrowleft'. The LARGE font is
    ///     the score font
    ///     (6×6 px, ROM10-12 @0xEC93); the SMALL font (4×5 px, @0xEA2B) is the
    ///     message/title font. White-on-transparent — tinted at draw time
    ///     (arcade: solid blit, P1 = palette index 1 blue, P2 = palette index 10).
    /// </summary>
    public Texture2D[] FontLargeTextures { get; }

    public Texture2D[] FontSmallTextures { get; }

    /// <summary>Gorf's two animation frames, which it flaps between (notes §138.2).</summary>
    public Texture2D[] GorfAnimationFrameTextures { get; }

    public Texture2D[] GruntAnimationFrameTextures { get; }
    public Texture2D[] HulkAnimationFrameTextures { get; }

    /// <summary>
    ///     Player laser sprites — the four ROM sprites (R5 $35BE-$35DC, byte-identical
    ///     to old source RRG23 LLPC/ULPC/DLLPC/ULLPC). Built at arcade-pixel size (1 arcade pixel = 1 texture pixel);
    ///     <see cref="BlitterDraw.DrawSprite" /> scales by PortPixelsPerArcadePixel and centres in the laser's
    ///     4x4 arcade pixel collision box. ROM LTAB picks one per direction, no flipping:
    ///     L/R = bar, U/D = column (left pixel lit), UL/DR = main diagonal,
    ///     DL/UR = anti-diagonal.
    /// </summary>
    public Texture2D LaserBarTexture { get; }

    public Texture2D LaserColumnTexture { get; }

    public Texture2D LaserDiagonalAntiTexture { get; }

    public Texture2D LaserDiagonalMainTexture { get; }

    /// <summary>
    ///     The human family — 12 frames each (4 directions × 3 walk
    ///     frames, same layout as the player frames; Mikey = Mikey_*, Mommy =
    ///     Mommy_*, Daddy = Daddy_*).
    /// </summary>
    public Texture2D[] MikeyAnimationFrameTextures { get; }

    /// <summary>
    ///     The arcade's spare-life icon sprite — the LIVES icon (notes §58.2). ROM MNPIC
    ///     (`$3592` metadata, `$3596` pixels): 3 bytes x 8 rows = 6x8 px, one icon per
    ///     spare life, 8 px apart, next to that player's score. It is NOT the 8x12
    ///     player sprite. Loaded from <c>Sprites/SpareLife</c>, which the sprite extractor cut out of the ROM listing;
    ///     its palette index 11 body pixels carry the palette index 11 cycling marker. Each pixel keeps its own colour:
    ///     head palette index 2, body and arms 11 (a cycling palette index, so the body shimmers), legs 8, feet 3.
    /// </summary>
    public Texture2D SpareLifeTexture { get; }

    public Texture2D[] MommyAnimationFrameTextures { get; }

    public Texture2D[] PlayerAnimationFrameTextures { get; }
    public Texture2D ProgBurstTexture { get; }
    public Texture2D[] QuarkAnimationFrameTextures { get; }

    /// <summary>
    ///     Rescue bonus animation frames "1000".."5000" (ROM P1000..P5000, vector table
    ///     $000C+) — the 60-tick marker left at a rescue (HUMKIL PCFLG path),
    ///     index 0..4 = min(SAVCNT,5).
    /// </summary>
    public Texture2D[] RescueBonusAnimationFrameTextures { get; }

    /// <summary>Skull &amp; crossbones family-death marker (ROM: familydeath).</summary>
    public Texture2D SkullTexture { get; }

    public Texture2D[] SparkAnimationFrameTextures { get; }
    public Texture2D[] SpheroidAnimationFrameTextures { get; }

    public Texture2D[] TankAnimationFrameTextures { get; }

    /// <summary>
    ///     ROM `MTNKP1..4` — the tank's BIRTH animation frames (4x4, 8x7, 8x8 and 12x12
    ///     arcade px; notes §52/§53). These are NOT the tank: <c>Tank</c> draws them
    ///     while `MTANK` grows the drop, and each has its own size.
    /// </summary>
    public Texture2D[] TankGrowAnimationFrameTextures { get; }

    /// <summary>ROM tank shell (raw data $4FF2, 7×16 = 14×16 px; notes §11.5).</summary>
    public Texture2D TankShellTexture { get; }

    /// <summary>The arcade's text, printed in these sprites' fonts.</summary>
    public ArcadeText ArcadeText { get; }

    /// <summary>
    ///     The "2084" mark beneath the wordmark — COLOUR sprite, traced the same way and snapped to the
    ///     arcade's own palette (notes §103.4). Unlike the wordmark it keeps its own colours: only the
    ///     wordmark cycles.
    /// </summary>
    public Texture2D Title2084Texture { get; }

    /// <summary>
    ///     The attract page's wordmark — "ROBOTRON:" — as two WHITE MASKS at 1x arcade pixels,
    ///     TRACED from an arcade screenshot by <c>tools/extract-title-logos.py</c> (notes
    ///     §103.4): the R5 CPU ROM we hold does not contain these sprites. <see cref="TitleWordmarkCoreTexture" />
    ///     is the letters' body and <see cref="TitleWordmarkRimTexture" /> the one-pixel rim round them,
    ///     because the arcade blits a shape in a colour taken from the LIVE palette — so the page
    ///     draws each mask in a palette index and the wordmark colour-cycles with the page's own
    ///     colour processes (notes §104).
    /// </summary>
    public Texture2D TitleWordmarkCoreTexture { get; }

    /// <summary>The one-pixel rim round the wordmark's letters (see <see cref="TitleWordmarkCoreTexture" />).</summary>
    public Texture2D TitleWordmarkRimTexture { get; }

    /// <summary>1x1 white pixel for the wall ring (tinted per draw call).</summary>
    public Texture2D WallPixelTexture { get; }

    /// <summary>
    ///     The Williams "W" of the attract page's border, a WHITE MASK decoded from the ROM's plotting instructions at
    ///     <c>$8CF4</c> by <c>tools/extract-williams-logo.py</c>: the opaque pixels are the ones the ROM draws, in the colour
    ///     of the palette index it is given.
    /// </summary>
    public Texture2D WilliamsLogoTexture { get; }

    /// <summary>The asset names of a font's glyphs, which are numbered by character rather than by index.</summary>
    /// <param name="prefix">The font's asset prefix.</param>
    /// <param name="count">How many glyphs the font starts with.</param>
    private static string[] GetGlyphNames(string prefix, int count)
    {
        var names = new string[count];
        for (var i = 0; i < count; i++) names[i] = string.Concat(prefix, '_', GlyphSuffixes[i]);

        return names;
    }

    /// <summary>The asset names of a numbered run, <c>{prefix}_1</c> … <c>{prefix}_{count}</c>.</summary>
    /// <param name="prefix">The run's asset prefix.</param>
    /// <param name="count">How many sprites the run holds.</param>
    private static string[] GetNumberedNames(string prefix, int count)
    {
        var names = new string[count];
        for (var i = 0; i < count; i++) names[i] = string.Concat(prefix, '_', i + 1);

        return names;
    }
}
