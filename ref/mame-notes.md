# MAME driver notes — Robotron 2084

Source: `mamedev/mame` @ `master`, `src/mame/williams/williams.cpp` (shared Williams 6809 machine code; Robotron driver section). Fetched 2026-08-30 (scratch copy `/tmp/williams.cpp`).

## Version identification
- MAME set **`robotron` = "Release 5" — Solid Blue labels** ("B" type 2732 ROMs, labels 3005-13…3005-24). This matches the target of the user's disassembly.
- Six "release" versions were found in a 2004 backup of the **Vid Kidz GIMIX 6809 development system** (Duncan Brown's backup): R1–R3 = early field-test builds; R3 is the **censored** build (skull → X over generic human; visible in promo video `youtu.be/j0tqQryAmN0?t=209`). Eugene Jarvis convinced Williams to revert to the skull just before production.
- **R4 = yellow label** (MAME `robotronyo`), **R5 = solid blue** (`robotron`) ← our target, **R6 = 1987 patch** by Christian Gingras (`robotron87`, "shot in the corner" bug fix).
- **Same label numbers, different data:** blue and yellow/red "B" ROMs both use 3005-13…24. Blue part numbers A-5343-09945…09956 (yellow/red: A-5343-09898…09909). Do **not** mix sets.
- Other MAME sets: `robotronr3` (R3 censored prototype), `robotronun`, `robotron12`, `robotrontd` (Tie-Die; starts from a blue set).
- Label format: `2084 ROM 1-A / (c) 1982 WILLIAMS ELECTRONICS, INC. / 3005-1`; type B = 2732 EPROM (board jumpers W1 & W3 wired). Board = D-9144-3005 ROM board assembly (PIA @ 1B; ROM1…ROM12 at sockets 4E/4C/4A, 5E/5C/5A, 6E/6C/6A, 7E/7C/7A).

## Main-CPU memory map (64K region)
12 × 4KB 2732 ROMs (verified against local set — see bottom):

| ROM | File (blue label) | Offset | CRC32 |
|----:|---|---|---|
| 1 | 2084_rom_1b_3005-13.e4 | 0x00000 | 66c7d3ef |
| 2 | 2084_rom_2b_3005-14.c4 | 0x01000 | 5bc6c614 |
| 3 | 2084_rom_3b_3005-15.a4 | 0x02000 | e99a82be |
| 4 | 2084_rom_4b_3005-16.e5 | 0x03000 | afb1c561 |
| 5 | 2084_rom_5b_3005-17.c5 | 0x04000 | 62691e77 |
| 6 | 2084_rom_6b_3005-18.a5 | 0x05000 | bd2c853d |
| 7 | 2084_rom_7b_3005-19.e6 | 0x06000 | 49ac400c |
| 8 | 2084_rom_8b_3005-20.c6 | 0x07000 | 3a96e88c |
| 9 | 2084_rom_9b_3005-21.a6 | 0x08000 | b124367b |
| 10 | 2084_rom_10b_3005-22.a7 | 0x0d000 | 13797024 |
| 11 | 2084_rom_11b_3005-23.c7 | 0x0e000 | 7e3c1b87 |
| 12 | 2084_rom_12b_3005-24.e7 | 0x0f000 | 645d543e |

**(SHA1s are in williams.cpp; all verified locally.)**

Main-CPU 64K address space (16-bit; ROM-region offsets map 1:1 to CPU addresses). MAME `williams_b1` →
`main_map_blitter`; user's RRF.ASM equates agree:

| CPU range | Contents |
|---|---|
| $0000–$8FFF | ROM1–9 (36K) when switch=ROM; RAM (48K block, `m_videoram`) when switch=RAM |
| $9000–$BFFF | RAM (always) — same 48K block |
| $C000–$C00F | palette RAM (CRAM), 16×4-bit, write-only (mirrored) |
| $C804–$C807 | PIA-A (user: PIA2) — keyboard/panel |
| $C80C–$C80F | PIA-B (user: PIA0) — panel; port B $C80E = sound command (out of scope) |
| $C900 | ROM/RAM select (RWCNTL) |
| $CA00–$CA07 | blitter (Robotron: `WILLIAMS_BLITTER_SC1`) |
| $CB00 | vertical beam counter (read) |
| $CBFF | watchdog reset (write) |
| $CC00–$CFFF | CMOS/NVRAM (battery-backed, BCD, 4-bit writes; MAME shares with the `nvram` device for defaults/save) |
| $D000–$FFFF | ROM10–12 (12K), always ROM |

**The 16K ROM-region hole (offset 0x09000–0x0CFFF) has no ROMs** — it's the always-RAM area of the
48K RAM block. 48K RAM layout (user-confirmed):
- **$0000–$97FF: screen RAM (38K)** — 304×256 pair-major 4bpp, 2 px/byte, 256 B per 2-px column × 152 columns = exactly 38,912 B
- **$9800–$980F: base-page RAM (`PCRAM`)** — runtime palette copy ($DA51 ROM default → here at startup, code $D795)
- $9811–$9823: entity list pointers (disasm annotations); task list $9815; task alloc $D1E3
- $9840: `num_players` (direct $40); $984F–$9851: credit counters (bonus credit, units-for-credit, total)
- player object ~$985A; credits persisted in CMOS $CD00 (BCD 2 bytes)

Note: MAME's `set_visarea(12, 303, 7, 246)` (292×240) belongs to **defender()**, not robotron.
Robotron (williams_b1) uses the base `set_raw(8MHz, 512, 6, 298, 260, 7, 247)` — emulator geometry,
reference only (user's 304×256 @ 50 fps RE is authoritative, D-004).

## Sound CPU (separate 64K region — out of scope, D-003)
- MC6808 @ 3.12 MHz XTAL (internal /4 divider → effective ≈894.886 kHz per driver comment)
- `video_sound_rom_3_std_767.ic12` (P/N A-5342-09910) at 0xF000, 4KB
- Main CPU pokes 6-bit sound tokens to PIA1 port B ($C80E)

## PROMs (0x400 region)
- `decoder_rom_4.3g` — Universal Horizontal decoder PROM (P/N A-5342-09694)
- `decoder_rom_6.3c` — Universal Vertical decoder PROM (P/N A-5342-09821)
- Not needed for the port (we render directly, no decoder PROM emulation).

## Clocks / display (MAME's model — reference only)
- Master clock 12 MHz; main CPU **6809E = 12 MHz / 3 / 4 = 1.0 MHz** (`MC6809E(config, m_maincpu, MASTER_CLOCK/3/4)`); sound 6808 @ 3.579545 MHz (÷4 → ≈894.886 kHz)
- MAME display: **292×240** vs user's hardware RE **304×256 @ 50 fps** — **user's RE is authoritative** (D-004); MAME geometry recorded for cross-check only
- Scanline timers (va11 @ line 32, count240 @ line 240) = MAME's raster emulation — **not modelled in the port** (D-015, no raster tracking)
- Screen: 4bpp, 16 colours; MAME uses a 256-entry palette table to emulate the colour decoder

## Local ROM set (verified 2026-08-30)
`E:\roms\MAME ALL ROMS - DO NOT DELETE\robotron`
- 12 blue-label game ROMs + 2 decoder PROMs; **all CRC+SHA1 match MAME master** → confirmed solid blue set
- Subsets present: `robotron12/`, `robotrontd/`, `robotronun/`

## Cross-check status (2026-08-30)
- 64K image built from verified ROMs: `/tmp/romtool/robotron64k.bin` (hole zeroed)
- Disassembly vs verified image: 46,160 ROM bytes listed, **34 mismatches in 8 small local regions** (byte-shift pattern) — see `status.md` "Live thread". 99.93% match ⇒ the disassembly targets the blue label.
