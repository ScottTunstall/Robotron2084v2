#!/usr/bin/env python3
"""
Extract the arcade small/large font glyphs from ref/rom/robotron64k.bin into
Content/Sprites/Font_S_*.png / Font_L_*.png (regenerates the hand-drawn
placeholders — same file names, so Content.mgcb / SpriteContentPaths stay
valid).

Offsets + dimensions come from the author's sprite editor
(WmsGfxSpriteEditor.Roms.Robotron2084/Shared/Sprites/
RobotronBlueLabelSpriteRepository.cs; data sourced from
seanriddle.com/robotronsprites.txt). Format: 4bpp linear, 2 px/byte,
row-major — "blitted same as the sprites" (author, 2026-09-16), which means
the HIGH nibble of a byte is the LEFT pixel: every glyph written before
2026-09-17 had the two pixels of each byte SWAPPED, so the score digits
rendered as broken shapes (author: "the font characters don't look
correct"). `tools/verify-fonts.py` now guards the committed PNGs against
the ROM.
Notes: docs/arcade-fidelity-notes.md §36.1, §38, §60.

Output: RGBA8. Non-zero nibble = white opaque; zero = transparent. The
port tints at draw time (P1 score = palette slot 1 blue, P2 = slot 10).

Cycling slots (notes §39, §92 — blitter remap semantics): the arcade blits
glyphs with a palette INDEX; if that slot is a colour-cycling slot (10-15)
the text cycles too. The port draws the WHITE MASTERS through the
M4 shader's GlyphCycle pass (a slot-indexed uniform selects the slot's
live colour), so no marker-baked variants are emitted — the 468 of them
were deleted in notes §92.
"""
from PIL import Image

ROM = "ref/rom/robotron64k.bin"
OUT = "src/Robotron2084/Content/Sprites"
# (offset, width_bytes, height) — decimal offsets, from the author's editor.
SMALL = {
    "0": (59947, 2, 5), "1": (59958, 2, 5), "2": (59969, 2, 5),
    "3": (59980, 2, 5), "4": (59991, 2, 5), "5": (60002, 2, 5),
    "6": (60013, 2, 5), "7": (60024, 2, 5), "8": (60035, 2, 5),
    "9": (60046, 2, 5), "A": (60119, 2, 5), "B": (60130, 2, 5),
    "C": (60141, 2, 5), "D": (60152, 2, 5), "E": (60163, 2, 5),
    "F": (60174, 2, 5), "G": (60185, 2, 5), "H": (60196, 2, 5),
    "I": (60207, 2, 5), "J": (60218, 2, 5), "K": (60229, 2, 5),
    "L": (60240, 2, 5), "M": (60251, 3, 5), "N": (60267, 2, 5),
    "O": (60278, 2, 5), "P": (60289, 2, 5), "Q": (60300, 2, 5),
    "R": (60311, 2, 5), "S": (60322, 2, 5), "T": (60333, 2, 5),
    "U": (60344, 2, 5), "V": (60355, 2, 5), "W": (60366, 3, 5),
    "X": (60382, 2, 5), "Y": (60393, 2, 5), "Z": (60404, 2, 5),
    "(": (60415, 2, 5), ")": (60426, 2, 5),
    # The GAME ADJUSTMENT page's CURSOR (notes §108.4): SMALL_CHARACTER_TABLE
    # entry 45 — pointer $EC14, a width byte ($07) then 5 rows of 4 bytes, so the
    # record ends exactly at the next entry's pointer ($EC29). SHOW_GAME_ADJUSTMENT_CURSOR
    # ($71FE) prints text string $2C at column $0C on the setting's row, and that
    # string @$6790 is `04 99` (set text colour to palette entry 9 — the service
    # palette's white) `5D` (the glyph) `04 66` (back to entry 6, the settings'
    # green) — the art is a hyphen and a `>` chevron, 6 px of it.
    #
    # It is written next to the other small glyphs, but it is NOT one of
    # FontSmall's 38 (that array stops at the parens), so SpriteSet loads it on
    # its own as `CursorArrow` and the DEFINE INPUTS page draws it instead of the
    # large font's `arrowleft`, which is the ROM's LEFT arrow (notes §108.5).
    "cursorright": (60437, 4, 5),
}

LARGE = {
    "0": (60563, 3, 6), "1": (60582, 3, 6), "2": (60601, 3, 6),
    "3": (60620, 3, 6), "4": (60639, 3, 6), "5": (60658, 3, 6),
    "6": (60677, 3, 6), "7": (60696, 3, 6), "8": (60715, 3, 6),
    "9": (60734, 3, 6), "A": (60864, 3, 6), "B": (60883, 3, 6),
    "C": (60902, 3, 6), "D": (60921, 3, 6), "E": (60940, 3, 6),
    "F": (60959, 3, 6), "G": (60978, 3, 6), "H": (60997, 3, 6),
    "I": (61016, 3, 6), "J": (61035, 3, 6), "K": (61054, 3, 6),
    "L": (61073, 3, 6), "M": (61092, 3, 6), "N": (61111, 3, 6),
    "O": (61130, 3, 6), "P": (61149, 3, 6), "Q": (61168, 3, 6),
    "R": (61187, 3, 6), "S": (61206, 3, 6), "T": (61225, 3, 6),
    "U": (61244, 3, 6), "V": (61263, 3, 6), "W": (61282, 3, 6),
    "X": (61301, 3, 6), "Y": (61320, 3, 6), "Z": (61339, 3, 6),
    "(": (61358, 2, 6), ")": (61371, 2, 6), ":": (60838, 2, 6),
    "arrowleft": (61403, 3, 6),
    # The PUNCTUATION the ROM's own large-font table carries between '9' and 'A'
    # (notes §96). BLIT_LARGE_CHARACTER indexes the table at $EC34 by
    # (charCode - $30), so the ROM's codes are: $3A space, $3B '!', $3C ',',
    # $3D '.', $3E a solid 10x6 block, $3F ':', $40 '-'. Each table entry is a
    # pointer to a 1-byte WIDTH (in pixels) followed by 6 rows of
    # ceil(width/2) bytes, and the port's offset below is the pointer + 1.
    # The story movie's text crawl needs ',' '.' ':' '-' '!' — without them the
    # intro screen printed blanks for every punctuation mark. (The old ':' entry
    # pointed at the table's $5D slot instead, a 2x5 fragment; it is fixed here.)
    "!": (60766, 2, 6),  # $3B — pointer $ED5D, width 2 px
    ",": (60781, 2, 6),  # $3C — pointer $ED6C, width 2 px
    ".": (60794, 2, 6),  # $3D — pointer $ED79, width 2 px
    "-": (60851, 2, 6),  # $40 — pointer $EDB2, width 3 px
}

# char -> file name suffix (must match the existing PNG file names)
NAME = {
    "(": "(", ")": ")", ":": "colon", "arrowleft": "arrowleft",
    "!": "exclaim", ",": "comma", ".": "period", "-": "hyphen",
    "cursorright": "cursorright",
}
NAME.update({c: c for c in "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ"})

def glyph_pixels(data, offset, width_bytes, height):
    """Yield (x, y, set) for every pixel of the glyph.

    HIGH nibble = LEFT pixel (the arcade stores two pixels per byte, and every
    other extraction path in this repo — tools/SpriteExtractor, the author's
    sprite editor — reads them left-to-right within the byte).
    """
    for row in range(height):
        for byte_i in range(width_bytes):
            b = data[offset + row * width_bytes + byte_i]
            yield byte_i * 2, row, ((b >> 4) & 0xF) != 0
            yield byte_i * 2 + 1, row, (b & 0xF) != 0


def write_png(prefix, table, data):
    for char, (offset, width_bytes, height) in table.items():
        # Master glyph: white on transparent (tinted at draw time).
        img = Image.new("RGBA", (width_bytes * 2, height), (0, 0, 0, 0))
        px = img.load()
        for x, y, set_ in glyph_pixels(data, offset, width_bytes, height):
            if set_:
                px[x, y] = (255, 255, 255, 255)
        name = f"{prefix}_{NAME[char]}.png"
        img.save(f"{OUT}/{name}")
        print(f"  {name}")


def main():
    data = open(ROM, "rb").read()
    print("small font ->", OUT)
    write_png("Font_S", SMALL, data)
    print("large font ->", OUT)
    write_png("Font_L", LARGE, data)


if __name__ == "__main__":
    main()
