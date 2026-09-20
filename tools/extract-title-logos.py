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
blurred, so a point sample beats an average), with the black background left transparent. The
"2084" mark is then snapped to the nearest colour of the arcade's own 16-entry palette — its
colour is fixed art.

The WORDMARK is emitted differently: as two WHITE MASKS, `Title_Wordmark_Core` (the letters'
body) and `Title_Wordmark_Rim` (the one-pixel rim round them). Its colours are not art, because
the arcade blits a shape in a colour taken from the LIVE palette — so the page draws the core and
the rim each in a palette slot and the wordmark colour-cycles with the page's own colour
processes (notes §104). The two masks are split from the traced SHAPE alone: the capture is too
blurred to classify per texel (it renders the art's one-pixel rim as two or three blended pixels,
which is exactly the orange the first trace had to snap somewhere), while "the rim is the shape's
outer layer" is a property no blur can move.
"""
from PIL import Image

SRC = "ref/title-reference.png"
OUT = "src/Robotron2084/Content/Sprites"

# (name, x0, y0, x1, y1, target width, target height) — x1/y1 exclusive.
MARK = ("Title_2084", 461, 364, 784, 498, 79, 34)

# The wordmark band: the same measurement, but traced into two masks (module docstring).
WORDMARK = (178, 218, 1054, 336, 213, 29)

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


def sampled(image, band, width, height):
    """The centre pixel of each output texel's source block, as a width x height grid."""
    x0, y0, x1, y1 = band
    crop = image.crop((x0, y0, x1, y1))
    step_x, step_y = crop.width / width, crop.height / height
    return [
        [
            crop.getpixel((
                min(crop.width - 1, int((tx + 0.5) * step_x)),
                min(crop.height - 1, int((ty + 0.5) * step_y)),
            ))
            for tx in range(width)
        ]
        for ty in range(height)
    ]


def erode(mask, width, height):
    """The mask minus its boundary: True where the texel and its four neighbours are all set.

    On this art that is exactly "the letters' body": the trace is a solid shape whose rim is one
    texel thick, so one erosion leaves the rim behind (and a feature thinner than three texels —
    the colon's dots — is rim rather than core, which is how the capture shows it too). The band's
    own edges count as outside, so the shape keeps its rim where it touches them.
    """

    def set_(tx, ty):
        return 0 <= tx < width and 0 <= ty < height and mask[ty][tx]

    return [
        [
            set_(tx, ty) and set_(tx - 1, ty) and set_(tx + 1, ty)
            and set_(tx, ty - 1) and set_(tx, ty + 1)
            for tx in range(width)
        ]
        for ty in range(height)
    ]


def write_mask(path, mask, width, height):
    out = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    for ty in range(height):
        for tx in range(width):
            if mask[ty][tx]:
                out.putpixel((tx, ty), (255, 255, 255, 255))

    out.save(path)
    lit = sum(1 for row in mask for set_ in row if set_)
    print(f"  {path.split('/')[-1]} {width}x{height} arcade px, {lit} lit ({100 * lit / (width * height):.0f}%)")


def main():
    image = Image.open(SRC).convert("RGB")
    print(f"source {SRC} {image.size}")

    name, x0, y0, x1, y1, width, height = MARK
    out = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    for ty, row in enumerate(sampled(image, (x0, y0, x1, y1), width, height)):
        for tx, pixel in enumerate(row):
            out.putpixel((tx, ty), snap(pixel))

    out.save(f"{OUT}/{name}.png")
    opaque = sum(1 for p in out.getdata() if p[3])
    print(f"  {name}.png {width}x{height} arcade px, {opaque} opaque ({100 * opaque / (width * height):.0f}%)")

    x0, y0, x1, y1, width, height = WORDMARK
    shape = [[sum(pixel) >= ART_THRESHOLD
              for pixel in row]
             for row in sampled(image, (x0, y0, x1, y1), width, height)]
    core = erode(shape, width, height)
    rim = [[shape[ty][tx] and not core[ty][tx] for tx in range(width)] for ty in range(height)]
    write_mask(f"{OUT}/Title_Wordmark_Core.png", core, width, height)
    write_mask(f"{OUT}/Title_Wordmark_Rim.png", rim, width, height)


if __name__ == "__main__":
    main()
