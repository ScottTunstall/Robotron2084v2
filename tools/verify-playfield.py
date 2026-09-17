#!/usr/bin/env python3
"""Headless playfield render check — the regression guard for notes section 40.

WHY THIS EXISTS
---------------
The 12-second launch smoke test only ever exercises the TITLE screen, so it
never noticed the M4 regression in which every sprite and every HUD element
disappeared while the wall alone kept drawing (notes section 40). This tool
launches the game, presses SPACE to start a real game, screenshots the client
area and asserts the playfield INTERIOR contains actual pixels instead of the
solid black void.

Exit code 0 = interior has content; 1 = interior is (near) black = REGRESSION.

Usage:
    python tools/verify-playfield.py            # launch, start, check, exit
    python tools/verify-playfield.py --keep     # leave the game running
    python tools/verify-playfield.py --exe PATH # check a specific build

Requires: Pillow (already used by tools/screenshot-map.py) and a real display.
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

# These two need EXPLICIT signatures. Without argtypes ctypes passes the
# arguments as C ints and SetWindowPos silently fails (returns 0), leaving the
# game behind whatever window is in front -- and the capture below is a SCREEN
# grab, so it would measure that other window instead.
user32.SetWindowPos.argtypes = [wt.HWND, wt.HWND, ctypes.c_int, ctypes.c_int,
                                ctypes.c_int, ctypes.c_int, ctypes.c_uint]
user32.SetWindowPos.restype = wt.BOOL
user32.ShowWindow.argtypes = [wt.HWND, ctypes.c_int]
user32.ShowWindow.restype = wt.BOOL

WM_KEYDOWN = 0x0100
WM_KEYUP = 0x0101
VK_SPACE = 0x20
SPACE_SCAN = 0x39

# Wall band thickness (screen margin) as a fraction of the client area, plus
# slack for the HUD strip. The interior test region must sit well inside the
# wall ring so the ring itself cannot satisfy the check.
INSET_FRACTION = 0.14
# A healthy started wave draws the player, ~9 robots, humans and electrodes;
# at any integer scale that is thousands of pixels. 200 is a safe floor.
MIN_INTERIOR_PIXELS = 200
PIXEL_STEP = 2
# The other direction: an interior that is MOSTLY lit cannot be Robotron -- a
# started wave is black apart from its sprites (a healthy reading is ~3%). This
# only trips when the capture grabbed the wrong window (see WindowRaised), which
# is otherwise a silent false PASS.
MAX_INTERIOR_FRACTION = 0.5


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
    """Start the game. PostMessage needs no foreground rights; if the game
    ignores it (SDL filters synthetic messages on some setups) fall back to
    SendInput with an AttachThreadInput foreground grab."""
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
    """Raise the game above everything for the duration of the capture.

    ImageGrab.grab captures a SCREEN REGION, not a window, so anything
    overlapping the game's rect (a maximised browser, say) is what gets
    measured. SetWindowPos to HWND_TOPMOST needs no foreground rights -- unlike
    SetForegroundWindow, which Windows routinely refuses -- so this is what
    makes the reading trustworthy no matter what the author has open.
    """

    def __init__(self, hwnd: int):
        self._hwnd = hwnd

    def __enter__(self):
        user32.ShowWindow(self._hwnd, 9)  # SW_RESTORE: un-minimise
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


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--exe", default=default_exe())
    parser.add_argument("--keep", action="store_true", help="do not kill the game")
    parser.add_argument("--png", default="playfield-check.png")
    parser.add_argument("--settle", type=float, default=1.5,
                        help="seconds to let the started wave render")
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

        time.sleep(1.0)
        press_space(hwnd)
        time.sleep(args.settle)

        # Raise the game first: the grab below is a screen-region capture, so an
        # overlapping window would otherwise be measured instead of the game.
        with WindowRaised(hwnd):
            left, top, right, bottom = client_rect(hwnd)
            w, h = right - left, bottom - top
            print(f"client area: {w}x{h} at {left},{top}")

            img = ImageGrab.grab(bbox=(left, top, right, bottom)).convert("RGB")
            img.save(args.png)

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
        fraction = lit / max(total, 1)
        print(f"playfield interior: {lit} lit samples of {total} "
              f"({100.0 * fraction:.3f}%)  [saved {args.png}]")

        if lit < MIN_INTERIOR_PIXELS:
            print(f"FAIL: interior is effectively empty (< {MIN_INTERIOR_PIXELS} "
                  f"lit samples) — sprites/HUD are not rendering (notes section 40)")
            return 1

        if fraction > MAX_INTERIOR_FRACTION:
            print(f"FAIL: interior is {100.0 * fraction:.1f}% lit, which is not a "
                  f"Robotron playfield. Either the capture is not the game window "
                  f"(something in front of it?) or it caught a pre-render frame — "
                  f"re-run, and if it persists, investigate.")
            return 1

        print("PASS: playfield interior has content")
        return 0
    finally:
        if not args.keep:
            proc.terminate()


if __name__ == "__main__":
    sys.exit(main())
