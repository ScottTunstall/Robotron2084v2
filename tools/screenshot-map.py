#!/usr/bin/env python3
"""Screenshot the game window and print an ASCII colour map of the client area.

Why this exists: the operator cannot "see" the game (this harness overlaps the
window), so gameplay is verified by dumping a coarse colour map. The map makes
the playfield structure legible in text: K=black, W=white (sprites/HUD),
G/B/R/Y=colours (cycling wall ring, entities), ?=other.

Usage:  python tools/screenshot-map.py [output.png]

Requires the game window to be RUNNING and in the state you want to inspect.
Works on the single 3440x1440 display (9070XT) of the author's machine;
GDI's ImageGrab captures the correct (and only) monitor.
"""
import ctypes
import ctypes.wintypes as wt
import sys
from collections import Counter

user32 = ctypes.windll.user32


def find_game_window() -> int:
    hwnd = None

    @ctypes.WINFUNCTYPE(ctypes.c_bool, wt.HWND, ctypes.c_long)
    def cb(h, _):
        nonlocal hwnd
        if user32.IsWindowVisible(h):
            n = user32.GetWindowTextLengthW(h)
            buf = ctypes.create_unicode_buffer(n + 1)
            user32.GetWindowTextW(h, buf, n + 1)
            if "ScottOTron" in buf.value:
                hwnd = h
        return True

    user32.EnumWindows(cb, 0)
    if hwnd is None:
        raise SystemExit("game window not found (is the game running?)")
    return hwnd


def main() -> None:
    hwnd = find_game_window()
    r = wt.RECT()
    user32.GetWindowRect(hwnd, ctypes.byref(r))
    print(f"game rect: {r.left},{r.top} - {r.right},{r.bottom}")

    from PIL import ImageGrab
    img = ImageGrab.grab(bbox=(0, 0, 3440, 1440))
    out = sys.argv[1] if len(sys.argv) > 1 else "game-shot.png"
    img.save(out)
    print(f"full desktop saved to {out}")

    # Client area = window minus ~8px borders and ~31px title bar.
    sub = img.crop((r.left + 8, r.top + 31, r.right - 8, r.bottom - 8)).convert("RGB")
    w, h = sub.size
    px = sub.load()

    def cellchar(x: int, y: int, cw: int = 40, ch: int = 40) -> str:
        c: Counter = Counter()
        for yy in range(y, min(y + ch, h), 4):
            for xx in range(x, min(x + cw, w), 4):
                p = px[xx, yy]
                if sum(p) < 24:
                    c["K"] += 1
                elif p[0] > 180 and p[1] > 180 and p[2] > 180:
                    c["W"] += 1
                elif p[1] > 150 and p[0] < 80 and p[2] < 80:
                    c["G"] += 1
                elif p[2] > 150 and p[0] < 80 and p[1] < 80:
                    c["B"] += 1
                elif p[0] > 150 and p[1] < 80 and p[2] < 80:
                    c["R"] += 1
                elif p[0] > 150 and p[1] > 150 and p[2] < 80:
                    c["Y"] += 1
                else:
                    c["?"] += 1
        return c.most_common(1)[0][0]

    print("K=black W=white G/B/R/Y=colours ?=mixed  (cell=40px)")
    for y in range(0, h, 40):
        print("".join(cellchar(x, y) for x in range(0, w, 40)))


if __name__ == "__main__":
    main()
