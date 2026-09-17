# Ledger — Robotron 2084 → MonoGame .NET 10

Companion to `plan.md`. Rules:

- Update a task's **Status** the moment it changes; keep **Notes/Evidence**
  current (addresses, files, commits).
- Every non-obvious decision or user directive gets a **Decision** entry
  (D-xxx). Decisions are final unless a later entry supersedes them (it says
  which).
- Add one line to the **Log** whenever the ledger changes materially.

**Status values:** `TODO` · `IN PROGRESS` · `BLOCKED` · `DONE` · `SKIPPED`

---

## Decisions

| ID | Date | Decision | Source |
|----|------|----------|--------|
| D-001 | 2026-08-29 | Authoritative references: (1) **original source** `historicalsource/robotron` — recovered 6809 assembly, 28 modules, commit `da11ac04361ddae40e5c9f9fd328d228f92a953a` — **primary behaviour reference**; (2) `robomame.asm` (annotated 6809 disassembly, full 64K) — **asset-data source** + annotated cross-reference. Both copied into the repo (`ref/`, `asm/`). | User + plan.md |
| D-002 | 2026-08-29 | Original CPU is **6809** (not Z80). | User |
| D-003 | 2026-08-29 | **Sound is out of scope** — ROM pokes 6-bit tokens to the sound board (`$C80E`); port ignores them. | User |
| D-004 | 2026-08-29 | Original screen: **304 × 256, 16 colours, 4 bits per pixel** (2026-08-30: corrected from 255 — each 2-px column is exactly a 256-byte block; carry into bit 8 of the address moves a column right). | User |
| D-005 | 2026-08-29 | Original screen RAM was pair-major (byte N = leftmost 2 px of row N) — historical context only; **not modelled** at runtime (D-011). Sprite pixel buffers are per-frame row-major (e.g. `$0437`: width 6 bytes = 12 px, height 13, data `$043B`). | User |
| D-006 | 2026-08-29 | No 6809 emulator — native C# only. (Superseded by D-011 for the fidelity level.) | plan.md v1 |
| D-007 | 2026-08-29 | `spec.txt` in the repo root is **not** to be used for this project. | User |
| D-008 | 2026-08-29 | Window upscale: **as big as the display allows** — largest integer scale that fits (2×/3×/4×+), no downscale, no distortion; F11 cycles scale. | User |
| D-009 | 2026-08-29 | **Power-on "INITIAL TESTS INDICATE:" sequence is out of scope** — boot straight into adjustment/attract. | User |
| D-010 | 2026-08-29 | Original **interrupt/raster-driven graphics are converted to idiomatic MonoGame** — no raster model, no blitter timing, no ROM/RAM bus switch, no watchdog (see plan.md §1). | User |
| D-011 | 2026-08-29 | **Not an exact port.** Deliverable: same gameplay, idiomatic MonoGame implementation. 6809 speed artefacts (incremental sprite drawing, blitter/scanline interleaving, task-script machinery as a mechanism, cycle-count timers) are NOT reproduced — sprites are full textured quads each frame; flow scripts become state-machine steps. (Supersedes D-006's "direct translation".) | User |
| D-012 | 2026-08-29 | **No hard-coded timers** unless MonoGame recommends them: engine fixed timestep drives `Update(GameTime)`; all durations are `TimeSpan`s advanced by `ElapsedGameTime`; periodic effects derive from `TotalGameTime`; original per-frame speeds converted once to px/s (×50, 50 fps original) in a generated `Speeds.cs`. No `Stopwatch`/`Thread.Sleep`/frame-cycles in game code. | User |
| D-013 | 2026-08-29 | **No cocktail / screen-flip support** — `FLIP_SCR_UP/DOWN` (`$D099`/`$D09C`) and any related adjustment setting are out; screen always upright. | User |
| D-014 | 2026-08-29 | Behaviour extraction reads the **original source modules** first (`RRSCRIPT.ASM` script engine, `RRP8` player, `RRG23` grunts, `RRH11` hulks, `RRS22` spheroids, `RRSET`, `RRTEXT`, `RRTABLE`, …); `robomame.asm` annotations cross-check; MAME is the last-resort tie-breaker. | plan.md |
| D-015 | 2026-08-29 | **No raster tracking at all** — beam counters, `DRAW_PLAYER_IF_…_RASTER_BEAM` gating and slow/E-clock DMA sync are 6809 slowness artefacts, not game behaviour. The *visible* partial-blit effects (explosions, player/object appearance) are **noted** (plan §2b: `RevealEffect` — SpriteBatch source-rect reveal; explosion ≈1 row/frame, 16-frame life; appear ≈½ row/frame, tracks its object) but **deferred** — not in the first release; first release draws them as normal instant sprites. | User |
| D-016 | 2026-08-29 | Coding preferences: **new .NET project only** (delete any old project + files before recreating); **Roslynator installed**; clean code; **one class per file**; **composition over inheritance**; **principle of least surprise**; **SRP**; **DI only where needed for unit testing** (no DI containers, plain construction otherwise). Recorded in plan §7. | User |
| D-017 | 2026-08-30 | **Incremental delivery: gameplay first.** Build the playable game (playfield, player, entities, waves, collision, scoring — M2 scope) before the pre-game screens (adjustment/high-scores/attract/credits — M1 scope). Core foundation pieces needed for gameplay (input, palette, text, persistence) land as needed along the way. Execution order: scaffold → assets → gameplay (old Phase 2) → pre-game screens (old Phase 1) → polish. | User |
| D-018 | 2026-08-30 | **Pinned versions** (latest stable on nuget.org, 2026-08-30): `MonoGame.Framework.DesktopGL` **3.8.5.1**, `MonoGame.Content.Builder.Task` **3.8.5.1** (no MonoGame 4.x exists on NuGet — 3.8.5.1 targets net8.0, compatible with net10.0; resolves risk R5), `Roslynator.Analyzers` **5.0.0**, tests: `xunit.v3` **4.0.0** + `xunit.runner.visualstudio` **4.0.0** + `Microsoft.NET.Test.Sdk` **18.9.0**. | NuGet MCP lookup |
| D-019 | 2026-09-17 | **No operator/service subsystem.** The port does NOT implement the self-test / service (adjustment) mode or anything coin related: no `RRTEST*.ASM` screens, no switch/ROM/RAM/colour-RAM tests, no CMOS settings or bookkeeping totals (credits, coins, "men played", average time/turns per credit), no free-play/coin-door handling, no credit counting. This is a GAME that must look and behave like the arcade's game, not an operator's cabinet. Directly in the same class as D-013 (no cocktail) and D-015 (no raster). Start buttons ARE kept: `1` / `2` start a one- or two-player game (the arcade's START 1/START 2), because that is how the player chooses a game, not an accounting feature. | Author, 2026-09-17: *"we do not need the service test or coin tests for this game. This game is to look and play like the arcade but we don't need self test code or coin counting etc"* |

## Open questions

| ID | Question | Where to answer |
|----|----------|-----------------|
| Q-001 | Game-coordinate → pixel mapping (player x `7..140`, y `24..223` vs 304×256 grid) | T-006 — player draw + spawn code (original source + `$DD3E`) |
| Q-002 | Sprite decode details: nibble order, per-entity palette mapping | T-005 — PNG preview sheet |
| Q-003 | Original cycle-count / script-sleep values → `TimeSpan` conversions (50 fps basis; verify feel) | T-006 — `RRSCRIPT.ASM` sleep semantics + annotations |
| Q-004 | Which source is authoritative for asset data — **RESOLVED 2026-08-30**: verified blue-label ROM image (`ref/rom/robotron64k.bin`) is byte-level authority (46,160 listed disasm bytes match; 34-byte delta in 8 local regions, see Q-006); original source = behaviour reference; disasm = annotations | T-004 |
| Q-005 | Entity rules to capture: laser limits, spawn constraints, projectile lifetimes, wave counts per difficulty — wave counts per wave now captured (`ref/robowaves.md`, tables $2E24/$2C12, routine $2B7C) | T-006, then per-entity tasks T-015…T-025 |
| Q-006 | The 34 mismatched bytes (8 local regions, byte-shift pattern — see status.md): hand edits in the disasm or a dump quirk? Non-blocking: ROM image is byte authority regardless | Ask author |

---

## Tasks

### Phase 0 — References, scaffold, assets
| ID | Task | Status | Notes / Evidence |
|----|------|--------|------------------|
| T-001 | Copy `asm/robomame.asm` + `ref/original-source/` (historicalsource/robotron @ `da11ac0`) into repo | DONE | committed in `108db54`: `asm/robomame.asm`, `ref/original-source/` (28 .ASM), `ref/rom/robotron64k.bin` |
| T-002 | Scaffold: git init, .gitignore, solution, `net10.0` MonoGame `DesktopGL` + content pipeline, Roslynator, warnings=errors; 304×256 window at integer scale | IN PROGRESS | versions pinned per D-018 |
| T-003 | Verify MonoGame 4.x + net10.0 + DesktopGL; pin versions | IN PROGRESS | no 4.x on NuGet (D-018); 3.8.5.1 + net10.0 verified at build/run (risk R5) |
| T-004 | ExtractAssets v0: asset-data authority check (Q-004); build 64K ROM image; validate anchors (`$DA51`/`$F60B`/`$F6EE`, "INITIAL T…" `$6CA5`, hulk tables `$01CC`+) | IN PROGRESS | Q-004 resolved (image = byte authority); 64K image built + cross-checked (see log 2026-08-30); `$DA51` verified vs ripper table. Remaining: `$6CA5`/`$01CC` anchor spot-checks |
| T-005 | ExtractAssets v1: decode sprites/fonts/palettes/animation tables → PNGs + data + `Speeds.cs` (×50); preview sheet (Q-002) | TODO | |
| T-006 | Extract gameplay facts into ledger: coordinate map (Q-001), speeds, timings (Q-003), entity rules (Q-005) | TODO | |
| T-007 | **M0:** window runs; asset set complete and eyeballed | TODO | |

### Phase 1 — Core + pre-game screens
| ID | Task | Status | Notes / Evidence |
|----|------|--------|------------------|
| T-008 | Core foundation: input (keyboard + gamepad), palette, text renderer, settings + scores persistence (JSON) | TODO | |
| T-009 | State machine + scene rendering | TODO | |
| T-010 | Game adjustment screen (editable, persisted) | TODO | |
| T-011 | High score screen | TODO | |
| T-012 | Attract (title + fancy attract per setting) | TODO | |
| T-013 | Credits/coins/pricing, 1P/2P start, initials entry, lives | TODO | |
| T-014 | **M1:** full pre-game flow end-to-end | TODO | |

### Phase 2 — Gameplay
| ID | Task | Status | Notes / Evidence |
|----|------|--------|------------------|
| T-015 | Level start: playfield, boundary, spawn order/constraints, robot freeze period | TODO | |
| T-016 | Player: 8-way movement, per-direction animations, facing, 4 fire directions, lasers, death, lives | TODO | `RRP8.ASM` |
| T-017 | Family members (mommy/daddy/Mikey) | TODO | `RRM1.ASM` |
| T-018 | Electrodes | TODO | |
| T-019 | Grunts & hulks (spawn, AI, laser effects) | TODO | `RRG23.ASM`, `RRH11.ASM`, `RRHX4.ASM` |
| T-020 | Spheroids → enforcers (+sparks) | TODO | `RRS22.ASM` |
| T-021 | Quarks → tanks (+shells) | TODO | |
| T-022 | Brains → cruise missiles | TODO | `RRB10.ASM` |
| T-023 | Collision system (all rules) | TODO | |
| T-024 | Waves + difficulty + per-player restart state | TODO | `RRSCRIPT.ASM` scripts |
| T-025 | Scoring, extra man, game over → attract | TODO | |
| T-026 | **M2:** playable game — waves, all entity classes | TODO | |

### Phase 3 — Fidelity & polish
| ID | Task | Status | Notes / Evidence |
|----|------|--------|------------------|
| T-027 | Unit tests for every extracted rule (spawn, collision, waves, scoring, settings, speeds) | TODO | |
| T-028 | Playtest matrix (1P/2P, difficulties, death/restart, extra man, tilt, auto-up, HS entry); spot-check vs MAME | TODO | |
| T-029 | Performance: 60 fps, zero GC in hot path | TODO | |
| T-030 | README (run, controls, layout, behaviour→source cross-reference) | TODO | |
| T-031 | **M3:** done | TODO | |
| T-032 | *(deferred, D-015)* `RevealEffect` fancy blitter fx per plan §2b (explosion + appear, source-rect reveal, tracking) | TODO | parameters captured in T-006 |

---

## Log

| Date | Entry |
|------|-------|
| 2026-08-29 | Project started. Fetched `robomame.asm` (21,335 lines, full 64K ROM). Author confirmed: 6809 CPU; 304×255 screen, 16 colours, 4bpp; pair-major original screen RAM (context only); sprite data in ROM; sound out of scope; power-on test out of scope; upscale as big as display allows; interrupt-driven graphics → idiomatic MonoGame (D-001…D-010). Wrote `plan.md`. |
| 2026-08-29 | Approach revised per author: **not an exact port** — same gameplay, idiomatic MonoGame (D-011); **no hard-coded timers** — engine timestep + `GameTime`/`TimeSpan`, speeds as px/s (D-012); **no cocktail/screen-flip** (D-013). Added **original source** `historicalsource/robotron` @ `da11ac0` (28 named 6809 .ASM modules, incl. `RRSCRIPT.ASM` script engine) as primary behaviour reference (D-001, D-014); `robomame.asm` becomes asset source + cross-reference. Tasks reorganised (T-001…T-031). |
| 2026-08-29 | Partial blits resolved (D-015): no raster/beam modelling — 6809 slowness artefacts. Read `RRHX4.ASM`/`RRDX2.ASM`: explosions ≈1 row/frame with 16-frame life; appears ≈½ row/frame and track their object. Design note added as plan §2b (`RevealEffect`, SpriteBatch source-rect). **Deferred per author** — first release draws explosions/appearances as normal sprites; task T-032 added to Phase 3; parameters still captured in T-006. |
| 2026-08-29 | Execution started. Coding preferences recorded (D-016, plan §7): new project only, Roslynator, one class per file, composition over inheritance, least surprise, SRP, DI only for unit testing. Environment: .NET SDK 10.0.400 present; repo not yet a git repository (none to delete — first-ever project). |
| 2026-08-30 | Author clarified: **model the game's functionality/gameplay, not the hardware** — no ROM/RAM switch, blitter, beam, watchdog, PIA, 4bpp storage, or 1 MHz timing as mechanisms (game features like 304×255, 16-colour palette, 50 fps stay). Recorded as the top principle in `status.md`. |
| 2026-08-30 | MAME driver analysed (`src/mame/williams/williams.cpp` @ master) → `ref/mame-notes.md`: MAME `robotron` = **Release 5 solid blue label** = author's disassembly target; full ROM layout + CRC/SHA1; 48K RAM block at $0000–$BFFF (switchable with ROM1–9 at $0000–$8FFF); palette RAM $C000; PIAs $C804/$C80C; switch $C900; blitter $CA00–$CA07; beam $CB00; watchdog $CBFF; CMOS $CC00–$CFFF; ROM10–12 $D000–$FFFF; 6809E = 12 MHz/3/4 = 1.0 MHz; 6808 sound @ 3.579545 MHz; decoder PROMs 7641-5. |
| 2026-08-30 | **ROM verified**: local set `E:\roms\MAME ALL ROMS - DO NOT DELETE\robotron` — all 12 blue-label ROMs match MAME master CRC32+SHA1. Built 64K image (`/tmp/romtool/robotron64k.bin`, hole $9000–$CFFF zeroed) → to be committed as `ref/rom/robotron64k.bin`. Cross-checked `asm/robomame.asm`: 46,160 listed ROM bytes, **34 mismatches in 8 small local regions (99.93% match)** — local byte-shift pattern; ROM image is byte authority; Q-006 asks author about the regions. (First two parser revisions had bugs — mnemonic-hex consumption — causing a false 1,649 count; fixed with the full-field rule, noted in status.md.) |
| 2026-08-30 | Facts settled (author-confirmed + code-verified): **DP = $98** (header `_d` EQUs are direct offsets: `credits_d $0051` → $9851, `num_players_d $0040` → $9840); **palette** $DA51 ROM default → $9800 RAM at startup (code $D795), $9800 = runtime colour source; **credits** runtime $9851, **persisted in CMOS $CD00** (BCD 2 bytes; `paid_credits` $CD14, `credits_played` $CD2C; unit settings $CC0C/$CC0E/$CC10 BCD); **screen RAM = $0000–$97FF** (38K, 152 B/row × 255), game state from $9800. |
| 2026-08-30 | Wave data captured → `ref/robowaves.md` (seanriddle.com): wave setup routine **$2B7C** (waves 1–40 unique, then repeats 21–40), enemy counts table **$2E24**, ~12 gameplay params **$2C12** (9 decrease / 3 increase with wave, difficulty-modified); wave = 8-bit, wraps after 255 (displayed 55), 2-digit display. Full 40-wave table in repo. Q-005 partially closed. |
| 2026-08-30 | **Colour construction resolved** (seanriddle.com/ripper.html + ripper source `C:\Users\scott\source\repos\WmsGfxSpriteRipper` + MAME `williams_v.cpp::palette_init`): pixel nibble → 16-entry RAM palette ($9800, default $DA51) → 8-bit value → **256-entry resistor-network palette** (3-bit R + 3-bit G taps 1200/560/330Ω, 2-bit B taps 560/330Ω) → analog RGB. The 7641-5 decoder PROMs are decoder timing, **not** colour. Ripper's built-in "Robotron @da51" palette is **byte-identical** to the ROM $DA51 bytes (cross-verified). Full details, MAME code snippet, ripper 4-bit RGB conversion, 16 default colours (approx RGB), port plan → `ref/palette-notes.md`. T-008 approach updated (port MAME's palette_init; no PROM decoding needed). |
| 2026-08-30 | **D-004 corrected: screen is 304 × 256** (author: "It's 256 rows, that's why incrementing the MSB of the memory address moves a pixel column along"). Screen RAM $0000–$97FF = 152 × 256 B = exactly 38,912 B. Pair-major layout = 2 px/byte vertical stripes, 256 B per column. Docs updated (status/plan/mame-notes). |
| 2026-08-30 | **Sprite storage format identified** (ripper.html, author-verified vs disasm): Robotron sprites = **Linear layout** (row-major, width bytes/row, 2 px/byte); stored as little "animations" — several frames back-to-back with no bytes between; first frame preceded by **4 header bytes, first 2 = width + height, NOT XORed with $04** (Joust/Bubbles XOR; Robotron doesn't). Cross-checks disasm sprite $0437 (12×13, data @ $043B). → `ref/palette-notes.md`; simplifies T-005/T-007 extraction. |
| 2026-08-30 | **Sprite entry format VERIFIED** (author-confirmed + ROM-checked): entry = 4 bytes `[width][height][ptr_hi][ptr_lo]` — width = bytes/row (2 px/byte), height = pixels, pointer big-endian to first frame data; entries in consecutive 4-byte tables, frames contiguous with pitch = w×h. All 17 known entries (skull 12×11 @043B, black dot @047D, 5-frame 12×5 table @0485–0498, mommies 8×14, daddies 10×13, mikeys 6×11, 14×16 ×2, 16×15 ×3, 10×11, 8×7, 14×16 @2141, 8×12 @3603) verified header-bytes + frame pitch against `ref/rom/robotron64k.bin` → ALL OK. $0437 annotation "0B = 13" was a doc error (0B = 11). → `ref/palette-notes.md`. |
| 2026-08-30 | Author directive: **incremental — gameplay first, title/pre-game screens later** (D-017). Ledger task statuses corrected (T-001 DONE). NuGet MCP verified working; versions pinned (D-018): MonoGame 3.8.5.1 (no 4.x on NuGet — R5 resolved), Roslynator 5.0.0, xunit.v3 4.0.0 line. T-002 scaffold started. |
