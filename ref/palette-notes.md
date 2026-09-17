# Palette & colour construction (resolved 2026-08-30)

Question: *how are colours constructed from the palette?* — answered below.

## The colour chain

```
screen pixel = 4-bit nibble (0-15)
  → 16-entry RAM palette ($9800-$980F in game RAM; drives the $C000-$C00F colour RAM)
  → each entry is an 8-bit value
  → 256-entry "hardware palette" = an ANALOG RESISTOR NETWORK (not a PROM!):
        bits 0-2 = Red  (3 taps: 1200 / 560 / 330 Ω)
        bits 3-5 = Green(3 taps: 1200 / 560 / 330 Ω)
        bits 6-7 = Blue (2 taps:  560 / 330 Ω)
  → analog RGB voltage → monitor colour decode → colour on screen
```

- "16 colours at a time out of a hardware palette of 256" (seanriddle.com/ripper.html).
- The two 7641-5 decoder PROMs in the ROM set (`decoder_rom_4.3g` / `decoder_rom_6.3c`) are **decoder timing/blanking logic, NOT colour** — colour is the resistor network above.
- Colour-cycling / red-screen effects = rewriting the 16-entry RAM palette (e.g. `RRFRED.ASM`).

## MAME reference (exact)

`mamedev/mame` → `src/mame/williams/williams_v.cpp`, line 340, `williams_state::palette_init`
(Robotron is `williams_b1` → inherits this verbatim; no PROM reads involved):

```cpp
void williams_state::palette_init(palette_device &palette) const
{
    static constexpr int resistances_rg[3] = { 1200, 560, 330 };
    static constexpr int resistances_b[2]  = { 560, 330 };

    double weights_r[3], weights_g[3], weights_b[2];
    compute_resistor_weights(0, 255, -1.0,
            3, resistances_rg, weights_r, 0, 0,
            3, resistances_rg, weights_g, 0, 0,
            2, resistances_b,  weights_b, 0, 0);

    for (int i = 0; i < 256; i++)
    {
        int const r = combine_weights(weights_r, BIT(i, 0), BIT(i, 1), BIT(i, 2));
        int const g = combine_weights(weights_g, BIT(i, 3), BIT(i, 4), BIT(i, 5));
        int const b = combine_weights(weights_b, BIT(i, 6), BIT(i, 7));
        palette.set_pen_color(i, rgb_t(r, g, b));
    }
}
```

**TODO (T-008):** port `compute_resistor_weights` + `combine_weights` (classic MAME `src/emulib/resist.cpp`, unchanged for ~15 years). Raw-path guessing 404'd on 2026-08-30 (tried `src/lib/util/resist.cpp|h`, `src/emucore/resist.cpp|h`, `src/emulib/resist.cpp|h` on master + 0.289/0.280/0.139 tags, libretro mirrors). Find it via GitHub web search or a MAME source tarball. The relative weights are `1/R_i` normalised to 0-255, gamma -1 = no gamma correction; hand-computed values below match that.

## Ripper cross-verification

Sean Riddle's Williams Graphics Ripper — **local source at `C:\Users\scott\source\repos\WmsGfxSpriteRipper`** (MFC C++ app; the palette data + rendering live in `Form1.h`).

1. `Form1.h` line 787, `gamecolors[10][16]` — built-in palettes. **Robotron @da51 row is byte-identical to the ROM default palette at $DA51**:
   `00 07 17 C7 1F 3F 38 C0 A4 FF 38 17 CC 81 81 07` ✓ ($DA51 in `ref/rom/robotron64k.bin`)
   (Others for reference: Joust @e5fa, Sinistar @50cb, Bubbles @0d7f, Defender @f8be, Splat @dd2f, plus "def reset" / "false color" / defreset variants.)
2. `Form1.h` lines 871-901 — pixel→RGB conversion (4-bit quantisation of the same 3/3/2 network):
   ```cpp
   r = (colors[nibble] & 0x07) << 1;      // 3 value bits → 4-bit R (0,2,...,14)
   g = (colors[nibble] & 0x38) >> 2;      // 3 value bits → 4-bit G (0..7)
   b = ((colors[nibble] & 0xC0) >> 6) * 5;// 2 value bits → 4-bit B (0,5,10,15)
   ```
   Use this as a fast sanity cross-check on the ported 256-entry palette.

## Default 16 colours (RAM palette = ROM $DA51-$DA5F)

Hex values (ROM bytes) with hand-computed RGB from the 1/R weighting (= MAME's classic weights up to rounding):

| idx | hex | RGB (approx) | name |
|----:|-----|--------------|------|
| 0 | 0x00 | (0, 0, 0) | black |
| 1 | 0x07 | (255, 0, 0) | red |
| 2 | 0x17 | (255, 137, 0) | orange |
| 3 | 0xC7 | (255, 0, 255) | magenta |
| 4 | 0x1F | (255, 118, 0) | dark orange |
| 5 | 0x3F | (255, 255, 0) | yellow |
| 6 | 0x38 | (0, 255, 0) | green |
| 7 | 0xC0 | (0, 0, 255) | blue |
| 8 | 0xA4 | (137, 137, 161) | lavender |
| 9 | 0xFF | (255, 255, 255) | white |
| 10 | 0x38 | (0, 255, 0) | green |
| 11 | 0x17 | (255, 137, 0) | orange |
| 12 | 0xCC | (137, 37, 255) | violet |
| 13 | 0x81 | (37, 0, 161) | deep blue |
| 14 | 0x81 | (37, 0, 161) | deep blue |
| 15 | 0x07 | (255, 0, 0) | red |

Bit decode used: `R = bits 0-2` (taps 1200/560/330 → weights ≈ 37.5/80.7/136.8), `G = bits 3-5` (same), `B = bits 6-7` (taps 560/330 → ≈ 94.2/160.8).

## Port plan (T-008)

- Ship a **constant 256-entry base palette** (computed once from MAME's resistor formula, verified against ripper's 4-bit RGB) as `Palette.cs` (or a generator + committed table).
- 16-entry RAM palette: **default = $DA51 bytes** (verified above); in-game variants (colour cycling, red screen) extracted when porting those effects — the game rewrites the 16 entries, so model it as a mutable 16-slot lookup over the fixed 256-entry base.
- Sprite rendering: per-pixel nibble → slot → base RGB. (Optionally also implement the ripper's 4-bit quantisation as a "crt-ish" debug view — not required.)

## Screen-memory layout (from ripper.html, corroborates D-005)

- "$0000 upper-left; each byte = 2 pixels side by side; $0001 = 2 pixels immediately below; … bottom-left corner is $00FF; $0100 = 3rd and 4th pixels in the top line."
- → **2-pixel-wide vertical stripes, 256 bytes per stripe, 152 stripes = 38,912 B = exactly the 38K screen RAM** ($0000-$97FF).
- **RESOLVED (author 2026-08-30): screen is 304 × 256.** "It's 256 rows, that's why incrementing the
  MSB of the memory address moves a pixel column along" — each 2-px column is exactly a 256-byte
  block, so carry into bit 8 of the address = one column (2 px) right. D-004 updated; port window =
  304×256.

## Sprite storage format — Robotron (KEY for T-007) — VERIFIED MODEL

From ripper.html (Riddle) + author's annotations + ROM-image verification (2026-08-30):

- Robotron is a "Special Chip" (blitter) game → sprites stored in **"Linear" layout: row by row,
  2 pixels per byte** (NOT the screen pair-major layout — that's Defender/Stargate, which is why
  those games store duplicate 1px-shifted sprites; Robotron does not).
- **Sprite entry = 4 bytes: `[width][height][ptr_hi][ptr_lo]`** (pointer big-endian, 6809 D order —
  author-confirmed). NOT XORed with $04 (that's Joust/Bubbles).
  - **width = bytes per row** (2 px/byte — author: "there's 2 pixels per byte")
  - **height = pixels** (author-confirmed; the "0B = 13" in the $0437 annotation was a doc error —
    0B = 11)
  - **ptr = 16-bit address of the first frame's pixel data**
- **Entries live in tables of consecutive 4-byte entries; the frame data they point at is stored
  contiguously with pitch = width × height bytes.** A "little animation" = several consecutive
  frames with no bytes between frames (Riddle). How many frames an animation has comes from the
  object/animation tables (T-007), not from the sprite entries.
- **ROM verification — every entry's 4 header bytes AND every frame pitch checked against
  `ref/rom/robotron64k.bin` → ALL OK:**
  - `0437: 06 0B 04 3B` — 12×11 "family death" (skull & crossbones), frame @$043B (66 B)
  - `047D: 02 02 04 81` — small all-black dot (author: "2px × 2px black dot"), 4 B of $00 @$0481
  - table @$0485–$0498 (5 entries `06 05 …`) — 12×5 frames @$0499/$04B7/$04D5/$04F3/$0511, pitch 0x1E
  - mommies @$052F/$0533 — 8×14 frames @$055F/$0597, pitch 0x38
  - daddies @$07FF/$0803 — 10×13 frames @$082F/$0870, pitch 0x41
  - mikeys  @$0B3B/$0B3F — 6×11 frames @$0B6B/$0B8C, pitch 0x21
  - @$0CF9/$0CFD — 14×16 frames @$0D1D/$0D8D, pitch 0x70
  - @$14F2/$14F6/$14FA — 16×15 frames @$1512/$158A/$1602, pitch 0x78
  - @$18D2/$18D6 — 10×11 frames @$1921/$1958, pitch 0x37
  - @$1A34/$1A38 — 8×7 frames @$1A44/$1A60, pitch 0x1C
  - @$2141/$2145 — 14×16 frames @$2171/$21E1, pitch 0x70
  - @$3603 — 8×12 frame @$373B
- **Code corroboration:** $00A8 `ADDD [$02,X]` "add in width in bytes and height in pixels of the
  object" (16-bit value = [width | height]); $1B74 `ADDA ,Y` "add width of brain image".
- **Extraction recipe (T-007):** locate entry tables (or take the object/animation tables from the
  original source), read `[w][h][ptr_hi][ptr_lo]`, copy w×h bytes per frame, decode 2 px/byte.
- His walk-through finds the classic **skull & crossbones** as a **6-byte-wide (12 px)** sprite near the start of the ROM.
- Tip he gives generally: `MAME -log` logs every blit (width/height XORed with $04 in the log) — useful for locating hard sprites.

## Sources

- https://seanriddle.com/ripper.html (fetched 2026-08-30; also has Defender sprite list etc.; his "Robotron Sprite List" + "Robotron Sprites Pic" are linked from that page — check `willy.html` on that site if needed)
- `mamedev/mame` master: `src/mame/williams/williams_v.cpp` (palette_init @340), `src/mame/williams/williams.cpp` (memory map) — see `ref/mame-notes.md`
- `C:\Users\scott\source\repos\WmsGfxSpriteRipper` (Form1.h @787 palettes, @871-901 + @1229-1259 rendering)
- `ref/rom/robotron64k.bin` ($DA51 default palette)
