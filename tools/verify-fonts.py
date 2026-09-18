#!/usr/bin/env python3
"""Verify the committed font glyph PNGs against the ROM — the guard for notes §60.

WHY THIS EXISTS
---------------
Every glyph in `Content/Sprites/Font_L_*.png` / `Font_S_*.png` was mirrored
inside each 2-pixel block for a day, because `tools/extract-fonts.py` wrote the
LOW nibble of a byte to the LEFT pixel where the arcade (and every other
extraction path) puts the HIGH nibble. Nothing caught it: the glyphs were the
right SIZE and the right COUNT, so the content pipeline, the tests and the
render gates were all happy while the score digits looked like broken shapes
(author: "the font characters don't look correct").

This script decodes the ROM directly and compares every committed glyph
pixel (the white masters — notes §92 deleted the 468 colour-cycle marker
variants, so a glyph drawn in a cycling slot now goes through the shader's
GlyphCycle pass), so a bad regeneration fails loudly instead of quietly
shipping.

Usage:
    python tools/verify-fonts.py

Exit code 0 = every glyph matches the ROM; 1 = mismatches (printed).
"""
import importlib.util
import os
import sys

from PIL import Image


def repo_root() -> str:
    return os.path.dirname(os.path.dirname(os.path.abspath(__file__)))


def load_extractor():
    """Import tools/extract-fonts.py (it owns the offsets and the decoding)."""
    path = os.path.join(repo_root(), "tools", "extract-fonts.py")
    spec = importlib.util.spec_from_file_location("extract_fonts", path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def main() -> int:
    os.chdir(repo_root())
    ef = load_extractor()
    rom = open(ef.ROM, "rb").read()

    failures = []
    checked = 0

    def expect(path, width, height, colour_for):
        """colour_for(nibble) -> RGBA tuple or None when the pixel must be transparent."""
        nonlocal checked
        if not os.path.exists(path):
            failures.append(f"{path}: MISSING")
            return
        image = Image.open(path).convert("RGBA")
        if image.size != (width, height):
            failures.append(f"{path}: size {image.size} != {(width, height)}")
            return
        px = image.load()
        for y in range(height):
            for x in range(width):
                want = colour_for(x, y)
                got = px[x, y]
                if want is None:
                    if got[3] != 0:
                        failures.append(f"{path}: pixel ({x},{y}) should be transparent, got {got}")
                        return
                elif got[3] == 0 or tuple(got[:3]) != tuple(want[:3]):
                    failures.append(f"{path}: pixel ({x},{y}) should be {want}, got {got}")
                    return
        checked += 1

    def nibble_at(data, offset, width_bytes, x, y):
        b = data[offset + y * width_bytes + (x >> 1)]
        return (b >> 4) & 0xF if x % 2 == 0 else b & 0xF

    for prefix, table in (("Font_S", ef.SMALL), ("Font_L", ef.LARGE)):
        for char, (offset, width_bytes, height) in table.items():
            name = ef.NAME[char]
            width = width_bytes * 2

            def master(x, y, offset=offset, width_bytes=width_bytes):
                return (255, 255, 255, 255) if nibble_at(rom, offset, width_bytes, x, y) else None

            expect(f"{ef.OUT}/{prefix}_{name}.png", width, height, master)

    if failures:
        print(f"FAIL: {len(failures)} font glyph problem(s); {checked} file(s) OK")
        for line in failures[:40]:
            print("  " + line)
        if len(failures) > 40:
            print(f"  ... and {len(failures) - 40} more")
        print("Regenerate with: python tools/extract-fonts.py")
        return 1

    print(f"PASS: {checked} font glyph PNGs match the ROM")
    return 0


if __name__ == "__main__":
    sys.exit(main())
