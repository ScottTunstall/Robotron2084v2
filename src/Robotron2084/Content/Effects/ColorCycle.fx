// M4 colour-cycle pixel shader (arcade-fidelity-notes §0.4, §3.3, §34,
// progress log 10). Sprite PNGs store six FIXED marker colours for the six
// colour-cycling palette slots (10-15) — the de-duplicated
// RobotronPaletteService values C4/F4/CC/81/45/2F. This shader detects a
// texel whose RGB equals one of those markers and substitutes the slot's
// CURRENT live colour (pushed per-frame by GamePalette.UpdateEffectColors).
// All other texels pass through unchanged (tinted by the SpriteBatch colour
// as usual).
//
// *** THIS EFFECT HAS NO VERTEX SHADER — that absence is the fix, not an
// omission (notes §40). *** SpriteBatch feeds its vertex buffer in SCREEN
// pixels and relies on the vertex shader to apply its screen-space
// projection. It binds the built-in sprite VS+PS pair once per Begin()
// (SpriteBatch.Setup), and EffectPass.Apply() only overwrites the stages a
// pass actually declares (`if (_vertexShader != null) { device.VertexShader
// = ...; }`). This effect used to declare its own VS; the first Apply()
// therefore replaced the sprite VS for the REST of the batch with one that
// performed no projection at all — so every later sprite (including draws
// that pass no effect: HUD text, life icons) landed outside the clip volume.
// The wall, drawn before that first Apply(), was the only survivor: the
// author's "nothing renders but the wall" bug. Leaving the pass PS-only
// keeps SpriteBatch's sprite VS bound, so positions are transformed exactly
// as the built-in path does — pixel-perfect rendering preserved.
//
// NO ARRAYS IN THIS FILE (notes §34): the MonoGame 3.8.5.1 EffectProcessor
// TPGParser rejects the HLSL array-constructor syntax (`float4[6](...)` =
// error X3000). The six marker comparisons are unrolled as literals and the
// live colours are six named uniforms (Live10..Live15) — zero '[' tokens.
//
// Compiled by the MonoGame 3.8 EffectProcessor (dxc) at build time via
// Content.mgcb — MonoGame's Effect has no HLSL-source constructor.

sampler2D SpriteTexture : register(s0);

float4 Live10 : register(c0);
float4 Live11 : register(c1);
float4 Live12 : register(c2);
float4 Live13 : register(c3);
float4 Live14 : register(c4);
float4 Live15 : register(c5);

// Blitter REMAP COLOUR value (DMACON, $CA01). Used by the SolidRemap
// technique below; not read by MainPS.
float4 RemapColor : register(c6);

struct PSInput
{
    float2 TextureCoordinate : TEXCOORD0;
    float4 Color : COLOR;
};

float4 MainPS(PSInput input) : COLOR
{
    float4 t = tex2D(SpriteTexture, input.TextureCoordinate);

    // Slot 10 (LF) — marker $C4
    if (abs(t.r - 0.56470588f) < 0.004f &&
        abs(t.g - 0.0f)         < 0.004f &&
        abs(t.b - 0.94117647f) < 0.004f)
    {
        t = float4(Live10.rgb, t.a);
    }
    // Slot 11 (RGB) — marker $F4
    if (abs(t.r - 0.56470588f) < 0.004f &&
        abs(t.g - 0.81568627f) < 0.004f &&
        abs(t.b - 0.94117647f) < 0.004f)
    {
        t = float4(Live11.rgb, t.a);
    }
    // Slot 12 (DECAY) — marker $CC
    if (abs(t.r - 0.56470588f) < 0.004f &&
        abs(t.g - 0.12549020f) < 0.004f &&
        abs(t.b - 0.94117647f) < 0.004f)
    {
        t = float4(Live12.rgb, t.a);
    }
    // Slot 13 (LASER) — marker $81
    if (abs(t.r - 0.12549020f) < 0.004f &&
        abs(t.g - 0.0f)         < 0.004f &&
        abs(t.b - 0.62745098f) < 0.004f)
    {
        t = float4(Live13.rgb, t.a);
    }
    // Slot 14 (BPR) — marker $45
    if (abs(t.r - 0.69019608f) < 0.004f &&
        abs(t.g - 0.0f)         < 0.004f &&
        abs(t.b - 0.31372549f) < 0.004f)
    {
        t = float4(Live14.rgb, t.a);
    }
    // Slot 15 (RGOLD) — marker $2F
    if (abs(t.r - 0.94117647f) < 0.004f &&
        abs(t.g - 0.69019608f) < 0.004f &&
        abs(t.b - 0.0f)         < 0.004f)
    {
        t = float4(Live15.rgb, t.a);
    }

    return t * input.Color;
}

// The arcade blitter's REMAP COLOUR mode, operation DMACTL $1A ("solid mode,
// transparent" — RRS22 `MPCTNV`, "ON MONOCHROME PICT"; the disassembly's
// BLIT_IMAGE_IN_SOLID_COLOUR_AND_TRANSPARENCY).
//
// The hardware draws the source's shape in ONE colour taken from the constant
// register DMACON instead of the source's own colours: bit $10 of the control
// byte means "use the constant", bit $08 means "skip the source's zero
// pixels". Every op that needs a one-colour object — the flashing human while
// a brain reprograms it (BRNON/HUMON), a post being "turned on" (OPON), any
// mono text — goes through it.
//
// Pixel shader equivalent: any texel that is NOT transparent becomes the
// remap colour, with the source's alpha kept so the shape reads correctly.
// The port's sprite PNGs write alpha 0 for a zero source nibble (notes §29),
// so `alpha` IS the source's non-zero test.
float4 SolidRemapPS(PSInput input) : COLOR
{
    float4 t = tex2D(SpriteTexture, input.TextureCoordinate);
    clip(t.a - 0.5f);
    return float4(RemapColor.rgb, t.a) * input.Color;
}

technique MainTech
{
    pass MainPass
    {
        // PIXEL SHADER ONLY — see the header. SpriteBatch has already bound
        // its own sprite VS for this Begin/End block; declaring a VS here
        // would replace it (and the sprites with it). SM 3.0 because the
        // unrolled six-marker PS exceeds the ps_2_0 64-slot arithmetic limit
        // (X5608 with vs/ps_2_0).
        PixelShader = compile ps_3_0 MainPS();
    }
}

// Blitter ops $12 (solid block) and $1A (solid + transparent), i.e. "draw
// this object in this one colour". PIXEL SHADER ONLY for the same reason as
// MainTech: an EffectPass.Apply() binds every stage the pass declares, so a
// VS here would clobber SpriteBatch's projection for the rest of the batch
// (notes §40 — that bug is what made "only the wall" render).
technique SolidRemap
{
    pass SolidRemapPass
    {
        PixelShader = compile ps_3_0 SolidRemapPS();
    }
}
