#!/usr/bin/env python3
"""Cuts Gorf's two animation frames out of its sprite sheet (ref/gorf-sheet.png).

The sheet is a blown-up picture of two frames side by side on a black background: every sprite pixel is a 7 x 7 block of the
picture, and each frame is 15 pixels wide and 20 tall. The pixels are red, yellow and blue. The frames are written as
Gorf_1 (the left one) and Gorf_2 (the right one), in the sheet's own colours, with the black background made transparent.

Run from the repository root:  python tools/extract-gorf.py
Then run tools/generate-mgcb.py to add the new PNGs to the content pipeline.
"""
import pathlib
import sys

from PIL import Image

ROOT = pathlib.Path(__file__).resolve().parents[1]
SHEET = ROOT / "ref/gorf-sheet.png"
OUT = ROOT / "src/Robotron2084/Content/Sprites"

BLOCK = 7
FRAME_WIDTH = 15
FRAME_HEIGHT = 20
TOP = 18
FRAME_LEFTS = [30, 184]
BACKGROUND = (0, 0, 0)


def main() -> int:
    if not SHEET.exists():
        print(f"missing {SHEET}", file=sys.stderr)
        return 1

    sheet = Image.open(SHEET).convert("RGB")
    for number, left in enumerate(FRAME_LEFTS, start=1):
        frame = Image.new("RGBA", (FRAME_WIDTH, FRAME_HEIGHT), (0, 0, 0, 0))
        for y in range(FRAME_HEIGHT):
            for x in range(FRAME_WIDTH):
                colour = sheet.getpixel((left + (x * BLOCK) + BLOCK // 2, TOP + (y * BLOCK) + BLOCK // 2))
                if colour != BACKGROUND:
                    frame.putpixel((x, y), colour + (255,))
        frame.save(OUT / f"Gorf_{number}.png")
        print(f"Gorf_{number}: {FRAME_WIDTH} x {FRAME_HEIGHT}")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
