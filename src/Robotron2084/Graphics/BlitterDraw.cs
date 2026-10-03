using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Palette;

namespace Robotron2084.Graphics;

/// <summary>
/// The arcade's blitter operations over the port's textures: plain sprite draws, the solid-colour remap, solid
/// rectangles and colour-cycling glyphs, all through the colour-cycle shader when one is set.
/// </summary>
public sealed class BlitterDraw
{
    /// <summary>The colour-cycle shader's technique for a cycling glyph; must match
    /// <c>Content/Effects/ColorCycle.fx</c>.</summary>
    private const string GlyphCycleTechnique = "GlyphCycle";

    /// <summary>The colour-cycle shader's glyph technique's slot parameter; must match
    /// <c>Content/Effects/ColorCycle.fx</c>.</summary>
    private const string GlyphSlotParameter = "SlotId";

    /// <summary>The colour-cycle shader's solid technique's colour parameter; must match
    /// <c>Content/Effects/ColorCycle.fx</c>.</summary>
    private const string SolidColorParameter = "RemapColor";

    /// <summary>The colour-cycle shader's technique for a solid fill; must match
    /// <c>Content/Effects/ColorCycle.fx</c>.</summary>
    private const string SolidRemapTechnique = "SolidRemap";

    private readonly Texture2D _wallPixel;

    /// <summary>Creates a blitter that fills rectangles with a 1x1 white pixel.</summary>
    /// <param name="wallPixel">The white pixel every solid fill is a tinted, stretched copy of.</param>
    public BlitterDraw(Texture2D wallPixel) => _wallPixel = wallPixel;

    /// <summary>
    /// When set, entity draws run through the colour-cycle pixel shader
    /// (remaps the six cycling-slot marker colours to their live palette
    /// colours). Null = plain draws.
    /// </summary>
    public Effect? ColorCycleEffect { get; set; }

    /// <summary>The live 16-slot palette the effect remaps into (slots 10-15 cycle).</summary>
    public GamePalette? Palette { get; set; }

    /// <summary>
    /// The rectangle an animation frame is DRAWN in inside an entity's collision box: centred in the box, at the
    /// render scale. It is the ONE definition of where a sprite sits in its box — the drawer uses it, and so
    /// does pixel-perfect collision (<see cref="SpriteMask.Overlap"/>), so the two cannot drift apart
    /// (notes §118).
    /// </summary>
    /// <param name="bounds">The entity's collision box, in screen pixels.</param>
    /// <param name="animationFrame">The animation frame being drawn.</param>
    public static Rectangle DrawnRect(Rectangle bounds, Texture2D animationFrame)
    {
        int w = ScreenSize.ToPortPixels(animationFrame.Width);
        int h = ScreenSize.ToPortPixels(animationFrame.Height);
        return new Rectangle(
            bounds.X + (bounds.Width - w) / 2,
            bounds.Y + (bounds.Height - h) / 2,
            w, h);
    }

    /// <summary>
    /// Draws a font glyph in a COLOUR-CYCLING slot (10-15): the white master
    /// through the colour-cycle effect's <c>GlyphCycle</c> pass (notes §92) —
    /// the pass alpha-clips the master and paints every pixel with the slot's
    /// LIVE colour (the same blitter semantics as §39's marker-baked variants,
    /// which the pass makes unnecessary). Without the effect it falls
    /// back to a CPU tint of the slot's live colour, so the glyph still cycles
    /// with the palette.
    /// </summary>
    public void DrawGlyphCycling(SpriteBatch spriteBatch, Texture2D glyph, int x, int y, int slot)
    {
        int w = ScreenSize.ToPortPixels(glyph.Width);
        int h = ScreenSize.ToPortPixels(glyph.Height);
        if (ColorCycleEffect is { } effect &&
            effect.Techniques[GlyphCycleTechnique] is { } technique)
        {
            Palette?.UpdateEffectColors(effect);
            effect.Parameters[GlyphSlotParameter].SetValue((float)(slot - FontSlots.FirstCyclingSlot));
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
    /// Draws a font glyph in a STATIC palette slot (0-9): the white master
    /// glyph tinted with the slot's live colour (static slots never change,
    /// so a tint is exact). Notes §38, §39.
    /// </summary>
    public void DrawGlyphStatic(SpriteBatch spriteBatch, Texture2D glyph, int x, int y, int slot,
        SpriteEffects effects = SpriteEffects.None)
    {
        Color tint = Palette?.Color(slot) ?? Color.White;
        int w = ScreenSize.ToPortPixels(glyph.Width);
        int h = ScreenSize.ToPortPixels(glyph.Height);
        spriteBatch.Draw(glyph, new Rectangle(x, y, w, h), null, tint, 0f, Vector2.Zero, effects, 0f);
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
        spriteBatch.Draw(_wallPixel, bounds, color);
    }

    /// <summary>
    /// Draws a ROM frame at SpecScale× arcade pixels, centered inside
    /// <paramref name="bounds"/> (the entity's collision box).
    /// </summary>
    public void DrawSprite(SpriteBatch spriteBatch, Texture2D texture, Rectangle bounds, Color tint)
    {
        UsePassThrough();
        spriteBatch.Draw(texture, DrawnRect(bounds, texture), null, tint, 0f, Vector2.Zero, SpriteEffects.None, 0f);
    }

    /// <summary>
    /// Blitter op <c>$1A</c> — "SOLID + TRANSPARENT" (RRS22 <c>MPCTNV</c>,
    /// "ON MONOCHROME PICT"; notes §47): draws the sprite's SHAPE in one
    /// colour, discarding the animation frame's own colours. This is the arcade's REMAP
    /// COLOUR mode, and it is how the ROM draws anything in a single colour —
    /// an electrode being "turned on" (<c>OPON</c>), the flashing human and brain
    /// while a brain reprograms one (<c>BRNON</c>/<c>HUMON</c>), mono text.
    ///
    /// <paramref name="color"/> usually comes from <see cref="GetSlotColour"/> so
    /// the caller can name a palette slot; a cycling slot then cycles here for
    /// free, exactly as the hardware's palette does.
    /// </summary>
    public void DrawSpriteSolid(SpriteBatch spriteBatch, Texture2D texture, Rectangle bounds, Color color)
    {
        if (ColorCycleEffect is { } effect &&
            effect.Techniques[SolidRemapTechnique] is { } technique)
        {
            effect.Parameters[SolidColorParameter].SetValue(color.ToVector4());
            technique.Passes[0].Apply();
        }

        // Without the effect the draw degrades to a tint (exact for white sprites).
        spriteBatch.Draw(texture, DrawnRect(bounds, texture), null, color, 0f, Vector2.Zero, SpriteEffects.None, 0f);

        // Hand the pass-through back immediately: this pass is DEVICE state, and
        // the caller's next draw (usually a card fill) is not a remap.
        UsePassThrough();
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
        // in that colour.
        UsePassThrough();
        spriteBatch.Draw(_wallPixel, DrawnRect(bounds, texture), background);
        DrawSpriteSolid(spriteBatch, texture, bounds, shape);
    }

    /// <summary>
    /// Blitter op <c>$1A</c> for a PIECE of a sprite: draws the shape of one part of a white mask in one colour, which is how a
    /// sprite that is being cut into strips is drawn in a palette colour.
    /// </summary>
    /// <param name="spriteBatch">The batch to draw into.</param>
    /// <param name="texture">The white mask.</param>
    /// <param name="source">The part of the mask to draw, in its own pixels.</param>
    /// <param name="destination">Where to draw it, in port pixels.</param>
    /// <param name="color">The colour, usually from <see cref="GetSlotColour"/>.</param>
    public void DrawSpriteSolidPiece(SpriteBatch spriteBatch, Texture2D texture, Rectangle source, Rectangle destination, Color color)
    {
        if (ColorCycleEffect is { } effect &&
            effect.Techniques[SolidRemapTechnique] is { } technique)
        {
            effect.Parameters[SolidColorParameter].SetValue(color.ToVector4());
            technique.Passes[0].Apply();
        }

        spriteBatch.Draw(texture, destination, source, color);
        UsePassThrough();
    }

    /// <summary>The live RGB of a palette slot (0-15); white when no palette is wired.</summary>
    public Color GetSlotColour(int slot) => Palette?.Color(slot) ?? Color.White;

    /// <summary>
    /// Binds the effect's PASS-THROUGH pass (<c>MainPS</c>: remap the six baked
    /// cycling markers, then <c>return t * input.Color</c> — aside from that
    /// remap it is exactly the built-in sprite shader). EVERY draw that is not a
    /// remap has to bind this itself, because <c>Apply()</c> writes the pixel
    /// shader straight to the device and the binding OUTLIVES the draw that made
    /// it (the same class as notes §40's vertex-shader bug), so the next plain
    /// fill or sprite would be rendered through whatever pass was bound last.
    /// </summary>
    internal void UsePassThrough()
    {
        if (ColorCycleEffect is { } effect)
        {
            Palette?.UpdateEffectColors(effect);
            effect.CurrentTechnique.Passes[0].Apply();
        }
    }
}
