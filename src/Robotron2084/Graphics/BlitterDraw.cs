using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Palette;

namespace Robotron2084.Graphics;

/// <summary>
///     The arcade's blitter operations over the port's textures: plain sprite draws, the solid-colour remap, solid
///     rectangles and colour-cycling glyphs, all through the colour-cycle shader when one is set.
/// </summary>
public sealed class BlitterDraw
{
    /// <summary>
    ///     The colour-cycle shader's technique for a cycling glyph; must match
    ///     <c>Content/Effects/ColourCycle.fx</c>.
    /// </summary>
    private const string GlyphCycleTechnique = "GlyphCycle";

    /// <summary>
    ///     The colour-cycle shader's glyph technique's palette index parameter; must match
    ///     <c>Content/Effects/ColourCycle.fx</c>.
    /// </summary>
    private const string GlyphPaletteIndexParameter = "SlotId";

    /// <summary>
    ///     The colour-cycle shader's solid technique's colour parameter; must match
    ///     <c>Content/Effects/ColourCycle.fx</c>.
    /// </summary>
    private const string SolidColourParameter = "RemapColor";

    /// <summary>
    ///     The colour-cycle shader's technique for a solid fill; must match
    ///     <c>Content/Effects/ColourCycle.fx</c>.
    /// </summary>
    private const string SolidRemapTechnique = "SolidRemap";

    private readonly Texture2D _wallPixelTexture;

    /// <summary>Creates a blitter that fills rectangles with a 1x1 white pixel.</summary>
    /// <param name="wallPixelTexture">The white pixel every solid fill is a tinted, stretched copy of.</param>
    public BlitterDraw(Texture2D wallPixelTexture)
    {
        _wallPixelTexture = wallPixelTexture;
    }

    /// <summary>
    ///     When set, entity draws run through the colour-cycle pixel shader
    ///     (remaps the six cycling palette index marker colours to their live palette
    ///     colours). Null = plain draws.
    /// </summary>
    public Effect? ColourCycleEffect { get; set; }

    /// <summary>The live 16-index palette the effect remaps into (palette indices 10-15 cycle).</summary>
    public GamePalette? GamePalette { get; set; }

    /// <summary>
    ///     The rectangle an animation frame is DRAWN in inside an entity's collision box: centred in the box, at the
    ///     render scale. It is the ONE definition of where a sprite sits in its box — the drawer uses it, and so
    ///     does pixel-perfect collision (<see cref="SpriteMask.Overlap" />), so the two cannot drift apart
    ///     (notes §118).
    /// </summary>
    /// <param name="bounds">The entity's collision box, in screen pixels.</param>
    /// <param name="animationFrameTexture">The animation frame being drawn.</param>
    public static Rectangle GetDrawnBounds(Rectangle bounds, Texture2D animationFrameTexture)
    {
        var width = ScreenSize.ToPortPixelsFromArcadePixels(animationFrameTexture.Width);
        var height = ScreenSize.ToPortPixelsFromArcadePixels(animationFrameTexture.Height);
        return new Rectangle(
            bounds.X + (bounds.Width - width) / 2,
            bounds.Y + (bounds.Height - height) / 2,
            width, height);
    }

    /// <summary>
    ///     Draws a font glyph in a COLOUR-CYCLING palette index (10-15): the white master
    ///     through the colour-cycle effect's <c>GlyphCycle</c> pass (notes §92) —
    ///     the pass alpha-clips the master and paints every pixel with the palette index's
    ///     LIVE colour (the same blitter semantics as §39's marker-baked variants,
    ///     which the pass makes unnecessary). Without the effect it falls
    ///     back to a CPU tint of the palette index's live colour, so the glyph still cycles
    ///     with the palette.
    /// </summary>
    public void DrawGlyphCycling(SpriteBatch spriteBatch, Texture2D glyphTexture, int x, int y, int paletteIndex)
    {
        var width = ScreenSize.ToPortPixelsFromArcadePixels(glyphTexture.Width);
        var height = ScreenSize.ToPortPixelsFromArcadePixels(glyphTexture.Height);
        if (ColourCycleEffect is { } effect &&
            effect.Techniques[GlyphCycleTechnique] is { } technique)
        {
            GamePalette?.UpdateEffectColours(effect);
            effect.Parameters[GlyphPaletteIndexParameter].SetValue((float)(paletteIndex - FontPaletteIndices.FirstCyclingPaletteIndex));
            technique.Passes[0].Apply();
        }

        // Without the effect the draw degrades to a CPU tint of the palette index's live
        // colour (exact for the white master, and it cycles with the palette).
        var tint = ColourCycleEffect is null ? GamePalette?.GetColour(paletteIndex) ?? Color.White : Color.White;
        spriteBatch.Draw(glyphTexture, new Rectangle(x, y, width, height), tint);

        // Hand the pass-through back immediately: this pass is DEVICE state, and
        // the caller's next draw is not a glyph-cycle (same as DrawSpriteSolid).
        UsePassThrough();
    }

    /// <summary>
    ///     Draws one arcade-font glyph in palette SLOT <paramref name="paletteIndex" />,
    ///     picking the right path (notes §58.1, §92): a static palette index (0-9) tints the
    ///     white master glyph with the palette index's live colour; a cycling palette index (10-15)
    ///     draws the master through the effect's per-palette-index pass, so the glyph
    ///     cycles with the palette exactly as the arcade's solid blit does.
    /// </summary>
    public void DrawGlyphPaletteIndex(
        SpriteBatch spriteBatch,
        Texture2D[] glyphTextures,
        int glyphIndex,
        int x,
        int y,
        int paletteIndex,
        SpriteEffects effects = SpriteEffects.None)
    {
        if (FontPaletteIndices.IsCycling(paletteIndex))
            DrawGlyphCycling(spriteBatch, glyphTextures[glyphIndex], x, y, paletteIndex);
        else
            DrawGlyphStatic(spriteBatch, glyphTextures[glyphIndex], x, y, paletteIndex, effects);
    }

    /// <summary>
    ///     Draws a font glyph in a STATIC palette index (0-9): the white master
    ///     glyph tinted with the palette index's live colour (static palette indices never change,
    ///     so a tint is exact). Notes §38, §39.
    /// </summary>
    public void DrawGlyphStatic(SpriteBatch spriteBatch, Texture2D glyphTexture, int x, int y, int paletteIndex,
        SpriteEffects effects = SpriteEffects.None)
    {
        var tint = GamePalette?.GetColour(paletteIndex) ?? Color.White;
        var width = ScreenSize.ToPortPixelsFromArcadePixels(glyphTexture.Width);
        var height = ScreenSize.ToPortPixelsFromArcadePixels(glyphTexture.Height);
        spriteBatch.Draw(glyphTexture, new Rectangle(x, y, width, height), null, tint, 0f, Vector2.Zero, effects, 0f);
    }

    /// <summary>
    ///     Blitter op <c>$12</c> — "SOLID" (RRS22 <c>BLKONV</c>, "ON DMA BLOCK"):
    ///     fills the rectangle with one colour, ignoring any source. The ROM uses
    ///     it for the brain's flashing block while it reprograms a family member (notes
    ///     §47) and, with colour 0, to erase a rectangle (<c>PCTOFV</c>/<c>BLKCLV</c>).
    /// </summary>
    public void DrawSolidRectangle(SpriteBatch spriteBatch, Rectangle bounds, Color colour)
    {
        UsePassThrough(); // a solid rect is NOT a remap — see UsePassThrough
        spriteBatch.Draw(_wallPixelTexture, bounds, colour);
    }

    /// <summary>
    ///     Draws one animation frame at PortPixelsPerArcadePixel× arcade pixels, centred inside
    ///     <paramref name="bounds" /> (the entity's collision box).
    /// </summary>
    public void DrawSprite(SpriteBatch spriteBatch, Texture2D texture, Rectangle bounds, Color tint)
    {
        UsePassThrough();
        spriteBatch.Draw(texture, GetDrawnBounds(bounds, texture), null, tint, 0f, Vector2.Zero, SpriteEffects.None, 0f);
    }

    /// <summary>
    ///     Blitter op <c>$1A</c> — "SOLID + TRANSPARENT" (RRS22 <c>MPCTNV</c>,
    ///     "ON MONOCHROME PICT"; notes §47): draws the sprite's SHAPE in one
    ///     colour, discarding the animation frame's own colours. This is the arcade's REMAP
    ///     COLOUR mode, and it is how the ROM draws anything in a single colour —
    ///     an electrode being "turned on" (<c>OPON</c>), the flashing family member and brain
    ///     while a brain reprograms one (<c>BRNON</c>/<c>HUMON</c>), mono text.
    ///     <paramref name="colour" /> usually comes from <see cref="GetPaletteIndexColour" /> so
    ///     the caller can name a palette index; a cycling palette index then cycles here for
    ///     free, exactly as the hardware's palette does.
    /// </summary>
    public void DrawSpriteSolid(SpriteBatch spriteBatch, Texture2D texture, Rectangle bounds, Color colour)
    {
        if (ColourCycleEffect is { } effect &&
            effect.Techniques[SolidRemapTechnique] is { } technique)
        {
            effect.Parameters[SolidColourParameter].SetValue(colour.ToVector4());
            technique.Passes[0].Apply();
        }

        // Without the effect the draw degrades to a tint (exact for white sprites).
        spriteBatch.Draw(texture, GetDrawnBounds(bounds, texture), null, colour, 0f, Vector2.Zero, SpriteEffects.None, 0f);

        // Hand the pass-through back immediately: this pass is DEVICE state, and
        // the caller's next draw (usually a card fill) is not a remap.
        UsePassThrough();
    }

    /// <summary>
    ///     Blitter op <c>$1A</c> for a PIECE of a sprite: draws the shape of one part of a white mask in one colour, which is
    ///     how a
    ///     sprite that is being cut into strips is drawn in a palette colour.
    /// </summary>
    /// <param name="spriteBatch">The batch to draw into.</param>
    /// <param name="texture">The white mask.</param>
    /// <param name="source">The part of the mask to draw, in its own pixels.</param>
    /// <param name="destination">Where to draw it, in port pixels.</param>
    /// <param name="colour">The colour, usually from <see cref="GetPaletteIndexColour" />.</param>
    public void DrawSpriteSolidPiece(SpriteBatch spriteBatch, Texture2D texture, Rectangle source,
        Rectangle destination, Color colour)
    {
        if (ColourCycleEffect is { } effect &&
            effect.Techniques[SolidRemapTechnique] is { } technique)
        {
            effect.Parameters[SolidColourParameter].SetValue(colour.ToVector4());
            technique.Passes[0].Apply();
        }

        spriteBatch.Draw(texture, destination, source, colour);
        UsePassThrough();
    }

    /// <summary>
    ///     The ROM's two-colour object blit ($1DC2
    ///     <c>BLIT_IMAGE_AS_SOLID_COLOUR_WITH_SOLID_BACKGROUND</c>): op <c>$12</c>
    ///     fills the frame with <paramref name="background" />, then op <c>$1A</c>
    ///     draws the sprite's SHAPE in <paramref name="shape" /> on top. This is how
    ///     a family member looks while a brain reprograms it (notes §46/§47). Both are
    ///     drawn on the same destination rectangle, so the block and the silhouette
    ///     stay aligned.
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
        // in that colour.
        UsePassThrough();
        spriteBatch.Draw(_wallPixelTexture, GetDrawnBounds(bounds, texture), background);
        DrawSpriteSolid(spriteBatch, texture, bounds, shape);
    }

    /// <summary>The live RGB of a palette index (0-15); white when no palette is wired.</summary>
    public Color GetPaletteIndexColour(int paletteIndex)
    {
        return GamePalette?.GetColour(paletteIndex) ?? Color.White;
    }

    /// <summary>
    ///     Binds the effect's PASS-THROUGH pass (<c>MainPS</c>: remap the six baked
    ///     cycling markers, then <c>return t * input.Color</c> — aside from that
    ///     remap it is exactly the built-in sprite shader). EVERY draw that is not a
    ///     remap has to bind this itself, because <c>Apply()</c> writes the pixel
    ///     shader straight to the device and the binding OUTLIVES the draw that made
    ///     it (the same class as notes §40's vertex-shader bug), so the next plain
    ///     fill or sprite would be rendered through whatever pass was bound last.
    /// </summary>
    internal void UsePassThrough()
    {
        if (ColourCycleEffect is { } effect)
        {
            GamePalette?.UpdateEffectColours(effect);
            effect.CurrentTechnique.Passes[0].Apply();
        }
    }
}
