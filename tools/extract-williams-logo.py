#!/usr/bin/env python3
"""Decode the Williams "W" logo from the R5 ROM's plotting instructions and write it as a PNG.

The attract page's border is 28 moving "W" logos (notes §103.1). The ROM does not store the W as a
bitmap: it stores PLOTTING INSTRUCTIONS at $8CF4 (86 bytes, ending in the $A0 terminator) that
RENDER_GRAPHIC ($8D69) interprets, one pixel column at a time, into screen memory. The disassembly
(asm/robomame.asm, $8D69-$8E0E) gives the instruction set. Screen memory is `column * 256 + row`
with two pixels to a byte, so an instruction stream describes one vertical pixel column after
another:

    $00-$1F  move down that many rows (LEAX A,X)
    $20-$3F  draw a vertical line of (low 5 bits) pixels in COLOUR 2, then move down past it
    $40-$5F  draw a vertical line of (low 5 bits) pixels in COLOUR 1, then move down past it
    $90      this pixel column is done: back to the top row, next pixel (even pixel, then the
             odd pixel of the same byte, then the next byte)
    $A0      the whole graphic is done
    $C0-$CF  repeat the current column's instructions (low nibble) times - the W never uses it

The W is rendered once into a 14-byte x 27-row template with A=$10 ($88C6: "Colour 1 = 1,
Colour 2 = 0") and copied to RAM at $B426 ($8945: 14 bytes wide, $1B rows). It is then blitted with
`$1E` = transparency + solid mode ($8A33), so ONLY the colour-1 pixels are drawn (in the colour of
the palette slot the caller supplies) and the colour-2 pixels are the transparent gaps between the
strokes. The PNG therefore holds the colour-1 pixels as an opaque WHITE MASK on a transparent
background, tinted at draw time like the wordmark masks.

The instruction bytes are read from ref/rom/robotron64k.bin (the ROM image the rest of the port's
extraction tools use) and cross-checked against the size the ROM itself copies ($0E1B).

Usage (from the repository root):  python tools/extract-williams-logo.py
"""
import pathlib
import sys

from PIL import Image

ROOT = pathlib.Path(__file__).resolve().parents[1]
ROM = ROOT / "ref/rom/robotron64k.bin"
OUT = ROOT / "src/Robotron2084/Content/Sprites/WilliamsLogo.png"

INSTRUCTIONS_ADDRESS = 0x8CF4
TEMPLATE_WIDTH_BYTES = 0x0E  # $8945 LDD #$0E1B: width in bytes...
TEMPLATE_HEIGHT_ROWS = 0x1B  # ...and height in rows

NEXT_COLUMN = 0x90
DONE = 0xA0


def decode(rom: bytes):
    """Interpret the plotting instructions. Returns {(x, y): colour} with colour 1 or 2."""
    pixels = {}
    address = INSTRUCTIONS_ADDRESS
    column = 0  # pixel column: byte * 2 + (odd pixel ? 1 : 0)
    row = 0
    while True:
        instruction = rom[address]
        address += 1
        if instruction & 0x80:
            if instruction & 0x20:  # $A0: rendering complete
                return pixels, address
            if instruction & 0x10:  # $90: next pixel column, back to the top row
                column += 1
                row = 0
                continue
            raise SystemExit(f"repeat instruction {instruction:#04x} at {address - 1:#06x}: not used by the W")
        if instruction & 0x60:  # a vertical line: bit 6 = colour 1, bit 5 = colour 2
            colour = 1 if instruction & 0x40 else 2
            height = instruction & 0x1F
            for offset in range(height):
                pixels[(column, row + offset)] = colour
            row += height
        else:  # move down
            row += instruction


def main() -> int:
    rom = ROM.read_bytes()
    pixels, end = decode(rom)

    width = max(x for x, _ in pixels) + 1
    height = max(y for _, y in pixels) + 1
    expected = (TEMPLATE_WIDTH_BYTES * 2, TEMPLATE_HEIGHT_ROWS)
    print(f"instructions {INSTRUCTIONS_ADDRESS:#06x}-{end - 1:#06x} ({end - INSTRUCTIONS_ADDRESS} bytes)")
    print(f"decoded extent {width} x {height}; the ROM copies {expected[0]} x {expected[1]}")
    if (width, height) != expected:
        # The template is copied at its full size, so the drawn extent may be smaller, never larger.
        if width > expected[0] or height > expected[1]:
            print("decoded W is larger than the template the ROM copies", file=sys.stderr)
            return 1

    # The PNG is the template's full size so the image lines up with the ROM's blit.
    image = Image.new("RGBA", expected, (0, 0, 0, 0))
    ones = 0
    for (x, y), colour in pixels.items():
        if colour == 1:
            image.putpixel((x, y), (255, 255, 255, 255))
            ones += 1
    image.save(OUT)
    print(f"wrote {OUT.relative_to(ROOT)}: {ones} colour-1 pixels, "
          f"{len(pixels) - ones} colour-2 (transparent) pixels")

    for y in range(expected[1]):
        print("".join({1: "#", 2: "."}.get(pixels.get((x, y)), " ") for x in range(expected[0])))
    return 0


if __name__ == "__main__":
    sys.exit(main())
