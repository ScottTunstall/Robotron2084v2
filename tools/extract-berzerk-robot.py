#!/usr/bin/env python3
"""Cuts the BerzerkRobot's sprite frames out of its sprite sheet (ref/berzerk-robot-sheet.png).

The sheet is a blown-up picture of the robot: every sprite pixel is an 8 x 8 block of the picture, and the frames sit in
cells on a dark red background. It holds:

  * a block of 18 cells in 3 rows of 6. The first row is the robot standing still, a cycle of six frames:
    BerzerkRobot_Idle_1 .. BerzerkRobot_Idle_6. The other two rows are the walking frames, with two empty cells in the middle row.
    The ten frames are numbered in reading order. The first two are the robot walking right (BerzerkRobot_WalkRight_1 and
    _2), the next two walking left (BerzerkRobot_WalkLeft_1 and _2) and the next three walking up (BerzerkRobot_WalkUp_1
    to _3, which play in the order 1, 2, 3, 2) and the last three walking down (BerzerkRobot_WalkDown_1 to _3);
  * a block of 4 cells in 2 x 2: the robot standing (BerzerkRobot_Stand) and then the three stages of it being
    destroyed (BerzerkRobot_Explode_1 .. 3), read left to right, top to bottom.

Every idle and walking frame is 8 x 12 pixels with the head on the top row, so a walk keeps the head still (the shorter frames
get an empty bottom row). The four frames of the other block share one canvas, so they line up when shown one after another.
Each pixel is drawn in the sheet's own red; everything else is transparent.

Run from the repository root:  python tools/extract-berzerk-robot.py
Then run tools/generate-mgcb.py to add the new PNGs to the content pipeline.
"""
import pathlib
import sys

from PIL import Image

ROOT = pathlib.Path(__file__).resolve().parents[1]
SHEET = ROOT / "ref/berzerk-robot-sheet.png"
OUT = ROOT / "src/Robotron2084/Content/Sprites"

BLOCK = 8
RED = (255, 0, 0)
BORDER = (82, 2, 7)

# The walking block: the left edge of each of the six columns, and the top and bottom of each of the three rows (in sheet pixels).
WALK_COLUMNS = [4, 76, 148, 220, 292, 364]
WALK_COLUMN_WIDTH = 64
WALK_ROWS = [(2, 89), (106, 193), (210, 305)]
WALK_FRAME_ROWS = 12

# The walking frames the author has named, by their reading-order number.
WALK_NAMES = {
    1: "BerzerkRobot_WalkRight_1",
    2: "BerzerkRobot_WalkRight_2",
    3: "BerzerkRobot_WalkLeft_1",
    4: "BerzerkRobot_WalkLeft_2",
    5: "BerzerkRobot_WalkUp_1",
    6: "BerzerkRobot_WalkUp_2",
    7: "BerzerkRobot_WalkUp_3",
    8: "BerzerkRobot_WalkDown_1",
    9: "BerzerkRobot_WalkDown_2",
    10: "BerzerkRobot_WalkDown_3",
}

# The other block: the four cells, as (left, top, width, height) in sheet pixels.
OTHER_CELLS = [
    (436, 10, 128, 144),
    (572, 10, 128, 144),
    (436, 162, 128, 144),
    (572, 162, 128, 144),
]
OTHER_NAMES = ["BerzerkRobot_Stand", "BerzerkRobot_Explode_1", "BerzerkRobot_Explode_2", "BerzerkRobot_Explode_3"]


def read_cell(sheet, left, top, width, height):
    """Returns the cell as rows of booleans, one per sprite pixel (True where the sheet is red)."""
    return [
        [sheet.getpixel((left + (x * BLOCK) + 3, top + (y * BLOCK) + 3)) == RED for x in range(width // BLOCK)]
        for y in range(height // BLOCK)
    ]


def write_png(name, rows, width, height):
    image = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    for y, row in enumerate(rows[:height]):
        for x, filled in enumerate(row[:width]):
            if filled:
                image.putpixel((x, y), RED + (255,))
    image.save(OUT / f"{name}.png")
    print(f"{name}: {width} x {height}")


def main() -> int:
    if not SHEET.exists():
        print(f"missing {SHEET}", file=sys.stderr)
        return 1

    sheet = Image.open(SHEET).convert("RGB")
    assert sheet.getpixel((150, 150)) == BORDER, "the sheet's layout has changed"

    walk_number = 0
    for row_number, (top, bottom) in enumerate(WALK_ROWS):
        for column_number, left in enumerate(WALK_COLUMNS):
            rows = read_cell(sheet, left, top, WALK_COLUMN_WIDTH, bottom - top + 1)
            if not any(any(row) for row in rows):
                continue
            if row_number == 0:
                name = f"BerzerkRobot_Idle_{column_number + 1}"
            else:
                walk_number += 1
                name = WALK_NAMES.get(walk_number, f"BerzerkRobot_Walk_{walk_number}")
            write_png(name, rows, WALK_COLUMN_WIDTH // BLOCK, WALK_FRAME_ROWS)

    cells = [read_cell(sheet, *cell) for cell in OTHER_CELLS]
    filled = [(x, y) for rows in cells for y, row in enumerate(rows) for x, on in enumerate(row) if on]
    left = min(x for x, _ in filled)
    top = min(y for _, y in filled)
    width = max(x for x, _ in filled) - left + 1
    height = max(y for _, y in filled) - top + 1
    for name, rows in zip(OTHER_NAMES, cells):
        cropped = [row[left:left + width] for row in rows[top:top + height]]
        write_png(name, cropped, width, height)

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
