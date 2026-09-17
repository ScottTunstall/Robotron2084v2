# Handoff: make the code survive a screen-resolution increase

**Task (user, 2026-07):** "You should have the code such that if I decide the screen
resolution needs to increase, the code won't break."

If I decide later to raise `ScreenSize.SpecScale` (e.g. 2 → 3 or 4, internal render
960×600 / 1280×800) or `SpecWidth`/`SpecHeight` (320×200 spec space → larger), nothing
should break: no crashes, no mis-rendered sprites, no failing tests.

**Status: COMPLETE (2026-09-12).** All changes (a)–(e) applied and verified:
- `dotnet build` → 0 warnings, 0 errors.
- Tests: **89/89** pass — run via the MTP exe, NOT `dotnet test` (see "Test runner quirk").
- Proof run: SpecScale 2→3 (960×600) 89/89; SpecScale 2→4 (1280×800) 89/89;
  SpecHeight 250 (640×500) 88/89 (only the deliberate `SpecSpace_IsTheGameLayout_320x200`
  pin fails, as designed); all probes reverted — final baseline 89/89.
- `rebuild-ledger.md` header + watch items updated. Not yet git-committed
  (checkpoint per ledger convention happens at the author's commit).

```
./tests/Robotron2084.Tests/bin/Debug/net10.0/Robotron2084.Tests.exe
```

## How resolution works today (the good news)

- `src/Robotron2084/Core/ScreenSize.cs` is the single funnel:
  `SpecScale = 2`, `SpecWidth = 320`, `SpecHeight = 200`,
  `Width = SpecWidth * SpecScale` (640), `Height = SpecHeight * SpecScale` (400),
  `Scaled(specPx) = specPx * SpecScale`.
- All gameplay geometry (entity sizes/speeds/spawn distances, wall thickness, HUD
  margins) already goes through `ScreenSize.Scaled(GameplayConstants.XxxSpecPixels)`.
  `GameplayConstants` is the single tuning file (spec-px values, no `Scaled` in-file).
- Sprite PNGs (ROM-extracted, `Content/Sprites/*.png`) are 1× arcade pixels
  (player 8×12, brains 14×16 max, quark 16×15 …). They are scaled at DRAW time by
  `SpriteSet.DrawSprite` (`texture.Size * ScreenSize.SpecScale`), so any integer scale
  keeps them fitting their `Scaled(16)` collision boxes (tallest/widest ROM frames are
  16 px, so they fit at ANY integer SpecScale).
- Walls: `PlayfieldWall` stretches a 1×1 `WallPixel` texture — resolution-independent.
- Window sizing: `RobotronGame` renders to a `ScreenSize.Width×Height` RenderTarget2D,
  blits at integer scale; `_maxScale = ScreenSize.MaxIntegerScale(DisplayInfo.WorkArea)`
  adapts automatically; F11 cycles 1×…max. `app.manifest` is per-monitor DPI aware, so
  WorkArea is real pixels.
- Content pipeline (mgcb) is resolution-independent (no pipeline-side scaling).
- spec.txt says "320×200 (or scaled up to look like it)" → the design intent is that
  only the SCALE should change; the 320×200 spec space is the game's layout.

## What breaks / degrades if the resolution changes (the audit findings)

Order of severity. (a)–(c) are the real fixes; (d) is consistency polish; (e) is the
test-suite guard.

### (a) `src/Robotron2084/Rendering/PixelArtFactory.cs` — patterns hard-coded to a 32×32 canvas
`PatternSize = ScreenSize.Scaled(16)` is the OUTPUT size, but every `Build*Pattern`
authors directly into a canvas that must be 32×32:
- `BuildSpheroidPattern` / `BuildQuarkPattern`: fixed center (15,15) and radius 13 in
  loops bounded by `PatternSize` → at PatternSize≠32 the disc/diamond is off-centre and
  the wrong size (loops also read/write out of intended bounds logic).
- All other `Build*Pattern`: absolute `FillRect` coords authored for 32×32 → pattern
  pinned to the top-left of a bigger canvas.
- NOTE: these patterns are currently UNUSED in the production path (entity sprites come
  from ROM PNGs; `SpriteSet` only uses `PixelArtFactory.CreateSolid` for PlayerLaser and
  WallPixel). They are public API and would silently mis-render if reused — this is the
  main latent landmine.
**Fix:** introduce `DesignSize = 32` as the authoring canvas (`NewCanvas()`/`FillRect`
clamp to it; spheroid/quark loops bound to it), then nearest-neighbour scale each
pattern DesignSize → `PatternSize` before returning. `Build*` signatures unchanged.

### (b) `src/Robotron2084/States/TitleScreenState.cs` — `DrawTopTen`: `y += 24;`
Hard-coded internal px (12 spec-px × 2). → `y += ScreenSize.Scaled(12);`

### (c) `tests/Robotron2084.Tests/Core/ScreenSizeTests.cs` — pins absolute resolution
- `InternalResolution_Is_640x400` asserts `Width == 640`, `Height == 400`.
- `Scaled_MultipliesSpecPixelsBySpecScale` asserts `Scaled(16) == 32`.
- `MaxIntegerScale_FitsDisplay` InlineData expectations (e.g. (1920,1080)→2) are
  computed from 640×400 and would be wrong at other scales.
A resolution change = red test suite.
**Fix:** rewrite as invariants:
- pin the SPEC space (the game layout per spec.txt): `SpecWidth == 320`,
  `SpecHeight == 200`;
- `Width == SpecWidth * SpecScale`, `Height == SpecHeight * SpecScale`;
- `Scaled(n) == n * SpecScale` (n = 0, 1, 16);
- `MaxIntegerScale` cases expressed against the actual constants, e.g.
  `MaxIntegerScale(Width * 3, Height * 3) == 3`,
  `MaxIntegerScale(Width, Height * 2) == 1` (width-limited),
  plus tiny/zero-area inputs → 1 (never 0).
- DO NOT pin `SpecScale` — that's exactly the knob the user may turn.

### (d) Consistency polish (same values, route through the funnel)
- `src/Robotron2084/Level/PlayfieldWall.cs:17` —
  `Thickness = ScreenSize.SpecScale * GameplayConstants.WallThicknessSpecPixels`
  → `ScreenSize.Scaled(...)` (identical value; single-funnel hygiene; update comment).
- `src/Robotron2084/Rendering/SpriteSet.cs` `DrawSprite` (~line 114) and
  `src/Robotron2084/States/PlayingState.cs` `DrawHud` (~lines 120–121) —
  `texture.Width * ScreenSize.SpecScale` → `ScreenSize.Scaled(texture.Width)`.

### (e) Docs hygiene (comments that go stale)
- `RobotronGame.cs` class doc: "The whole game is a 640x400 image (spec's 320x200
  doubled)" → phrase via `ScreenSize.Width/Height`.
- `ScreenSize.cs` Width/Height docs "(640)"/"(400)" → formula phrasing; beef up the
  class doc to be the explicit "how to raise resolution" guide (SpecScale = sharper
  same-layout; SpecWidth/SpecHeight = larger playfield, a design change).

## Confirmed NON-issues (already resolution-safe — do not touch)

- `PlayField.cs` (all spawn/collision math), `WallFleeHelper`, `RectangleExtensions`,
  `IntVector2` — parameterised by bounds/`Scaled`.
- All entity `Size`/speed constants in `Entities/*.cs` — via `Scaled(GameplayConstants…)`.
- Menu states' text Y positions use `ScreenSize.Scaled(specPx)` — stay anchored at top;
  a taller screen just gains empty space (layout doesn't break).
- `SpriteFont` (Arial 32) drawn with explicit scale factors — unaffected.
- `MaxIntegerScale` guards: min-1x, degenerate inputs → 1.
- `PlayingState.LifeIconSize = Scaled(8)` (spacing only); icon drawn at
  `lifeIcon.Size * SpecScale` — consistent at any scale.
- ROM PNGs fit the `Scaled(16)` box at any integer scale (largest frame dim is 16).

## Test runner quirk (environment, pre-existing — not caused by any change)

- `dotnet test tests/Robotron2084.Tests` → "Zero tests ran", exit code 5 (xunit.v3 /
  Microsoft.Testing.Platform via global.json `"test": { "runner": "Microsoft.Testing.Platform" }`).
- Working baseline command:
  `./tests/Robotron2084.Tests/bin/Debug/net10.0/Robotron2084.Tests.exe`
  → `Total: 78, Errors: 0, Failed: 0` (as of this handoff).
- Optional follow-up (out of scope): fix `dotnet test` MTP integration or document the
  direct-exe command in README/docs.

## Suggested order of implementation on resume

1. Apply (d) + (e) (trivial, zero behaviour change).
2. Apply (b).
3. Apply (a) PixelArtFactory design-canvas refactor.
4. Rewrite (c) ScreenSizeTests as invariants; add small pattern-invariant tests for
   `BuildSpheroidPattern`/`BuildQuarkPattern` (length == PatternSize², centre pixel
   filled, corner empty — holds at any SpecScale).
5. `dotnet build` → 0 warnings; run test exe → all green.
6. **Prove the task:** temporarily set `SpecScale = 3`, rebuild, run tests, confirm
   green and (if a headless check is possible) the render target is 960×600 with sprites
   centred; then revert to 2. Same quick pass for `SpecHeight = 250` (or similar) to
   prove the spec-space knob.
7. Update `status.md` / `rebuild-ledger.md` per project convention when done.
