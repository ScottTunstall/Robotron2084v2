# Robotron: 2084 — a from-source port in MonoGame

![A started wave: orange boundary, grunts closing in, the family loose in the middle, "1 WAVE" at the
bottom](docs/images/gameplay.png)

**Robotron: 2084** (Williams Electronics, 1982) re-implemented in C# / MonoGame
(`net10.0`, desktop OpenGL) — **not an emulator, and not a re-skin.** Nothing here
runs 6809 code: every robot, every timer, every explosion and every colour is
re-derived from the original assembly listings, cross-checked against the shipped
ROM image and the author's own annotated 6809 disassembly, and then validated by
playtest, by unit tests and by render gates.

This repository is one person's reverse-engineering study of the machine — *"I
don't need you to emulate Robotron's hardware, just the functionality and
gameplay"* — built in tandem with AI coding agents (see [Credits](#credits--provenance)).

---

## What "port" means here

The house rules, learned the hard way over ~90 documented decode rounds:

- **The arcade is always the standard.** *"Don't cut corners unless it's ABSOLUTELY
  something impossible."* No stand-in art, no "good enough" behaviour while the real
  one can still be recovered. If a playtest disagrees with the ROM, the **decode is
  wrong** — so keep looking. That rule alone found the shader bug behind a black
  robot trail, the font that was mirrored inside every byte, four separate
  explosion errors and the spheroid's missing animation frame.
- **Source hierarchy.** The original 1982 6809 listings are the **Gospel** for
  *semantics*; the author's own **annotated 6809 disassembly** (`asm/robomame.asm`,
  the blue-label R5 build, committed in this repo) is a **guide** for locating code
  in the released build and for byte-level data — its comments are leads, and at
  least one of them is flatly wrong. Sprite/table bytes come from the verified ROM
  image, at the offsets the author's **sprite editor** supplies (below).
- **Model the game, not the hardware.** No blitter, no DMA quirks, no beam tracking,
  no watchdog, no 4bpp screen RAM, no coin door, no self-test/service mode. The
  *visible* consequences of the hardware — colour cycling, the palettes, the
  partial-blit effects — are re-created with idiomatic effects.
- **Two deviations, both asked for, both documented:** the human family walks at 16
  ROM frames a step instead of the Gospel's 8, and humans are never placed on top of
  an electrode. Everything else is intended to be the arcade's behaviour.

## What is in the box

| | |
|---|---|
| **Playfield** | The 304×256 arcade buffer mapped into a 320×200 internal canvas, integer-scaled to the window (F11 cycles the scale), 16-colour palette, ROM wave tables, 50 fps ROM pacing on an exact-6ths tick clock |
| **Robots** | Grunts (with the kill-driven speed ramp), hulks, spheroids that *pulse*, give birth to enforcers and then flee sideways, quarks that drop tanks, tanks and their shells, brains that reprogram humans into **progs**, cruise missiles, electrodes/posts, sparks |
| **Humans** | Mommy, Daddy and Mikey, walking their ROM cadence, rescued or lost for score — and converted into progs when a brain catches them |
| **Effects** | The three-engine strip explosion (rows, columns, diagonal chevron), the wave-start materialisation ("appear"), the wave-complete colour-cycling **tunnel**, the death bursts, the player's own `PDTHV` flash-and-fade |
| **Presentation** | The arcade HUD (score layout, blanked leading zeros, the mini-man spare lives, "<n> WAVE"), arcade font glyphs, 1- and 2-player alternating games with the ROM's turn-passing and "PLAYER n" announcement |
| **Attract** | The arcade's whole attract sequence, driven by its own script: the title screen, then the **story movie** — the 2084 text crawl, the hero, the family and their name popups, the grunts, the hulk, the spheroid/tank/enforcer scene with a human reprogrammed into a prog, and the score posts (~96 s) — then the machine's phony-player demo game. Both script interpreters run the ROM's own bytes |
| **Sound** | The ROM's priority sound sequencer ($D3C7/$D3E0) and decoded sound tables run for real — but the note→frequency map lives on the sound board, not in the CPU ROM, so the sink is a **stub beeper and is OFF by default** (`ROBOTRON2084_SOUND=1` to hear it) |

**Deliberately absent:** self-test / adjustment / bookkeeping screens, coin counting,
and the marquee art claim (author asset pending). The real sound note table is
similarly blocked on hardware knowledge.

## The journey (what has been through this repo)

The interesting part of this project is not the code — it is the **decode log**. Every
finding, every false start and every retraction lives in
[`docs/arcade-fidelity-notes.md`](docs/arcade-fidelity-notes.md): **96 numbered
sections** of ROM forensics, most of them ending in "…and the previous section was
wrong".

A few that cost real time, and what they taught:

- **The font was mirrored inside every byte** (a nibble-order slip in the glyph
  extractor) while every test stayed green — hence `tools/verify-fonts.py`, which
  re-decodes the ROM and compares all **82** font master PNGs pixel-for-pixel
- **The prog's trail rendered as a black card.** Not a decode error at all: a pixel
  shader pass was being bound on the device by an earlier draw and outliving it, so
  the card fill ran in the previous silhouette's colour. (The same class of leak had
  earlier hidden *every* sprite while the wall alone kept drawing — hence
  `tools/verify-playfield.py`.)
- **Explosions had to be derived four times:** the shape (one fan, not two halves — a
  stale 1982 comment had misled the model), then which axis a shot cuts, then the
  mirroring (a laser hits the sprite's *edge*, not its centre), then the unit (the
  vertical-shot engine counts *pixels*, not byte columns).
- **"A column is two arcade pixels."** Coordinates are `column*256 + row` addresses, so
  anything added to or compared with one is in columns. That single fact corrected the
  prog's walk speed, the tank's birth, the missile's steps, several velocities — and
  the spheroid's escape columns.
- **The spheroid's escape strobed at 3.6×** because that one phase animated per tick
  instead of per body — and the same routine revealed that the idle spin and the
  escape cycle *five* pictures, not four.
- **The prog's death card was literal noise** — a hand-transcribed sprite table with
  nine rows four bytes too long, which the PNG writer happily consumed as a
  misaligned stream. It is pixel-verified against the listing now, and the extractor
  refuses inline art that isn't exactly `w × h`.

The method that keeps working: read the routine → disbelieve the port → find a second
witness (the disassembly, the art itself, or a measurement) → pin it with a test →
write it up, including what was wrong before.

## Repository layout

| Path | What it is |
|---|---|
| `src/Robotron2084/` | The game: `Entities/`, `Level/`, `States/`, `Rendering/`, `Audio/`, `Input/`, `Tuning/GameplayConstants.cs` |
| `tests/Robotron2084.Tests/` | **270 tests** — ROM contracts (timings, counts, layouts), not smoke checks |
| `docs/arcade-fidelity-notes.md` | The master decode log (91 sections, ROM-vs-port, with retractions) |
| `docs/handoff-*.md`, `status.md`, `rebuild-ledger.md` | Session handoffs, the current state, and the per-checkpoint ledger |
| `asm/robomame.asm` | The author's own annotated 6809 disassembly of the blue-label ROM — the locator and second witness behind most of the notes |
| `tools/SpriteExtractor/` | Sprite/table extraction (148 PNGs) + the inline passthrough art |
| `tools/*.py` | Render gates and helpers (`verify-playfield`, `verify-fonts`, `extract-fonts`, `generate-mgcb`, `screenshot-map`) |
| `ref/` | Committed research notes (palette, MAME, wave tables, the sprite list). `ref/rom/` and `ref/original-source/` are **git-ignored** — supply your own ROM image and listings to regenerate assets |

## Build & run

```bash
dotnet build Robotron2084.slnx                     # 0 warnings, warnings-as-errors
./src/Robotron2084/bin/Debug/net10.0/Robotron2084.exe
```

**Controls:** `WASD` move · `IJKL` aim *and* fire (8-way; Space also fires) ·
`1` / `2` start a one- or two-player game · `F11` cycle window scale · `Esc` quit.
**The title screen (notes §101):** `F1` one player game · `F2` two player game
(alternate) · `F3` two player game (simultaneous — the mode is offered, the sharing
is not built yet) · `F10` define inputs. All four work from **every** attract
screen — title, storyline movie, demo game and high score table — and `P` pauses a
game (the arcade has no pause; port-only).
**Define inputs (`F10`, notes §101):** every MOVE/SHOOT UP, DN, LEFT, RIGHT of both
players on its own line. `Up`/`Down` scroll (player 2's block is beneath player 1's,
then the pause line), `Enter` arms the highlighted line and the next key, pad button
or stick push becomes it, `Del` clears the line, `R` restores the factory scheme and
`F10` saves and returns. Definitions live in
`%LocalAppData%\Robotron2084\controls.ini`, hand-editable and reloaded at start-up.
**Attract dev keys (port-only, notes §97/§98):** `F5` jump straight into the attract
storyline movie · `F6` jump straight into the attract demo game · `F7` (held)
fast-forward the movie 8× — the hulk's walk arrives in ~7 s instead of ~51 s ·
`F4` jump straight to the high score table · `Ins` skip a wave (the port's old `P`
test key moved when `P` became PAUSE).
**Sound:** off by default; `set ROBOTRON2084_SOUND=1` to enable it.

### The gates (run before every checkpoint)

```bash
dotnet build Robotron2084.slnx                                     # 0 warnings
./tests/Robotron2084.Tests/bin/Debug/net10.0/Robotron2084.Tests.exe # 400 tests
./src/Robotron2084/bin/Debug/net10.0/Robotron2084.exe              # launch smoke
python tools/verify-playfield.py                                   # a started wave really draws
python tools/verify-fonts.py                                       # all 82 glyph masters, pixel for pixel
python tools/verify-attract.py                                     # title -> storyline -> title, end to end
```

*(The test project is run through its executable, not `dotnet test` — the runner is a
non-standard in-process MTP host.)*

## Credits & provenance

**Robotron: 2084** was designed by Eugene Jarvis and Larry DeMar (Vid Kidz) and
released by **Williams Electronics in 1982**. All of the art, the tables and the
behaviour decoded here are theirs; this repository is a non-commercial
reverse-engineering study and is not affiliated with or endorsed by the rights
holders. The ROM image and the original source listings are **not** distributed here;
you need your own copy of the ROM to regenerate assets.

**The reverse engineering, the tooling and the truth are the author's.** The
hardware facts; the **sprite editor** (`WmsGfxSpriteEditor`), whose
`RobotronBlueLabelSpriteRepository` is the definitive name/offset/width/height list
for every sprite and whose `RobotronPaletteService` is the palette guide (following
seanriddle.com's byte→RGB conversion); the **annotated 6809 disassembly**
(`asm/robomame.asm`) that ships in this repo; the spec; and every playtest and every
*"no, it doesn't look like that"* — all of that came from the person driving this
project. The AI agents did the reading, the writing, the tests and the digging; the
author decided what was true.

**Built in tandem with AI coding agents** — sessions on this repository have been run
with **GitHub Copilot**, **Qwen 3.8** and **DeepSeek**, all of them working from the
same documents you can read here: the notes, the ledger, the handoffs and the gates.
The decode log is deliberately written for a *fresh* agent ("read this first, here are
the traps that have already bitten"), which is why a session can be picked up months
later without re-learning the machine.