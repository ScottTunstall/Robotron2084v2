using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Hud;
using Robotron2084.Tuning;

namespace Robotron2084.Rendering;

/// <summary>
/// ROM sprite textures loaded from the content pipeline (extracted from
/// ref/rom/robotron64k.bin by tools/SpriteExtractor — definitive source and
/// per-sprite ROM offsets in docs/sprite-map.md), plus runtime-generated
/// textures for the four player laser pictures (ROM data R5 $35BE-$35DC,
/// see the Laser* properties) and the 1x1 wall pixel.
///
/// Arcade fidelity: ROM frames are drawn at SpecScale× arcade pixels (the
/// screen is the spec's 320x200 space widened by SpecScale) and centered
/// inside the entity's collision
/// box. Frame counts per entity match the definitive sprite source
/// (RobotronBlueLabelSpriteRepository): player/mummy/daddy/mikey 12
/// (4 dirs x 3), hulk 9, spheroid 8, enforcer 6, quark 9, tank 4,
/// electrode 27 variants, enforcer bullet (spark) 4, grunt 3.
/// </summary>
public sealed class SpriteSet
{
    /// <summary>Number of spark (enforcer bullet) frames — SPKP0..3 in the ROM (notes 32).</summary>
    public const int SparkFrameCount = 4;

    /// <summary>Representative player frame: frame 7 = first of the down-facing set (frames 1-3 left, 4-6 right, 7-9 down, 10-12 up).</summary>
    public Texture2D Player { get; }

    public Texture2D[] PlayerFrames { get; }

    public Texture2D Grunt { get; }

    public Texture2D[] GruntFrames { get; }

    public Texture2D Hulk { get; }

    public Texture2D[] HulkFrames { get; }

    public Texture2D[] SpheroidFrames { get; }

    public Texture2D Enforcer { get; }

    public Texture2D[] EnforcerFrames { get; }

    public Texture2D[] QuarkFrames { get; }

    public Texture2D Tank { get; }

    public Texture2D[] TankFrames { get; }

    /// <summary>
    /// ROM `MTNKP1..4` — the tank's BIRTH pictures (4x4, 8x7, 8x8 and 12x12
    /// arcade px; notes §52/§53). These are NOT the tank: <c>Tank</c> draws them
    /// while `MTANK` grows the drop, and each has its own size.
    /// </summary>
    public Texture2D[] TankGrowFrames { get; }

    public Texture2D Electrode { get; }

    public Texture2D[] ElectrodeFrames { get; }

    /// <summary>Enforcer bullet / spark (ROM: enforcerbullet1-4).</summary>
    public Texture2D Spark { get; }

    public Texture2D[] SparkFrames { get; }

    /// <summary>Skull &amp; crossbones family-death marker (ROM: familydeath).</summary>
    public Texture2D Skull { get; }

    /// <summary>
    /// Rescue score displays "1000".."5000" (ROM P1000..P5000, vector table
    /// $000C+) — the 60-tick marker left at a rescue (HUMKIL PCFLG path),
    /// index 0..4 = min(SAVCNT,5).
    /// </summary>
    public Texture2D[] RescueScoreDisplays { get; }

    /// <summary>
    /// PHASE D: the human family — 12 frames each (4 directions × 3 walk
    /// frames, same layout as the player frames; Mikey = Mikey_*, Mom =
    /// Mummy_*, Dad = Daddy_*).
    /// </summary>
    public Texture2D[] MikeyFrames { get; }

    public Texture2D[] MomFrames { get; }

    public Texture2D[] DadFrames { get; }

    /// <summary>
    /// PHASE E: the brain's 12 walk frames (4 directions × 3, same layout as
    /// the family), the prog's phony burst (PGXPIC — solid blit), and the
    /// cruise missile's two flicker frames (CMPIC/CMP1).
    /// </summary>
    public Texture2D[] BrainFrames { get; }

    public Texture2D ProgBurst { get; }

    public Texture2D[] MissileSmallFrames { get; }

    /// <summary>
    /// Player laser art — the four ROM pictures (R5 $35BE-$35DC, byte-identical
    /// to old source RRG23 LLPC/ULPC/DLLPC/ULLPC; author ROM-verified
    /// 2026-09-13). Built at arcade-pixel size (1 art pixel = 1 texture pixel);
    /// <see cref="DrawSprite"/> scales by SpecScale and centers in the laser's
    /// 4x4 (spec) collision box. ROM LTAB picks one per direction, no flipping:
    /// L/R = bar, U/D = column (left pixel lit), UL/DR = main diagonal,
    /// DL/UR = anti-diagonal.
    /// </summary>
    public Texture2D LaserBar { get; }

    public Texture2D LaserColumn { get; }

    public Texture2D LaserDiagonalMain { get; }

    public Texture2D LaserDiagonalAnti { get; }

    /// <summary>ROM tank shell (author-located 2026-09-12: raw data $4FF2, 7×16 = 14×16 px; notes §11.5).</summary>
    public Texture2D TankShell { get; }

    /// <summary>
    /// The attract movie's CRUISE MISSILE — ROM `CRUSB` ($86BA, the 9x2 picture
    /// the movie's own CRUSM descriptor points at; notes §95.6). The playfield's
    /// cruise missile is a different picture (<see cref="MissileSmallFrames"/>).
    /// </summary>
    public Texture2D AttractCruise { get; }

    /// <summary>
    /// The attract movie's four SCORE POSTS — ROM `POSTS` images 12, 0, 4 and 8
    /// (notes §95.6): the main post and the three that fork off it in the POSTER
    /// script. They are WHITE masks because the movie only ever draws them SOLID,
    /// through MONO's colour pair.
    /// </summary>
    public Texture2D[] PostFrames { get; }

    /// <summary>
    /// Arcade font glyphs, extracted from the ROM (notes §38; offsets from
    /// the author's sprite editor): index 0..9 = '0'..'9', 10..35 = 'A'..'Z',
    /// then '(', ')', and (large only) ':' and 'arrowleft'. The LARGE font is
    /// the score font
    /// (6×6 px, ROM10-12 @0xEC93); the SMALL font (4×5 px, @0xEA2B) is the
    /// message/title font. White-on-transparent — tinted at draw time
    /// (arcade: solid blit, P1 = palette slot 1 blue, P2 = slot 10).
    /// </summary>
    public Texture2D[] FontLarge { get; }

    public Texture2D[] FontSmall { get; }

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

    /// <summary>
    /// The arcade's mini man picture — the LIVES icon (notes §58.2). ROM MNPIC
    /// (`$3592` metadata, `$3596` pixels): 3 bytes x 8 rows = 6x8 px, one icon per
    /// spare man, 8 px apart, next to that player's score. It is NOT the 8x12
    /// player sprite. Built at runtime from the ROM nibbles; its slot-11 body
    /// pixels carry the slot-11 cycling marker.
    /// </summary>
    public Texture2D MiniMan { get; }

    /// <summary>1x1 white pixel for the wall ring (tinted per draw call).</summary>
    public Texture2D WallPixel { get; }

    /// <summary>
    /// The attract page's wordmark — "ROBOTRON:" — as two WHITE MASKS at 1x arcade pixels,
    /// TRACED from the author's arcade screenshot by <c>tools/extract-title-logos.py</c> (notes
    /// §103.4): the R5 CPU ROM we hold does not contain this artwork. <see cref="TitleWordmarkCore"/>
    /// is the letters' body and <see cref="TitleWordmarkRim"/> the one-pixel rim round them,
    /// because the arcade blits a shape in a colour taken from the LIVE palette — so the page
    /// draws each mask in a palette slot and the wordmark colour-cycles with the page's own
    /// colour processes (notes §104).
    /// </summary>
    public Texture2D TitleWordmarkCore { get; }

    /// <summary>The one-pixel rim round the wordmark's letters (see <see cref="TitleWordmarkCore"/>).</summary>
    public Texture2D TitleWordmarkRim { get; }

    /// <summary>
    /// The "2084" mark beneath the wordmark — COLOUR art, traced the same way and snapped to the
    /// arcade's own palette (notes §103.4). Unlike the wordmark it keeps its own colours: the
    /// author asked for the wordmark to cycle, not for this.
    /// </summary>
    public Texture2D Title2084 { get; }

    /// <summary>
    /// M4: when set, entity draws run through the colour-cycle pixel shader
    /// (remaps the six cycling-slot marker colours to their live palette
    /// colours). Null = plain draws.
    /// </summary>
    public Effect? ColorCycleEffect { get; set; }

    /// <summary>M4: the live 16-slot palette the effect remaps into (slots 10-15 cycle).</summary>
    public GamePalette? Palette { get; set; }

    public SpriteSet(GraphicsDevice device, ContentManager content)
    {
        var factory = new PixelArtFactory(device);
        PlayerFrames = LoadRange(content, "Sprites/Player", 12);
        Player = PlayerFrames[6];
        GruntFrames = LoadRange(content, "Sprites/Grunt", 3);
        Grunt = GruntFrames[0];
        HulkFrames = LoadRange(content, "Sprites/Hulk", 9);
        Hulk = HulkFrames[0];
        SpheroidFrames = LoadRange(content, "Sprites/Spheroid", 8);
        EnforcerFrames = LoadRange(content, "Sprites/Enforcer", 6);
        Enforcer = EnforcerFrames[0];
        QuarkFrames = LoadRange(content, "Sprites/Quark", 9);
        TankFrames = LoadRange(content, "Sprites/Tank", 4);
        Tank = TankFrames[0];

        // ROM MTNKP1..4 (notes §53): the four birth pictures. Each is a
        // different size, so the drawer reads each texture's own dimensions
        // rather than the tank's collision box.
        TankGrowFrames = LoadRange(content, "Sprites/TankGrow", GameplayConstants.TankGrowSteps);
        ElectrodeFrames = LoadRange(content, "Sprites/Electrode", 27);
        Electrode = ElectrodeFrames[0];
        SparkFrames = LoadRange(content, "Sprites/Spark", SparkFrameCount);
        Spark = SparkFrames[0];
        Skull = content.Load<Texture2D>("Sprites/Skull");
        RescueScoreDisplays =
        [
            content.Load<Texture2D>("Sprites/Score_1000"),
            content.Load<Texture2D>("Sprites/Score_2000"),
            content.Load<Texture2D>("Sprites/Score_3000"),
            content.Load<Texture2D>("Sprites/Score_4000"),
            content.Load<Texture2D>("Sprites/Score_5000"),
        ];
        MikeyFrames = LoadRange(content, "Sprites/Mikey", 12);
        MomFrames = LoadRange(content, "Sprites/Mummy", 12);
        DadFrames = LoadRange(content, "Sprites/Daddy", 12);
        BrainFrames = LoadRange(content, "Sprites/Brain", 12);
        ProgBurst = content.Load<Texture2D>("Sprites/ProgBurst");
        TitleWordmarkCore = content.Load<Texture2D>("Sprites/Title_Wordmark_Core");
        TitleWordmarkRim = content.Load<Texture2D>("Sprites/Title_Wordmark_Rim");
        Title2084 = content.Load<Texture2D>("Sprites/Title_2084");
        MissileSmallFrames =
        [
            content.Load<Texture2D>("Sprites/MissileSmall_0"),
            content.Load<Texture2D>("Sprites/MissileSmall_1"),
        ];

        LaserBar = factory.Create(6, 1, PixelArtFactory.BuildLaserBarPattern(Color.White));
        LaserColumn = factory.Create(2, 6, PixelArtFactory.BuildLaserColumnPattern(Color.White));
        LaserDiagonalMain = factory.Create(6, 6, PixelArtFactory.BuildLaserDiagonalMainPattern(Color.White));
        LaserDiagonalAnti = factory.Create(6, 6, PixelArtFactory.BuildLaserDiagonalAntiPattern(Color.White));
        TankShell = content.Load<Texture2D>("Sprites/TankShell");
        AttractCruise = content.Load<Texture2D>("Sprites/AttractCruise");
        PostFrames =
        [
            content.Load<Texture2D>("Sprites/AttractPost_1"),
            content.Load<Texture2D>("Sprites/AttractPost_2"),
            content.Load<Texture2D>("Sprites/AttractPost_3"),
            content.Load<Texture2D>("Sprites/AttractPost_4"),
        ];
        WallPixel = factory.CreateSolid(1, 1, Color.White);
        MiniMan = BuildMiniMan(factory);
        FontLarge = LoadGlyphs(content, "Sprites/Font_L", GlyphSuffixes.Length);
        FontSmall = LoadGlyphs(content, "Sprites/Font_S", 38);
        CursorArrow = content.Load<Texture2D>("Sprites/Font_S_cursorright");
    }

    /// <summary>
    /// ROM MNPIC (RRG23; R5 `$3596`, metadata `$3592` = 3 bytes x 8 rows): the
    /// 6x8 mini man drawn once per spare life (notes §58.4). Each nibble is a
    /// PALETTE INDEX, and the ROM blits it with op `$02` (no transparency, mask
    /// unused) — so the icon keeps its own colours: head slot 2, body/arms slot
    /// 11 (a CYCLING slot — the RGB process, which is why the little man's body
    /// shimmers), legs slot 8, feet slot 3.
    /// </summary>
    private static Texture2D BuildMiniMan(PixelArtFactory factory)
    {
        byte[] source =
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

        const int widthBytes = GameplayConstants.HudMiniManWidthPixels / 2;
        const int height = GameplayConstants.HudMiniManHeightPixels;
        var pixels = new Color[widthBytes * 2 * height];

        for (int row = 0; row < height; row++)
        {
            for (int column = 0; column < widthBytes * 2; column++)
            {
                int value = (source[(row * widthBytes) + (column / 2)] >> (column % 2 == 0 ? 4 : 0)) & 0x0F;
                pixels[(row * widthBytes * 2) + column] =
                    value == 0 ? Color.Transparent : PaletteColorForSlot(value);
            }
        }

        return factory.Create(widthBytes * 2, height, pixels);
    }

    /// <summary>
    /// The colour a baked texture pixel carries for a palette slot: a cycling
    /// slot (10-15) uses its MARKER colour — the shader remaps it to the slot's
    /// live colour at draw time (notes §34/§39) — and a static slot (0-9) uses
    /// the slot's fixed ROM colour.
    /// </summary>
    private static Color PaletteColorForSlot(int slot)
    {
        if (FontSlots.IsCycling(slot))
        {
            byte marker = GamePalette.CyclingSlotMarkers[slot - FontSlots.FirstCyclingSlot];
            return RobotronColor.FromByte(marker);
        }

        return RobotronColor.FromByte(GamePalette.DefaultSlots[slot]);
    }

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

    private static Texture2D[] LoadGlyphs(ContentManager content, string prefix, int count)
    {
        var glyphs = new Texture2D[count];
        for (int i = 0; i < count; i++)
        {
            glyphs[i] = content.Load<Texture2D>(string.Concat(prefix, '_', GlyphSuffixes[i]));
        }

        return glyphs;
    }

    /// <summary>
    /// Draws a font glyph in a STATIC palette slot (0-9): the white master
    /// glyph tinted with the slot's live colour (static slots never change,
    /// so a tint is exact). Notes §38, §39.
    /// </summary>
    public void DrawGlyphStatic(SpriteBatch spriteBatch, Texture2D glyph, int x, int y, int slot,
        SpriteEffects effects = SpriteEffects.None)
    {
        Color tint = Palette?.Color(slot) ?? Color.White;
        int w = ScreenSize.Scaled(glyph.Width);
        int h = ScreenSize.Scaled(glyph.Height);
        spriteBatch.Draw(glyph, new Rectangle(x, y, w, h), null, tint, 0f, Vector2.Zero, effects, 0f);
    }

    /// <summary>
    /// Draws a font glyph in a COLOUR-CYCLING slot (10-15): the white master
    /// through the colour-cycle effect's <c>GlyphCycle</c> pass (notes §92) —
    /// the pass alpha-clips the master and paints every pixel with the slot's
    /// LIVE colour (the same blitter semantics as §39's marker-baked variants,
    /// which the pass makes unnecessary). Without the effect (M4 off) it falls
    /// back to a CPU tint of the slot's live colour, so the glyph still cycles
    /// with the palette.
    /// </summary>
    public void DrawGlyphCycling(SpriteBatch spriteBatch, Texture2D glyph, int x, int y, int slot)
    {
        int w = ScreenSize.Scaled(glyph.Width);
        int h = ScreenSize.Scaled(glyph.Height);
        if (ColorCycleEffect is { } effect &&
            effect.Techniques["GlyphCycle"] is { } technique)
        {
            Palette?.UpdateEffectColors(effect);
            effect.Parameters["SlotId"].SetValue((float)(slot - FontSlots.FirstCyclingSlot));
            technique.Passes[0].Apply();
        }

        // Without the effect the draw degrades to a CPU tint of the slot's live
        // colour (exact for the white master, and it cycles with the palette).
        Color tint = ColorCycleEffect is null ? Palette?.Color(slot) ?? Color.White : Color.White;
        spriteBatch.Draw(glyph, new Rectangle(x, y, w, h), tint);

        // Hand the pass-through back immediately: this pass is DEVICE state, and
        // the caller's next draw is not a glyph-cycle (same as DrawSpriteSolid).
        UsePassThrough();
    }

    /// <summary>
    /// Binds the effect's PASS-THROUGH pass (<c>MainPS</c>: remap the six baked
    /// cycling markers, then <c>return t * input.Color</c> — aside from that
    /// remap it is exactly the built-in sprite shader). EVERY draw that is not a
    /// remap has to bind this itself, because <c>Apply()</c> writes the pixel
    /// shader straight to the device and the binding OUTLIVES the draw that made
    /// it (the same class as notes §40's vertex-shader bug), so the next plain
    /// fill or sprite is rendered through whatever pass was bound last. That is
    /// what made the prog's ghost frames black: the card fill ran through
    /// <c>SolidRemap</c>, i.e. in the black silhouette colour, so the trail lost
    /// its colour cycle (author, 2026-09-16: "It should be a black silhouette on
    /// a colourful block").
    /// </summary>
    private void UsePassThrough()
    {
        if (ColorCycleEffect is { } effect)
        {
            Palette?.UpdateEffectColors(effect);
            effect.CurrentTechnique.Passes[0].Apply();
        }
    }

    /// <summary>
    /// Draws one arcade-font glyph in palette SLOT <paramref name="slot"/>,
    /// picking the right path (notes §58.1, §92): a static slot (0-9) tints the
    /// white master glyph with the slot's live colour; a cycling slot (10-15)
    /// draws the master through the effect's slot-indexed pass, so the glyph
    /// cycles with the palette exactly as the arcade's solid blit does.
    /// </summary>
    public void DrawGlyphSlot(
        SpriteBatch spriteBatch,
        Texture2D[] glyphs,
        int glyphIndex,
        int x,
        int y,
        int slot,
        SpriteEffects effects = SpriteEffects.None)
    {
        if (FontSlots.IsCycling(slot))
        {
            DrawGlyphCycling(spriteBatch, glyphs[glyphIndex], x, y, slot);
        }
        else
        {
            DrawGlyphStatic(spriteBatch, glyphs[glyphIndex], x, y, slot, effects);
        }
    }

    /// <summary>
    /// Draws the mini man LIVES icon (notes §58.2) at (x, y), SpecScale x its
    /// 6x8 arcade-pixel size, through the colour-cycle effect so its slot-11
    /// body/arms cycle like the arcade's.
    /// </summary>
    public void DrawMiniMan(SpriteBatch spriteBatch, int x, int y)
    {
        UsePassThrough();
        spriteBatch.Draw(
            MiniMan,
            new Rectangle(x, y, ScreenSize.Scaled(MiniMan.Width), ScreenSize.Scaled(MiniMan.Height)),
            Color.White);
    }

    /// <summary>
    /// Draws a string in the arcade's SMALL font (notes §58.3 — the font every
    /// ROM message uses, 4x5 glyphs) in one palette slot, returning the X after
    /// the last glyph. Glyphs advance their width + 1 (ROM $6009); a space
    /// advances 2 px without drawing (the ROM blits its 1-px ':' glyph for a
    /// space); an unknown character is skipped.
    /// </summary>
    public int DrawSmallFontText(SpriteBatch spriteBatch, string text, int x, int y, int slot)
    {
        foreach (char character in text)
        {
            if (character == ' ')
            {
                x += ScreenSize.Scaled(GameplayConstants.HudSmallFontSpaceAdvancePixels);
                continue;
            }

            int index = GlyphIndex(character);
            if (index >= 0 && index < FontSmall.Length)
            {
                DrawGlyphSlot(spriteBatch, FontSmall, index, x, y, slot);
                x += ScreenSize.Scaled(FontSmall[index].Width + GameplayConstants.HudSmallFontGlyphGapPixels);
                continue;
            }

            // The arcade's SMALL font stops at ')' — 38 glyphs: digits, A-Z and the two brackets
            // — so it has no ':' of its own (the ROM's ':' lives in the LARGE font). Rather than
            // drop the character, draw the large font's glyph in its place: that is the arcade's
            // own artwork, and it is why the DEFINE INPUTS page can print "ENTER: SET THE INPUT"
            // (notes §101.11). The colon is one row taller than the capitals, exactly as the two
            // arcade fonts differ.
            if (index >= 0 && index < FontLarge.Length)
            {
                DrawGlyphSlot(spriteBatch, FontLarge, index, x, y, slot);
                x += ScreenSize.Scaled(FontLarge[index].Width + GameplayConstants.HudSmallFontGlyphGapPixels);
            }
        }

        return x;
    }

    /// <summary>
    /// Draws a string in the arcade's LARGE font (the score digits, the title
    /// screen's "ROBOTRON 2084" / "SAVE THE LAST HUMAN FAMILY", notes §94.1) in
    /// one palette slot, returning the X after the last glyph. Glyphs advance
    /// their width + 1 (the ROM's large-font advance, the same rule the score
    /// uses — <c>HudScoreDigitAdvancePixels</c>); a space advances the same as
    /// the small font's blank; an unknown character is skipped.
    /// </summary>
    public int DrawLargeFontText(SpriteBatch spriteBatch, string text, int x, int y, int slot)
    {
        foreach (char character in text)
        {
            if (character == ' ')
            {
                x += ScreenSize.Scaled(GameplayConstants.HudSmallFontBlankAdvancePixels);
                continue;
            }

            int index = GlyphIndex(character);
            if (index < 0 || index >= FontLarge.Length)
            {
                continue;
            }

            DrawGlyphSlot(spriteBatch, FontLarge, index, x, y, slot);
            x += ScreenSize.Scaled(FontLarge[index].Width + GameplayConstants.HudSmallFontGlyphGapPixels);
        }

        return x;
    }

    /// <summary>
    /// Index into <see cref="FontSmall"/> (or <see cref="FontLarge"/>) for an
    /// ASCII character, or -1 for one the font has no glyph for. The ROM indexes
    /// its font tables by (ASCII - $30) and substitutes its blank glyph for a
    /// space, so the table order is '0'-'9', 'A'-'Z', then '(' ')' (':' and the
    /// arrow exist in the large font only), then the LARGE font's punctuation
    /// '!' ',' '.' '-' (notes §96 — indices 40-43, appended so nothing moves).
    /// </summary>
    public static int GlyphIndex(char c) => c switch
    {
        >= '0' and <= '9' => c - '0',
        >= 'A' and <= 'Z' => 10 + (c - 'A'),
        '(' => 36,
        ')' => 37,
        ':' => 38,
        '!' => 40,
        ',' => 41,
        '.' => 42,
        '-' => 43,
        _ => -1,
    };

    /// <summary>
    /// The width of a string in the arcade's SMALL font, in SPEC pixels: each glyph advances its own
    /// width + 1 (the ROM's $6009 rule) and a space advances the blank's 2 (the ROM blits its 1-px
    /// ':' glyph for a space). The states that CENTRE a line use this rather than assuming a fixed
    /// advance — the glyph widths differ, and the LARGE font's differ from the small one's.
    /// </summary>
    public int MeasureSmallText(string text) => MeasureText(FontSmall, text);

    /// <summary>The same measurement for the LARGE font (the score's own advance rule).</summary>
    public int MeasureLargeText(string text) => MeasureText(FontLarge, text);

    private static int MeasureText(Texture2D[] glyphs, string text)
    {
        int width = 0;
        foreach (char character in text)
        {
            if (character == ' ')
            {
                width += GameplayConstants.HudSmallFontBlankAdvancePixels;
                continue;
            }

            int index = GlyphIndex(character);
            if (index >= 0 && index < glyphs.Length)
            {
                width += glyphs[index].Width + GameplayConstants.HudSmallFontGlyphGapPixels;
            }
        }

        return width;
    }

    /// <summary>
    /// The high score table's number printer (notes §98.5, from the author's photo
    /// of the arcade): the SIGNIFICANT digits only, packed from the cursor — the
    /// table does NOT use the in-play score display's blanked-leading-zero field
    /// (a row reads "1) DRJ 52127", not "1) DRJ   52127"), which is what makes the
    /// rows' digits line up. Returns the X after the number.
    /// </summary>
    public int DrawTableNumber(SpriteBatch spriteBatch, Texture2D[] glyphs, int value, int x, int y, int slot)
    {
        foreach (ScoreFormatter.ScoreDigit digit in ScoreFormatter.Digits(value))
        {
            if (digit.Suppressed)
            {
                continue;
            }

            DrawGlyphSlot(spriteBatch, glyphs, digit.Value, x, y, slot);
            x += ScreenSize.Scaled(glyphs[digit.Value].Width + GameplayConstants.HudSmallFontGlyphGapPixels);
        }

        return x;
    }

    /// <summary>The high score table's LARGE-font score (<c>PRSCOR</c> → <c>WRD7V</c>).</summary>
    public int DrawLargeTableNumber(SpriteBatch spriteBatch, int value, int x, int y, int slot) =>
        DrawTableNumber(spriteBatch, FontLarge, value, x, y, slot);

    /// <summary>The high score table's SMALL-font score (<c>PRSCOR</c> → <c>WRD5V</c>).</summary>
    public int DrawSmallTableNumber(SpriteBatch spriteBatch, int value, int x, int y, int slot) =>
        DrawTableNumber(spriteBatch, FontSmall, value, x, y, slot);

    /// <summary>
    /// Draws a ROM frame at SpecScale× arcade pixels, centered inside
    /// <paramref name="bounds"/> (the entity's collision box).
    /// </summary>
    public void DrawSprite(SpriteBatch spriteBatch, Texture2D texture, Rectangle bounds, Color tint)
    {
        UsePassThrough();
        spriteBatch.Draw(texture, CentredIn(bounds, texture), null, tint, 0f, Vector2.Zero, SpriteEffects.None, 0f);
    }

    /// <summary>
    /// Blitter op <c>$1A</c> — "SOLID + TRANSPARENT" (RRS22 <c>MPCTNV</c>,
    /// "ON MONOCHROME PICT"; notes §47): draws the sprite's SHAPE in one
    /// colour, discarding the art's own colours. This is the arcade's REMAP
    /// COLOUR mode, and it is how the ROM draws anything in a single colour —
    /// a post being "turned on" (<c>OPON</c>), the flashing human and brain
    /// while a brain reprograms one (<c>BRNON</c>/<c>HUMON</c>), mono text.
    ///
    /// <paramref name="color"/> usually comes from <see cref="SlotColor"/> so
    /// the caller can name a palette slot; a cycling slot then cycles here for
    /// free, exactly as the hardware's palette does.
    /// </summary>
    public void DrawSpriteSolid(SpriteBatch spriteBatch, Texture2D texture, Rectangle bounds, Color color)
    {
        if (ColorCycleEffect is { } effect &&
            effect.Techniques["SolidRemap"] is { } technique)
        {
            effect.Parameters["RemapColor"].SetValue(color.ToVector4());
            technique.Passes[0].Apply();
        }

        // Without the effect the draw degrades to a tint (exact for white art).
        spriteBatch.Draw(texture, CentredIn(bounds, texture), null, color, 0f, Vector2.Zero, SpriteEffects.None, 0f);

        // Hand the pass-through back immediately: this pass is DEVICE state, and
        // the caller's next draw (usually a card fill) is not a remap.
        UsePassThrough();
    }

    /// <summary>
    /// Blitter op <c>$12</c> — "SOLID" (RRS22 <c>BLKONV</c>, "ON DMA BLOCK"):
    /// fills the rectangle with one colour, ignoring any source. The ROM uses
    /// it for the brain's flashing block while it reprograms a human (notes
    /// §47) and, with colour 0, to erase a rectangle (<c>PCTOFV</c>/<c>BLKCLV</c>).
    /// </summary>
    public void DrawSolidRectangle(SpriteBatch spriteBatch, Rectangle bounds, Color color)
    {
        UsePassThrough(); // a solid rect is NOT a remap — see UsePassThrough
        spriteBatch.Draw(WallPixel, bounds, color);
    }

    /// <summary>
    /// The ROM's two-colour object blit ($1DC2
    /// <c>BLIT_IMAGE_AS_SOLID_COLOUR_WITH_SOLID_BACKGROUND</c>): op <c>$12</c>
    /// fills the frame with <paramref name="background"/>, then op <c>$1A</c>
    /// draws the sprite's SHAPE in <paramref name="shape"/> on top. This is how
    /// a human looks while a brain reprograms it (notes §46/§47). Both are
    /// drawn on the same destination rectangle, so the block and the silhouette
    /// stay aligned.
    /// </summary>
    public void DrawSpriteSolidWithBackground(
        SpriteBatch spriteBatch,
        Texture2D texture,
        Rectangle bounds,
        Color background,
        Color shape)
    {
        // The frame is a plain fill, so it must bind the pass-through EXPLICITLY:
        // otherwise it is drawn through whatever pass was bound last and comes out
        // in that colour (the black-ghost-card bug).
        UsePassThrough();
        spriteBatch.Draw(WallPixel, CentredIn(bounds, texture), background);
        DrawSpriteSolid(spriteBatch, texture, bounds, shape);
    }

    /// <summary>The live RGB of a palette slot (0-15); white when no palette is wired.</summary>
    public Color SlotColor(int slot) => Palette?.Color(slot) ?? Color.White;

    private static Rectangle CentredIn(Rectangle bounds, Texture2D texture)
    {
        int w = ScreenSize.Scaled(texture.Width);
        int h = ScreenSize.Scaled(texture.Height);
        return new Rectangle(
            bounds.X + (bounds.Width - w) / 2,
            bounds.Y + (bounds.Height - h) / 2,
            w, h);
    }

    private static Texture2D[] LoadRange(ContentManager content, string prefix, int count)
    {
        var frames = new Texture2D[count];
        for (int i = 0; i < count; i++)
        {
            frames[i] = content.Load<Texture2D>(string.Concat(prefix, '_', i + 1));
        }

        return frames;
    }
}
