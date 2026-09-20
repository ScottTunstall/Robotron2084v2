# STATUS — continuation point (2026-09-17)

Read this first if resuming. Current state: the "Where we are" and "Next steps"
sections below. Detailed resume docs: `rebuild-ledger.md` (checkpoint log — every
commit, gates, next step), **`docs/handoff-2026-09-17.md` (CURRENT session handoff —
written for a fresh AI: read order, hard rules, the FIVE gates, every remaining
work item with exact files/steps, and the traps that have already bitten)**,
`docs/arcade-fidelity-notes.md` (master ROM-vs-port research log — §58 HUD, §59 two
players, §60 the font bug).
`docs/handoff-2026-09-16.md` and `docs/handoff-2026-09-13.md` are previous handoffs.
Historical: `plan.md` = the original rebuild plan, `ledger.md` = its decision log
(D-001…D-018), `ref/mame-notes.md` = MAME driver notes, `ref/robowaves.md` = wave tables.
`spec.txt` is **BANNED** (D-007) — never read or use it.

## PROTOCOL (every session)

- **SOURCE HIERARCHY — the author's directive (2026-09-16): `ref/original-source/*.ASM`
  (the 1982 original listings) is the GOSPEL and the authority for SEMANTICS.
  `asm/robomame.asm` (the disassembly) is a GUIDE ONLY** — use it to locate code in
  the R5 build and for R5-specific addresses/data, but never as the authority for
  what something MEANS and never trust its comments (at least one is flatly wrong:
  the `$5C23` comment contradicts its own `BNE`). The source names its fields and
  documents its structs (`RRDX2.ASM`'s `XSIZE`/`YSIZE`/`SLOPE`, for example) where
  the disasm only has anonymous `$BA`/`$BB` — that naming is what resolves
  ambiguity. When the two disagree on meaning, the source wins. Byte-level DATA
  (sprite/tables) still comes from the verified ROM image.

- **THE ARCADE IS ALWAYS THE STANDARD (author directive, 2026-09-16): *"It ALWAYS has
  to be like the arcade. Don't cut corners unless it's ABSOLUTELY something
  impossible."*** Never ship a stand-in, approximation or "good enough" while the real
  arcade art or behaviour can still be recovered — dig until it is recovered, or prove
  it is genuinely unreachable and say which. Never *offer* a deliberate deviation from
  the Gospel as a convenience option. If the author's playtest disagrees with the ROM,
  that is evidence the ROM decode is WRONG — keep looking rather than defending it.
  Two examples from one afternoon: the tank's birth art was declared "impossible to
  extract" and was not (notes §52), and the tank shell had been extracted at the wrong
  size for months (notes §54).

- **BEEP only when the author is actually needed** (narrowed 2026-09-16 — author:
  *"Please stop beeping unless you need my attention."*) — specifically:
  (a) a question/decision/confirmation is blocked on them, (b) they need to CHECK
  THE GAMEPLAY (playtest a build), or (c) something genuinely needs them. **Do NOT
  beep because a turn or phase finished, and not for routine progress** (that
  clause was removed). Run it at the END of the message that needs them:
  `powershell -NoProfile -Command "[console]::beep(1000,300); [console]::beep(1400,300)"`
  (verified working on the author's machine, 2026-09-13).
- Project resume docs: **`docs/handoff-2026-09-17.md` (current handoff — read this
  one)** and `docs/arcade-fidelity-notes.md` (master ROM-vs-port research log).
  `docs/handoff-2026-09-16.md` and `docs/handoff-2026-09-13.md` are historical.
- Gates before any commit: `dotnet build Robotron2084.slnx` (0 warnings), `./tests/Robotron2084.Tests/bin/Debug/net10.0/Robotron2084.Tests.exe` (NOT `dotnet test`),
  `timeout 12 ./src/Robotron2084/bin/Debug/net10.0/Robotron2084.exe` (smoke).
- **The build also enforces CA1502: cyclomatic complexity > 25 is an ERROR** (notes §99, D-020).
  The rule is off by default in .NET 10, so `.editorconfig` sets its severity to `error` and
  `CodeMetricsConfig.txt` (an `AdditionalFile`) carries the 25. Split the method — the two that
  broke it were refactored, and there are NO suppressions anywhere.
- **Playfield render gate (notes §40)**: `python tools/verify-playfield.py` —
  launches the game, presses SPACE, asserts the ring interior is not black.
  Run it whenever the draw path or any `.fx` changes; the plain smoke test
  only covers the title screen and missed the M4 "only the wall renders" bug.
- **Font glyph gate (notes §60, §92, §96.2)**: `python tools/verify-fonts.py` — re-decodes
  the ROM and compares every pixel of all **82** font master PNGs (the 468 cycling-slot
  variants were deleted — §92 draws them through the shader's GlyphCycle pass; §96.2
  added the large font's '!' ',' '.' '-' and corrected the ':').
  Run it after ANY font regeneration; the glyphs were mirrored
  inside each byte for a day while every other check stayed green.
- **Attract gate (notes §94, §96)**: `python tools/verify-attract.py` — launches the
  game and checks the attract sequence end to end: the title screen has content
  (the ROM strings + men + score), the STORYLINE MOVIE has taken over after the
  12 s idle timeout (the ROM's HISTO text crawl — ~8.7% of the interior lit against
  the title's 1.8%), and a fire press hands back to the title. ~35 s. `--full`
  (~2.5 min) also waits out the movie and checks the attract DEMO. Run it whenever
  the title screen, the movie, the demo AI, or any state transition changes; it
  saves `attract-title.png` / `attract-story.png` / `attract-demo.png` /
  `attract-back-to-title.png` (gitignored) for the author's eyes.

## The task
Translate **Robotron 2084** (1982, Williams, 6809) into a **MonoGame .NET 10 OpenGL** desktop app that
**plays the same game**, implemented **idiomatically in MonoGame** — *not* a line-by-line ROM port.
Primary behaviour reference: original source (`ref/original-source/`, historicalsource/robotron @ da11ac0,
28 .ASM modules). Byte-level data source: verified blue-label ROM image (below).
**The user is the author of the reverse engineering — "trust me"; their hardware facts are authoritative.**

## PRINCIPLE: model the game, not the hardware (user re-confirmed 2026-08-30)
> "I don't need you to emulate robotron's hardware, just the functionality and gameplay.
>  But you definitely don't need to emulate the restrictions of the ancient hardware."

- **DO NOT model** (hardware mechanisms / 6809-era restrictions): ROM/RAM bus switch ($C900),
  blitter/DMA ($CA00-$CA07), beam tracking ($CB00, D-015), watchdog ($CBFF), PIA registers ($C8xx),
  4bpp screen-RAM storage, pair-major row packing (D-005), 1 MHz CPU timing, 16-line/frame DMA limits,
  staged line buffers, task-script machinery *as a mechanism*, ROM image loading as a runtime concept.
- **DO NOT model either — the OPERATOR side (D-019, author 2026-09-17):** the self-test /
  service (adjustment) mode and everything coin related. No `RRTEST*.ASM` screens, no switch /
  ROM / RAM / colour-RAM tests, no CMOS settings or **bookkeeping totals** (credits, coins,
  "men played", average time/turns per credit), no free play or coin-door handling, no credit
  counting. *"This game is to look and play like the arcade but we don't need self test code or
  coin counting etc."* The coin-door START 1 / START 2 buttons ARE kept (they are how a player
  picks one or two players — `1` / `2` on the title screen), but nothing behind them.
- **DO model** (game functionality / what the player experiences): 304×256 screen, the 16-colour
  palette, 50 fps pacing, entities + their behaviours, waves + spawn counts, collision/scoring,
  pre-game screens, text, the *visible* partial-blit effects (D-015 — implemented as idiomatic
  effects, not as blits).
- The memory/screen layout sections below exist **to read data out of the ROM image** — they are
  reference knowledge, not a spec to emulate.

## Where we are (2026-09-19)

### Session 6 (2026-09-19) — THE FULL ATTRACT MOVIE (the intro screen and the whole storyline, notes §96)

- **"I want you to do the intro screen. ROBOTRON 2084, INSPIRED BY HIS NEVER ENDING
  QUEST FOR PROGRESS (etc.) — make it arcade faithful."** The intro screen IS the ROM's
  HISTO page script, so §95's decode became the implementation: the port now RUNS the
  arcade's own storyline — the story text crawl, the hero walking on and shooting, the
  family (MUMMY/DADDY/MIKEY with their name popups and the two deaths), the 14 grunts,
  the hulk's bounce, the spheroid/tank/enforcer scene with the brain reprogramming a
  human into a prog, and the score posts — all driven by the ROM's bytes.
- **Two interpreters, no transcription.** `Level/Attract/AttractPageMachine` is SPWAKE
  ($79DD) and `Level/Attract/AttractObjectMachine` is the object level ($7B58, all 28
  opcodes); both are MonoGame-free and unit-tested. `tools/extract-attract-scripts.py`
  generates `Level/Attract/AttractMovieData.cs` from the ROM: HISTO, the object scripts,
  the walk tables, ANATAB, the font width bytes, the twelve message strings and the four
  score-post masks.
- **The sequence is now title → STORYLINE MOVIE → the §94 demo game → title** (the
  arcade's FAMPAG/SPGSUB → HISTO → RUNIT). The movie is ~4785 ROM frames ≈ 96 s.
- **The author's report — "the player animations on the demo are not quite right" — was
  TWO defects, both caught by building it (notes §96.5/§96.6):** the generated `AnimTable`
  was **12 bytes instead of 4** (the generator's writer never truncated a short table, so
  ANATAB ran on into the following code and the hero cycled through arbitrary player
  frames), and MONO's colour operands were used as raw **slot values** where they are
  doubled-nibble **slot numbers** (`$BB` = slot 11) — an `IndexOutOfRangeException` in
  `StorylineState.DrawObjects` that killed the process ~80 s into the movie.
- **New art/figures:** the LARGE font's punctuation ('!' ',' '.' '-' at $3B-$40, plus a
  corrected ':'), the font's own pen advance (width + 1 px, not texture width + 1), the
  attract cruise missile (already extracted) and the four score-post masks; the story's
  title band uses the ROM's own row 36 (`$36,$24` is column 54, ROW 36 — §94.1 read the
  column as the row), which is why it survives the movie's clear-at-row-48.
- **Gates:** 0 warnings, **284 tests, 0 failed, 1 skipped** (+6), smoke OK,
  `verify-playfield.py` PASS, `verify-fonts.py` PASS (82), `verify-attract.py` PASS — now
  title → story → (fire) → title, with `--full` adding the demo phase.
- **2026-09-20 follow-up — the walk PHASE (notes §96.10).** Author: *"In the demo mode, I
  think either the humans are walking too fast, or the hulk is walking too fast, because
  the human only dies after the hulk walks past him!"* Both rates are the ROM's exactly
  (measured: hulk 3.5 px per 8 frames, family 1.5 px per 8 frames) — the walk ACTION slept
  the full step period after its LAST step, where `ANA2`/`BANA2` only sleep while steps
  remain (`DEC / BNE`, then `JMP [LEV2,U]`). Every MOVE opcode cost one extra period, so
  `FAMSCR`'s six MOVEs ran the mother's scripted death 48 frames late while `SCHULK`'s
  three put the hulk 24 frames early: the skull appeared five columns AFTER he passed her.
  Now they meet (both at column 59 on frame 2996, the skull on 3001) exactly as the arcade's
  choreography was written. An action that completes also returns into the script in the
  same pass now (the ROM's `JMP [LEV2,U]`). +1 test (**285**).
- **2026-09-20 — attract DEV KEYS, the hulk verified, the HUD live (notes §97).** Author:
  *"I need you to add a key that takes me directly to the 'attract' mode with the humans and
  hulks … How's about F1?"* — **F1** jumps straight into the attract STORYLINE movie, **F2**
  into the attract DEMO game (the phony player), **F3** HELD runs the movie's ROM frame clock
  8× so the hulk's walk is reached in ~7 s instead of ~51 s (`Input/DevKeys.cs` +
  `RobotronGame.Update`; port-only, no arcade claim). The author's other two reports:
  **(a)** *"the hulk in the demo mode is not instantly killing daddy and mikey when it walks
  into them"* is the §96.10 phase error, VERIFIED fixed from the port's own timeline —
  MIKEY's skull lands on frame 2675 with the hulk's box on him, the `FAMSCR` walk (the phase
  that wears the DADDY art) on 2994 with the hulk at c59, and the demo game's own
  hulk-vs-family kills happen on the FIRST overlapping tick (waves 2-3 measured);
  **(b)** *"when the player is rescuing family members in the attract mode, the score display
  is delayed"* was REAL and not only in attract: the HUD draws the session slots, which were
  only synced at a wave clear / death, so every point scored in between (a kill, a rescue's
  1000-5000, an earned spare man) was invisible until the wave ended. New
  `PlayField.SyncInto(slot)` is called every tick by both states (+1 test, **286**).
- **2026-09-20 — THE ARCADE'S HIGH SCORE TABLE (notes §98).** Author: *"add the high score
  table - make it look like the arcade's. We don't need to have the 'input your name' part just
  yet."* The port's Phase-11.7 placeholder (an XNA-font top-ten list swapped into the title, plus
  a "NEW HIGH SCORE / ENTER INITIALS" screen) is DELETED. In its place: `HighScoreTableState`
  draws RRTABLE's own screen — "ROBOTRON HEROES" (slot 7) over TODAY's ten rows in the LARGE
  font (5 per column × 2, from (26,53), 9 rows/52 columns apart), "ALL TIME HEROES" over the
  36-row SMALL-font list (12 per column × 3, from (20,136), 7 rows/40 columns apart), each row
  "N) XXX score" with the score at the ROM's fixed 11/12-column offset from the post-rank
  cursor, the operator's top entry at (21,122) as "( WILLY ELKTRIX ) 151782", the ROM's
  50%-dithered frame, the `CLSET` "you are here" highlight, a 12 s hold and a switch-to-leave.
  The model is RRTESTC's: TODAY's (10, reloaded from the ROM's `TODTAB` every run) + ALL-TIME
  (36, persisted) + the top entry, all seeded from the ROM's factory tables (`GODCHK`/`TODCHK`/
  `ALLCHK` insertion). The game-over screen is RRG23's too (string 40 "GAME OVER", large font,
  slot 10, (62,128), 2.4 s — no key wait) and the attract cycle ends in the table (`LOGG1`'s
  `JSR SCRCLR / JSR TABORG`), so: title → movie → demo → **table** → title, and a real game over
  → table with the posted scores highlighted. Open (notes §98.4): the frame's grow/erase
  animation + the LOOPP/COLA/COLC/COLD palette cycling, the initials/name entry screens, the
  score-field alignment question — and whether the arcade's attract demo posts its score.
  New dev key **F4** jumps straight to the table. +10 tests (**300**).
- **2026-09-20 — THE BRAIN'S REPROGRAMMING + THE DEMO'S STICK AND COLLISIONS (notes §97.4/§97.5).**
  Author: *"in the attract mode the brain behaviour isn't right. When mummy is touched by the
  brain, she isn't showing as being 'progged' and another brain flies over the screen!"* The
  cause was one line: `RPROG` (opcode 26) returned "keep reading", so the GHOSTed ONE-BYTE
  `PSHAKE` script ($868C) ran on into **BRAING** ($868D) — the shaking human's object executed
  `SETOB BRAIN / SETPOS (10,160)` on ITSELF and then streaked off at `SETXV $0200` (the "second
  brain"). `RunAction` also cancelled any action on a descriptor-less object (the same run-on,
  one layer up). RPROG now owns the process until the shake ends (R5 $7CD9: 64 two-frame
  iterations, base row restored, process freed) and only WALK needs a descriptor. Verified from
  the object timeline: MUMMY boxed `$AA`/`$BB` + shaking over frames 3943-4062, then the solid
  prog + three `$EE`-boxed clones from 4063. And *"in the demo where the 'AI' player is shooting
  automatically, there's a lot of jerking on the player sprite and the collision detection isn't
  working"*: **(a)** the AI's flee direction flipped on **1347 of 1800 ticks** and the port
  RESETS the walk animation on every facing change (R5 $3003-3009) — a direction now needs 3
  votes and must outlive the previous one by 9 ticks (**44** flips after); **(b)** the round-7
  playtest aid (`PlayerInvincibleForTesting`) applied to the DEMO too, so its phony player
  walked through robots, electrodes and missiles — the aid is per player now
  (`Player.InvincibleForTesting`) and `AttractState` builds its field with it OFF. That also
  un-skipped the round-7 electrode contact test (**0 skipped** from here on, +4 tests, **290**).
- **2026-09-20 — THE TABLE'S WALL AND THE PAGE'S COLOUR CYCLING (notes §98.5).** Author:
  *"There's colour cycling going on on the page, and there's a hatched 'wall' surrounding the
  scores, that also cycles."* Both are the ROM's and neither was built. **The wall** is
  `FRAMER`'s two passes, and the author's words corrected its SHAPE: it is not a rectangle
  (nor the two concentric marquees the last pass guessed from the photo's tones): each pass
  draws ~57 stroke OUTLINES, two a ROM frame, growing from (col 62, row 125)-(col 89, row 127)
  to a terminal point — `$060D` = (col 6, row 13)-(col 145, row 239) for the growing pass, then
  the SAME walk again in flavour 0 (BLACK, `CLR PD+14,U`) up to `$0E1D` = (col 14, row 29). The
  strokes tile the annulus and the erase pass retraces the inner ones, so what is left is a
  16-pixel band round the page — a HATCH, because MARQ lights exactly one pixel of every packed
  pair (the flavour's high nibble on one, its low on the next), i.e. one checkerboard of a
  single palette colour on black (`HighScoreFrameAnimation` + `HighScoreTableLayout.FrameStroke`
  / `FramePixelIsLit`). Verified with eight screenshots at 30× slow: the wall GROWS out, then
  the black pass eats the middle out. **The cycling** is the five processes `TABLE` starts for
  itself (`HighScorePalette`): `LOOPP` shifts COLTAB into slots 1-8 every 3 ROM frames — the
  wall is slot 8 and the headers (slot 7) trail it by one step — while `DECAZ`/`COLA` ramp the
  lists' slots 9/10 and `COLC`/`COLD` the highlight slots 12/13 from CATAB/CCTAB every 4 frames,
  OUT of phase for the DECAZ/COLC pair, with their `$00` terminators sending each ramp back to
  its table's start (so they pulse rather than stop). `FRAMER` also zeroes all sixteen slots, so
  the page comes up from black, and the page takes slots 10/12/13 off the in-game animator and
  hands them back when it leaves. **Two corrections while in there:** the two lists do NOT share
  a colour pair (today is `$99`/`$CC`, the top entry AND the all-time list are `$AA`/`$DD` —
  `NOINTS` prints the second list without re-setting them), and the author's photo settled
  §98.4's alignment question — the table's numbers PACK (no advance for a suppressed leading
  zero) and the ranks are unpadded. +14 tests (**314**).
- **2026-09-20 follow-up — THE PAGE BUILDS ITSELF (notes §98.6).** Author: *"The palette isn't
  cycling and the scores are being shown as the wall expands."* The second half was REAL and the
  port had it wrong: `TABLE` does not put a finished page up, it BUILDS one, and the port drew
  every element from frame 0. The ROM's order, now implemented: `MAKP LOOPP` → `JSR FRAMER`
  (**53 ROM frames with nothing else on screen** — the wall grows, then the black pass eats the
  middle) → `JSR PRJNK` for TODAY's list **four rows a ROM frame** (`TOD44`'s `LDA #4` + `TOD66`'s
  `NAP 1`; the first group prints at once and a list's last group does not sleep) → the top entry
  → the all-time list (four rows a frame) → **the headers LAST** (`SCRMES`/`WRD7V`) → and only THEN
  `MAKP DECAZ/COLA/COLC/COLD` and the 600-frame hold. Because the ramps come last, the rows that
  have just printed sit in the zeroes FRAMER left — so the text is drawn INVISIBLE for the ~0.2 s
  it takes to print and comes up dark red as `DECAZ`/`COLA` write their first `$07`. New
  `Hud/HighScorePrintSequence` (phases + the four-rows-a-frame clock), `HighScorePalette.Start`
  split from `StartRamps`, and a press during the printing is ignored (PRJNK has no switch check).
  Measured with a temporary probe + eight screenshots: wall alone for ~1.0 s, text red at ~1.2 s,
  then the two lists ramping out of phase (the all-time list white while today's is red). +7 tests
  (**321**). **For the author's eye:** the cycling RATE — the port reads `NAP n` as n ROM frames
  (16 steps/s for COLTAB, a 1.26 s lap); if the arcade's page cycles visibly slower, the A9
  "a pass is two frames" reading halves it (notes §98.6).
- **2026-09-20 follow-up 2 — THE WALL IS EIGHT COLOURS AT ONCE (notes §98.7).** Author: *"The
  wall surrounding the scores does change colour, but the arcade wall is split into multiple
  different cycling colours and this one isn't."* Real decode miss, in the one place §98.5 was
  confident: `GETA`'s `SUBA #$11` subtracts the flavour **IN PLACE**, so MARQ's strokes walk
  `$11, $88, $77, … $11, $88, …` — **a palette slot per stroke**, eight repeating. LOOPP's
  register of **exactly eight** slots was the tell: the visible band is the last eight strokes
  (the black pass eats strokes 0…48), i.e. slots **8…1**, so LOOPP's shift turns the band into a
  colour chase — eight COLTAB colours visible at once, three frames apart. Also corrected: MARQ's
  right edge is `RIGHT`/`RIGHT-1` (`DECA` before that `VLOW`), so the lit right column zigzags
  across `right-2`/`right-1`, not `right-1`/`right`. New `HighScoreTableLayout.FrameStrokeSlot` +
  `HighScoreFrameAnimation.DrawnStroke`/`ErasedStroke` replace the old outer/inner-RECT pair —
  a single band between two rectangles could only ever be one colour, which was the shape of the
  bug. `GameplayConstants.HighScoreFrameSlot` deleted. The old `DrawFrame` that refused to apply
  before compaction is now replaced by a per-stroke `DrawStroke`. +1 test (a band of
  `{8,7,6,5,4,3,2,1}`); pacing/geometry unchanged. **Cycle rate stands** (author: "seems OK, not
  a deal breaker").
- **2026-09-20 follow-up 3 — THE PAGE'S EXIT WAS BACKWARDS (notes §98.8).** Re-reading `TABLE`
  against the R5 after the wall work: `TAB888`'s 600-frame hold has **no switch check at all**
  (a press can never shorten the page), and `TAB999` then **leaves when the switches are CLEAR**
  (`BEQ $E059`) — a still-held switch *delays* the exit by one `$FF` count per four-frame check.
  The polarity is settled by `$3031`: `MOVE_PLAYER` indexes the movement table with PIA-A's four
  stick bits and entry 1 (bit 0 = "Move up") is the up delta, so a **set bit is a PRESSED
  switch**. The port had it inverted twice over: a press skipped the page, and an idle cabinet
  sat 32 s instead of 12 s. New `Hud/HighScorePageHold` + 4 tests (**326**). Also verified §98.7
  the hard way: a Python re-implementation of `FRAMER`/`MARQ` from the R5's byte writes diffed
  against a live screenshot — **0 pixels wrong in any of the four wall bands, no slot mis-coloured**.
- **2026-09-20 — CYCLOMATIC COMPLEXITY > 25 IS NOW A BUILD ERROR (notes §99, D-020).** Author:
  *"Can you make the cyclomatic complexity threshold of 25 a build error? CA1502 I think it is.
  I want well designed code."* CA1502 is off by default in .NET 10, and its threshold is not an
  editorconfig key: severity goes in `.editorconfig`, the 25 goes in `CodeMetricsConfig.txt`,
  and that file is registered as an `AdditionalFile` in `Directory.Build.props`. The wiring was
  verified both ways (threshold 60 = clean, 25 = the hits) so the file is proven to be READ.
  Two methods were over 25 and NEITHER was suppressed: `PlayField.ResolveCollisions` 46 → 2 (each
  ROM collision phase from plan 9.2 is now its own method, plus `KillPlayerOnContact<T>`) and
  `AttractObjectMachine.ReadOp` 34 → 8 (the 28-opcode ROM table split into six families behind a
  range dispatch). Bodies moved verbatim, comments and ROM citations intact; 326 tests and all
  six gates green.
- **2026-09-20 — THE BRAIN FACES THE HUMAN IT PROGRAMS (notes §100).** Author: *"When the brain is
  progging a human, the brain does not face the human it is progging!"* The ROM picks the brain's
  PICTURE with the placement — `BMUT00 LDD #BRLP1` (BRNAL frame 0, facing LEFT) when the human
  lands just left of the brain, `BMUT10 LDD #BRRP1` (BRNAR frame 0, RIGHT) when the XMIN case
  puts it 8px right, and `BMUT1 STD OPICT,X` stores it — so the pose holds for the whole 20
  iterations and shows through the `$BB` block (§72). `BeginReprogramming` did the placement but
  never set the facing, so the brain kept whatever the chase gave it. The four ABAC bases are now
  named (BRNAL/BRNAR/BRNAD/BRNAU, in `SpriteSet.BrainFrames` order) and set from the placement
  with `_frameStep = 0` (the direction's P1). +1 test (**327**); all six gates green.
- **Needs your eye:** the movie end to end (it is 96 s — start a build and wait 12 s, or
  press **F1** to jump straight in and hold **F3** to skim; **F2** goes straight to the demo
  game), the hero's walk, and whether the title screen should also move to the ROM's row 36
  (§96.3 flags it; that screen was left exactly as you signed it off).
- **PARKED by the author (2026-09-20, "not a deal breaker, it's something that can be fixed
  later"): the wall's pattern on its INNER parts** (notes §98.8's open item). The MARQ
  byte-diff cleared the interiors of all four bands (8 stripes, slots 8…1 inner→outer, 16 px,
  hatch phase included), so the next places to look are the **four corners** and the **inner
  boundary** (the erase's stroke 48 / the visible stroke 49) — neither was sampled — plus
  MARQ's `VLOW` 1-row overshoot past `LOWER`, which the port does not model. The definitive
  check needs a **photo or MAME capture of the arcade's own page**: diffing the port against
  the decode cannot catch a misreading the port and the decode share.
- **Known open (notes §96.8):** PDEAD's `PKPRCV` (the posts vanish rather than playing it),
  the POSTS table's unreferenced records, and FAMPAG's DUMPLR half — the arcade's title
  ALSO walks the "dumb player" family past the title (HELPME $8715) before HISTO starts;
  the script and the engine both exist, so that is wiring, not decode.

### Session 5 (2026-09-17, night) — THE TITLE SCREEN AND THE ATTRACT DEMO (notes §94)

- **"Ignore the gameplay, just get the SCOTTOTRON 2084 out and the demo in."**
  The arcade's attract is two things: the TITLE SCREEN and the FANCY ATTRACT
  MODE (the machine playing a real game by itself). Both are in now, and the
  machine cycles title → demo → (you press anything) → title.
- **The title screen is the ROM's.** $79C7/$79AF set the wall to $CC (slot 12),
  draw the wall + the player's lives + the score, then print string 128
  "ROBOTRON 2084" and string 129 in the LARGE font, colour $AA = slot 10.
  **The tagline is "SAVE THE LAST HUMAN FAMILY"** — the author remembered
  "PROTECT"; the ROM says SAVE (RRET.ASM:982 / RRSCRIPT.ASM:913), and the ROM
  wins. `TitleScreenState` draws the wall in `TitleWallSlot = 12`, a real 1P
  session's score and men, and the two strings centred via the new
  `SpriteSet.DrawLargeFontText`. The port's conventions stay: **1** = one
  player, **2** = two, the blinking prompt, the 5-second high-score swap.
  After `TitleIdleSeconds = 12` with nobody at the machine, the demo takes
  over (the arcade's attract runs while the cabinet sits idle).
- **The demo is the machine playing a REAL 1P game.** `AttractState` runs a
  genuine session driven by `DemoPlayerInputSource`, the port's phony player.
  **Labelled placeholder, deliberately:** the arcade's AI is the OS ROM's
  writer of the fake joystick/fire bytes ATRSW2/ATRSW3 — that ROM is
  disassembly-only and its writer is not decoded in `robomame.asm`, so we do
  not have the arcade's AI and we say so in the code. The stand-in: flee a
  robot within 60 spec px (steering clear of the walls), fire at a robot
  within 120, drift to the field's centre otherwise, and pause one tick in
  sixteen so it does not look robotic. Wave clears go through the real
  tunnel (`WaveClearState` gained an `attract` flag) and back to the demo;
  a death rebuilds the field for the next man; losing ALL men silently starts
  a NEW demo game — no high-score entry (the OS ROM just replays the demo);
  and any human START/FIRE press takes the machine back to the title.
- **Supporting changes:** the shared HUD left `PlayingState` for `Hud/ArcadeHud`
  (title + demo + play all use it); `PlayField.NearestLivingRobotPositionTo`
  (Manhattan — the ROM's GETHTG rule, notes §90) feeds the AI; `SpawnTank` now
  returns the tank. New gate `tools/verify-attract.py` (the sixth) checks the
  whole loop with PNG captures.
- **Gates as of this state:** 0 warnings, **278 tests, 0 failed, 1 skipped**
  (+8: 5 demo-AI, 3 nearest-robot), 12 s launch smoke OK, `verify-playfield.py`
  PASS, `verify-fonts.py` PASS, `verify-attract.py` PASS (title 4.4% lit →
  demo 3.0% lit after the 12 s idle → fire → title again).
  **Needs your eye:** the demo's judgement calls are the placeholder's, not the
  arcade's — how well it plays, and whether the title screen reads right.

### Session 4 (2026-09-17, evening) — the PROG's death and the SPHEROID's escape (notes §90/§91)

- **"THE SPHEROID, AFTER GIVING BIRTH TO ALL THE ENFORCERS, LOOKS WEIRD ANIMATION WISE" — the
  escape was strobing (notes §91), and *"Yeah that's it!"* confirms the fix.** The escape phase
  (`CIRC3`) advanced its picture on every
  **port tick** instead of once per `NAP 2` **body** — 3.6× the arcade's rate, a 15 Hz flicker
  through four growth frames as it ran for the exit. It also had the picture COUNT wrong: `CIRCLE`
  and `CIRC3L` both wrap at **`CIRP4`** (`CMPD #$1502 / BLS` — the `BLS` STORES `CIRP4`), so those
  phases cycle **five** pictures, `CIRP0..CIRP4`, not §56.2's four (the idle pulse never reached
  the medium ring — the art is one growth animation: dot → plus → small ring → ring-with-hole →
  medium ring → big ring → opening ring → fragments). `Spheroid` now runs the ROM's chain: one
  pointer, advanced one entry per body, with the phase's last picture deciding the wrap, the
  countdown (and the escape's X exit test, once per five-picture cycle rather than every frame) on
  the wrap pass, the drop phase continuing from `CIRP4` into `CIRP5` (the port restarted at the
  dot), a spheroid born on `CIRP4` (MPROB stores the `MKPROB` picture in `OPICT`), and the freeze
  gate only on the SPIN phase (the ROM gates `CIRCLE` and neither `CIRC2L` nor `CIRC3L` — a gate is
  per routine, §88). One side-effect to expect: the spin's rotation is 15 frames, not 12, so a
  spheroid's FIRST enforcer now arrives a quarter of a rotation later.
- **Also flagged, NOT changed — three items parked as A10/A11/A12 (notes §91.3, all three in the
  handoff's work table).** (**A10**) The generic mover runs **20% fast**: `OPB80` adds the velocity
  once per ROM **frame**, but the port adds it once per 60 Hz **tick**, so the spheroid, the sparks
  and the missiles travel 60/50 faster than the arcade — a systemic change across several entities,
  parked for your call. (**A11**) The spheroid's drop countdown re-arm: `CIRC2L`'s `BRA $11DB`
  re-arms `RMAx(CDPTIM/4)` and the `$11E5` chain then DECrements the fresh value in the SAME body, so
  the arcade's cadence is `RND(1..CDPTIM/4) − 1` wraps — and a countdown that reaches 0 becomes
  `$FF` (a ~2-minute stall). Needs a MAME measurement before it is touched. (**A12**) The escape's
  exit columns are the port's own mapping of `XMIN+3`/`XMAX-10` (the left measured from the wall,
  the right absolutely) and interact with A9 — also unmeasured.
- **"WHEN I SHOOT THE PROGS THE EXPLOSION EFFECT LOOKS WEIRD" — two defects, and the loud one
  was the ART (notes §90).** (1) **`ProgBurst.png` decoded to NOISE.** It is the port's only
  *inline* sprite (`PGXPIC` is not in the R5 ROM, so `tools/SpriteExtractor` carries the
  original source's `PGXD` bytes in the program), and nine of that array's sixteen rows had
  been written as EIGHT bytes instead of six — as if `FDB $AA00,$0000,$0AA0` were four 16-bit
  values. The writer reads exactly `w*h` = 96 bytes, so it walked a misaligned stream out of a
  114-byte array and emitted a plausible-looking PNG of slot-10/11 noise. Fixed from the
  listing and verified by decoding the PNG back to palette indices and diffing all 16 rows
  against `PGXD`; **the extractor now rejects inline data whose length is not exactly `w x h`**,
  which is the guard that would have caught it. (2) **The death was wrong in KIND.** The port
  put the prog in a `Dying` state and drew the whole 12×16 card as a 20-tick static "pop", but
  `PRGKIL` erases the seven trail images, swaps the object's **picture descriptor** to `PGXPIC`,
  clamps the corner and then calls the ordinary `EXST` (a RAM vector = `JMP EXSTV`) — so a prog
  dies in the **same direction-dispatched strip explosion as every other robot**, only fed the
  phony card instead of the human it was. `Prog` is now `IExplodable` (art = `ProgBurst`,
  `Kill()` instant, no `Dying` phase) and the new defaulted `IExplodable.ExplosionBounds` gives
  `EXSTV`'s rect — `UL = OBJX/OBJY` with the **picture's** W/H, i.e. the card's rect at the
  prog's corner rather than the card centred in the smaller human box. The prog kill also now
  plays the sound it was missing (`PGKSND` = `SoundTables.RobotDeath`).
- **The 468 baked font cycling variants are deleted (notes §92, handoff B9)** — the shader
  already receives the six live colours as uniforms, and a cycling-slot glyph is exactly
  "every non-transparent pixel of the white master becomes the slot's live colour", so
  `ColorCycle.fx` gained a pixel-only `GlyphCycle` pass (a new `SlotId` c7, 0-5 = slots
  10-15; unrolled if-chain — no arrays, notes §34). `FontLargeCycling`/`FontSmallCycling`
  are gone, `DrawGlyphSlot` drops its `cyclingGlyphs` parameter, and the M4-off fallback now
  tints the master with the slot's live palette colour (the old one drew the fixed marker,
  which never cycled). `extract-fonts.py` writes masters only and gate 5 checks the 78
  masters (was 546). The entity sprite markers are unchanged.
- **Gates as of this state:** 0 warnings (Debug + Release), **270 tests, 0 failed, 1 skipped**
  (new: `ShootingAProg_LeavesNoDyingPop_AndTheFieldShattersTheBurstCard`, plus
  `SpheroidAnimationTests` × 3), 12 s launch smoke OK, `tools/verify-playfield.py` PASS.
  **Needs your eye** — shoot a prog and watch a spheroid run out of enforcers.

### Session 3 (2026-09-17) — read this BEFORE the bullets below

- **EXPLOSIONS: "WHEN YOU SHOOT ENEMIES VERTICALLY AREN'T THEY SUPPOSED TO EXPLODE
  HORIZONTALLY? AND VICE VERSA?" — they are, and the two axes were swapped (notes §69).**
  Another of my decode errors (§61, inherited by §67): it read `EXSTV`'s first byte as the
  shot's *vertical* component, but it is the **X step** — so a shot with no X step is a
  **vertical** shot, and that is the branch that reaches the **HORIZONTAL** explosion. The
  disassembly agrees at `$5C1F`: a **vertical** shot builds its split from `$A6` (the
  collision's **column**) against the picture's **WIDTH**; a **horizontal** shot uses
  `$A7` (the **row**) against the HEIGHT. **So shoot up/down → the sprite is cut into
  COLUMNS flying apart left/right; shoot sideways → cut into ROWS flying apart up/down.**
  A diagonal is unchanged (§67's two halves leaning opposite ways). One line changed in
  `Explosion.Dispatch`.
- **THE REMAINING TIMING TRUNCATIONS ARE GONE — AND THE PORT NOW HAS EXACTLY ONE
  DELIBERATE DEVIATION FROM THE ARCADE (notes §70).** §65 left four "under 1-3%"
  approximations; they are now on the exact clock as well (the human's step, the
  enforcer's five grow pictures — which were changing ~6% early — the tank shell's life
  and the spark's life). The one thing that STAYS non-arcade is the **human walk: 16
  ROM frames a step instead of the Gospel's 8** (`RRH11`: `NAP 8,HUMAN` with the
  position committed every pass = one pixel every 8 frames). That is your round-8
  instruction ("the mommies are walking too fast"), not a decode shortcut, so I have
  left it — **say the word and it reverts to the arcade's 8 in one line.**
- **THE EXPLOSION IS ONE FAN THAT OPENS BOTH WAYS (notes §71).** Your report *"the
  vertical explosion only explodes upwards — it's meant to be up AND down at the same
  time"* was right again: §67's two-half split came from a **stale 1982 comment** (the
  `$473F` header mentions a `B` direction for a bottom half; the routine never reads
  `B`). The Gospel's own writer does `base = YCENT − step*YOF + step/2` and then marches
  its segments DOWN by `step` — the base climbs as the step grows, so **one fan opens up
  and down at once**. The diagonal case is unchanged in *direction* (rows + a lean) but
  the whole fan now leans the one way, twice as far as before.
- **YOUR NEXT TWO REPORTS, BOTH FIXED (notes §72).**
  1. *"When the brain progs a human, the brain sometimes turns into a square block!"* —
     the ROM's `DRAW_BRAIN_IN_PROGGING_STATE` ($1DAF) does **two** blits: it fills the
     brain's rectangle with colour `$BB` (slot 11) **and then draws the brain's own
     picture on top** (`JMP $D018`). §46/§47 read only the first blit, so the port drew
     the bare block. The block is a **backdrop** — the brain now appears as a slot-11
     card with the brain on it, as the arcade's does.
  2. *"the vertical explosion works sometimes, but doesn't last very long"* — the fan's
     **unit** was wrong for the vertical-shot engine: §71 counted it in byte columns
     (2 px), but `RRHX4`'s `WRITE` walks the picture's **pixels** (`ASLA` "DOUBLE FOR
     PIXEL WIDTH", `DECA`). At twice the arcade's rate the fan left the playfield by
     its third frame and showed as a sparse comb. It now spans `x21..x99` instead of
     `x0..x245` at the same moment, keeps 7-8 of its 8 strips on screen to the last
     frame, and its first frame reconstitutes the sprite exactly (`x50..x57`).
     **A measured check, not a guess:** a throwaway dump (deleted before commit) printed
     the visible strips for both engines at every split and position; the lifetime was
     re-derived too (`RRS22`'s `TIMER == 2` gate is ONE video field — `IRQV` counts
     twice per field — so 15 frames ≈ 0.30 s is right and unchanged).
- **THE EXPLOSION FAN IS MIRRORED NOW (notes §73) — your "not mirrored" report.**
  *"one side of the explosion is not mirrored on the other side … the half going UP is
  bigger than the half going DOWN, and … going LEFT is bigger than … RIGHT"*. The port was
  anchoring the fan at the **hit point**, and a laser only just reaches its target when it
  strikes, so the hit is always at the sprite's **near edge** — the up (or left) half got
  nearly all the strips and reached ~20 px while the other reached ~1 px. The ROM has the
  other path for exactly this case: **`NWCENT`** ("NO GOOD") uses the picture's **MIDDLE**,
  so the two halves are equal by construction. `Explosion.Start` now takes the middle for
  every kill. The zombie-strips at frame 0 still reconstitute the sprite exactly, and the
  **appears** always used this centre path (which is why a wave's materialisation never
  looked wrong). New tests pin it from both axes: `reachUp == reachDown`,
  `reachLeft == reachRight`.
- **…AND THE DIAGONAL FAN IS A MIRRORED CHEVRON NOW (notes §74) — your "still lop-sided".**
  §73 fixed the *reach*; what was left was the **lean**. The explosion has **three** engines
  and I had only ever read two: `RRX7` (rows), `RRHX4` (columns) and **`RRDX2` (diagonals)**.
  `RRDX2` places the strips from `XCENT − YOFF*XSIZE` and adds `XSIZE` per strip — so a
  strip's sideways shift is `(i − YOF) * XSIZE`, measured from the fan's **fixed point**, and
  the rows above it lean one way while the rows below lean the other. The port was shifting
  by `i * drift`, shearing the whole fan to one side; with the clip, the strips at one end
  left the playfield and were dropped, so half the burst vanished. **Straight shots are
  unchanged** (their slope is 0) and the frame-0 reconstruction is untouched.
- **THE FAN WAS BEING DRAWN AT HALF SIZE — and you confirmed the fix ("That worked!", notes §75).**
  `Explosion.Draw` divided the *texture* dimensions by `SpecScale`, but a texture is already in
  **art pixels** (`SpriteSet.CentredIn` is what scales it), so the fan spanned half the
  picture's rows and sampled every other one — while the anchor still came from the bounds, so
  `split` clamped to `extent−1`: five steps of reach above and **zero** below. Both §73 (the
  middle anchor) and §74 (the chevron) were right *in the model* and invisible on screen,
  because this unit error was discarding half the result before it was drawn. The fan now
  **draws the whole picture**, every row, centred where the art was — so expect explosions to
  look fuller than the last build.
- **HUMANS CAN NO LONGER SPAWN ON AN ELECTRODE (notes §77.1).** You were right — they were placed
  with a bare random point and no checks at all, and since a human won't step into a live
  electrode it was stuck there for the rest of the wave. Humans now use the same placement rule
  the electrodes and grunts get.
- **THE QUARKS' TANK DROP — I re-verified the whole algorithm and it matches the ROM** (the first
  countdown, the inter-drop roll, the re-roll after every drop, the 3-frame body, the wave's
  `TDPTIM` table and the 20-tank cap all line up). **One suspect left**, and it is the kind you
  reported: the ROM rolls the *number of tanks* from the wave's **enforcer** number (`ENFNUM`),
  while the port hands the quark the wave table's **tank** column — if that column is bigger, the
  quark drops proportionally more tanks. Notes §77.2 has the table and the next step.
- **Gates as of this state:** 0 warnings (Debug + Release), **247 tests (246 pass +
  1 skipped)**, 12 s launch smoke OK, `tools/verify-playfield.py` PASS, and the fifth gate
  `python tools/verify-fonts.py` PASS.
- **SOUND IS OFF BY DEFAULT (notes §68).** Author: *"The sound annoys me — can you disable
  it with a feature flag … So many beeps is annoying."* `Sound.Enabled` (in
  `Audio/Sound.cs`) is the master switch and now defaults to **false**; set it to `true`,
  or launch with `ROBOTRON2084_SOUND=1`, to hear it again. The stub beeper is the only
  sink we have (§36.2, blocked on you for the real note table), so nothing is lost — the
  sequencer, the ROM sound tables and their tests all still run.
- **AUTHOR PLAYTEST 2026-09-17: "the explosions are completely wrong and the grunts move
  fast too early" — both were my decode errors (notes §67).** The explosion is a
  **two-half SPLIT** at the collision (the disassembly's own header: the top half flies
  UP, the bottom half DOWN, each leaning its own way) — not the single downward fan §61
  built. The grunt speed-up's `×7/8` applies **only while it stays at or above the
  floor** (the port clamped down to the floor a kill early) and the ROM does **not**
  re-roll the survivors' pending countdowns (the port did, so every kill lurched the
  wave forward). Wave 1 now ramps 20→17→14→12→10 and stops.
- **THE PLAYER'S DEATH IS THE GOSPEL'S NOW (notes §66)** — it was a wall-clock 2-second
  placeholder drawing the player in its normal colours. RRX7's `PDTHV`: ten iterations of
  a SOLID-colour flash (`$99` = slot 9 for 2 frames, then a random `PDCTAB` colour — slots
  0/1/3/7 — for 6 frames), then the slot-12 fade where the DECAY colour process is killed
  off and `FF F6 AD A4 5B 52 09 00` is written into slot 12 a byte per 4 frames, ending
  with the player drawn black and erased. 129.6 port ticks. `DeathTimer.cs` is deleted; new
  `GamePalette.SuspendSlot`/`ResumeSlot` is the ROM's `KILL OFF DECAY`/`COLST`.
  **It cannot be seen yet: `PlayerInvincibleForTesting` is still ON.**
- **THE SHORT-DELAY SWEEP IS FINISHED (notes §65)** — the systemic `PortTicks()` truncation
  found in §52. The **grunt** body was running 20% fast (as were the prog and cruise
  missile, the spark's flicker and move interval, the brain's body and reprogram,
  the hulk's step, the electrode shrivel), and the **six palette colour processes**
  were counted in port ticks, so every colour cycle — the current player's score, the
  mini man, the posts, the wall — shimmered 20% too fast. All now run on ROM frames.
  **Expect the robots to feel slightly slower than the last build you played: that is
  the arcade's rate.**
- **SPHEROID + QUARK DEATH BURSTS (notes §64)** — they were the last enemies dying with the
  invented/generic strip explosion. Both use ONE ROM routine (the quark's `CIRKV` is
  `$1143: JMP $12FA`, i.e. the spheroid's `CIRKP` entered past its setup): the enemy's own
  pictures play as SOLID SILHOUETTES (spheroid in `$AA` = slot 10, the player-score slot;
  quark in `$DD` = slot 13) and then its "1000" picture appears down-right of the death
  spot. `P1KD` turned out to be a ROM constant pointing at the very "1000" art the port
  already ships for the rescue marker.
- **WAVE-START MATERIALISATION + the per-wave WALL/LASER-COLLIDE colours (notes §62/§63)**
  — while the author was away. (a) The robots no longer pop in: RRG23's `APPEAR` makes ONE
  appear record per FRAME, every FOURTH one with the horizontal column fan, anchored on the
  robot's own centre, while the robots are held OFF (`ROBOFF`) for the whole sequence — so
  an assembling robot neither acts nor draws itself until its chain converges. (b) `GTWCOL`
  has FOUR per-wave tables, not the two the port knew: `WALCOL` is the BORDER colour (the
  wave-1 border is ORANGE, not the port's cyan; wave 9's is BLACK), and `LASCOL` — RRF.ASM's
  "LASER WALL COLLIDE COLOR" — is the FLARE where a laser runs off the field (`LASDIE`),
  which the port never drew. Both wrap every 10 waves. Verified on screen.
- **EXPLOSIONS ARE THE GOSPEL'S NOW (notes §61, `e72b424`)** — the biggest item on the
  author's list, and the port had been wrong in KIND: one symmetric fan for every kill.
  RRX7's `EXSTV` is the dispatch: a diagonal shot fans the sprite's ROWS at 45 degrees
  (RRDX2), a pure vertical shot the ROWS with no lean (RRX7), a pure horizontal shot its
  COLUMNS (RRHX4); a non-laser kill is a row fan. The fan is anchored at the IMPACT (the
  laser/robot overlap centre — the ROM's `CENTMP`), the step is the ROM's own 16-bit
  accumulator (`$100` a frame: 15 draws, steps 2..16; the APPEAR runs backwards from
  `$1000` and converges), the pool is TEN records shared by explosions and appears, and
  strips outside the playfield are DROPPED, keeping the sprite's bottom rows.
  **§35.3 CORRECTED:** the player death is NOT a strip cascade — `PDEATH` is RRX7's
  `PDTHV` (the player drawn solid in $99/PDCTAB colours, then the DECAY process killed
  and the fade table written to slot 12) — so **that fade is the §33 death-fade
  mechanism**, still open with the author. Next explosion item: the wave-start APPEAR.
- **D-019: NO operator/service subsystem** (author: *"we do not need the service test or
  coin tests for this game ... we don't need self test code or coin counting etc."*) — no
  self-test/adjustment screens, no ROM/RAM/colour-RAM tests, no CMOS settings, no
  bookkeeping totals, no coin/free-play handling. START 1 / START 2 stay (they pick 1 or
  2 players). In `ledger.md` and in the DO-NOT-MODEL list above.
- **THE HUD IS NOW THE ARCADE'S (notes §58)** — the author asked to start here ("the
  score and lives display ... are not correct"). Decoded end to end from
  `DRAW_PLAYER_SCORES` ($DC13), `DRAW_LIVES_REMAINING` ($34E0) and the `$6291` string
  table: score at P1 col 21 / P2 col 85 on **row 14**, seven large-font digits with the
  ROM's leading-zero blanking (6 px blank vs a 7 px glyph) and the **last two digits
  always drawn** (a fresh game shows "00"); the CURRENT player's score in **slot 10 — a
  cycling slot** and an idle player's in slot 1; the spare men as the **6x8 mini man
  picture** (`MNPIC`, $3592/$3596) 8 px apart beside the score, capped at 7, with its own
  palette slots (its body is slot 11, so it shimmers); and the wave indicator at the
  **bottom** of the screen as "<n>  WAVE" (string 104). The old top-right "LEVEL n" is
  gone — in a 2P game that space belongs to player 2. **The port's previous layout
  (left-anchored score, full-size player sprites as life icons at the bottom-left) was
  never the arcade's.**
- **TWO-PLAYER MODE (notes §59)**: `1` starts a one-player game, `2` a two-player game
  (the arcade's START 1 / START 2; fire is kept as a 1P alias). `Level/GameSession` +
  `Level/PlayerSlot` hold each player's score, lives, wave and rescues, and the turn
  passes on a death while the other player has men (ROM `PLE1B`), with the ROM's
  "PLAYER n GAME OVER" pause and the "PLAYER n" turn announcement; a wave clear keeps
  the SAME player (the ROM never touches CURPLR there); both scores are offered to the
  high-score table at the end. `PlayerSlot.Input` is per-slot deliberately, so the
  SIMULTANEOUS mode the author wants later is a matter of plugging in a second input
  source and giving each player a field.
  **Caveat: the turn-passing paths cannot be playtested while
  `PlayerInvincibleForTesting` is ON** (a life can never end) — that switch is still the
  author-gated item, and the turn rules are covered by `GameSessionTests` until then.
- **FONT BUG FOUND AND FIXED (notes §60)** — the author looked at the new score digits
  and said "the font characters don't look correct". Every committed glyph PNG had the
  two pixels of each byte **swapped**: `tools/extract-fonts.py` wrote the LOW nibble to
  the LEFT pixel. All 546 PNGs regenerated, `tools/SpriteExtractor` now skips font
  glyphs entirely, and `tools/verify-fonts.py` is the new guard (it re-decodes the ROM
  and compares every pixel of all 78 glyphs + 6 cycling variants each).
- **Open with the author:** playtest the new HUD (1P and 2P), the 2P turn flow (needs
  `PlayerInvincibleForTesting` OFF), and the font shapes.

### Session 2 (2026-09-16) — read this BEFORE the bullets below

- **Latest CODE commit `391befb` (with docs `f74691b` and this file's own commit on top).** Gates green: 0 warnings, **201 tests (200 pass + 1
  skipped)**, 12 s launch smoke OK, `tools/verify-playfield.py` PASS (~3% lit).
- **`docs/handoff-2026-09-16.md` was the read-first doc at the time** — the section-4
  addendum lists today's commits, section 5 has three new work items (B0 PortTicks
  sweep, B0b blocked tank art, B0c the open quark question) and section 7 has the three
  traps that cost time today. `docs/handoff-2026-09-13.md` is historical only.
- **Done since the bullets below were written** (all in `docs/arcade-fidelity-notes.md`
  §45–§52): electrode/post types are wave-dependent (§45); brains, progs and cruise
  missiles re-derived from the Gospel (§46); the blitter's COLOUR/REMAP modes decoded and
  a pixel shader built for the progs and posts (§47); the brain's BMUT reprogram
  animation implemented (§48); progs and cruise missiles are BLITTER objects, not
  sprites (§49); **every** enemy kill vector mapped — no enemy blinks when it dies, and
  "flashing X" in the spec means PALETTE CYCLING, not a visibility toggle, so five
  invented blink constants were deleted (§50); the quark's motion re-derived as a
  random-speed drift (§51, corrected in §52); the quark's body cadence was 17% fast and
  tanks now play their ROM **birth** sequence (§52). The tank's birth ART was then FOUND
  as well — "cannot be extracted" was a PARSER verdict (Pass B cannot read 6-byte
  animated descriptors), not missing data (§52, `99f5c00`). Finally the **spheroid and
  enforcer MOTION models are now the Gospel's** (§56): the spheroid accumulates a random
  acceleration and damps it by a 64th per body (so its speed is emergent), clamps at the
  walls instead of reflecting, and leaves by a sideways run at exactly 2 px/frame with no
  burst; the enforcer uses ENFNV's proportional approach (`v = 2 x offset`, so it crawls
  as it arrives) with both countdowns in BODIES and a 45-frame grow-up. Neither robot had
  a speed constant in the ROM, so `SpheroidSpeed` and the enforcer's step are deleted.
  **Then the PROG TRAIL was fixed from two more author reports (§57) — and the real
  fault was a RENDERING bug, not a decode.** `SpriteSet` binds its pixel shader with a
  bare `Passes[0].Apply()`, which is DEVICE state: with `SpriteSortMode.Immediate` the
  binding outlives the draw that made it, so the ghost card fill ran through
  `SolidRemap` in the previous silhouette's colour — black. `UsePassThrough()` now
  rebinds the pass-through pass before/after every non-remap draw. **The same leak is
  the cause of the earlier "the human that has been progged is solid black" report**
  (same helper): §53 was right that the colour pairs are correct, the renderer was
  wrong. The trail is also SEVEN ghosts now (was six — `PD` = 7 with `SPSIZE` = 31 puts
  the ring at byte offsets 17..29), each frozen in the pose it was born with.
- **Open with the author:** the quark's perceived speed (the port matches the Gospel and
  the unit is proven five ways — **do not re-litigate without new evidence**, §52); the
  spheroid's and enforcer's motion models (§56) and the fixed prog trail (§57 — worth
  also checking a brain reprogramming a human, which uses the same draw path); movement
  models can only be judged by PLAYING, never by a screenshot;
  `PlayerInvincibleForTesting`
  is still ON.

- **The 12-phase spec.txt rebuild: COMPLETE** (2026-09-05) — the game builds, runs,
  and passes its gates.
- **Arcade-fidelity ROM-port follow-up: COMPLETE through playtest round 13** as of the
  bullets below, which predate the session-2 block above (newest commit `74a5c3f`). Every entity's behaviour/tempo/spawn rule/collision/score is
  implemented and ROM-verified where the author flagged it; rounds logged in
  `docs/arcade-fidelity-notes.md` (progress (11)–§31).
- **Gates green: build 0 warnings; 201 tests (200 pass + 1 skipped); launch smoke OK;**
  **playfield render check OK (`tools/verify-playfield.py` — new §40 guard).**
- **`GameplayConstants.PlayerInvincibleForTesting` is STILL ON (temporary)** — flip it
  off when the author confirms gameplay, then un-skip the contact test (Next steps #2).
- Frills: spark flicker DONE (614d36a); **explosions (RRX7) DONE (§35)** — the dying
  sprite shatters into 16 horizontal strips that fan out over 16 ticks, biased by the
  killing laser's direction (ROM: 7 × 242-byte slots, $BA per-strip offset grows per
  frame; see notes §35 — **fan MAGNITUDE "fixed" 2026-09-16 (`a070290`) but that
  model was then RETRACTED — see notes §35.1-§35.5.** The authoritative decode is
  §35.5 (Gospel: `ref/original-source/RRDX2.ASM`, `RDXORG $4680`): the draw loop's
  `ADDD XSIZE` reads the 16-bit word `(XSIZE, YSIZE_high)`, so one step advances
  BOTH axes — a deliberate 45° diagonal, not the straight-down fan the port draws.
  `EXSTZ` = start an explosion (rows diverge), `APSTZ` = start an appear (rows
  converge) — so `$46E6` is an APPEAR and the player-death cascade is ~12 APPEAR
  records, not explosions. Straight shots use RRX7's struct (no XSIZE) = a
  vertical row-spread. The fan is impact-anchored and rows are DROPPED by four
  clip passes (YMAX 234, YMIN 24, XMAX $8F, XMIN 7). Work order in
  `docs/handoff-2026-09-16.md` B1); colour-cycle shader BUILT, wired & TICKED (M4 — unrolled .fx, ps_3_0, six
  named Live10..15 uniforms; author go-ahead 2026-09-16) and the "only the wall
  renders" regression it introduced is **FIXED (notes §40)**: the effect had
  declared its own vertex shader, which replaced SpriteBatch's for the rest of
  the batch and performed no projection, so every entity (and even the
  effect-less HUD text and life icons) fell outside the clip volume. The pass
  is now **pixel-shader only**, which leaves SpriteBatch's own sprite VS bound
  and preserves pixel-perfect rendering. **Verified on screen** via
  `tools/screenshot-map.py` + a driven SPACE press: entities, electrodes, the
  player, "LEVEL 1" and the life icons all draw, in live (cycling) palette
  colours. **If a custom VS is ever needed, the draw must move to
  `SpriteBatch.Begin(effect: ...)`.** A playtest by the author is the one
  outstanding check; font glyphs drawn
  in cycling slots use marker-baked variants through the same effect —
  blitter-remap semantics (notes §39);
  red screen BLOCKED on author's answer to the §33 question (which mechanism:
  full-screen flash vs per-wave wall slot vs slot-12 death fade). Frills session 2
  (notes §36) researched the remaining frills: **score font — UNBLOCKED & DONE
  (notes §38)** (the author's sprite-editor offsets pin the glyph ROM source:
  large font @0xEC93, small @0xEA2B, same 4bpp format as sprites; all 78 glyph
  PNGs + 468 cycling-slot marker variants regenerated from the ROM and the
  HUD now draws the real arcade score font — large digits, 7-px advance,
  leading zeros suppressed, slot-1 blue; cycling variants for the demo-screen
  text, notes §39)
  and **sounds**
  (single-voice priority sequencer, $D3C7; 29 call sites decoded; the
  note→frequency map is sound-board hardware, not in the CPU ROM). Optional
  checks done: hulk tempo (already ROM-derived, no action), tank shells +
  electrode placement (documented; shell-speed + electrode-model questions
  parked to author). Remaining unblocked: marquee (needs author asset).
- **Sound engine DONE with flagged stub (notes §37)**: `Audio/SoundEngine.cs`
  (line-for-line port of the $D3C7/$D3E0/$D3B6 sequencer — priority
  preemption, (dur,len,note) entries, one tick ≈ one vblank),
  `Audio/SoundTables.cs` (every decoded ROM table with its address),
  `Audio/MonoGameSoundSink.cs` (8-bit square waves; **STUB scale** 110×2^(n/12)
  Hz — the real note table is sound-board hardware → BLOCKED on author), wired
  into laser fire / robot death / player death / shell fire / shell bounce /
  brain spawn / bonus life. 171 tests (170 pass + 1 skipped).

## ROM folder locations
- **Source (verified blue-label set):** `E:\roms\MAME ALL ROMS - DO NOT DELETE\robotron\`
  - 12 game ROMs (4KB each): `2084_rom_1b_3005-13.e4` … `2084_rom_12b_3005-24.e7`
    (label = ROM number + board socket: .e4/.c4/.a4 = board positions E4/C4/A4, etc.)
  - 2 decoder PROMs (512B): `decoder_rom_4.3g` (universal horizontal, P/N A-5342-09694),
    `decoder_rom_6.3c` (universal vertical, P/N A-5342-09821)
  - Subsets: `robotron12/`, `robotrontd/`, `robotronun/` (clone ROMs — do not use for our build)
  - **All 12 game ROMs' CRC32+SHA1 verified against MAME master (2026-08-30) → confirmed solid blue label.**
- **Repo (byte-level extraction authority):** `ref/rom/robotron64k.bin` — 64KB image, 12 ROMs at their
  CPU offsets, $09000-$0CFFF (CPU $9000-$CFFF) zeroed. In the working tree (gitignored —
  `ref/` is reference-only per D-1, author directive 2026-09-05; NOT committed, deliberately).
- **Original source:** `ref/original-source/` (28 .ASM modules @ da11ac0).
- **Disassembly:** `asm/robomame.asm` (user's annotated 64K listing; 34-byte delta vs verified ROM — see live thread).

## Memory layout (REFERENCE for extraction — NOT to be emulated)
Main-CPU 64K address space (MAME `williams_b1` → `main_map_blitter`; user's RRF.ASM equates agree):

| CPU range | Contents | Port relevance |
|---|---|---|
| $0000–$8FFF | ROM1–9 (36K) when switch=ROM; **48K RAM (video + main) when switch=RAM** | read code/data only |
| $9000–$BFFF | RAM (always) — game state; base-page RAM $9800 | RAM layout docs (disasm lists it) |
| $C000–$C00F | palette RAM (CRAM), 16×4-bit, write-only | palette concept (see screen layout) |
| $C804–$C807 | PIA-A (user: PIA2) — keyboard/panel | ignore (input in MonoGame) |
| $C80C–$C80F | PIA-B (user: PIA0) — panel; port B $C80E = sound tokens | ignore (D-003) |
| $C900 | ROM/RAM select (RWCNTL) | ignore |
| $CA00–$CA07 | blitter (DMACTL/CON/ORG/DES/SIZ) | ignore |
| $CB00 | vertical beam counter (read) | ignore (D-015) |
| $CBFF | watchdog (write) | ignore |
| $CC00–$CFFF | CMOS battery-backed NVRAM (BCD, 4-bit writes, checksum $CC8C) | **functionality: persisted credits/settings** |
| $D000–$FFFF | ROM10–12 (12K), always ROM | read data only |

- RAM model: MAME uses one 48K block (`m_videoram`) at $0000–$BFFF. Layout (user-confirmed):
  - **$0000–$97FF: screen RAM (38K)** — 304×256 pair-major 4bpp = 2 px/byte, **256 bytes per
    2-pixel column**, 152 columns = **exactly 38,912 B (38K)**
  - **$9800+: game state** (palette copy $9800–$980F, entity lists, credits, …)
- Known RAM addresses (from disasm annotations — resolve against DP=$98 where `_d`): base-page RAM
  $9800–$980F (palette copy), entity list pointers $9811–$9823, task list $9815, task alloc $D1E3,
  credits $984F–$9851, player object ~$985A, `num_players` $9840 (direct $40).
- **Sound CPU (separate 64K — out of scope):** MC6808 @ 3.579545 MHz (÷4 → ≈894.886 kHz),
  `video_sound_rom_3_std_767.ic12` @ $F000 (4K).
- **PROMs (separate 4K — not needed):** the two decoder PROMs above.
- **Clocks:** master 12 MHz; 6809E = 12 MHz / 3 / 4 = **1.0 MHz**. Game logic tick = 50 fps (user RE).
  MAME raster: 8 MHz pixel clock, 512×260, visible 6..298 × 7..247 — reference only.

## Screen layout (game functionality — DO model the look)
- **304 × 256 pixels, 16 colours, 4 bits/pixel** (user's hardware RE — authoritative, D-004;
  corrected from 255 on 2026-08-30).
  MAME's raw geometry (293×241 visible in 512×260) is an emulator approximation — ignore.
- The **16-colour palette is a game feature** — keep it. The 4bpp storage format is not:
  the port can render into a 304×256 texture using the 16-colour palette (SpriteBatch/tiles),
  no pair-major packing (D-005).
- **Palette chain (RESOLVED — full details in `ref/palette-notes.md`):** pixel nibble → ROM default
  16-colour palette at **$DA51** (`00 07 17 C7 1F 3F 38 C0 A4 FF 38 17 CC 81 81 07`, byte-verified
  against the ripper's "Robotron @da51" table) → copied to RAM **$9800** at startup (code $D795)
  → $9800 drives the colour hardware ($C000) → **analog resistor network** (NOT the decoder PROMs):
  8-bit value = 3-bit R (1200/560/330Ω) + 3-bit G (same) + 2-bit B (560/330Ω) → monitor. MAME models
  it in `williams_state::palette_init` (`src/mame/williams/williams_v.cpp` @340). **T-008:** port that
  → 256-entry base palette constants; 16-slot RAM palette (mutable — the game rewrites it for
  colour-cycling/red-screen effects).
- Screen RAM: **$0000–$97FF** (38K, user-confirmed) — pair-major: each byte = 2 px side-by-side,
  **256-byte vertical 2-px columns, 152 columns**; incrementing the 256-byte page bit (bit 8 of the
  address) moves the view one column (2 px) right — user-confirmed 2026-08-30. Historical context
  only (port renders a 304×256 texture, no pair packing, D-005).
- Beam/vertical counter: not modelled (D-015).
- 50 fps pacing; original per-frame speeds → px/s via ×50 (D-012, `Speeds.cs`).
- Player object coordinate ranges: **x 7..140, y 24..223** (object coords → pixels still open, Q-001).
- Text: message strings live in ROM (e.g. "…IN YOUR PATH" @ ~$83A0), phrases via RRET.ASM table,
  text colour is a script action; font glyphs in ROM (big ASCII font).

## The verified ROM + live thread (34-byte discrepancy)
- Cross-check of `asm/robomame.asm` (46,160 uniquely listed ROM bytes) vs verified image:
  **34 mismatches in 8 small local regions — 99.93% match** → the disassembly targets the blue label.
- Pattern: local byte-shifts (disasm byte N = ROM byte N±k in a few-byte window), e.g.
  0x7904–0x7907 ROM `8E 79 75 10` vs disasm `10 8E 79 75`.
- **Regions:** `115B-115D, 135F, 2C4A, 41F0-41F3, 4D1C-4D1F, 7904-7907, 8392+83A1-83AD, 8411, E316-E317`
- **Rule:** use the verified ROM image as byte-level authority; near those 8 regions re-derive
  addresses from the image (annotations may be off by 1–4 bytes).
- Open question for the user: *hand edits in the disasm, or a dump quirk?* (one question, not a blocker)

## Disassembly coverage (extraction aid)
- ROM: 46,160 bytes listed; unlisted ~2.9KB (read from image):
  `07C7-07D4, 1174-117C, 1FCC-1FFB, 28DA-28FD, 305C-3070, 3237-3278, 382B-3835, 41E6-41EF,
   41F4-420D, 4CF1-4D0F, 5CF9-5D47, 6395-6934(1440B), 6F67-6F98, 6FD5-7088, 7A08-7A1B,
   7B5A-7B6F, 7B72-7B83, 7B86-7B8F, 80F0-80FD, 8870-88BE, D4A8-D4FB, DE8A-DEDD, E194-E1E2,
   E46C-E589, F7F0-F887, FFD7-FFEF`
- $9000–$CFFF (CPU): fully listed in the disasm (16,384/16,384) = RAM layout documentation
  (initial values + semantics).

## Facts settled this session
- **DP register = $98** for main game code (base-page RAM at $9800, `SETDP RAM>>8` in RRF.ASM).
  Disasm header `_d` EQUs are **direct-page offsets, not absolute addresses**
  (`credits_d $0051` → $9851, `num_players_d $0040` → $9840). Port does not model DP.
- **Palette:** $DA51 ROM default → $9800 RAM (runtime source) — user-confirmed + code-verified.
  Full colour construction: pixel nibble → 16-entry RAM palette (8-bit values) → 256-entry
  **resistor-network** palette (3/3/2 bits: R/G taps 1200/560/330Ω, B taps 560/330Ω) → analog RGB.
  The 7641-5 decoder PROMs are decoder timing, **not** colour. $DA51 bytes verified byte-identical to
  the ripper's built-in "Robotron @da51" palette. → **`ref/palette-notes.md`** (MAME code, ripper
  snippets, the 16 default colours with approx RGB, port plan).
- **Screen = 304 × 256** (user-confirmed 2026-08-30, correcting the earlier 255). Explains the
  pair-major layout: each 2-px column is exactly a 256-byte block, so carry into bit 8 of the address
  moves a pixel column along.
- **Sprite storage format (T-007) — VERIFIED:** entry = 4 bytes **[width][height][ptr_hi][ptr_lo]**
  (width = bytes/row, 2 px/byte; height = pixels; pointer big-endian to first frame data). Entries
  sit in tables of consecutive 4-byte entries; frames contiguous, pitch = w×h bytes. All 17 known
  entries + every frame pitch verified against the ROM image (skull 12×11 @$043B, 5-frame 12×5
  table @$0485, mommies 8×14, daddies 10×13, mikeys 6×11, …) → ALL OK. → `ref/palette-notes.md`.
- **Credits:** runtime `total_credits` = **$9851** (direct $51); **persisted in CMOS $CD00** (BCD,
  2 bytes, byte 0 = high nibble) — user-confirmed + code-verified ($2729). Also `paid_credits` $CD14,
  `credits_played` $CD2C, `bonus_credit_counter` $984F, `units_required_for_credit_counter` $9850;
  unit settings CMOS: $CC0C (units for credit), $CC0E (units for bonus credit), $CC10 (min units for
  any credit) — all BCD.
- **Waves (ref/robowaves.md, seanriddle.com):** wave setup routine **$2B7C** (unique data for waves
  1–40, then repeats 21–40); enemy counts table **$2E24** (Grunts 1–40, then Electrodes, Mommies, …);
  ~12 other params table **$2C12** (9 decrease with wave, 3 increase; modified by difficulty).
  Wave = 8-bit, wraps after 255 (shown 55); display is 2 decimal digits (100 shown as 0).
  Full 40-wave count table captured in the repo.
- **Partial-blit fx (deferred, D-015):** explosions ≈1 row/frame reveal, 16-frame life (→ 50 rows/s,
  0.32 s, fire-and-forget); appearances ≈½ row/frame (→ 25 rows/s, tracks its object). Mechanism
  details in plan §2b. First release: instant sprites; Phase 3: `RevealEffect` (SpriteBatch
  source-rect clip).
- **MAME set = blue label:** `robotron` = Release 5 solid blue (verified). Other sets: `robotronyo`
  (R4 yellow), `robotronr3` (R3 censored proto), `robotron87` (1987 "shot in the corner" fix, R6),
  `robotron12` (2012 "wave 201 start" hack), `robotrontd` (2015 tie-die V2). Same 3005-13..24 labels
  exist on yellow/red with **different data** — do not mix (see ref/mame-notes.md).

## File map
```
C:/Users/scott/source/repos/Robotron2084/        (git repo, branch main)
  plan.md            original rebuild plan (phases, mechanism mapping, §2b partial blits)
  ledger.md          original RE decision log (D-001…D-018, Q-001…Q-005, T-001…T-032) — historical
  rebuild-ledger.md  rebuild + arcade-fidelity checkpoint log (every commit, gates, next step)
  status.md          ← this file
  docs/handoff-2026-09-13.md      current session handoff (state, playtest checklists, constants)
  docs/arcade-fidelity-notes.md   master ROM-vs-port research log (resume: after §31)
  .gitignore
  asm/robomame.asm   user's annotated 64K disassembly (blue label; 34-byte delta — see above)
  ref/original-source/   28 .ASM modules — primary behaviour reference
  ref/mame-notes.md      MAME driver notes (versions, ROM layout + hashes, memory map, clocks)
  ref/robowaves.md       40-wave enemy tables + $2B7C/$2E24/$2C12 notes (seanriddle.com)
  ref/palette-notes.md   colour chain (resistor network), MAME palette_init, 16 default colours,
                         sprite storage format (4-byte header, Linear layout)
  ref/rom/robotron64k.bin  64KB verified image — byte-level authority (gitignored per D-1)
  spec.txt           BANNED (D-007)

E:\roms\MAME ALL ROMS - DO NOT DELETE\robotron    verified blue-label ROMs + decoder PROMs (source)

C:\Users\scott\source\repos\WmsGfxSpriteRipper    Sean Riddle's Williams sprite ripper (MFC C++);
    Form1.h @787 built-in palettes ("Robotron @da51" verified vs ROM), @871-901 4-bit RGB conversion

/tmp/romtool/        scratch C# tool (hash verify, 64K build, disasm cross-check, wave parse)
                     (/tmp = C:/Users/scott/AppData/Local/Temp)
/tmp/williams.cpp    MAME driver scratch copy (mamedev/mame @ master)
```

- **THE TUNNEL'S BARS ARE ITS PALETTE — the ROM's colour-cycling RAMP (notes §86).**
  Author: *"Its not the colours at fault, its the size of the bars making up the tunnel -
  they looked a bit slim."* **Measured, not eyeballed:** the arcade reference's centre column
  is **40 bright / 39 dark runs — 4.32 ROWS bright against 0.86 dark** (bars of five rows with a
  one-row seam, three to a colour cycle); the port drew fifteen ONE-ROW stripes with no dark runs
  at all. The ring geometry was not at fault (§85's one-ROM-pixel edges and tiling rows stand) —
  **the walk leaves one palette slot per ROW**, so the bar structure is the PALETTE's, and the
  arcade's slots hold a RAMP with **three of the fifteen blacked out**. The tables are in the
  walker's own ROM block ($576D: twelve ramp descriptors + data): `$59B1` picks a ramp and a
  window at random, `$59D0` writes fifteen consecutive values into slots 1-15, blacks `1+A`,
  `6+A`, `11+A` with `A` walking 4,3,2,1,0, and slides the window one value a pass. New
  `Rendering/TunnelPalette.cs` (generated from the ROM's tables) + `WaveClearState` wiring
  (colour processes suspended while the ramp owns the palette, `CRTAB` restored after). **The port
  now measures 38 bright / 38 dark runs at 3.96 / 1.57 rows — the arcade's bar structure.**
  **§85.2's "just a colour-shifted photo" verdict is CORRECTED:** it compared only the bright
  pixels' hue mix and threw away the run structure that carried the answer.

- **THE PROG'S STEP IS 2 COLUMNS, AND THE QUARK'S FIRST TANK COUNTS ANIMATION CYCLES
  (notes §87).** Author: *"Fix the prog movement, the quark birth speed."* (a) `PRGAL/PRGAR`'s
  ±2 delta is added to `OX16` — a `column*256 + row` SCREEN ADDRESS — so it moves two COLUMNS
  (4 px) a body, and the vertical tables' ±4 ROWS give the same 4 px: the port stepped 2 spec px
  (half speed, the reported "horizontal movement seems a little slow") and read the column as a
  pixel. The aim offsets and wrap margin are columns too (±28 columns = ±56 px). (b) The quark's
  `DEC PD2,U` sits on the WRAP branch of its idle animation, so the first tank is due 1..TDPTIM
  cycles of SIX BODIES: measured, the first drop went from **0-47 ticks to 139-379 ticks** while
  the drop-phase cadence was already right. §77.2's "tank column" suspicion is cleared.
  **Flagged:** RRS22's SLEEP dispatcher (`DEC PTIME,U / BNE`) makes `NAP n` wake every n passes,
  contradicting §83's two-frames-per-pass for the tunnel; one unit is wrong for every
  task-driven body and it needs the same MAME measurement.

- **THE FAMILY WALKS FROM THE FIRST MOMENT OF A WAVE, AND THEY STAY OFF THE POSTS
  (notes §88).** Author: *"At the start of the level, the family members wait too long before
  they start moving. Also can you check if they should spawn on top of electrodes? I don't think
  they should."* The ROM creates the family BEFORE the robots assemble, writes the stagger
  (`SEED&7+1`) straight into the process timer so the wake-up IS the first step, and `HUMAN` is
  the one robot routine with **no STATUS check** — the port held them behind `RobotsFrozen` (the
  2 s grace) plus an extra step period (~2.4 s of standing). Fixed, with the corollary that no
  brain or hulk may touch a human while the robots are held (the ROM's collision process is
  created immediately before `CLR STATUS`). The electrodes: the ROM's own placement is a bare
  `RANDXY`, so the arcade CAN leave a human stuck inside a post — the port's rule against it is a
  **deliberate deviation at the author's request** (the second, after the human walk period), now
  documented as one and made airtight: the last-resort placement scan used to stride by the whole
  entity size and then fall through to an UNCHECKED random point. Two new tests cover six levels
  × 120 seeds.

- **SESSION 3 WRAP-UP — read this first (notes §89).** The day's traps in the order they bit
  (a header is a lead not evidence; a hit is not a centre; textures are ART px and bounds are
  PORT px; an effect owns its palette and tables; an accumulating effect must accumulate; map
  geometry onto the real 640x400 screen; thickness is mapping; compare reference STRUCTURE not
  hue mix; a ROM coordinate's X byte is a COLUMN; a countdown can be gated on an animation
  wrap; a gate is per ROUTINE; an unchecked fallback breaks its caller), **the port's exactly
  TWO deliberate deviations** (the human walk period 16 vs `NAP 8`; humans never spawning on an
  electrode), and the open questions (the task-pass unit — needs a MAME measurement; the scoring
  report's laser-hit question; A8 the progging brain's pose; §72.6's `2W−1`; the tunnel's
  `PassFifths`). End-of-session gates: 0 warnings both configurations, **266 tests, 0 failed,
  1 skipped**, smoke OK, both render gates PASS.

## Next steps (priority order, 2026-09-17)

0. **Session-3 carry-over — what is actually left (the rest of this list is history):**
   the ONE open algorithmic question is **what a task pass is worth** (notes §87.4/A9: `NAP n`
   wakes on the n-th pass per RRS22's `SLEEP`, but §83 needed two frames per pass for the
   tunnel's ~2 s — it decides the body length of every task-driven object and needs a MAME
   measurement, not a guess). Then **simultaneous two-player** (the author's stated wish;
   `PlayerSlot.Input` is already per-slot and nothing but `SwitchToPlayerWithMen` assumes
   alternation). **DONE since this list was written:** the spheroid bubble burst + the quark's
   `CIRKV` burst (§64), the whole wave-complete tunnel (§79-§86), the prog's step and the
   quark's drop timer (§87), the family's start of wave (§88), the prog's death — the
   `PGXPIC` + `EXST` strip explosion plus the corrupt `ProgBurst` art (§90) — and the
   spheroid's picture chain (§91) — and **A10, DONE (notes §93)**: the five generic-mover
   entities (spheroid, enforcer, quark, spark, tank shell) now integrate their velocity once
   per ROM FRAME on a sixths accumulator with raw per-frame velocities; the sweep also caught
   ENFNV's `ASLB/ROLA` as a signed HALVE (the enforcer had been 4x fast) and measured the
   spheroid's escape at the ROM's `CIRC3` `$100` = 0.25 col/frame (the "±1 column/frame"
   wording in the §91 bullet above is superseded) — and **the title screen +
   attract demo, DONE (notes §94, author's priority this session)**: the ROM's
   title ("ROBOTRON 2084" / "SAVE THE LAST HUMAN FAMILY" over the $CC wall with
   the score and men) and a real 1P game the machine plays itself after 12 s
   idle, driven by the labelled-placeholder `DemoPlayerInputSource` (the
   arcade's real AI is the OS ROM's ATRSW2 writer — undecodable from what we
   have), with wave clears through the tunnel and any human input back to the
   title. **The author playtested that build: "i expected a proper demo mode"** —
   the arcade plays a SCRIPTED STORYLINE first, and notes §95 now decodes it
   completely (build = handoff work table **B28**, pending): the story text
   crawl with the hero, the family, 14 grunts, the hulk, the spheroid scene
   with the brain reprogramming MUMMY, the score posts, then the phony game.
   **TWO OPEN QUESTIONS remain, parked in the
   handoff's work table and detailed in notes §91.3:** (**A11**, needs a MAME measurement) the spheroid's drop-countdown re-arm is decremented again
   in the same body by the ROM, and a countdown that reaches 0 becomes `$FF` (a ~2-minute stall);
   (**A12**, needs a measurement) the escape's exit columns are the port's own mapping and
   interact with A9. The ROM's colour
   processes **are** modelled — `Rendering/PaletteAnimator.cs` drives slots 10-15, and §86
   suspends and resumes them while the tunnel's ramp owns the palette.
1. **BUILD THE STORYLINE MOVIE (B28, notes §95) — the author's call on the first
   demo: "i expected a proper demo mode".** Follow §95.9: embed the HISTO and
   object-script bytes verbatim from the ROM, write the two interpreters, render
   the movie between the title's 12 s idle and the §94 demo game, extract the
   missing sprites (POINTS/SKULLV/POSTS/SQUARE + verify CRUSM/YOU), resolve the
   §95.10 open items as they come up, keep all six gates green. Then **the author
   playtest**: watch the full movie (mummy, daddy, mikey, grunts, hulk, brain,
   posts) and confirm it reads like the arcade. (The earlier playtest of the
   partial attract is DONE: title OK, demo OK, but not "a proper demo mode".)
   Then the rest:
   the tunnel (its bars,
   its 2-second pace, and the fact that it now runs over a colour-cycling ramp palette — the
   colours sweep through the wheel), the progs walking **twice as fast horizontally as the last
   build**, quarks whose first tank now arrives 2-6 s after they appear rather than instantly,
   and a family that starts walking the moment a wave starts (and never stands on an electrode).
   **Shooting a prog now shatters the 12×16 PHONY CARD in the ordinary strip explosion (§90)**
   instead of flashing a 20-tick blob — and the card's art itself was noise before this build.
   **A spheroid that has dropped its last enforcer now pulses at the arcade's rate while it runs
   for the exit, and its idle pulse reaches the medium ring again (§91).**
   Older items still worth a look: the materialisation (§62/§63), wave 9's invisible border
   (faithful — slot 0), and the explosions' shot-direction shatter (notes §35).
2. **Flip `GameplayConstants.PlayerInvincibleForTesting` to `false`** once gameplay is
   confirmed; un-skip `PlayerWalksIntoElectrode_BothStartDying`
   (`tests/Robotron2084.Tests/Level/PlayFieldCollisionTests.cs`) and update
   `PlayerInvincibilityTests` (it asserts the flag is on).
3. **Frills (author: last)** — DONE: spark flicker (SPKP0..3, 614d36a), explosions
   (RRX7, §35), **score font (§38)** — real ROM glyphs extracted (author's
   sprite-editor offsets; tools/extract-fonts.py) and wired into the HUD.
   Researched in session 2 (notes §36), now **BLOCKED on author**:
   - **Sounds: engine DONE with a flagged stub (notes §37)** — sequencer + event
     wiring built and wired (laser / robot death / player death / shell fire /
     shell bounce / brain spawn / bonus life); 171 tests green. The note→
     frequency + waveform map is sound-board hardware (not in the CPU ROM). Need
     author: the note table (or a recording to match by ear) to replace the stub
     scale in `MonoGameSoundSink.StubFrequency`.
   - **Red screen** (RRFRED) — still BLOCKED on the §33 question.
   - **Marquee** — needs an author asset.
   Colour-cycle shader built & wired (M4) — TICKED with the author's go-ahead
   (2026-09-16); font glyphs in cycling slots now draw through the same
   effect (notes §39). The M4 regression that hid every sprite is FIXED
   (notes §40 — the effect no longer declares a vertex shader; it must stay
   pixel-shader-only, or the draw must move to `SpriteBatch.Begin(effect:)`).
4. Optional ROM checks (never flagged by the author): hulk tempo — DONE (already
   ROM-derived, no action). Tank-shell speed + electrode placement — researched
   (notes §36.3/36.4); both parked behind the author (shell: constant vs
   accuracy-table scaling; electrode: player-distance vs per-wave safe-rectangle).

## Tool working notes
- Disasm parser rule that works: a byte = **full whitespace-delimited field of exactly two hex digits**
  (followed by space/EOL). NEVER consume hex-looking runs inside mnemonics ("ADDD" contains AD+DD —
  that bug caused a false 1,649-mismatch count; the true count is 34).
- ROM hole in the 64K image: $9000–$CFFF (16K) zeroed. RAM initial values = disasm listing of that range.
- CRC32 = standard (poly 0xEDB88320 reflected, init/fin xor 0xFFFFFFFF) — matches MAME.
- Bash `/tmp` = `C:/Users/scott/AppData/Local/Temp` (C# needs the Windows path).
