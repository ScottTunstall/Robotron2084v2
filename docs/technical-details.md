# Technical details

[Back to the README](../README.md)

This page is for readers who want to know how the port is built and how it was verified against the original.

## What "port" means here

The rules the project follows, developed over about 90 documented decode rounds:

- **The arcade is the standard.** *"Don't cut corners unless it's ABSOLUTELY something impossible."* No stand-in art and no "good enough" behaviour while the real one can still be recovered. If a playtest disagrees with the ROM, the decode is wrong, so the search continues. That rule found the shader bug behind a black robot trail, the font that was mirrored inside every byte, four separate explosion errors and the spheroid's missing animation frame.
- **Source hierarchy.** The original 1982 6809 listings are the authority for *semantics*. The author's annotated 6809 disassembly (`asm/robomame.asm`, the blue-label R5 build, committed in this repository) is a guide for locating code in the released build and for byte-level data. Its comments are leads, and at least one of them is wrong. Sprite and table bytes come from the verified ROM image, at the offsets the author's sprite editor supplies.
- **Model the game, not the hardware.** There is no blitter, DMA behaviour, beam tracking, watchdog, 4bpp screen RAM, coin door or self-test mode. The visible consequences of the hardware (colour cycling, the palettes, the partial-blit effects) are re-created with ordinary effects.
- **Two deviations, both documented:** the human family walks at 16 ROM frames a step instead of the original's 8, and humans are never placed on top of an electrode. Everything else is intended to be the arcade's behaviour.

## What is in the box

| | |
|---|---|
| **Playfield** | The 304×256 arcade buffer mapped into a 320×200 internal canvas, integer-scaled to the window (F11 cycles the scale), 16-colour palette, ROM wave tables, 50 fps ROM pacing on an exact-6ths tick clock |
| **Robots** | Grunts (with the kill-driven speed ramp), hulks, spheroids that pulse, give birth to enforcers and then flee sideways, quarks that drop tanks, tanks and their shells, brains that reprogram humans into progs, cruise missiles, electrodes/posts, sparks |
| **Humans** | Mommy, Daddy and Mikey, walking their ROM cadence, rescued or lost for score, and converted into progs when a brain catches them |
| **Effects** | The three-engine strip explosion (rows, columns, diagonal chevron), the wave-start materialisation, the wave-complete colour-cycling tunnel, the score bursts, the player's own `PDTHV` flash-and-fade |
| **Presentation** | The arcade HUD (score layout, blanked leading zeros, the mini-man spare lives, "<n> WAVE"), arcade font glyphs, 1- and 2-player alternating games with the ROM's turn-passing and "PLAYER n" announcement |
| **Attract** | The arcade's whole attract sequence, driven by its own script: the title screen, then the story movie (the 2084 text crawl, the hero, the family and their name popups, the grunts, the hulk, the spheroid/tank/enforcer scene with a human reprogrammed into a prog, and the score posts, about 96 s), then the machine's demo game. Both script interpreters run the ROM's own bytes. Each page waits until the page before it is done, however long that takes (in the arcade's own timing the waits change nothing, and a test checks it frame for frame) |
| **Sound** | The ROM's priority sound sequencer (`$D3C7`/`$D3E0`) and its tables drive a native C# sound board, ported routine by routine from the sound ROM's own source (`VSNDRM3.SRC`: the wave table synth, square wave, noise, radio, hyper and scream routines) and timed in the board's clock cycles. It matches the real ROM change for change, needs no ROM, and plays through MonoGame on Windows and Linux. Sounds are placed in stereo by their maker's position |
| **Speech** | Port-only (the arcade has no speech chip): a separate voice, apart from the sound board, plays the speech lines in `Content/Speech/` (built by the content pipeline). A line is never cut off by a sound effect or by the next line; sound effects still cut each other off, as on the arcade's one-voice board |
| **Game adjustment** | The cabinet's service-mode GAME ADJUSTMENT page (minus its coin/pricing rows), on `F5`: extra-man score, turns per player, difficulty of play (the ROM's own `$2C20` wave-table scaling) and the factory/high-score resets, plus a port-only attract-sound switch and switches for three arcade behaviours (Bozo mode and two bugs). See [Settings](settings.md) |

**Deliberately absent:** the self-test and bookkeeping screens, coin counting and the pricing rows of the adjustment page (its gameplay rows are built, press `F5`), and the marquee art claim (author asset pending).

## The decode log

The main record of the project is the decode log. Every finding, false start and retraction is in [arcade-fidelity-notes.md](arcade-fidelity-notes.md): 96 numbered sections of ROM analysis, many of them ending with a correction to an earlier section.

Some of the findings that took the longest, and what they showed:

- **The font was mirrored inside every byte** (a nibble-order slip in the glyph extractor) while every test stayed green. `tools/verify-fonts.py` now re-decodes the ROM and compares all 82 font master PNGs pixel for pixel.
- **The prog's trail rendered as a black card.** This was not a decode error. A pixel shader pass was bound on the device by an earlier draw and outlived it, so the card fill ran in the previous silhouette's colour. The same class of leak had earlier hidden every sprite while the wall alone kept drawing, hence `tools/verify-playfield.py`.
- **Explosions had to be derived four times:** the shape (one fan, not two halves, after a stale 1982 comment misled the first attempt), then which axis a shot cuts, then the mirroring (a laser hits the sprite's edge, not its centre), then the unit (the vertical-shot engine counts pixels, not byte columns).
- **"A column is two arcade pixels."** Coordinates are `column*256 + row` addresses, so anything added to or compared with one is in columns. That single fact corrected the prog's walk speed, the tank's birth, the missile's steps, several velocities and the spheroid's escape columns.
- **The spheroid's escape strobed at 3.6×** because that one phase animated per tick instead of per body. The same routine showed that the idle spin and the escape cycle five pictures, not four.
- **The prog's death card was noise.** A hand-transcribed sprite table had nine rows four bytes too long, which the PNG writer consumed as a misaligned stream. It is now pixel-verified against the listing, and the extractor refuses inline art that is not exactly `w × h`.

The method that works: read the routine, distrust the port, find a second witness (the disassembly, the art itself, or a measurement), pin it with a test, and write it up, including what was wrong before.

## Repository layout

| Path | What it is |
|---|---|
| `src/Robotron2084/` | The game: `Entities/`, `Level/`, `States/`, `Rendering/`, `Audio/`, `Input/`, `Tuning/GameplayConstants.cs` |
| `tests/Robotron2084.Tests/` | Over 800 tests: ROM contracts (timings, counts, layouts), not smoke checks |
| `docs/arcade-fidelity-notes.md` | The master decode log (ROM versus port, with retractions) |
| `docs/handoff-*.md`, `status.md`, `rebuild-ledger.md` | Session handoffs, the current state, and the per-checkpoint ledger |
| `docs/coding-standards.md`, `docs/refactoring-ledger.md` | The coding rules, and the record of code changed to meet them: every renamed member, old name to new |
| `asm/robomame.asm` | The author's annotated 6809 disassembly of the blue-label ROM, the locator and second witness behind most of the notes |
| `tools/SpriteExtractor/` | Sprite/table extraction (148 PNGs) and the inline passthrough art |
| `tools/RobotronSoundPlayer/` | The Robotron sound player: plays any game sound or sound number on an emulated sound board running the real sound ROM, to the speakers or a WAV file. The only home of the 6800 emulator; the game itself reads no ROM |
| `tests/RobotronSoundPlayer.Tests/` | The emulator's tests, and the fidelity tests that hold the game's native sound board to the real ROM (they skip without the ROM) |
| `tools/*.py` | Render gates and helpers (`verify-playfield`, `verify-fonts`, `extract-fonts`, `generate-mgcb`, `screenshot-map`) |
| `ref/` | Committed research notes (palette, MAME, wave tables, the sprite list). `ref/rom/` and `ref/original-source/` are git-ignored: supply your own ROM image and listings to regenerate assets |

## Attract dev keys

Port-only keys for inspecting the attract sequence (notes §97/§98):

- `End` jumps into the storyline movie, `Home` into the demo game, `Ins` to the high score table, and `Del` to the end of a game (the GAME OVER page and the initials ceremony).
- `PgUp` (held) fast-forwards the movie 8×, so the hulk's walk arrives in about 7 s instead of about 51 s.
- `Right` and `Left` step through the whole sequence by hand: the title page on its two text panes, the seven pages of the story, the demo game, the high score table, and round again.

These work on the attract screens only. In a game, `Ins` skips a wave.

## Checks run before every checkpoint

```bash
dotnet build Robotron2084.slnx                                     # 0 warnings
./tests/Robotron2084.Tests/bin/Debug/net10.0/Robotron2084.Tests.exe # over 800 tests
./src/Robotron2084/bin/Debug/net10.0/Robotron2084.exe              # launch smoke
python tools/verify-playfield.py                                   # a started wave really draws
python tools/verify-fonts.py                                       # all 82 glyph masters, pixel for pixel
python tools/verify-attract.py                                     # title -> storyline -> title, end to end
```

The test project is run through its executable, not `dotnet test`, because the runner is a non-standard in-process MTP host.

## Sound and speech

The native sound board plays through MonoGame on Windows and Linux. The speech voice is separate from it (notes §149).
