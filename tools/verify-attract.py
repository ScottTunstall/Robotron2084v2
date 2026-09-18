#!/usr/bin/env python3
"""Headless attract-mode check — the regression guard for Phase 12.1 (notes section 94).

WHY THIS EXISTS
---------------
The demo/attract mode is the machine playing itself. Nothing else exercises
it: the 12-second smoke test stops at the title screen and verify-playfield.py
starts a HUMAN game. This tool:

  1. launches the game and waits for the TITLE screen,
     asserts the title interior has content (the "ROBOTRON 2084" /
     "SAVE THE LAST HUMAN FAMILY" large-font lines, the men, the score),
  2. sits out the full idle timeout (GameplayConstants.TitleIdleSeconds)
     and asserts the ATTRACT DEMO has taken over (a live game: the interior
     is no longer just title text — it must have real wave content),
  3. presses SPACE (the arcade's fire = a human at the cabinet) and asserts
     the machine hands back to the TITLE screen.

Exit code 0 = all three phases passed; 1 = a phase regressed.

Usage:
    python tools/verify-attract.py            # ~30 s, launches, checks, exits
    python tools/verify-attract.py --keep     # leave the game running
    python tools/verify-attract.py --exe PATH # check a specific build

Requires: Pillow (already used by tools/verify-playfield.py) and a real display.
"""
import argparse
import ctypes
import ctypes.wintypes as wt
import os
import subprocess
import sys
import time

user32 = ctypes.WinDLL("user32", use_last_error=True)
kernel32 = ctypes.windll.kernel32

user32.SetWindowPos.argtypes = [wt.HWND, wt.HWND, ctypes.c_int, ctypes.c_int,
                                ctypes.c_int, ctypes.c_int, ctypes.c_uint]
user32.SetWindowPos.restype = wt.BOOL
user32.ShowWindow.argtypes = [wt.HWND, ctypes.c_int]
user32.ShowWindow.restype = wt.BOOL

WM_KEYDOWN = 0x0100
WM_KEYUP = 0x0101
VK_SPACE = 0x20
SPACE_SCAN = 0x39

INSET_FRACTION = 0.14
PIXEL_STEP = 2
# Title interior: two large-font lines + three men + the score block.
TITLE_MIN_LIT = 150
# Demo interior: the player plus the wave's robots.
DEMO_MIN_LIT = 200
MAX_INTERIOR_FRACTION = 0.5
# The C# side reads TitleIdleSeconds from GameplayConstants; keep them in sync.
TITLE_IDLE_SECONDS = 12


def repo_root() -> str:
    return os.path.dirname(os.path.dirname(os.path.abspath(__file__)))


def default_exe() -> str:
    return os.path.join(repo_root(), "src", "Robotron2084", "bin", "Debug",
                        "net10.0", "Robotron2084.exe")


def window_for_pid(pid: int) -> int:
    found = []

    @ctypes.WINFUNCTYPE(ctypes.c_bool, wt.HWND, ctypes.c_long)
    def cb(h, _):
        wpid = wt.DWORD()
        user32.GetWindowThreadProcessId(h, ctypes.byref(wpid))
        if wpid.value == pid and user32.IsWindowVisible(h):
            n = user32.GetWindowTextLengthW(h)
            buf = ctypes.create_unicode_buffer(n + 1)
            user32.GetWindowTextW(h, buf, n + 1)
            if "ScottOTron" in buf.value:
                found.append(h)
        return True

    user32.EnumWindows(cb, 0)
    return found[0] if found else 0


def client_rect(hwnd: int):
    r = wt.RECT()
    user32.GetClientRect(hwnd, ctypes.byref(r))
    origin = wt.POINT(r.left, r.top)
    user32.ClientToScreen(hwnd, ctypes.byref(origin))
    return origin.x, origin.y, origin.x + r.right, origin.y + r.bottom


class KEYBDINPUT(ctypes.Structure):
    _fields_ = [("wVk", wt.WORD), ("wScan", wt.WORD), ("dwFlags", wt.DWORD),
                ("time", wt.DWORD), ("dwExtraInfo", ctypes.POINTER(ctypes.c_ulong))]


class _INPUTUNION(ctypes.Union):
    _fields_ = [("ki", KEYBDINPUT), ("pad", ctypes.c_byte * 24)]


class INPUT(ctypes.Structure):
    _fields_ = [("type", wt.DWORD), ("u", _INPUTUNION)]


def press_space(hwnd: int) -> None:
    down = 0x00000001 | (SPACE_SCAN << 16)
    up = 0xC0000001 | (SPACE_SCAN << 16)
    user32.PostMessageW(hwnd, WM_KEYDOWN, VK_SPACE, down)
    time.sleep(0.06)
    user32.PostMessageW(hwnd, WM_KEYUP, VK_SPACE, up)
    time.sleep(0.4)

    fg = user32.GetForegroundWindow()
    tid_fg = user32.GetWindowThreadProcessId(fg, None)
    tid_me = kernel32.GetCurrentThreadId()
    attached = user32.AttachThreadInput(tid_fg, tid_me, True)
    try:
        user32.SetForegroundWindow(hwnd)
        time.sleep(0.4)
        for flags in (0, 0x0002):
            inp = INPUT(type=1, u=_INPUTUNION(ki=KEYBDINPUT(VK_SPACE, SPACE_SCAN, flags, 0, None)))
            user32.SendInput(1, ctypes.byref(inp), ctypes.sizeof(INPUT))
            time.sleep(0.08)
    finally:
        if attached:
            user32.AttachThreadInput(tid_fg, tid_me, False)


HWND_TOPMOST = -1
HWND_NOTOPMOST = -2
SWP_NOMOVE = 0x0002
SWP_NOSIZE = 0x0001
SWP_SHOWWINDOW = 0x0040


class WindowRaised:
    def __init__(self, hwnd: int):
        self._hwnd = hwnd

    def __enter__(self):
        user32.ShowWindow(self._hwnd, 9)  # SW_RESTORE
        if not user32.SetWindowPos(self._hwnd, HWND_TOPMOST, 0, 0, 0, 0,
                                   SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW):
            raise RuntimeError(
                f"could not raise the game window (error {ctypes.get_last_error()}); "
                "refusing to measure a screen region the game may not be in")
        time.sleep(0.35)
        return self

    def __exit__(self, *exc):
        user32.SetWindowPos(self._hwnd, HWND_NOTOPMOST, 0, 0, 0, 0,
                            SWP_NOMOVE | SWP_NOSIZE)
        return False


def lit_samples(img) -> tuple:
    """(lit, total, fraction) for the interior region, same inset as verify-playfield."""
    w, h = img.size
    ix0, iy0 = int(w * INSET_FRACTION), int(h * INSET_FRACTION)
    ix1, iy1 = w - ix0, h - iy0
    px = img.load()
    lit = 0
    for y in range(iy0, iy1, PIXEL_STEP):
        for x in range(ix0, ix1, PIXEL_STEP):
            p = px[x, y]
            if p[0] + p[1] + p[2] > 24:
                lit += 1
    total = ((ix1 - ix0) // PIXEL_STEP) * ((iy1 - iy0) // PIXEL_STEP)
    return lit, total, lit / max(total, 1)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--exe", default=default_exe())
    parser.add_argument("--keep", action="store_true", help="do not kill the game")
    args = parser.parse_args()

    if not os.path.exists(args.exe):
        print(f"FAIL: exe not found: {args.exe}")
        return 1

    from PIL import ImageGrab

    print(f"launching {args.exe}")
    proc = subprocess.Popen([args.exe])
    try:
        hwnd = 0
        for _ in range(40):
            hwnd = window_for_pid(proc.pid)
            if hwnd:
                break
            time.sleep(0.5)
        if not hwnd:
            print("FAIL: game window never appeared")
            return 1

        with WindowRaised(hwnd):
            left, top, right, bottom = client_rect(hwnd)
            print(f"client area: {right - left}x{bottom - top} at {left},{top}")

            # ---- Phase 1: the title screen ---------------------------------
            time.sleep(1.5)
            title = ImageGrab.grab(bbox=(left, top, right, bottom)).convert("RGB")
            title.save("attract-title.png")
            lit, total, fraction = lit_samples(title)
            print(f"title interior: {lit} lit samples of {total} "
                  f"({100.0 * fraction:.3f}%)  [saved attract-title.png]")
            if lit < TITLE_MIN_LIT:
                print(f"FAIL: title interior is effectively empty (< {TITLE_MIN_LIT} "
                      "lit samples) — the title text / men / score are not rendering")
                return 1
            if fraction > MAX_INTERIOR_FRACTION:
                print(f"FAIL: title interior is {100.0 * fraction:.1f}% lit — the capture "
                      "is not the game window (something in front of it?)")
                return 1
            print("PASS: title screen has content")

            # ---- Phase 2: the attract demo ----------------------------------
            print(f"sitting out the {TITLE_IDLE_SECONDS}s idle timeout for the attract demo...")
            time.sleep(TITLE_IDLE_SECONDS + 3.0)
            demo = ImageGrab.grab(bbox=(left, top, right, bottom)).convert("RGB")
            demo.save("attract-demo.png")
            lit, total, fraction = lit_samples(demo)
            print(f"demo interior: {lit} lit samples of {total} "
                  f"({100.0 * fraction:.3f}%)  [saved attract-demo.png]")
            if lit < DEMO_MIN_LIT:
                print(f"FAIL: demo interior has no wave content (< {DEMO_MIN_LIT} lit "
                      "samples) — the attract demo never took over")
                return 1
            if fraction > MAX_INTERIOR_FRACTION:
                print(f"FAIL: demo interior is {100.0 * fraction:.1f}% lit — the capture "
                      "is not the game window (something in front of it?)")
                return 1
            print("PASS: the attract demo is playing")

            # ---- Phase 3: a human at the cabinet takes the machine back ------
            press_space(hwnd)
            time.sleep(1.0)
            back = ImageGrab.grab(bbox=(left, top, right, bottom)).convert("RGB")
            back.save("attract-back-to-title.png")
            lit, total, fraction = lit_samples(back)
            print(f"back-to-title interior: {lit} lit samples of {total} "
                  f"({100.0 * fraction:.3f}%)  [saved attract-back-to-title.png]")
            if lit < TITLE_MIN_LIT:
                print(f"FAIL: pressing fire during attract did not hand back to the "
                      f"title screen (< {TITLE_MIN_LIT} lit samples)")
                return 1
            print("PASS: fire during attract returns to the title screen")

        print("PASS: attract mode (title -> demo -> title) works")
        return 0
    finally:
        if not args.keep:
            proc.terminate()


if __name__ == "__main__":
    sys.exit(main())
