#!/usr/bin/env python3
"""Trace the attract page's two logos out of the author's reference screenshot.

The R5 CPU ROM we hold does not contain the attract wordmark (notes §103.2): RENDER_GRAPHIC's
only call sites are the "W", the attract page's task chain is fully mapped, and the Williams
blitter can only source CPU-addressable memory — so the artwork is not in `robotron64k.bin` and
the screenshot the author supplied is a different release. He sanctioned tracing it instead.

Source: ref/title-reference.png (1250x1025, a capture of the 304x256 screen, so about 4.11 px
per arcade column and 4.00 px per row). Bands were measured off the image:

    wordmark "ROBOTRON:"    x 178-1053, y 218-335   -> 213 x 29 arcade pixels
    logo "2084"             x 461- 783, y 364-497   ->  79 x 34 arcade pixels

Each output texel samples the CENTRE of its source block (the capture is scaled and slightly
blurred, so a point sample beats an average) and is snapped to the nearest colour of the
arcade's own 16-entry palette, with the black background left transparent.
"""
from PIL import Image

SRC = "ref/title-reference.png"
OUT = "src/Robotron2084/Content/Sprites"

# (name, x0, y0, x1, y1, target width, target height) — x1/y1 exclusive.
LOGOS = [
    ("Title_Wordmark", 178, 218, 1054, 336, 213, 29),
    ("Title_2084", 461, 364, 784, 498, 79, 34),
]

# The arcade's CRTAB defaults (notes §4.12) — the palette the page's own live colours come from.
CRTAB = [0x00, 0x07, 0x17, 0xC7, 0x1F, 0x3F, 0x38, 0xC0, 0xA4, 0xFF, 0x38, 0x17, 0xCC, 0x81, 0x81, 0x07]

# Slots 10-15 are the colour-cycling ones, and their sprite MARKER colours (GamePalette's
# CyclingSlotMarkers) are remapped by the shader to each slot's LIVE colour. Two of them — $CC and
# $81 — are also CRTAB entries, so a texel snapped onto one of those STRobes on screen. The snap
# set therefore excludes every marker: nothing in these logos may strobe (the author spotted
# exactly that in the first trace).
MARKERS = {0xC4, 0xF4, 0xCC, 0x81, 0x45, 0x2F}
SNAP_SLOTS = [i for i in range(1, 16) if CRTAB[i] not in MARKERS]

# Below this the pixel is the capture's own background or its blur halo, not artwork.
ART_THRESHOLD = 120


def rgb(value):
    """Robotron 8-bit colour (BBGGGRRR) -> RGB, exactly as the port's RobotronColor does."""
    red = (value & 0x07) << 1
    if red > 6:
        red += 1
    green = (value & 0x38) >> 2
    if green > 6:
        green += 1
    blue = ((value & 0xC0) >> 6) * 5
    return (min(255, red << 4), min(255, green << 4), min(255, blue << 4))


def snap(pixel):
    """The nearest arcade palette colour that is NOT a cycling marker, or transparent."""
    if sum(pixel) < ART_THRESHOLD:
        return (0, 0, 0, 0)
    best = min(SNAP_SLOTS, key=lambda i: sum((a - b) ** 2 for a, b in zip(rgb(CRTAB[i]), pixel)))
    return rgb(CRTAB[best]) + (255,)


def main():
    image = Image.open(SRC).convert("RGB")
    print(f"source {SRC} {image.size}")

    for name, x0, y0, x1, y1, width, height in LOGOS:
        crop = image.crop((x0, y0, x1, y1))
        out = Image.new("RGBA", (width, height), (0, 0, 0, 0))
        step_x, step_y = crop.width / width, crop.height / height
        for ty in range(height):
            for tx in range(width):
                sx = min(crop.width - 1, int((tx + 0.5) * step_x))
                sy = min(crop.height - 1, int((ty + 0.5) * step_y))
                out.putpixel((tx, ty), snap(crop.getpixel((sx, sy))))

        out.save(f"{OUT}/{name}.png")
        opaque = sum(1 for p in out.getdata() if p[3])
        print(f"  {name}.png {width}x{height} arcade px, {opaque} opaque ({100 * opaque / (width * height):.0f}%)")


if __name__ == "__main__":
    main()
