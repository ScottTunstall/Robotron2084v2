#!/usr/bin/env python3
"""Turn the author's menu painting into the menu page's 8-bit picture.

Source: ref/main-menu-art.png (1024x672), a painting of red robots on a green grid, given by the
author for the menu page.

Output: src/Robotron2084/Content/Sprites/MainMenuRobot.png, 320x200 arcade pixels, the whole
screen.

The steps:

1. Cut 16 rows off the top and the bottom, so the painting has the screen's shape (8 by 5).
2. Shrink it to 320x200 by averaging each block of the painting's pixels. Averaging is what
   smooths the edges: a pixel half on a robot and half on the sky comes out between the two.
3. Turn up the colour and contrast a little, since shrinking dulls them.
4. Change every pixel to the nearest of the arcade's own 256 colours, so the picture uses only
   colours the game can show. The six colours the game swaps for flashing colours are left out,
   so no part of the picture flashes by mistake.
5. Paint the front robot's eyes in the grunt's eye colour, so they flash the way a grunt's do.

Run it from the repository root:  python tools/make-main-menu-art.py
"""
from PIL import Image, ImageEnhance

SRC = "ref/main-menu-art.png"
OUT = "src/Robotron2084/Content/Sprites/MainMenuRobot.png"

# The screen, in arcade pixels.
WIDTH, HEIGHT = 320, 200

# The part of the painting kept: the full width, less 16 rows top and bottom (8 by 5, like the screen).
CROP = (0, 16, 1024, 656)

# How much the colour and the contrast are turned up after shrinking (1.0 leaves them as they are).
COLOUR_BOOST = 1.25
CONTRAST_BOOST = 1.1

# The six colours the game swaps for the flashing colours (GamePalette.CyclingPaletteIndexMarkers).
MARKERS = {0xC4, 0xF4, 0xCC, 0x81, 0x45, 0x2F}

# The grunt's eye colour: the marker for flashing colour 12 (Grunt_1.png's eyes).
EYE_MARKER = 0xCC

# A box round the front robot's two eyes, in the painting's pixels.
EYE_BOX = (550, 50, 660, 90)

# How much of a picture pixel must be eye before it is painted as eye (0 to 1).
EYE_COVER = 0.5


def rgb(value):
    """Arcade colour byte (BBGGGRRR) to RGB, exactly as the port's RobotronColour does."""
    red = (value & 0x07) << 1
    if red > 6:
        red += 1
    green = (value & 0x38) >> 2
    if green > 6:
        green += 1
    blue = ((value & 0xC0) >> 6) * 5
    return (min(255, red << 4), min(255, green << 4), min(255, blue << 4))


def is_eye(pixel):
    """True for the eyes' pale white and blue, and False for the red armour and its orange shine."""
    red, green, blue = pixel[:3]
    return green > 120 and blue > 120


def arcade_palette_image():
    """A palette image holding every arcade colour except the markers, for Pillow to match against."""
    colours = [rgb(value) for value in range(256) if value not in MARKERS]
    flat = [channel for colour in colours for channel in colour]
    flat += flat[:3] * (256 - len(colours))  # pad to 256 entries with a repeat, which is never closer
    palette = Image.new("P", (1, 1))
    palette.putpalette(flat)
    return palette


def eye_mask(painting):
    """How much of each picture pixel is the front robot's eyes, as a greyscale image (255 = all eye)."""
    mask = Image.new("L", painting.size, 0)
    x0, y0, x1, y1 = EYE_BOX
    for y in range(y0, y1):
        for x in range(x0, x1):
            if is_eye(painting.getpixel((x, y))):
                mask.putpixel((x, y), 255)
    return mask.crop(CROP).resize((WIDTH, HEIGHT), Image.BOX)


def main():
    painting = Image.open(SRC).convert("RGB")
    print(f"source {SRC} {painting.size}")

    small = painting.crop(CROP).resize((WIDTH, HEIGHT), Image.BOX)
    small = ImageEnhance.Color(small).enhance(COLOUR_BOOST)
    small = ImageEnhance.Contrast(small).enhance(CONTRAST_BOOST)
    picture = small.quantize(palette=arcade_palette_image(), dither=Image.Dither.NONE).convert("RGBA")

    eyes = eye_mask(painting)
    eye_colour = rgb(EYE_MARKER) + (255,)
    eye_pixels = 0
    for y in range(HEIGHT):
        for x in range(WIDTH):
            if eyes.getpixel((x, y)) >= EYE_COVER * 255:
                picture.putpixel((x, y), eye_colour)
                eye_pixels += 1

    picture.save(OUT)
    colours = len(set(picture.get_flattened_data()))
    print(f"  {OUT} {WIDTH}x{HEIGHT} arcade px, {colours} colours, {eye_pixels} eye pixels")


if __name__ == "__main__":
    main()
