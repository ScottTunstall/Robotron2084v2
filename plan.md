# Plan: Robotron 2084 → MonoGame .NET 10 (OpenGL)

**References (two, complementary):**
1. **Original source code** — `https://github.com/historicalsource/robotron`
   (recovered original 6809 assembly, 28 modules: `RRP8` player, `RRG23`
   grunts, `RRH11` hulks, `RRS22` spheroids, `RRSCRIPT` script engine,
   `RRSET` settings, `RRTEXT`, `RRTABLE`, `RRTEST*`, …). Commit
   `da11ac04361ddae40e5c9f9fd328d228f92a953a`. This is the **primary
   behaviour reference** — named routines and scripts are far more readable
   than a disassembly. Copied to `ref/original-source/` in P0.
2. **`robomame.asm`** — Scott Tunstall's annotated 6809 disassembly of the
   Solid Blue label ROM (full 64K listing, ~21,335 lines, copied to
   `asm/`). Source of **asset data** (sprite pixel buffers, animation
   tables, fonts, palettes, game tables all live in the ROM data in the
   file) and the annotated cross-reference for anything ambiguous in the
   original source.

**Target:** A MonoGame desktop app on **.NET 10**, OpenGL renderer
(`DesktopGL`), that plays Robotron 2084 — same gameplay — implemented
**idiomatically within MonoGame**, not as a line-by-line port of the ROM.

**Fidelity level (author directive):**
- **Same gameplay, idiomatic implementation.** We reproduce: the game flow
  and screens, every entity's behaviour and rules, wave contents, scoring,
  credits/lives/extra man, settings — as the ROM defines them.
- **We do NOT reproduce the 6809's CPU/hardware constraints** — incremental
  sprite drawing, blitter/scanline timing, the ROM task-scheduler machinery,
  ROM/RAM bus switching, the watchdog, or any "slow CPU" artefact. In
  MonoGame sprites are textured quads drawn every frame; there are no such
  constraints.
- **No hard-coded timers.** All timing is MonoGame-idiomatic: engine
  timestep + `GameTime`/`TimeSpan` (see §Timing).
- **Out of scope:** sound (ROM only pokes 6-bit tokens to the sound board),
  the power-on "INITIAL TESTS INDICATE:" sequence (boot straight into
  adjustment/attract), CMOS battery hardware (→ file persistence),
  **cocktail / screen-flip support** (`FLIP_SCR_UP/DOWN` `$D099`/`$D09C` and
  any related adjustment setting — the screen is always upright).

**Author-confirmed facts:**
- CPU was 6809; screen was **304 × 256, 16 colours, 4 bits per pixel**.
- Original screen RAM was pair-major (byte N = leftmost 2 pixels of row N) —
  historical context only; sprite pixel buffers in the ROM are per-frame
  row-major (e.g. `$0437`: width 6 bytes = 12 px, height 13 px, data at
  `$043B`). We render normally, so the VRAM layout is not modelled.
- The original machine ran at 50 frames/s; the ROM's per-frame movement
  deltas therefore convert to px/s by ×50 (see §Timing).

---

## 1. Approach

**Gameplay-faithful reimplementation in idiomatic MonoGame/C#.**

The **original source** is read as the behaviour spec (entity rules, wave
scripts, screens, scoring, settings semantics); `robomame.asm` is the
annotated cross-reference and the asset-data source. Each gameplay element
is implemented with normal MonoGame patterns:

| Original mechanism | Idiomatic MonoGame equivalent |
|---|---|
| Blitter + 6809 speed limit → sprites drawn incrementally, frame by frame | `SpriteBatch` textured quads, drawn in full every frame, in render-order |
| Task list (`$9815`, allocated at `$D1E3`) + action codes ("Sleep N cycles", …) — the script engine of `RRSCRIPT.ASM` | Per-entity `Update(GameTime)` state machines; flow scripts become state-machine steps; delays as `TimeSpan` countdowns |
| Frame interrupt (palette `$9800–$980F`, counters) | Normal update phase; palette is a 16-colour table loaded at startup (or swapped as a *game effect* where the ROM changes it) |
| Partial blits (`RRHX4.ASM`/`RRDX2.ASM`): staged row buffers, ≤16 rows/frame DMA budget, beam-safe redraw gating | **Deferred** — design note in §2b for a later polish pass (P3.5): explicit `RevealEffect`s (SpriteBatch source-rect clip). First release draws explosions/appearances as normal (instant) sprites |
| Cycle-count timers (6809 clock) | `TimeSpan` values driven by `GameTime.ElapsedGameTime` |
| ROM/RAM bus switch `$C900`, `vidctrs` `$CB00`, watchdog `$CBFF` | Dropped — no hardware model |
| Battery CMOS `$CC00–$CD68` (BCD-packed, checksummed) | JSON persistence with the same fields/semantics |
| Custom character set (space=0x3A, ◄=0x5E backspace glyph) | Font atlas textures (large + small) rendered as quads |

**Assets are extracted from the .asm**, not hand-drawn: a tool parses the
file's instruction + data lines into the full 64K ROM image, then decodes
sprite frames, fonts and palettes into PNGs + generated data files.

**Reference implementations for behavioural questions:** MAME (visual/behaviour
reference), the author's own knowledge. Where the annotation is ambiguous, the
task is resolved against MAME and recorded in the ledger.

---

## 2. Timing (MonoGame-idiomatic, no hard-coded timers)

- Use the engine: `Game.IsFixedTimeStep` (MonoGame's default fixed 1/60 s
  tick) drives `Update(GameTime)`. Nothing in game code counts frames or
  cycles.
- All durations are **`TimeSpan`** values advanced by
  `GameTime.ElapsedGameTime`: freeze periods, death animations, spawn delays,
  blink periods, projectile lifetimes.
- Blinking/periodic effects derive from `GameTime.TotalGameTime`
  (e.g. `((int)t.TotalGameTime.TotalSeconds) % 2`), the standard MonoGame
  pattern.
- **Speed fidelity:** ROM per-frame deltas (from animation tables) become
  **px/s = delta × 50** in a single generated `Speeds.cs` table, so the game
  feels exactly as fast as the original regardless of update rate. Animation
  frame durations are likewise converted (frame × 1/50 s, or from the ROM's
  frame-change timing where it exists).
- No `Stopwatch`, no `Thread.Sleep`, no real-time anywhere in game logic.

## 2b. Partial blits → progressive reveal (no raster) — **deferred**

> **Status: noted, not in the first release** (author directive). Explosions
> and appearances ship as normal instant sprites; this section is the design
> note for the later polish pass (P3.5).

In the original, explosions and object appearances are drawn **partially**,
several rows per frame (`RRHX4.ASM`: `HEXSTV`/`HAPSTV`, `RRDX2.ASM`:
`EXSTZ`/`APSTZ`). The ≤16-rows/frame DMA budget, the `SLOW`/E-clock sync bit
and the `DRAW_PLAYER_IF_…_RASTER_BEAM` gating exist only because the 6809
can't finish a full blit in a frame — **none of that is modelled** (no raster
tracking at all). The *visible* effect is reproduced as an explicit effect:

- **`RevealEffect`**: a sprite + top→bottom reveal, drawn each frame with a
  `SpriteBatch` source-rect clip (`src = (0, 0, w, revealedRows)`). No staged
  buffers, no per-row cost.
- **Explosion**: reveals ~1 row/frame → **50 rows/s**; 16-frame lifetime →
  **0.32 s**, then removed. Fire-and-forget.
- **Appear** (player/object spawn, and while scrolling): ~½ row/frame →
  **25 rows/s**, completes at full height; anchored to — and **tracking** —
  its object's live position each frame (so the player sprite still appears
  progressively while moving).
- Rates/lifetimes are the generated constants (×50 from the ROM's per-frame
  values, per D-012). Exact per-trigger parameters (which events start which
  variant, sizes, directions) are captured in P0.6.

## 3. Screens & flow (as defined by the ROM)

Adjustment (settings) → High scores → Attract (title; fancy attract table per
the `$CC12` setting) → Initials/credits → Play (waves) → Game over → back to
attract. Settings semantics from the CMOS block (`$CC00–$CC8C`): extra-man
every (BCD), turns per player, pricing, fancy attract, difficulty 0–10,
letters for highest score name, factory restore, clear bookkeeping, high-score
reset, auto-cycle, attract message, checksum. Bookkeeping totals
(`$CD02–$CD2C`) and high scores (`$CD32–$CD68`) persist to file.

## 4. Entities (behaviour to be extracted from the ROM logic per task)

Player (+ family: mommy/daddy/Mikey), grunts, hulks, brains (+cruise
missiles), tanks (+shells), quarks (+tanks), spheroids (+enforcers), sparks,
electrodes, player lasers. One C# class per entity, all sharing an `Entity`
base (position, velocity, state alive/dying, animation, `Update`/`Draw`).
Spawn rules, AI (e.g. hulk stalks the player — `$0113`), movement degrees of
freedom, projectile limits, collision rules and point values are extracted
from the original source modules (cross-referenced with `robomame.asm`) in
the gameplay tasks below and captured as tests.

Render order (original layering, back to front): electrodes → robots →
projectiles → player.

## 5. App architecture

```
Robotron2084/
├─ plan.md  ledger.md  README.md
├─ asm/robomame.asm                 # annotated disassembly (copied in)
├─ ref/original-source/             # historicalsource/robotron @ da11ac0
│  │                                # (28 .ASM files, primary behaviour ref)
├─ Robotron2084.sln
├─ src/Robotron2084/
│  ├─ Robotron2084.csproj           # net10.0, MonoGame.DesktopGL,
│  │                                # Content.Builder.Task, Roslynator,
│  │                                # warnings=errors
│  ├─ Content/
│  │  ├─ Content.mgcb
│  │  └─ Generated/                 # produced by tools/ExtractAssets
│  │     ├─ Sprites/*.png           # one atlas per entity + frames
│  │     ├─ Fonts/Large.png  Small.png
│  │     └─ Data/                   # palette, speeds, wave tables (raw/json)
│  ├─ Program.cs
│  ├─ RobotronGame.cs               # window, integer scale (as big as fits,
│  │                                # F11 to cycle), SpriteBatch pipeline
│  ├─ Core/
│  │  ├─ Input.cs                   # keyboard + gamepad → game actions
│  │  ├─ Palette.cs                 # 16 colours (ROM tables)
│  │  ├─ Text.cs                    # font-atlas quad renderer (large/small,
│  │  │                             # BCD numbers)
│  │  ├─ Settings.cs                # adjustment settings, checksum, JSON save
│  │  ├─ Scores.cs                  # credits, high scores, bookkeeping, save
│  │  └─ GameStateMachine.cs
│  ├─ Entities/
│  │  ├─ Entity.cs
│  │  ├─ Player.cs  Family.cs  Grunt.cs  Hulk.cs  Brain.cs
│  │  ├─ Tank.cs  Quark.cs  Spheroid.cs  Electrode.cs
│  │  └─ Projectiles.cs             # lasers, sparks, shells, cruise missiles
│  ├─ Systems/
│  │  ├─ Spawner.cs                 # level parameters, spawn rules
│  │  ├─ Waves.cs                   # progression, difficulty, per-player
│  │  │                             # restart state
│  │  ├─ Collision.cs
│  │  └─ Scoring.cs
│  └─ States/
│     ├─ GameAdjustment.cs  HighScores.cs  Attract.cs  Initials.cs
│     ├─ Play.cs  GameOver.cs  Tilt.cs
├─ tools/ExtractAssets/             # console tool: .asm → ROM image →
│  │                                # decoded PNGs + data + Speeds.cs
└─ tests/Robotron2084.Tests/        # xUnit: rules, waves, collision, scoring
```

**Window:** playfield is 304×256 (original geometry). Window uses an integer
scale as large as the display allows (2×/3×/4×+, no downscale, no
distortion); F11 cycles scale. Nearest-neighbour filtering.

**Input mapping** (original: 8-way stick, 4 fire buttons, advance, coins):

| Original | Keyboard | Gamepad |
|---|---|---|
| Stick up/down/left/right | WASD or Arrows | Left stick |
| Fire up / down / left / right | Z / X / C / V | 4 buttons |
| Advance (start) | Enter | Start |
| Coin left/centre/right | F1 / F2 / F3 | — |
| High-score reset, tilt, auto-up | F4 / T / U (debug) | — |
| Cycle screen scale | F11 | — |

*(Defaults decided alongside the input layer; changes ledgered.)*

## 6. Phases & milestones

### Phase 0 — References, scaffold + asset extraction
- **P0.1** Copy `asm/robomame.asm` and `ref/original-source/` (28 .ASM
  files from historicalsource/robotron @ `da11ac0`) into the repo; record
  provenance in the ledger.
- **P0.2** Scaffold: git init, .gitignore, solution, `net10.0` project
  (`DesktopGL` + content pipeline), Roslynator, `TreatWarningsAsErrors`;
  window opens at integer scale.
- **P0.3** Verify MonoGame 4.x on net10.0 with `DesktopGL`; pin versions.
- **P0.4** ExtractAssets v0: confirm where asset data is authoritative
  (original source modules vs `robomame.asm` data lines); build the 64K ROM
  image and validate anchors (default palette `$DA51` — $F60B/$F6EE turned out to be
  code, not palettes; wave tables `$2B7C`/`$2E24`, "INITIAL T…" string `$6CA5`,
  hulk animation tables `$01CC`+).
- **P0.5** ExtractAssets v1: decode all sprite frames (via object metadata),
  large/small fonts, palettes, animation tables → per-frame deltas →
  `Speeds.cs` (×50 px/s), wave/game tables; emit PNGs + data into
  `Content/Generated/`; PNG preview sheet for eyeballing (incl. nibble-order
  check).
- **P0.6** Extract gameplay facts into the ledger — read the original source
  (scripts, entity modules, settings) first, cross-check `robomame.asm`:
  coordinate→pixel mapping (player x `7..140`, y `24..223` vs 304×256),
  per-entity speeds, key timings (script sleep values → TimeSpans), entity
  rules (limits, spawn constraints), and **partial-blit parameters** (which
  in-game events start explosions/appears, images, sizes, reveal rates,
  lifetimes, tracking) per §2b.
- **M0:** window runs; extracted sprites/fonts visible; asset set complete
  and eyeballed.

### Phase 1 — Core + pre-game screens
- **P1.1** Core foundation: input (keyboard + gamepad), palette, text
  renderer, settings + scores persistence (JSON, same fields/semantics).
- **P1.2** State machine shell + scene rendering.
- **P1.3** Game adjustment screen (editable, persisted).
- **P1.4** High score screen.
- **P1.5** Attract (title + fancy attract per setting).
- **P1.6** Credits/coins/pricing, 1P/2P start, initials entry, lives.
- **M1:** full pre-game flow works end-to-end.

### Phase 2 — Gameplay
- **P2.1** Level start: playfield, boundary, spawn order/constraints (from
  ROM), robot freeze period at level start.
- **P2.2** Player: 8-way movement, per-direction animations (incl. standing),
  facing, 4 fire directions, laser slots, death animation, lives.
- **P2.3** Family members.
- **P2.4** Electrodes.
- **P2.5** Grunts & hulks (wave spawns, AI, laser effects).
- **P2.6** Spheroids → enforcers (+sparks).
- **P2.7** Quarks → tanks (+shells).
- **P2.8** Brains → cruise missiles.
- **P2.9** Collision system (all rules, incl. "dying cannot kill" semantics).
- **P2.10** Waves: progression, difficulty scaling, per-player restart state.
- **P2.11** Scoring, extra man, game over → attract.
  *(Explosions/appearances are drawn as normal sprites in this release —
  the fancy partial-blit reveal is deferred to P3.5, §2b.)*
- **M2:** playable game — waves progress, all entity classes behave per ROM.

### Phase 3 — Fidelity & polish
- **P3.1** Unit tests for every extracted rule (spawn, collision, waves,
  scoring, settings, speeds table).
- **P3.2** Playtest matrix (1P/2P, difficulties, death/restart, extra man,
  tilt, auto-up, HS entry, bookkeeping); spot-check behaviour vs MAME.
- **P3.3** Performance: 60 fps, zero GC in hot path.
- **P3.4** README (run, controls, layout, behaviour→ROM-address
  cross-reference).
- **P3.5** *(deferred fancy blitter fx)* `RevealEffect` system per §2b:
  explosion + appear variants, SpriteBatch source-rect reveal, tracking.
- **M3:** done (P3.5 optional for the first release).

## 7. Coding standards
- Idiomatic C#, clean code.
- Roslynator analyzers installed; **all build warnings are errors**
  (`TreatWarningsAsErrors`).
- **One class per file.** No multiple classes in one file.
- **Composition over inheritance.**
- **Principle of least surprise** — APIs and behaviour follow C#/
  MonoGame conventions.
- **Single Responsibility Principle** for classes.
- **Dependency injection only where needed for unit testing** — no DI
  containers, no injected-everything; plain construction/composition by
  default.
- **New projects only** — never reuse or build on stale scaffolding; if a
  project must be redone, delete the old one and its files first.
- xUnit test project.
- `Content/Generated/*` is tool output — never hand-edited.

## 8. Risks / open items
| # | Item | Mitigation |
|---|---|---|
| R1 | "Same gameplay" without a port: subtle behaviours may be missed | Original source + annotations read per entity; rules captured as tests; MAME + author for disputes |
| R2 | Sprite decode details (nibble order, palette mapping per entity) | P0.4 PNG preview sheet; fix tool, re-extract |
| R3 | Original cycle-count → TimeSpan conversion for animations/delays | P0.5: derive from annotations (50 fps basis), verify feel in playtest |
| R4 | Coordinate→pixel mapping (x 7..140, y 24..223 vs 304×256) | P0.5, from player draw + spawn code in the .asm |
| R5 | MonoGame 4.x + net10.0 + DesktopGL compatibility | P0.2, early |
