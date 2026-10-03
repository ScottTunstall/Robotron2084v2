using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Palette;
using Robotron2084.Tuning;

namespace Robotron2084.Graphics;

/// <summary>
/// ROM sprite textures loaded from the content pipeline (extracted from
/// ref/rom/robotron64k.bin by tools/SpriteExtractor — definitive source and
/// per-sprite ROM offsets in docs/sprite-map.md), plus runtime-generated
/// textures for the four player laser sprites (ROM data R5 $35BE-$35DC,
/// see the Laser* properties) and the 1x1 wall pixel.
///
/// Arcade fidelity: ROM frames are drawn at SpecScale× arcade pixels (the
/// screen is the spec's 320x200 space widened by SpecScale) and centered
/// inside the entity's collision box.
/// </summary>
public sealed class SpriteSet
{
    /// <summary>Number of spark (enforcer bullet) frames — SPKP0..3 in the ROM (notes 32).</summary>
    public const int SparkAnimationFrameCount = 4;

    /// <summary>The electrode sprite variants (nine variants of three, see notes §47).</summary>
    private const int ElectrodeAnimationFrameCount = 27;

    /// <summary>Animation frames in the enforcer's walk cycle.</summary>
    private const int EnforcerAnimationFrameCount = 6;

    /// <summary>Animation frames in the grunt's walk cycle.</summary>
    private const int GruntAnimationFrameCount = 3;

    /// <summary>Animation frames in the hulk's walk cycle.</summary>
    private const int HulkAnimationFrameCount = 9;

    /// <summary>Animation frames in the player's walk cycle: 4 directions × 3 frames.</summary>
    private const int PlayerAnimationFrameCount = 12;

    /// <summary>Animation frames in the quark's spin/drop cycle.</summary>
    private const int QuarkAnimationFrameCount = 9;

    /// <summary>Glyphs the small font's table carries up to the closing bracket.</summary>
    private const int SmallFontGlyphCount = 38;

    /// <summary>Animation frames in the spheroid's spin/drop cycle.</summary>
    private const int SpheroidAnimationFrameCount = 8;

    /// <summary>Animation frames in the tank's walk cycle.</summary>
    private const int TankAnimationFrameCount = 4;

    /// <summary>Animation frames in one walk cycle of a family member or the brain — 4 directions x 3 frames.</summary>
    private const int WalkCycleAnimationFrameCount = 12;

    /// <summary>
    /// Font glyph file-name suffixes in ROM order (notes §38, §96): '0'..'9',
    /// 'A'..'Z', '(', ')', ':' (the large font has the colon; the small
    /// font stops after the parens), then the LARGE font's punctuation —
    /// '!', ',', '.', '-' — which the ROM's own table carries between '9' and
    /// 'A' and the story movie's text crawl prints. The punctuation is APPENDED
    /// so every existing index (and every caller's expectation of it) is
    /// unchanged.
    /// </summary>
    private static readonly string[] GlyphSuffixes =
    [
        "0", "1", "2", "3", "4", "5", "6", "7", "8", "9",
        "A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L", "M",
        "N", "O", "P", "Q", "R", "S", "T", "U", "V", "W", "X", "Y", "Z",
        "(", ")", "colon", "arrowleft",
        "exclaim", "comma", "period", "hyphen",
    ];

    /// <summary>Loads the game's sprites.</summary>
    /// <param name="source">Where the sprites come from — the content pipeline in the game, and nothing at
    /// all in a headless test.</param>
    public SpriteSet(ISpriteSource source)
    {
        PlayerAnimationFrames = source.LoadAll(NumberedNames("Sprites/Player", PlayerAnimationFrameCount));
        GruntAnimationFrames = source.LoadAll(NumberedNames("Sprites/Grunt", GruntAnimationFrameCount));
        BerzerkRobotIdleFrames = source.LoadAll(NumberedNames("Sprites/BerzerkRobot_Idle", 6));
        BerzerkRobotWalkRightFrames = source.LoadAll(NumberedNames("Sprites/BerzerkRobot_WalkRight", 2));
        BerzerkRobotWalkLeftFrames = source.LoadAll(NumberedNames("Sprites/BerzerkRobot_WalkLeft", 2));
        BerzerkRobotWalkUpFrames = source.LoadAll(NumberedNames("Sprites/BerzerkRobot_WalkUp", 3));
        BerzerkRobotWalkDownFrames = source.LoadAll(NumberedNames("Sprites/BerzerkRobot_WalkDown", 3));
        HulkAnimationFrames = source.LoadAll(NumberedNames("Sprites/Hulk", HulkAnimationFrameCount));
        SpheroidAnimationFrames = source.LoadAll(NumberedNames("Sprites/Spheroid", SpheroidAnimationFrameCount));
        EnforcerAnimationFrames = source.LoadAll(NumberedNames("Sprites/Enforcer", EnforcerAnimationFrameCount));
        Enforcer = EnforcerAnimationFrames[0];
        QuarkAnimationFrames = source.LoadAll(NumberedNames("Sprites/Quark", QuarkAnimationFrameCount));
        TankAnimationFrames = source.LoadAll(NumberedNames("Sprites/Tank", TankAnimationFrameCount));

        // ROM MTNKP1..4 (notes §53): the four birth animation frames. Each is a
        // different size, so the drawer reads each texture's own dimensions
        // rather than the tank's collision box.
        TankGrowAnimationFrames = source.LoadAll(NumberedNames("Sprites/TankGrow", TankTuning.GrowSteps));
        ElectrodeAnimationFrames = source.LoadAll(NumberedNames("Sprites/Electrode", ElectrodeAnimationFrameCount));
        SparkAnimationFrames = source.LoadAll(NumberedNames("Sprites/Spark", SparkAnimationFrameCount));
        Skull = source.Load("Sprites/Skull");
        RescueScoreDisplays = source.LoadAll(
        [
            "Sprites/Score_1000",
            "Sprites/Score_2000",
            "Sprites/Score_3000",
            "Sprites/Score_4000",
            "Sprites/Score_5000",
        ]);
        MikeyAnimationFrames = source.LoadAll(NumberedNames("Sprites/Mikey", WalkCycleAnimationFrameCount));
        MommyAnimationFrames = source.LoadAll(NumberedNames("Sprites/Mommy", WalkCycleAnimationFrameCount));
        DaddyAnimationFrames = source.LoadAll(NumberedNames("Sprites/Daddy", WalkCycleAnimationFrameCount));
        BrainAnimationFrames = source.LoadAll(NumberedNames("Sprites/Brain", WalkCycleAnimationFrameCount));
        ProgBurst = source.Load("Sprites/ProgBurst");
        TitleWordmarkCore = source.Load("Sprites/Title_Wordmark_Core");
        TitleWordmarkRim = source.Load("Sprites/Title_Wordmark_Rim");
        Title2084 = source.Load("Sprites/Title_2084");
        WilliamsLogo = source.Load("Sprites/WilliamsLogo");

        LaserBar = source.Create(6, 1, SpriteFactory.BuildLaserBarPattern(Color.White));
        LaserColumn = source.Create(2, 6, SpriteFactory.BuildLaserColumnPattern(Color.White));
        LaserDiagonalMain = source.Create(6, 6, SpriteFactory.BuildLaserDiagonalMainPattern(Color.White));
        LaserDiagonalAnti = source.Create(6, 6, SpriteFactory.BuildLaserDiagonalAntiPattern(Color.White));
        TankShell = source.Load("Sprites/TankShell");
        AttractCruise = source.Load("Sprites/AttractCruise");
        AttractElectrodeAnimationFrames = source.LoadAll(
        [
            "Sprites/AttractElectrode_1",
            "Sprites/AttractElectrode_2",
            "Sprites/AttractElectrode_3",
            "Sprites/AttractElectrode_4",
        ]);
        WallPixel = source.CreateSolid(1, 1, Color.White);
        MiniMan = BuildMiniMan(source);
        FontLarge = source.LoadAll(GlyphNames("Sprites/Font_L", GlyphSuffixes.Length));
        FontSmall = source.LoadAll(GlyphNames("Sprites/Font_S", SmallFontGlyphCount));
        CursorArrow = source.Load("Sprites/Font_S_cursorright");
        Blitter = new BlitterDraw(WallPixel);
        Text = new ArcadeText(Blitter, FontLarge, FontSmall, MiniMan);
    }

    /// <summary>
    /// The attract movie's CRUISE MISSILE — ROM `CRUSB` ($86BA, the 9x2 sprite
    /// the movie's own CRUSM descriptor points at; notes §95.6). The playfield's
    /// cruise missile draws itself as solid marks, not a sprite (see <see cref="Robotron2084.Entities.CruiseMissile"/>).
    /// </summary>
    public Texture2D AttractCruise { get; }

    /// <summary>The blitter operations that draw these sprites.</summary>
    public BlitterDraw Blitter { get; }

    /// <summary>
    /// The brain's 12 walk frames (4 directions × 3, same layout as
    /// the family), the prog's phony burst (PGXPIC — solid blit), and the
    /// cruise missile's two flicker frames (CMPIC/CMP1).
    /// </summary>
    public Texture2D[] BrainAnimationFrames { get; }

    /// <summary>
    /// The arcade's GAME ADJUSTMENT CURSOR (notes §108.4): the small font's own
    /// "->" glyph — the 46th entry of the ROM's SMALL_CHARACTER_TABLE (pointer
    /// `$EC14`: a 7px width byte then 5 rows of 4), which SHOW_GAME_ADJUSTMENT_CURSOR
    /// (`$71FE`) prints at column `$0C` on the row the move lever is on. The record
    /// ends exactly where the next table entry's pointer (`$EC29`) begins.
    ///
    /// It is NOT one of <see cref="FontSmall"/>'s 38 glyphs — that array stops at
    /// the parens, because the ROM's table order and the port's differ — so it
    /// loads on its own (see <c>Content/Sprites/Font_S_cursorright.png</c>, guarded
    /// against the ROM by <c>tools/verify-fonts.py</c>).
    /// </summary>
    public Texture2D CursorArrow { get; }

    public Texture2D[] DaddyAnimationFrames { get; }

    public Texture2D[] ElectrodeAnimationFrames { get; }

    public Texture2D Enforcer { get; }

    public Texture2D[] EnforcerAnimationFrames { get; }

    /// <summary>
    /// Arcade font glyphs, extracted from the ROM (notes §38; the offsets come from
    /// WmsGfxSpriteEditor's font editor): index 0..9 = '0'..'9', 10..35 = 'A'..'Z',
    /// then '(', ')', and (large only) ':' and 'arrowleft'. The LARGE font is
    /// the score font
    /// (6×6 px, ROM10-12 @0xEC93); the SMALL font (4×5 px, @0xEA2B) is the
    /// message/title font. White-on-transparent — tinted at draw time
    /// (arcade: solid blit, P1 = palette slot 1 blue, P2 = slot 10).
    /// </summary>
    public Texture2D[] FontLarge { get; }

    public Texture2D[] FontSmall { get; }

    public Texture2D[] GruntAnimationFrames { get; }

    /// <summary>The BerzerkRobot standing still: six frames that cycle (notes §138).</summary>
    public Texture2D[] BerzerkRobotIdleFrames { get; }

    /// <summary>The BerzerkRobot walking right: two frames.</summary>
    public Texture2D[] BerzerkRobotWalkRightFrames { get; }

    /// <summary>The BerzerkRobot walking left: two frames.</summary>
    public Texture2D[] BerzerkRobotWalkLeftFrames { get; }

    /// <summary>The BerzerkRobot walking up: three frames, played 1, 2, 3, 2.</summary>
    public Texture2D[] BerzerkRobotWalkUpFrames { get; }

    /// <summary>The BerzerkRobot walking down: three frames, played 1, 2, 3, 2.</summary>
    public Texture2D[] BerzerkRobotWalkDownFrames { get; }

    public Texture2D[] HulkAnimationFrames { get; }

    /// <summary>
    /// Player laser sprites — the four ROM sprites (R5 $35BE-$35DC, byte-identical
    /// to old source RRG23 LLPC/ULPC/DLLPC/ULLPC). Built at arcade-pixel size (1 arcade pixel = 1 texture pixel);
    /// <see cref="DrawSprite"/> scales by SpecScale and centers in the laser's
    /// 4x4 (spec) collision box. ROM LTAB picks one per direction, no flipping:
    /// L/R = bar, U/D = column (left pixel lit), UL/DR = main diagonal,
    /// DL/UR = anti-diagonal.
    /// </summary>
    public Texture2D LaserBar { get; }

    public Texture2D LaserColumn { get; }

    public Texture2D LaserDiagonalAnti { get; }

    public Texture2D LaserDiagonalMain { get; }

    /// <summary>
    /// The human family — 12 frames each (4 directions × 3 walk
    /// frames, same layout as the player frames; Mikey = Mikey_*, Mommy =
    /// Mommy_*, Daddy = Daddy_*).
    /// </summary>
    public Texture2D[] MikeyAnimationFrames { get; }

    /// <summary>
    /// The arcade's mini man sprite — the LIVES icon (notes §58.2). ROM MNPIC
    /// (`$3592` metadata, `$3596` pixels): 3 bytes x 8 rows = 6x8 px, one icon per
    /// spare man, 8 px apart, next to that player's score. It is NOT the 8x12
    /// player sprite. Built at runtime from the ROM nibbles; its slot-11 body
    /// pixels carry the slot-11 cycling marker.
    /// </summary>
    public Texture2D MiniMan { get; }

    public Texture2D[] MommyAnimationFrames { get; }

    public Texture2D[] PlayerAnimationFrames { get; }

    /// <summary>
    /// The attract movie's four SCORE ELECTRODES — ROM `POSTS` images 12, 0, 4 and 8
    /// (notes §95.6): the main electrode and the three that fork off it in the POSTER
    /// script. They are WHITE masks because the movie only ever draws them SOLID,
    /// through MONO's colour pair.
    /// </summary>
    public Texture2D[] AttractElectrodeAnimationFrames { get; }

    public Texture2D ProgBurst { get; }
    public Texture2D[] QuarkAnimationFrames { get; }

    /// <summary>
    /// Rescue score displays "1000".."5000" (ROM P1000..P5000, vector table
    /// $000C+) — the 60-tick marker left at a rescue (HUMKIL PCFLG path),
    /// index 0..4 = min(SAVCNT,5).
    /// </summary>
    public Texture2D[] RescueScoreDisplays { get; }

    /// <summary>Skull &amp; crossbones family-death marker (ROM: familydeath).</summary>
    public Texture2D Skull { get; }

    public Texture2D[] SparkAnimationFrames { get; }
    public Texture2D[] SpheroidAnimationFrames { get; }

    public Texture2D[] TankAnimationFrames { get; }

    /// <summary>
    /// ROM `MTNKP1..4` — the tank's BIRTH animation frames (4x4, 8x7, 8x8 and 12x12
    /// arcade px; notes §52/§53). These are NOT the tank: <c>Tank</c> draws them
    /// while `MTANK` grows the drop, and each has its own size.
    /// </summary>
    public Texture2D[] TankGrowAnimationFrames { get; }

    /// <summary>ROM tank shell (raw data $4FF2, 7×16 = 14×16 px; notes §11.5).</summary>
    public Texture2D TankShell { get; }

    /// <summary>The arcade's text, printed in these sprites' fonts.</summary>
    public ArcadeText Text { get; }

    /// <summary>
    /// The "2084" mark beneath the wordmark — COLOUR sprite, traced the same way and snapped to the
    /// arcade's own palette (notes §103.4). Unlike the wordmark it keeps its own colours: only the
    /// wordmark cycles.
    /// </summary>
    public Texture2D Title2084 { get; }

    /// <summary>
    /// The attract page's wordmark — "ROBOTRON:" — as two WHITE MASKS at 1x arcade pixels,
    /// TRACED from an arcade screenshot by <c>tools/extract-title-logos.py</c> (notes
    /// §103.4): the R5 CPU ROM we hold does not contain these sprites. <see cref="TitleWordmarkCore"/>
    /// is the letters' body and <see cref="TitleWordmarkRim"/> the one-pixel rim round them,
    /// because the arcade blits a shape in a colour taken from the LIVE palette — so the page
    /// draws each mask in a palette slot and the wordmark colour-cycles with the page's own
    /// colour processes (notes §104).
    /// </summary>
    public Texture2D TitleWordmarkCore { get; }

    /// <summary>The one-pixel rim round the wordmark's letters (see <see cref="TitleWordmarkCore"/>).</summary>
    public Texture2D TitleWordmarkRim { get; }

    /// <summary>1x1 white pixel for the wall ring (tinted per draw call).</summary>
    public Texture2D WallPixel { get; }

    /// <summary>
    /// The Williams "W" of the attract page's border, a WHITE MASK decoded from the ROM's plotting instructions at
    /// <c>$8CF4</c> by <c>tools/extract-williams-logo.py</c>: the opaque pixels are the ones the ROM draws, in the colour
    /// of the palette slot it is given.
    /// </summary>
    public Texture2D WilliamsLogo { get; }

    /// <summary>
    /// ROM MNPIC (RRG23; R5 `$3596`, metadata `$3592` = 3 bytes x 8 rows): the
    /// 6x8 mini man drawn once per spare life (notes §58.4). Each nibble is a
    /// PALETTE INDEX, and the ROM blits it with op `$02` (no transparency, mask
    /// unused) — so the icon keeps its own colours: head slot 2, body/arms slot
    /// 11 (a CYCLING slot — the RGB process, which is why the little man's body
    /// shimmers), legs slot 8, feet slot 3.
    /// </summary>
    private static Texture2D BuildMiniMan(ISpriteSource source)
    {
        byte[] nibbles =
        [
            0x02, 0x22, 0x00, // .222..
            0xBB, 0x0B, 0xB0, // BB.BB.
            0xBB, 0x0B, 0xB0, // BB.BB.
            0x00, 0x20, 0x00, // ..2...
            0x88, 0x08, 0x80, // 88.88.
            0x30, 0x80, 0x30, // 3.8.3.
            0x08, 0x08, 0x00, // .8.8..
            0x88, 0x08, 0x80, // 88.88.
        ];

        const int widthBytes = HudLayout.HudMiniManWidthPixels / 2;
        const int height = HudLayout.HudMiniManHeightPixels;
        var pixels = new Color[widthBytes * 2 * height];

        for (int row = 0; row < height; row++)
        {
            for (int column = 0; column < widthBytes * 2; column++)
            {
                int value = (nibbles[(row * widthBytes) + (column / 2)] >> (column % 2 == 0 ? 4 : 0)) & 0x0F;
                pixels[(row * widthBytes * 2) + column] =
                    value == 0 ? Color.Transparent : GetPaletteColour(value);
            }
        }

        return source.Create(widthBytes * 2, height, pixels);
    }

    /// <summary>The asset names of a font's glyphs, which are numbered by character rather than by index.</summary>
    /// <param name="prefix">The font's asset prefix.</param>
    /// <param name="count">How many glyphs the font starts with.</param>
    private static string[] GlyphNames(string prefix, int count)
    {
        var names = new string[count];
        for (int i = 0; i < count; i++)
        {
            names[i] = string.Concat(prefix, '_', GlyphSuffixes[i]);
        }

        return names;
    }

    /// <summary>The asset names of a numbered run, <c>{prefix}_1</c> … <c>{prefix}_{count}</c>.</summary>
    /// <param name="prefix">The run's asset prefix.</param>
    /// <param name="count">How many sprites the run holds.</param>
    private static string[] NumberedNames(string prefix, int count)
    {
        var names = new string[count];
        for (int i = 0; i < count; i++)
        {
            names[i] = string.Concat(prefix, '_', i + 1);
        }

        return names;
    }

    /// <summary>
    /// The colour a baked texture pixel carries for a palette slot: a cycling
    /// slot (10-15) uses its MARKER colour — the shader remaps it to the slot's
    /// live colour at draw time (notes §34/§39) — and a static slot (0-9) uses
    /// the slot's fixed ROM colour.
    /// </summary>
    private static Color GetPaletteColour(int slot)
    {
        if (FontSlots.IsCycling(slot))
        {
            byte marker = GamePalette.CyclingSlotMarkers[slot - FontSlots.FirstCyclingSlot];
            return RobotronColor.CreateFromByte(marker);
        }

        return RobotronColor.CreateFromByte(GamePalette.DefaultSlots[slot]);
    }
}
