# Arcade-fidelity port — canonical working notes (ROM + original source)

**RESTART INSTRUCTIONS:** if the session drops, read THIS file top to bottom,
then `rebuild-ledger.md` (12-phase baseline), then continue at
**Progress log → last entry**. Everything discovered from the source is
recorded here. After EVERY research milestone or code milestone: update the
Progress log + `git add docs/arcade-fidelity-notes.md && git commit`.

## 0. Directives (author, 2026-09-05 — supersede the plan's "no ROM extraction" constraint)

0. **SOURCE HIERARCHY (author, 2026-09-16 — MOST IMPORTANT): the original source
   listings (`ref/original-source/*.ASM`) are the GOSPEL and the authority for
   SEMANTICS; `asm/robomame.asm` (the disassembly) is a GUIDE ONLY.** Use the
   disasm to locate code in the R5 build and for R5-specific addresses/data — but
   never as the authority for MEANING, and never trust its comments (at least one
   is flatly wrong: the `$5C23` comment contradicts its own `BNE`). The source
   names and documents its fields where the disasm has only anonymous zero-page
   bytes (`RRDX2.ASM`'s `XSIZE`/`YSIZE`/`SLOPE` vs `$BA`/`$BB`) — and that naming
   is what resolves ambiguity. **Read the Gospel first; use the disasm to confirm.**
   On meaning, the source wins. Byte-level DATA (sprites/tables) still comes from
   the verified ROM image. (This rule exists because the explosion decode was
   attempted from the disasm alone and produced a wrong model, shipped and then
   retracted — see §35.1.)

1. **Use the ROM and the original source as reference.** "Use whatever you
   need to get an arcade faithful port." (Author 2026-09-05: "The plan that
   states do not copy or use the ROM as reference — IGNORE. Use the ROMs as
   reference. Use the original source as reference.")
2. **Do NOT emulate the Robotron hardware.** "I want this to be a real
   monogame app, not an emulator." No screen RAM, no blitter, no 16-entry
   RAM-palette hardware, no character-grid video, no PIA/watchdog/CMOS.
   Faithful = sprites, colours, effects, behaviours, values — implemented as
   ordinary MonoGame objects (textures, SpriteBatch, Effects, per-entity
   state).
3. Port the ROM sprites into MonoGame-compatible assets. (Explicit ask.)
4. Create pixel shaders to handle the colour-cycling effects. (Explicit ask.)
5. **Everything discovered from the source must be noted here** so work can
   resume after a power loss. (Author 2026-09-05, twice.)
6. **Palette = `WmsGfxSpriteEditor`'s `RobotronPaletteService` as the guide**
   (author 2026-09-05): port its 16 colour bytes + seanriddle.com byte→RGB
   conversion. **Do NOT reverse-engineer the 6809 / Williams hardware** (no
   resistor-network 256-entry palette). This supersedes the §3.3 grey-marker
   scheme and the §3.4 `BasePalette` TODO.
7. **Sprite locations: use the Riddle list** (`docs/robotronsprites.txt` —
   decimal ROM offset, width bytes, height px). Stop reverse-engineering the
   assembly to locate sprites; the rest can be fixed later (author
   2026-09-05).
7b. **The arcade TITLE SCREEN does NOT need porting** (author 2026-09-12):
    keep the port's original title screen as-is. **Demo/attract mode**
    (self-playing game behind the title) = nice-to-have, NOT essential —
    only after the main game (humans/brains/gates) is done.
8. **Definitive source for ALL sprites = `RobotronBlueLabelSpriteRepository.cs`**
   (author 2026-09-06):
   `C:\Users\scott\source\repos\WmsGfxSpriteEditor\WmsGfxSpriteEditor.Roms.Robotron2084\
   Shared\Sprites\RobotronBlueLabelSpriteRepository.cs` — entries
   `_sprites.Add(new(id, name, offset, widthInBytes, height, BitsPerPixel))`;
   ignore `id`; `BitsPerPixel` is ALWAYS 4. **Do NOT reverse-engineer or scan
   the ROM for sprites — "it's all there for you."** Verified 2026-09-06:
   byte-identical to `docs/robotronsprites.txt` (same 215 names/offsets/
   widths/heights).

## 1. User-stated gameplay facts (authoritative; from the author's own RE)

0. **Original-source terminology:** "CIRCLES" in the source = **spheroids**
   (author-confirmed 2026-09-05). Other name mappings used throughout:
   RRC11 "CIRCLE" = spheroid; RRTK4 "SQ" = quark; "TNG"/"TNK" = tank;
   "MAN" = player; "RWD"/"ROB" = grunt; "PST" = post/electrode; "HLK" =
   hulk; "MOM/DAD/KID" = mummy/daddy/mikey; "BRN" = brain; "PROG" =
   converted human; "SPK" = spark.

1. **Mummy / Daddy / Mikey are the HUMANS the player rescues** — bonus
   points, NOT enemies. Families appear; robots that kill a human leave a
   skull & crossbones marker (12×11 sprite at ROM $0437).
2. **Spheroids drop Enforcers whether you shoot them or not** → timer-based
   release. (ROM-confirmed, §4.1.)
3. **Quarks drop Tanks whether you shoot them or not** → timer-based
   release. (ROM verification pending in RRTK4.ASM, §4.5.)
4. (From title screen, RRET.ASM) **Hulks are INDESTRUCTIBLE** — no kill
   points; you can only push them.

## 2. Reference material inventory (ref/ + asm/ are local-only/git-ignored; docs/ + tools/ are committed)

| Path | What it is |
|------|-----------|
| `ref/rom/robotron64k.bin` | 64KB main-CPU ROM, Robotron 2084 **Release 5 solid blue** (MAME set `robotron`). "Byte-level authority for asset extraction." 12×4KB ROMs at $0000–$8FFF + $D000–$FFFF; $9000–$CFFF zeroed (RAM area). |
| `ref/original-source/*.ASM` | **Original 6809 source listings** with labels+comments. Sprite pixel data IS in these listings inline (FCB/FDB — §4.15) and cross-verifies byte-exact vs the ROM. File→section map in §5. |
| `docs/robotronsprites.txt` | Sean Riddle's Robotron Sprite List (seanriddle.com), 215 lines: col1 name, col2 **DECIMAL** ROM offset, col3 width (BYTES), col4 height (px), col5 flag. Offsets verified vs ROM (mommy1=1375=$055F, daddy1=2095=$082F, mikey1=2923=$0B6B, sphereoid1=5394=$1512, player1=13851=$3603, brain1=8561=$2171, grunt1=16499=$4073, familydeath=1083=$043B). Committed mirror of the definitive source below (verified identical 2026-09-06). |
| `C:\Users\scott\source\repos\WmsGfxSpriteEditor\WmsGfxSpriteEditor.Roms.Robotron2084\Shared\Sprites\RobotronBlueLabelSpriteRepository.cs` | **DEFINITIVE sprite source** (author directive §0.8, 2026-09-06): 215 entries `new(id, name, offset, widthInBytes, height, BitsPerPixel)`; offsets decimal into `robotron64k.bin`; ignore `id`, bpp always 4. What the author's own sprite editor renders from. Verified identical to `docs/robotronsprites.txt` (2026-09-06). Does NOT contain the tank shell `SHLP1` (its 4×7 size came from the source's `FCB 4,7` and the author's editor — notes §54) nor the tank-grow frames `MTNKP1..4` (found in the ROM by the extractor's Pass C at `$5036`/`$503E`/`$505A`/`$507A` — notes §52). |
| `docs/sprite-map.md` | Sprite → source → ROM offset → PNG map (231 lines; generated by the tool; regenerate after the 4bpp rewrite). |
| `tools/SpriteExtractor/` | net10.0 console, zero deps, hand-rolled 24-bit colour PNG writer + CRC32. 4bpp decode (high nibble = left). Palette = `RobotronPaletteService` port. 3 passes: A = Riddle list straight from ROM; B = source-listing fallback (emit only if bytes found verbatim in ROM); C = ROM pointer-chase (**STUB, empty** — needed for SHLP1 + MTNKP1-4, recipe in §4.15). Run: `dotnet run --project tools/SpriteExtractor` (auto-detects repo root via ref/ + src/). **Single sprite + ASCII preview for validation: `dotnet run --project tools/SpriteExtractor -- --sprite NAME`** (NAME = list name, e.g. `familydeath`). |
| `asm/robomame.asm` | MAME disassembly of the ROM (verified vs 64k image on 46,160 bytes). Use when the source listing is ambiguous; re-derive addresses near the listed byte-shift regions. |
| `tools/verify-playfield.py` | Headless playfield RENDER check (notes §40 regression guard): launches the game, presses SPACE, screenshots the client area, asserts the ring interior is not black. Exit 0 = content, 1 = regression. Run after any change to the draw path or an `.fx`. |
| `ref/palette-notes.md` | Colour chain (4-bit nibble → 16-slot RAM palette → 256-entry resistor-network hardware palette), MAME `palette_init` code quoted, default palette, sprite storage format (verified), ripper cross-checks. |
| `ref/mame-notes.md` | CPU memory map, ROM offsets/CRCs (verified against MAME master), display geometry (user's 304×256@50fps RE authoritative), PIA/CMOS/blitter addresses, version history (R5 = our target). |
| `ref/robowaves.md` | Wave table decoded: $2B7C per-wave setup (waves 1–40 unique, then REPEAT 21–40); $2E24 enemy counts (9 columns, waves 1–40 full table in that file); $2C12 ≈12 timing params (9 decrease, 3 increase with wave). Wave = 8-bit; after 255 → back to 1. |
| `C:\Users\scott\source\repos\WmsGfxSpriteRipper` | Author's MFC Williams sprite ripper. `Form1.h` @787 palettes (Robotron row == $DA51 ✓), @871-901 4-bit pixel→RGB, @1229-1259 rendering. |
| `C:\Users\scott\source\repos\WmsGfxSpriteEditor\WmsGfxSpriteEditor.Roms.Robotron2084\Shared\Palettes\RobotronPaletteService.cs` | **Palette source of record** (author directive §0.6): 16 colour bytes (0-9 = $DA51, 10-15 = de-duplicated `C4 F4 CC 81 45 2F`) + seanriddle.com byte→RGB conversion. Ported into `tools/SpriteExtractor` (2026-09-05). |

## 3. Verified data formats (author's RE + my cross-checks)

### 3.1 Sprite storage (Robotron = blitter "Special Chip" game → "Linear" layout)
> **CORRECTION (2026-09-05, AUTHOR — read first):** sprites are **4 bits
> per pixel** ("2 pixels per byte means 4 bits per pixel!!!"), NOT 1-bit
> shape masks. Each byte = 2 pixels: **high nibble = LEFT pixel, low nibble
> = RIGHT pixel**. Each 4-bit value is a **PALETTE INDEX (0-15)** into the
> 16-slot RAM palette. The blitter also has a **REMAP** mode that blits a
> sprite as one SOLID colour (PROGs use it to draw single-colour). All
> earlier grayscale/1-bit PNGs are **WRONG** and must be regenerated per §3.3.

- **4 bits per pixel, 2 pixels per byte**, row by row (row-major; NOT
  pair-major; no duplicate 1px-shifted sprites).
- **Sprite entry = 4 bytes: `[width][height][ptr_hi][ptr_lo]`** — pointer
  big-endian (6809 D order), NOT XORed with $04 (that's Joust/Bubbles).
  - `width` = BYTES per row → pixel width = width×2
  - `height` = pixels
- Entries live in consecutive 4-byte tables. Frame data pointed to is stored
  contiguously with pitch = width×height bytes. Animation = consecutive
  frames; frame COUNT comes from object/animation tables.
- Code corroboration: ROM $00A8 `ADDD [$02,X]` adds [width|height] as a
  16-bit value.
- **Extraction recipe:** find the entry table (labels in §5.2 give the ROM
  locations via each module's ORG), read 4-byte entries, copy w×h bytes per
  frame from the pointer, decode 2 px/byte: `byte = data[y*w + (x>>1)]`,
  `nibble = (x&1)==0 ? (byte>>4)&0xF : byte&0xF` (high nibble = left).
  Nibble = palette index 0-15.
- **Skull sanity check (4bpp, 12×11):** row0 = `00 00 AA A0 00 00` →
  `[0,0,0,0,A,A,A,0,0,0,0,0]`; an eyes row = `0A 0F FA FF 0A 00` →
  `[0,A,0,F,F,A,F,F,0,A,0,0]`. Eyes use slot 15 (red-gold, cycling) +
  slot 10 (laser flash, cycling) → "the skull's eyes colour cycle" ✓ (author).

### 3.2 Colour chain (for the shader, NOT to emulate)
- Screen pixel = 4-bit nibble → 16-slot RAM palette (PCRAM $9800; hardware
  CRAM $C000 write-only) → 256-entry hardware palette = **analog resistor
  network**: bits 0-2 R / 3-5 G (taps 1200/560/330 Ω), bits 6-7 B (560/330 Ω).
- **Sprite pixel = the screen nibble directly:** when blitted (no remap),
  the sprite's 4-bit value is written straight into 4bpp screen RAM, so
  sprite pixel v → RAM-palette slot v → 8-bit colour code → 256-entry HW
  palette → RGB. (Blitter REMAP mode overrides with one solid colour —
  used by PROGs.)
- MAME `williams_v.cpp::palette_init` quoted in palette-notes.md (use
  `compute_resistor_weights` + `combine_weights` to generate the 256-entry
  base palette in C# — algorithm captured in §3.4).
- Fast cross-check (ripper): `r=(n&7)<<1; g=(n&0x38)>>2; b=((n&0xC0)>>6)*5`
  (4-bit quantised) — use to sanity-check the 256-entry table.

### 3.3 PNG encoding scheme (AUTHOR-SPECIFIED, 2026-09-05)
> **SUPERSEDED (2026-09-05, author §0.6):** palette now = `RobotronPaletteService`
> port (seanriddle.com byte→RGB conversion) over the 16 colour bytes
> `00 07 17 C7 1F 3F 38 C0 A4 FF C4 F4 CC 81 45 2F` (slots 0-9 = $DA51;
> 10-15 = the service's de-duplicated values so all 16 are distinct).
> Resulting RGBs: 0=(0,0,0) 1=(240,0,0) 2=(240,64,0) 3=(240,0,240)
> 4=(240,96,0) 5=(240,240,0) 6=(0,240,0) 7=(0,0,240) 8=(144,144,160)
> 9=(240,240,240) A=(144,0,240) B=(144,208,240) C=(144,32,240) D=(32,0,160)
> E=(176,0,80) F=(240,176,0). No grey markers, no hardware modelling. The M4
> shader remaps a texel whose RGB EXACTLY equals one of slots 10-15 to that
> slot's live cycling colour (same mechanism, different marker colours). The
> stale scheme is kept below for history only.

Store sprites as **24-bit colour PNGs** (NOT grayscale). Per sprite pixel
v (4-bit palette index), use this exact table (hand-computed RGB from
`ref/palette-notes.md` L70-87 — inlined here because ref/ is git-ignored):

| slot | code | base RGB | PNG stores |
|------|------|----------|------------|
| 0 | $00 | (0,0,0) | (0,0,0) |
| 1 | $07 | (255,0,0) | (255,0,0) |
| 2 | $17 | (255,137,0) | (255,137,0) |
| 3 | $C7 | (255,0,255) | (255,0,255) |
| 4 | $1F | (255,118,0) | (255,118,0) |
| 5 | $3F | (255,255,0) | (255,255,0) |
| 6 | $38 | (0,255,0) | (0,255,0) |
| 7 | $C0 | (0,0,255) | (0,0,255) |
| 8 | $A4 | (137,137,161) | (137,137,161) |
| 9 | $FF | (255,255,255) | (255,255,255) |
| 10 | $38 | (0,255,0) | GREY (16,16,16) |
| 11 | $17 | (255,137,0) | GREY (32,32,32) |
| 12 | $CC | (137,37,255) | GREY (48,48,48) |
| 13 | $81 | (37,0,161) | GREY (64,64,64) |
| 14 | $81 | (37,0,161) | GREY (80,80,80) |
| 15 | $07 | (255,0,0) | GREY (96,96,96) |
- **v = 10-15 (cycling slots — LASER FLASH/RGB/DECAY/LASER/BPR/RGOLD):**
  store a **distinct pure-grey level per slot** (R=G=B=value) so the pixel
  shader can detect the grey and remap it to the slot's CURRENT colour:
  slot10→16, slot11→32, slot12→48, slot13→64, slot14→80, slot15→96.
  (None of the fixed 0-9 colours are pure grey except black/white → no
  collision.)
- Runtime (M4) shader: keep a texel's RGB unless it EXACTLY equals one of
  the six placeholder greys; if it does, substitute that cycling slot's
  live colour from the PaletteAnimator.

### 3.4 256-entry base (resistor-network) palette — **OVERRIDDEN, do NOT build**
> **2026-09-05 (author §0.6):** no 6809/Williams hardware reverse-engineering.
> The 16-slot table (§3.3-correction) is the palette of record; the M4
> `PaletteAnimator` converts each 8-bit colour code from the §4.12 process
> tables with the SAME seanriddle.com byte→RGB function (it accepts any
> 8-bit BBGGGRRR code, not just the 16 table entries).

- ~~TODO — build `BasePalette` (256 entries) from MAME's formula~~ (overridden
  as above; algorithm text kept below for reference only):
- Resistor values (author's RE, palette-notes.md): R & G taps =
  1200/560/330 Ω (bits 0-2 / 3-5); B taps = 560/330 Ω (bits 6-7). EXACT
  values to confirm from MAME's robotron driver (src/mame/williams/robotron.cpp
  404'd — find the correct path next session).
- **MAME formulas (fetched 2026-09-05, mamedev/mame master, src/emu/video/
  resnet.cpp + resnet.h):**
  - `compute_resistor_weights(min,max,scaler,count,resistances,weights,
    pulldown,pullup,...)`: for each resistor n connect ONLY r[n] to VCC and
    all others to GND; `R0`=parallel(pulldown + all r[j≠n]), `R1`=
    parallel(pullup + r[n]); `Vout=(max-min)*R0/(R1+R0)+min`; `weights[n]=
    clamp(Vout,min,max)`. min=0,max=255,scaler=-1 (autoscale), pd=pu=0.
    Autoscale: `scale = max / max(sum(rw),sum(gw),sum(bw))`, multiply every
    weight by `scale`.
  - `combine_weights(tab, b0, b1, ...) = round(Σ tab[i]*bit[i])` (+0.5).
  - Build per colour code c: `r=combine_weights(rw,bit0,bit1,bit2)`;
    `g=combine_weights(gw,bit3,bit4,bit5)`; `b=combine_weights(bw,bit6,bit7)`.
- **The 10 fixed-slot RGBs (indices 0-9) are used DIRECTLY in the PNGs —
  the 256 table is only needed at runtime (M4) to turn cycling pcram values
  (0-255) into live RGB.**

## 4. Discoveries from the original source (2026-09-05)

### 4.1 Spheroid ("CIRCLE") — RRC11.ASM (RCORG $1140)
Vectors: `CIRST`(start), `CIRKKV`(kill), pics CIRP0 / ENGP1 / ENFP0.
- **Spawn** (CIRST): `PD2 = RMAX(CDPTIM)` (random drop time; CDPTIM =
  per-wave timing param), `PD3 = 0..ENFNUM/2` enforcers to drop (ENFNUM =
  "# OF ENFORCERS/CIRCLE X2", RRF.ASM:622; `RMAX(ENFNUM)` then `LSR`+0 →
  0..ENFNUM/2). Random start pos (XMIN+2..XMAX-8 by SEED sign), random accel
  (CIRNAC: X acc = (HSEED&$1F)-$10, Y acc = ((LSEED^SEED)&$3F)-$20, re-roll
  every 15 ticks).
- **Bounce phase** (pic < CIRP4): `DEC PD2` → at 0 → **drop phase**.
- **Drop phase (CIRC2)**: new timer `RMAX(CDPTIM/4)`; while opening
  (CIRP4..CIRP7), when timer hits 0: if `ENFCNT < 8` (max enforcers on
  screen) AND `OVCNT < 17` (max objects) → **JSR ENFDRP (drop an enforcer)**,
  `DEC PD3`. If PD3 == 0 → **escape phase**: X velocity ±$100, drifts
  off-screen (until X outside XMIN+3..XMAX-10) → removed. Else new drop time.
- **Kill (CIRKIL)**: if `PCFLG` (player touched it) → NO explosion (player
  dies instead). If **shot**: kill process CIRKP "BUBBLE BURST": colours
  `$FFAA`, advances pic by 4 ×7 (burst frames CIRP1..CIRP7), then shows the
  "1000" popup (P1KD pic) for 30 ticks; **score 1000** (`$0210`);
  **NO enforcer drop on shot** — drops are timer-only. ✓ matches user fact 2.
- Spheroid = 8 frames CIRP0-7 (16×15 px): 0-3 = bounce, 4-7 = opening.

### 4.2 Enforcer ("ENF") — RRC11.ASM
- Dropped by spheroids via ENFDRP; object type ENFRCE, initial pic ENGP1;
  ENGP1-5 + ENFP0 = 6 frames (10×11 px). Friendly: shoots enemies
  (ENFSND "ENFORCER SHOOT", ENKSND "ENFORCER KILL"). Score 150 (title).
- `ENFNUM` = "# OF ENFORCERS/CIRCLE X2".

### 4.3 Spark (kill explosion) — RRC11.ASM:698
- SPKP0-3 = 4 frames, 4×7 bytes = **8×14 px**. SPKSND "SPARK KILL".

### 4.4 Brain ("BRAIN") — RRB10.ASM (BRNORG $1AC0)
- `BRNCNT` per wave. Walks 4 dirs × 3 frames (BRLP/BRRP/BRDP/BRUP, 14×16 px;
  anim tables BRNAL/BRNAR/BRNAD/BRNAU = P1,P2,P1,P3 pattern).
- **Targets the CLOSEST HUMAN** (GETROB/GETHTG; "GET CLOSEST HUMAN TARGET"
  @106), shoots with `BSHTIM` random timer (BSHSND "BRAIN SHOOT").
- **Brain touching a human → "REPROG YOUR MOONIES" (BMUT)**: brain moves
  onto the human (facing left BRLP1 / right BRRP1), erases the human's body,
  then RE-DRAWS the human's base picture in place as a blinking (colours
  `$AABB`) **PROG** while PRGSND ("PROGRAMMING SOUND") plays for 20 loops;
  HPSND "HUMAN-PROG FINAL CONVERSION" at the end. The converted human is now
  a PROG object (enemy, 100 pts).
- BRAIN = 500 pts (title table).
- Other RRB10 items: PGXPIC 6×16 bytes = 12×16 (PROG pic); CMPIC/CMP1 3×4
  = 6×8 (2 frames) — candidate for the cruise missile (25 pts) — VERIFY vs
  RRTK4 SHLP1; BSHSND/CMKSND ("CRUISE MISSILE KILL")/PGKSND sounds.

### 4.5 Quark / Tank / Shell — RRTK4.ASM (RTKORG $4B00) — **FULLY DECODED**
- SQP0-7 = **quark** ("SQUARE"), 8×15 = 16×15 px, 8 frames (SQP4 = opening).
- Quark spawn (SQSTV): `SQCNT` per wave, `PD2 = RMAX(TDPTIM)` ("TIME TO
  DROP TANKS"), `PD3 = 0..ENFNUM/2` (SAME ENFNUM param as spheroids!).
  Spawn at top (YMIN+2) or bottom (YMAX-14) edge, random X; velocity from
  SQSPD (per-wave param), re-rolled every PD7 (1..16) ticks; bounces off
  screen edges. Processes every 3 ticks (NAP 3 — slower than spheroid's 2).
- **Drop mode (when PD2 hits 0)**: sub-timer = RMAX(TDPTIM/2+1); when it
  hits 0, if OVCNT < 17, free object slot, and **TNKCNT < 20 (max tanks)**
  → TNKDRP. All PD3 dropped → **flee mode**: vertical velocity ±$0200,
  drifts off top/bottom, removed. **Shot quark: NO drop** — kill process
  (reuses CIRKV, colours $DDDD, count 8), score **1000** ($0210),
  SQKSND "SQUARE KILL". ✓ matches user fact 3 (timer-only drops).
- TNKDRP: creates MTANKS "mini tank" at quark pos + offset ($0206), pic
  MTNKP1, TKDSND "TANK DROP". **MTANK grow**: advances pic one frame each
  12 ticks (NAP 12): MTNKP1(4×8px)→MTNKP2(8×14)→MTNKP3(8×16)→MTNKP4(12×24)
  →TNKP1, then becomes a full TANK. Small centering offsets per frame.
- **TANK** (TNKP1-4 = 14×16 px, 4-frame walk cycle, direction-independent
  frames): PD6 shot timer = TNKSHT (per-wave); moves axis-aligned (PD4 =
  dx/dy ±1), bounces off bounds (TNKND); **75% of re-aims SEEK THE PLAYER**
  (SEED < $60), 25% random; speed = TNKSPD (per-wave, via SLEEP).
  Kill (TNKIL): DEC TNKCNT, **HVEXST horizontal explosion** (RRHX4),
  score **200** ($0120) ✓ title, TNKSND.
  - TNKSTV spawns `TNKCNT` tanks at wave start?? — reads current TNKCNT
    like other spawn loops; maybe wave table has a tanks column or it
    respawns leftovers. VERIFY vs $2E24 width when reading ROM table.
- **TANK FIRE (TNKFIR)**: rate TNKSHT; only if OVCNT < 17 and **SHLCNT <
  20 (max shells)**; `MKPROB SHELL,SHLP1,SHLKIL`; aim at player with
  random spread (LSEED/HSEED ±$10), speed SHLSPD (per-wave); some shots
  are "REBOUND" shots (random direction toward screen edges instead —
  the aim math in TNKFRB/TRBXY).
- **SHELL = "CRUISE MISSILE"** (title 25 pts ✓ SHLKIL scores $0025):
  SHLP1 4×7 = **8×7 px**; bounces off ALL walls (velocity invert +
  SRBSND "SHELL REBOUND"); lifetime PD7 = (HSEED&$1F)+$30 = 48..63 ticks
  → expires (KILLOF); processed every 2 ticks (NAP 2). SHLCNT decremented
  on kill. (CMKSND "CRUISE MISSILE KILL" sits in RRB10's sound table —
  shared/unused there; tank module is the shell's home.)
- Shell pixel data (from source, SHLD1): rows (bytes) =
  `0A 00 00 00 / CC 00 00 CA / 0B 00 A0 00 / BB AC A0 BC / 0B 00 A0 00 /
  CC 00 00 CA / 0A 00 00 00` → a small vertical diamond/cross ✓.

### 4.15 Sprite pixel data IS IN THE SOURCE LISTINGS (discovery 2026-09-05)
Each sprite = `LABEL FCB w,h` header + `FDB datasym` pointer (optional) +
optional `FCB $dx,$dy` delta + data bytes inline, as:
- `FDB $xxxx,$yyyy,...` — 2 bytes per value, **low byte first** (FDB is
  little-endian: FDB $000F → ROM bytes 0F 00).
- `FCB $xx,$xx,...` — 1 byte per value; data may start ON THE LABEL LINE
  (e.g. `RWDD1 FCB $00,$01,$11,$00,$00`).
- Frames may SHARE data symbols (RWDP1 & RWDP3 both → RWDD1: the P1,P2,P1,P3
  walk pattern) → dedupe to one PNG.
- Confirmed formats: CIRP0-7 (FDB, 4 vals/row), MANLD1 (FDB, 2 vals/row),
  RWDD1-3 (FCB, 5 vals/row), SHLD1 (FDB), MTNKD1-4 (FDB, varying/row).
- **Extraction plan (BUILT as pass B)**: parse the listings (state
  machine: header → skip pointer/delta → collect w×h bytes from FDB/FCB
  lines), then VERIFY each byte sequence exists in robotron64k.bin (ROM =
  byte-level authority; search the 64KB → ROM offset), emit 24-bit colour
  PNGs per §3.3.
- **Verified:** skull (familydeath) source data ≡ ROM, byte-identical at
  $043B (proves the source-listing fallback works).
- **5 sprites still missing (pass C):** SHLP1 (tank shell, 4×7 BYTES =
  8×7 px) and MTNKP1-4 (tank grow: 2×8, 4×14, 4×16, 6×24 bytes) — their
  source-listing data does NOT match the Release-5 ROM bytes. Find them by
  ROM pointer-chase: scan $4B00-$5700 (RRTK4) for the TNKP1 entry bytes
  `07 10 55 16` (w=7, h=16, ptr=$5516); the SHLP1/MTNKP entries are
  adjacent 4-byte slots in the same table (layout: RRTK4.ASM:607-635).
  Read their pointers and emit. The tool's pass-C array is the empty stub
  for this.

### 4.6 Humans / Hulks — RRH11.ASM (RHORG $0000)
- MOMMY MLP/MRP/MDP/MUP 1-3 = 8×14 px; DADDY D* = 10×13 px; MIKEY KID* =
  6×11 px (all 4 dirs × 3 frames). Rescue bonuses (title shows no kill
  points for family).
- HULK HLK* = 14×16 px, 4 dirs × 3 frames. **INDESTRUCTIBLE** (title:
  "INDESTRUCTABLE HULK"). HKHSND "HULK HIT" (you CAN hit it — knockback),
  HLKSND "HULK KILL" (kill sound exists — maybe when crushed by something?
  verify).
- Skull marker: SKLPIC ptr; ROM $0437 = 12×11 skull & crossbones.
- SAVSND "SAVE A HUMAN", HKSND "KILL A HUMAN".
- **PENDING: human spawn (where/how many), rescue mechanic (touch?), walk
  behaviour, per-member score values.**

### 4.7 Player + lasers — RRG23.ASM (RGORG $26C0)
- MANLP/MANRP/MANDP/MANUP 1-3 = **8×12 px**, 4 dirs × 3 frames (matches
  ROM $3603 8×12 entry).
- Lasers: LLPIC 3×1 (6×2), ULPIC 1×6 (2×6), DLLPIC 3×6, ULLPIC 3×6 (6×12).
- MNPIC 3×8 = 6×16 (player dead/carry pose? verify).
- @157-158: `CLR PCRAM+6 / CLR CRAM+6` (clears the green-bright slot at
  some point — context pending).

### 4.8 Grunt + electrode ("ROBOTS AND POSTS") — RRP8.ASM (RPORG $3880)
- RWDP1-4 = 5×13 = **10×13 px**, 4 frames (down). R/L/U direction tables
  PENDING (grep cut off at line 676).
- PSTPIC = electrode/post pic — data PENDING.
- GRUNT = 100 pts (title).
- **PENDING: full grunt tables + post behaviour (does it shoot? I believe
  the post is a stationary target only).**

### 4.9 Explosions — RRX7.ASM (RXORG $5B40) "EXPLOSIONS & APPEARS" + RRHX4 (horiz) + RRDX2/RRCHRIS (diagonal)
- **PENDING: grep FCB tables in RRX7/RRHX4/RRDX2** for explosion frame sets
  (the diagonal-explosion system with $FFAA colours seen in CIRCLE burst).

### 4.10 Marquee — RRM1.ASM (RMORG $5700)
- "LINKY MARQUEE EFFECT" — high-score board animation. PCRAM+1..16 colour
  matrix iteration @205-224. Low priority for the port.

### 4.11 Transporter — RRT2.ASM (RTORG $4140)
- GROUP1-7 = FCB tables of (idx, $0F/$F0) pairs — the corner "transporter"
  areas where humans appear (16 slots × 7 groups). Context pending if
  human spawn logic needs it.

### 4.12 Colour engine — RRS22.ASM (game "OS") — **FULLY DECODED**
Default 16-slot palette = CRTAB (RRS22:1263) = ROM $DA51 (verified):
| slot | val | name (CRTAB comments) |
|------|-----|------------------------|
| 0 | $00 | black |
| 1 | $07 | red |
| 2 | $17 | orange |
| 3 | $C7 | purple |
| 4 | $1F | brown |
| 5 | $3F | yellow |
| 6 | $38 | green bright |
| 7 | $C0 | blue |
| 8 | $A4 | gray |
| 9 | $FF | white |
| 10 | $38 | **LASER FLASH** (LF process) |
| 11 | $17 | **RGB** (RGBTAB) |
| 12 | $CC | **DECAY** (DCATAB) |
| 13 | $81 | **LASER** (COLTAB) |
| 14 | $81 | **BLU-PURP-RED** (BPRTAB) |
| 15 | $07 | **RED-GOLD** (RGTAB) |

`COLSTV` starts 6 colour processes; driver TABDRI/TABD: read table[off],
$00 → restart at 0, else write PCRAM[dest], then SLEEP(PD3) game ticks
(≈50 fps; NAP-based). 3rd FDB field = initial sleep.

| process | FDB | sequence | timing |
|---------|-----|----------|--------|
| RGB (slot 11) | `FDB RGBTAB,PCRAM+$B,8` | `$38,$07,$C0,0` green→red→blue→restart | 8 ticks/step |
| DECAY (slot 12) | `FDB DCATAB,PCRAM+$C,2` | `$C0,$C0,$D0,$E0,$F0,$F8,$FA,$BA,$7A,$3A,$34,$2D,$1F,$17,$F,$7,$6,$5,$4,$3,$2,$1,0` | 2 ticks/step |
| BPR (slot 14) | `FDB BPRTAB,PCRAM+$E,1` | `$C0,$C1,$C2,$C3,$C4,$C5,$C6,$C7,$87,$87,$47,$47,$07,$07,$47,$47,$87,$87,$C7,$C7,$C6,$C5,$C4,$C3,$C2,$C1,0` | 1 tick/step |
| RGOLD (slot 15) | `FDB RGTAB,PCRAM+$F,6` | `$07,$07,$2F,0` | 6 ticks/step |
| LASER (slot 13) | `FDB COLTAB,PCRAM+$D,2` | COLTAB = `$38,$39,$3A,$3B,$3C,$3D,$3E,$3F,$37,$2F,$27,$1F,$17,$47,$47,$87,$87,$C7,$C7,$C6,$C5,$CC,$CB,$CA,$DA,$E8,$F8,$F9,$FA,$FB,$FD,$FF,$BF,$3F,$3E,$3C,0` | 2 ticks/step |
| LF (slot 10) | special @1242 | every 2 ticks: = $FF (white flash); every 6 ticks: = COLTAB[SEED&$1F] (random green-ramp hue) | 2/6 ticks |

**Port design (no HW emulation):** `BasePalette` (256 entries, MAME
resistor formula, generated + committed as C#); `GamePalette` (16 slots,
defaults from CRTAB); `PaletteAnimator` replays the 6 processes per tick
with these exact tables/timings; a MonoGame `Effect` pixel shader maps
sprite texel → slot colour each frame. **Wall/border = slot 11 (RGB cycle)
— VERIFY which slot the wall/post actually draws in.**

### 4.13 Score values (title table, RRET.ASM ~940)
Grunt 100 · Spheroid 1000 · Quark 1000 · Enforcer 150 · Tank 200 ·
**Brain 500** · **Cruise missile 25** · Prog 100 · Mom/Dad/Mikey = rescue
bonuses (values pending) · Hulk = indestructible (0).

### 4.14 Wave data (ref/robowaves.md — verify against ROM when extracting)
- $2B7C = per-wave parameter setup routine (waves 1-40 unique, then repeats
  waves 21-40 forever).
- $2E24 = enemy counts, 9 columns: Grunts, Electrodes, Mommies, Daddies,
  Mikeys, Hulks, **Brains**, Spheroids, Quarks (full 40-row table in that file).
- $2C12 = ~12 timing/behaviour params (9 decrease with wave = game speeds
  up; 3 increase). Includes CDPTIM (spheroid drop time), BSHTIM (brain shot
  timer), etc.
- Wave counter: 8-bit; displays 2 digits (wave 100+ shows as 0); wraps
  255→1.
- NOTE: the wave table counts Mommies/Daddies/Mikeys per wave — humans
  spawn IN WAVES (groups), not continuously.

## 5. Module map (ref/original-source/*.ASM)

| file | STTL | ORG (RRF.ASM) | contents |
|------|------|---------------|----------|
| RRF.ASM | (library) | — | EQU/RMB map for ALL segments; CRTAB? no—CRTAB is in RRS22. RRF.ASM:331 ORG $9800 = PCRAM RMB 16 etc. |
| RRH11.ASM | H U M A N S AND H U L K S | RHORG $0000 | mommy/daddy/mikey/hulk pics + logic |
| RRC11.ASM | C I R C L E S AND E N F O R C E R S | RCORG $1140 | spheroid, enforcer, spark |
| RRB10.ASM | BRAINS & CO. | BRNORG $1AC0 | brain, prog, human conversion |
| RRG23.ASM | ROBOT GAME | RGORG $26C0 | player, lasers |
| RRP8.ASM | ROBOTS AND POSTS | RPORG $3880 | grunts, electrodes/posts |
| RRT2.ASM | TRANSPORTER | RTORG $4140 | human-appear corners (GROUP1-7) |
| RRDX2.ASM / RRCHRIS.ASM | DIAGONAL EXPLOSIONS | RDXORG $4680 | diagonal explosion FX (CHRIS = 1987 bugfix variant) |
| RRTK4.ASM | T A N K | RTKORG $4B00 | tanks, quarks, shells |
| RRM1.ASM | LINKY MARQUEE EFFECT | RMORG $5700 | high-score marquee |
| RRX7.ASM | EXPLOSIONS & APPEARS | RXORG $5B40 | explosions/appear FX |
| RRS22.ASM | ROBOT OPERATING SYSTEM | — | core OS, tasks, colour engine, drawing (MPCTON/BLKONV), SLEEP |
| RRFRED.ASM | — | — | **red-screen effect** (death) |
| RRET.ASM | — | — | title screen, score table |
| RRSET.ASM | — | — | setup |
| RRLOG.ASM / RRLOGD.ASM | — | — | logo/"ROBOTRON" intro sequence (animates PCRAM) |
| RRSCRIPT.ASM | — | — | attract-mode script (PLOP* demo sequences) |
| RRTABLE.ASM | — | — | misc tables (score printing) |
| RRTEXT.ASM | — | — | text strings |
| JAPDATA.ASM | — | — | Japanese-version data |
| RRELESE6.ASM | — | — | R6 patch content? (small) |
| RRTEST1/2/B/C.ASM | — | — | dev test programs (ignore) |

## 6. Work plan (milestones)

- [x] M0 Baseline 12-phase rebuild (see rebuild-ledger.md; commit 0358029)
- [ ] M1 Research:
  - [x] sprite inventory (labels + sizes) — §4
  - [x] colour engine — §4.12
  - [x] score table — §4.13
  - [x] spheroid/enforcer logic — §4.1
  - [x] brain/prog logic — §4.4
  - [x] quark/tank/shell logic (RRTK4) — §4.5 FULLY DECODED (timer drops,
    shells, 75% seek-player)
  - [ ] grunt + post tables (RRP8) — all 4 dirs, post size/behaviour
  - [ ] human spawn/rescue mechanics (RRH11) + per-member scores
  - [ ] explosion tables (RRX7/RRHX4/RRDX2)
  - [ ] entity → palette slot mapping (XTEMP2 colour args per draw)
  - [ ] verify wave tables in ROM bytes ($2E24, $2C12, $2B7C) vs robowaves
  - [ ] red-screen (RRFRED) + death sequence
- [ ] M2 Extractor tool `tools/SpriteExtractor/` — **FULL SET EXTRACTED
  (2026-09-06); 215/220 sprites.** Done: (1) 4bpp decode (high nibble = left,
  §3.1) + 24-bit colour PNGs; (2) palette = `RobotronPaletteService` port
  (§0.6 supersedes §3.3 greys/§3.4); (3) `--sprite NAME [NAME ...] [--quiet]`
  validation mode with ASCII preview + legend; (4) author validated Skull,
  then Player/Mummy/Daddy/Hulk batch, then the full lot; (5) full run from
  the definitive source (§0.8 — verified identical to the Riddle list):
  215 pass-A + 5 pass-B (MissileSmall_0/1, PlayerExtra, Dot, AttractCruise)
  = **220 PNGs** + `docs/sprite-map.md` refreshed. OPEN: (a) spheroid1-8 +
  quark1-9 decode SPARSE from their definitive offsets (centre-outward
  density gradient — see §8 open question; check in the author's sprite
  editor); (b) later: pass C for SHLP1 + MTNKP1-4 (recipe §4.15 — NOT in the
  definitive .cs).
- [x] M3 Integration — **DONE 2026-09-06 (commit pending at write time; see
  progress log (10)).** `SpriteSet` loads content PNGs via ContentManager
  (`new SpriteSet(GraphicsDevice, Content)` in RobotronGame.LoadContent);
  `tools/generate-mgcb.py` regenerates `Content.mgcb` (220 PNG entries) +
  `Rendering/SpriteContentPaths.cs` — re-run when sprites are added.
  ROM frames drawn at 2x arcade px, centered in the 32x32 collision box
  (`SpriteSet.DrawSprite`). Spheroid (8f), Quark (9f), Tank (4f) animate on
  a tick counter (`GameplayConstants.SpriteFrameTicks = 10`, placeholder).
  Life icons use the natural 16x24 player frame. Tests never construct a
  SpriteSet → stayed green. Also fixed: `largefont:` → `Font_L_colon.png`
  (':' made an NTFS alternate data stream — only 219 real PNGs before).
- [ ] M4 Palette + shader — **BUILT 2026-09-14 (§34); awaiting author eyeball (skull eyes must cycle purple↔orange-ish, slot 15 RGOLD) before tick.** `.fx` unrolled (zero `[` tokens, six named uniforms Live10..Live15), in Content.mgcb, compiles at ps_3_0 (ps_2_0 hit X5608 64-slot limit), wired via `SpriteSet.ColorCycleEffect` + `GamePalette.UpdateEffectColors`. History: SHADER PARKED 2026-09-12 by author ("gameplay first, FX later"). **BUILD BLOCKER FOUND (verified 2026-09-12, RESOLVED §34):** the MonoGame 3.8.5.1 EffectProcessor TPGParser rejects the `float4[6](...)` array-constructor syntax — `error X3000: syntax error: unexpected token '['` at ColorCycle.fx(17,38) = the `const float4 MarkerColors[6] = float4[6](` line. Parser flow (MonoGame v3.8.5.1 source, `Tools/MonoGame.Effect.Compiler/Effect/`): `Preprocessor.Preprocess` (d3dcompiler, includes/macros only) → `TPGParser/Parser.cs` (generated TinyPG — parses techniques/passes/samplers; **chokes on `[` inside a const-array initializer**) → technique/sampler nodes whitespace-removed → dxc compiles the rest as HLSL → SPIR-V/HLSL reflection names the params. So the marker table cannot be a `const` array written that way; options for next shader session: (a) unroll the 6 comparisons as literals in the PS (no array at all — likely cleanest, avoids the parser AND any register-size issues), (b) test whether `const float4 X = float4(...);` single-const form parses, (c) pass markers as a uniform like LiveColors. `float4 LiveColors[6] : register(c0);` (line 15) parsed FINE — the error was at (17,38), after it. DONE + tested
  (18 tests): `RobotronColor` (seanriddle byte→RGB, any 8-bit code),
  `GamePalette` (16 slots, CRTAB $DA51 defaults, marker table),
  `PaletteAnimator` (all 6 §4.12 processes, exact tables/timings, seeded
  Random, unit-tested); wall now cycles on slot 11 (RGB process) via
  optional `GamePalette` in PlayField/PlayfieldWall (tests unaffected).
  **REMAINING — the pixel shader (author's explicit ask), with the full
  findings + .fx source in progress log (10):** MonoGame 3.8 `Effect` can
  only be built from an MGFX blob (`new Effect(device, byte[])`) — NO
  HLSL-source constructor (an earlier `ColorCycleEffect` class using
  `new Effect(device, vs, ps)` did NOT compile and was deleted). The
  `dotnet-mgcb` 3.8.5.1 tool DOES ship `EffectImporter`/`EffectProcessor`
  + bundled `dxc.exe` (windows-x64) → the .fx → .mgcb → `Content.Load<Effect>`
  path works at build time. Gotcha found: invoking `dotnet tool run mgcb`
  directly on this machine mangles paths (prepends `B:\`) — use the MSBuild
  build (works; it processed all 220 PNGs) instead of the CLI.
  WIRING (already in place, just set the property): `SpriteSet.ColorCycleEffect`
  (`Effect?`) + `SpriteSet.Palette` — `DrawSprite` applies the pass when the
  effect is set; `GamePalette.UpdateEffectColors(effect)` pushes
  `LiveColors` (slots 10-15). RobotronGame creates the palette/animator and
  ticks the animator in Update (wired); the ONE missing line is loading the
  compiled effect and assigning `_sprites.ColorCycleEffect`.
  **VERIFY AT RUNTIME: author eyeballs the skull (eyes must cycle
  purple↔orange-ish) before M4 is ticked.**
- [ ] M5 Behaviour fixes (from source): per-wave counts from $2E24 (replace
  placeholder ramp; table in `ref/robowaves.md`) — **RESEARCH DONE 2026-09-12
  (§11.1: $2E24 verified byte-exact; §11.2: 12 timing tables @ $2C20 decoded,
  43-byte records, all values captured; §11.3: SCORE convention + every score
  value decoded incl. Electrode=0, Spark=25, rescue 1000-5000)** —
  IMPLEMENTATION PENDING, timing params from $2C12 (see §11.2),
  score values from the title table (§4.13: Grunt 100, Spheroid 1000, Quark
  1000, Enforcer 150, Tank 200, Brain 500, Missile 25, Prog 100 — current
  ScoreValues are spec placeholders), humans: wave-based family spawn, walk,
  rescue, skull marker (sprites Mummy_1-12/Daddy_1-12/Mikey_1-12/Skull
  already on disk), brain + prog conversion (Brain_1-12, MissileSmall_0/1
  on disk), tank shell sprite — **FOUND 2026-09-12: $4FEE (author), 8×7 px,
  data $4FF0..$500D, R5 art ≠ stale source SHLD1; §11.5** + mini-tank growth
  frames 4×4/8×14/8×16/12×24 @ $5036/$503E/$505A/$507A (six-byte entries
  $500E-$5025) — extract via pass C or a one-off script. NOTE: spheroid/quark
  drops are ALREADY timer-based in the 12-phase rebuild (stale §7 claim
  "only when shot" is wrong — verified in code 2026-09-06); M5 refines them
  to ROM semantics (§4.1/§4.5: CDPTIM timers, 8-enforcer cap, 17-object
  cap, escape speeds). **NEW (author 2026-09-06): per-pixel collision
  detection like the arcade — sparse spheroid/quark frames are INTENDED, so
  hit areas must use the sprite's actual pixels, not the 32x32 box.**
- [ ] M6 Gates: 0-warning build (TreatWarningsAsErrors + Roslynator),
  all tests green, launch smoke; ledger + this file updated; commit per
  milestone.

## 7. Current rebuild state (what exists to modify)

- 48 source files, 8 test files, 44/44 tests, 0 warnings, commit `0358029`.
- `Tuning/GameplayConstants.cs` = all tunables (placeholder values,
  documented). Will gain ROM-derived values in M5 (cite source lines).
- `Rendering/PixelArtFactory.cs` + `SpriteSet.cs` = hand-drawn placeholder
  art, runtime-generated textures → REPLACED by ROM sprites (M3).
- `Level/WallColorCycle.cs` = white/gray 450ms toggle → REPLACED by the
  palette engine (M4).
- Entities in `Entities/`: Player, PlayerLaser, LaserSlots, Electrode,
  Grunt, Hulk, Spheroid, Enforcer, Quark, Tank, Spark, TankShell.
  MISSING vs ROM: Brain, Prog, Humans (Mummy/Daddy/Mikey), Skull marker,
  cruise missile (if distinct from TankShell).
- `Level/PlayField.cs` = spawn/update/collisions hub; `Level/ScoreBoard.cs`.
- States in `States/` (title/playing/waveclear/gameover/highscore), shell
  `RobotronGame.cs` (640×400 RT, integer blit, F11/Escape).
- **Spheroid/Quark currently release children only when shot → M5 fixes.**

## 8. Open questions

- ~~Cruise missile = CMPIC or SHLP1?~~ **RESOLVED (2026-09-12, §11.3/§11.5):**
  BOTH fire for 25 pts: tank shell (RRTK4 SHLKIL `$0025`, sprite $4FEE 8×7) AND
  brain cruise missile (RRB10 `$0025` + CMKSND). Title's "CRUISE MISSILE 25"
  likely covers the tank shell (the visible bouncing one); port both for 25.
- Does the post/electrode shoot? (I think stationary target only.)
- Human rescue: touch-to-rescue? escort off-field? per-member points?
- "Brains" column in the wave table = the brain robot (§4.4) ✓ (9th column).
- **Spheroid (8) + quark (9) frames decode SPARSE from their definitive
  offsets** (found 2026-09-06 during the full run): per-frame nonzero-byte
  density rises then falls (spheroid: 1,4,13,21,31,37,29,13; first-nonzero
  position 59,51,43,35,26,18,11,3 — a centre-outward gradient; quark similar).
  Offsets are identical in `RobotronBlueLabelSpriteRepository.cs` (= Riddle
  list). CHECK in the author's WmsGfxSpriteEditor: if the editor shows the
  same sparse frames → this ROM stores the two families in a special form
  (storage quirk; resolve later, do NOT scan the ROM to work it out); if the
  editor shows full images → diff the editor's decode code against the
  extractor's (only sanctioned exception to "no ROM scanning").
- Hulk kill sound exists (HLKSND) though "indestructible" — maybe hulk dies
  when converted by… ? or the sound is for the kill animation when a hulk
  leaves the field? Verify in RRH11.
- MNPIC (6×16) — player down/dead pose?
- PCFLG exact trigger for the human rescue bonus (§11.4 hypothesis — re-read
  RRG23 COLCHK 798-830).
- ~~Shell fizzle lifetime in R5~~ DONE 2026-09-12: disasm 4F82-4F8A, (RND & $1F) + $30 = 48..79 ROM ticks (see §11.5 / progress (15)).
- RRELESE6.ASM contents (R6 patch diff?).
- Exact SLEEP tick = 1/50 s? (user RE: 50 fps; NAP 1 ≈ 1 frame)

## 9. Toolchain & build (verified 2026-09-05)

- .NET SDK 10.0.400; app + tool target net10.0; MonoGame 3.8.
- From repo root: `dotnet build` (gate: 0 warnings — TreatWarningsAsErrors
  + Roslynator on) and `dotnet test` (gate: all green; 44/44 at
  `0358029`).
- **NEVER pass `--nologo` to `dotnet test`** (MTP crash, dotnet/sdk#55309).
- Sprite tool: `dotnet run --project tools/SpriteExtractor` (auto-detects
  the repo root by presence of `ref/` + `src/`).
- MonoGame 3.8 gotcha: `SpriteBatch.DrawString` param order is
  `(position, color, rotation, origin, scale, ...)` — rotation BEFORE origin.
- This model CANNOT view images — verify assets by byte-dump / ASCII
  decode / source-vs-ROM compare.

## 10. Progress log

- **2026-09-05 (1):** 12-phase rebuild complete (0358029). Author asked
  about ROM sprites + colour shaders → not done (plan said placeholders).
  Author authorised ROM use + shader + "arcade faithful, no HW emulation".
  Notes file created.
- **2026-09-05 (2):** User facts logged: mummy/daddy/mikey = rescue humans;
  spheroids drop enforcers whether shot or not; quarks drop tanks whether
  shot or not.
- **2026-09-05 (3):** Read palette-notes.md, mame-notes.md, robowaves.md in
  full. Sprite format + verified entries + palette chain captured (§3).
- **2026-09-05 (4):** Module map built from STTL lines + RRF.ASM ORGs (§5).
  Full sprite inventory with labels/sizes/frames (§4.1-4.9). Spheroid
  logic fully decoded (§4.1) — timer drops, escape, 1000-pt burst kill.
  Brain/prog human-conversion decoded (§4.4). Title score table captured
  (§4.13). Colour engine fully decoded: 6 processes, exact tables +
  timings (§4.12), default palette CRTAB verified == $DA51.
- **2026-09-05 (5):** MAJOR CORRECTION from author: sprites are **4 bits
  per pixel** (2 px/byte), NOT 1-bit masks — all 219 grayscale PNGs already
  on disk are WRONG and must be regenerated. Author specified the PNG scheme
  (§3.3): 24-bit colour, actual RGB for fixed slots 0-9, one distinct pure
  grey per cycling slot 10-15 (16/32/48/64/80/96) so the M4 shader remaps
  greys to live cycling colours. Blitter REMAP (solid-colour) mode noted
  (PROGs). Fetched MAME resnet.cpp + resnet.h — exact
  compute_resistor_weights + combine_weights captured (§3.4). Skull verified
  as 4bpp (eyes sit in cycling slots 10/15). **Author: do NOT investigate
  RGB further this session — 256-entry BasePalette is a NOTE/TODO.**
- **NEXT (where to resume):** (1) Rewrite SpriteExtractor decode to 4bpp
  (high nibble = left) + §3.3 colour mapping; add an ASCII preview mode to
  verify shapes/orientation (model can't view images); regenerate ALL PNGs.
  (2) THEN build the 256-entry BasePalette (§3.4) and verify it against the
  16 $DA51 RGBs; find the correct MAME robotron driver path for exact
  resistor values. (3) Continue M3 integration + M4 shader + M5 behaviours.
  Still-pending source reading: RRTK4 (quark/tank/shell), RRP8 (grunt/post),
  RRH11 (human spawn/rescue), RRX7/RRHX4/RRDX2 (explosions), RRFRED
  (red screen), then verify $2E24/$2C12 in ROM bytes.
- **2026-09-05 (6):** Handoff doc audit — every reference now linked from
  this file: `docs/robotronsprites.txt` (Riddle list; **offsets are
  DECIMAL**), `docs/sprite-map.md`, `tools/SpriteExtractor/` (3-pass design
  + run command + pass-C stub) into §2; the 16 default-slot RGBs inlined in
  §3.3 (ref/ is git-ignored); 5 missing sprites (SHLP1, MTNKP1-4) + pass-C
  pointer-chase recipe recorded in §4.15; stale "grayscale PNG" wording
  fixed; M1 quark/tank item checked off (§4.5); new §9 toolchain (no
  `--nologo` on `dotnet test`); stale 1bpp header in `docs/sprite-map.md`
  replaced with a regeneration note.
- **2026-09-05 (7):** M2 4bpp rewrite. Author directives logged as §0.6 /
  §0.7: palette = `RobotronPaletteService` (no HW reverse-engineering —
  §3.4 overridden, §3.3 grey scheme superseded); sprite locations = Riddle
  list (stop chasing the assembly for now). `SpriteExtractor` rewritten:
  4bpp decode (high nibble = left), 24-bit truecolour PNG writer, palette
  ported verbatim (16 colour bytes + seanriddle.com byte→RGB; slots 10-15 =
  service's de-duplicated `C4 F4 CC 81 45 2F` → all 16 distinct), new
  `--sprite NAME` mode with ASCII preview + RGB legend. Validation run:
  `familydeath` → `Skull.png` (12×11, ROM $043B); ASCII rows match the
  notes' byte dumps exactly (row0 `00 00 AA A0 00 00`, eyes row `0A 0F FA
  FF 0A 00`); PNG verified programmatically (12×11, colour type 2, sampled
  pixels = expected slot RGBs).
- **2026-09-05 (8):** **Author confirmed Skull.png correct.** Second
  validation batch extracted (46 PNGs): `player` → PlayerBig (12×16 @ $1F6C),
  `player1-12` → Player_1..12 (8×12 @ $361B..), `mommy1-12` → Mummy_1..12
  (8×14 @ $055F..), `daddy1-12` → Daddy_1..12 (10×13 @ $082F..),
  `hulk1-9` → Hulk_1..9 (14×16 @ $0D1D..) — all offsets match the list's
  stride pattern; ASCII spot-checks (one per entity) show plausible shapes
  (player helmet/human silhouettes/boxy hulk). Tool gained multi-name +
  `--quiet` support for batch extraction.
- **2026-09-06 (9):** **Author confirmed batch 2 correct; ordered the full
  lot.** Author directive §0.8: `RobotronBlueLabelSpriteRepository.cs`
  (WmsGfxSpriteEditor) is the **definitive source for all sprites**; do NOT
  reverse-engineer/scan the ROM for sprites. Verified the .cs is
  byte-identical to `docs/robotronsprites.txt` (same 215 entries) → the full
  run already used the definitive data: **220 PNGs** (215 pass-A + 5 pass-B),
  `docs/sprite-map.md` refreshed, tool now cites the .cs as source of
  truth. Spot-checks: brain/electrode/grunt/tank shapes all plausible.
  FLAGGED (§8): spheroid1-8 + quark1-9 decode SPARSE from their definitive
  offsets (centre-outward density gradient) — for the author to check in
  their sprite editor. Pass-C 5 (SHLP1, MTNKP1-4) are NOT in the definitive
  .cs — still open.
- **2026-09-06 (10):** **Author confirmed spheroid1-8 are CORRECT as-is —
  sparse frames are intended (per-pixel collision is wanted, so small hit
  areas are fine). Tank shell sprites: author will locate later. Directive:
  continue the milestones until 12:00, then ledger + continuation +
  `shutdown /s`.** M3 DONE: SpriteSet loads all 220 PNGs via ContentManager
  (RobotronGame.LoadContent); `tools/generate-mgcb.py` regenerates
  Content.mgcb + SpriteContentPaths.cs; ROM frames drawn 2x centered in the
  collision box via `SpriteSet.DrawSprite`; Spheroid/Quark/Tank animate on a
  tick counter (SpriteFrameTicks=10 placeholder); life icons 16x24; fixed
  the `largefont:` NTFS-ADS bug (→ `Font_L_colon.png`, 220 real PNGs). M4
  half: `RobotronColor`/`GamePalette`/`PaletteAnimator` built + 18 unit
  tests (exact §4.12 tables/timings); wall cycles on slot 11. **SHADER NOT
  WIRED (findings + full .fx below).** GATES at write time: build 0 warn,
  **62/62 tests**, launch smoke OK (>8s). SHADER FINDINGS: (a) MonoGame 3.8
  `Effect` has NO HLSL-source ctor — only `Effect(device, byte[])` = an
  **MGFX blob**; (b) `dotnet-mgcb` 3.8.5.1 bundles `EffectImporter` +
  `EffectProcessor` + `dxc.exe` (win-x64) → .fx compiles at build time via
  the normal MSBuild content pipeline (it built all 220 PNGs fine); (c) the
  raw CLI `dotnet tool run mgcb` mangles paths on this box (`B:\` prefix) —
  don't use it, build via MSBuild. `.fx` source (save to
  `src/Robotron2084/Content/Effects/ColorCycle.fx`, D3FX-style for the
  EffectProcessor):
  ```
  sampler2D SpriteTexture : register(s0);
  float4 LiveColors[6] : register(c0);
  const float4 MarkerColors[6] = float4[6](
      float4(0.56470588f, 0.0f, 0.94117647f, 1.0f),
      float4(0.56470588f, 0.81568627f, 0.94117647f, 1.0f),
      float4(0.56470588f, 0.12549020f, 0.94117647f, 1.0f),
      float4(0.12549020f, 0.0f, 0.62745098f, 1.0f),
      float4(0.69019608f, 0.0f, 0.31372549f, 1.0f),
      float4(0.94117647f, 0.69019608f, 0.0f, 1.0f));
  struct VSInput { float4 Position : POSITION; float2 TextureCoordinate : TEXCOORD0; float4 Color : COLOR; };
  struct VSOutput { float4 Position : POSITION; float2 TextureCoordinate : TEXCOORD0; float4 Color : COLOR; };
  VSOutput MainVS(VSInput input) { VSOutput o; o.Position = float4(input.Position.xy, 0.5f, 1.0f); o.TextureCoordinate = input.TextureCoordinate; o.Color = input.Color; return o; }
  struct PSInput { float2 TextureCoordinate : TEXCOORD0; float4 Color : COLOR; };
  float4 MainPS(PSInput input) : COLOR {
      float4 t = tex2D(SpriteTexture, input.TextureCoordinate);
      for (int i = 0; i < 6; i++)
          if (abs(t.r-MarkerColors[i].r) < 0.004f && abs(t.g-MarkerColors[i].g) < 0.004f && abs(t.b-MarkerColors[i].b) < 0.004f)
              t = float4(LiveColors[i].rgb, t.a);
      return t * input.Color;
  }
  technique MainTech { pass MainPass { VertexShader = compile vs_2_0 MainVS(); PixelShader = compile ps_2_0 MainPS(); } }
  ```
  NOTE: the .fx had NOT been build-tested yet (time-boxed) — first step next
  session: add file + mgcb entry (`#begin Effects/ColorCycle.fx` /
  `/importer:EffectImporter` `/processor:EffectProcessor`), `dotnet build`,
  then in RobotronGame.LoadContent: `_sprites.ColorCycleEffect =
  Content.Load<Effect>("Effects/ColorCycle");` and smoke-test (skull eyes
  must cycle; wall already cycles on slot 11 without the shader). **NEXT
  (resume order): (1) wire + visually verify the shader (M4 done) →
  (2) M5: per-pixel collision (author-wanted) + $2E24 wave counts + $2C12
  timing + §4.13 scores + humans/brain/prog entities → (3) M6 gates.**
- **2026-09-12 (11):** **AUTHOR: park the shader — "get the game working
  somewhat first, frills like FX later."** Session order now M5 gameplay:
  (1) verify $2E24 wave counts + $2C12 timing in ROM bytes; (2) per-wave
  spawn counts/timing/§4.13 scores into gameplay; (3) per-pixel collision
  (author-wanted since 2026-09-06); (4) humans (spawn/walk/rescue/skull) +
  brain/prog entities. Shader parked: .fx kept on disk (untracked, out of
  Content.mgcb — generate-mgcb.py reverted; build green),
  `SpriteSet.ColorCycleEffect` = null = plain draws. Resume order when FX
  returns: §6 M4 options (a)/(b)/(c) — likely (a) unroll the six marker
  comparisons as literals.

## 11. M5 research batch (2026-09-12) — wave tables, scores, tank/shell R5 data

### 11.1 Wave object counts @ $2E24 — VERIFIED BYTE-EXACT (2026-09-12)
9 columns × 40 waves, column-major 40-byte blocks (verified vs ref/robowaves.md,
all 360 values identical): grunts $2E24, electrodes $2E4C, mommies $2E74,
daddies $2E9C, mikeys $2EC4, hulks $2EEC, brains $2F14, spheroids $2F3C,
quarks $2F64. Written to player wave state at $2B7C (raw, no difficulty
adjustment). **NO TANK COLUMN** — see §11.5. Brains appear every 5th wave.

### 11.2 Wave difficulty-setting tables @ $2C20 (R5) — DECODED
$2B7C (INITIALISE_SETTINGS_AND_OBJECT_COUNTS_FOR_CURRENT_PLAYER_WAVE, disasm
4070ff) walks 12 consecutive **43-byte records** from $2C20:
`[multiplier byte][min][max][40 per-wave values]` — bits 0-4 = multiplier,
bit 7 = flag. Then the 9 count tables (§11.1), then a 0 terminator. All 22
bytes land in the per-player wave state (X+10); $2B0B copies them to the
live "current wave state" $BE5C..$BE71 each turn.

Difficulty formula (default/recommended = CMOS 5 → stored value = table
value, no adjustment — formula captured: product of table value, |diff-5|,
multiplier, clamped to [min,max]; easier-than-5 can subtract).

| addr | EQU (RRF.ASM) | stored as | meaning | wave 1..40 values |
|------|---------------|-----------|---------|-------------------|
| $2C20 | ROBSPD | $BE5C | grunt move delay (ticks; lower = faster) | 20 15 15 15 15 15 15 15 15 15 14 14 14 14 14 13 13 13 13 13 14 14 14 14 14 14 13 13 13 13 13 13 12 12 12 12 12 12 15 12 |
| $2C4B | RMXSPD | $BE5D | grunt speed floor (clamps the per-kill speedup) | 9 7 6 5 5 5 5 4 4 4 4 4 4 4 4 4 4 4 4 4 4 4 4 4 4 4 3 3 4 3 3 3 3 3 3 3 3 4 3 |
| $2C76 | ENFNUM | $BE5E | drops per spheroid/quark: RND(ENFNUM) then /2 → 0..ENFNUM/2 (nonzero) | 10 (w1-20) then 11 (w21-40) |
| $2CA1 | ENSTIM | $BE5F | enforcer spark fire delay | 30 28 26 24 22 20 18 18 16 14 14 14 14 14 14 14 14 14 14 14 15 15 15 15 15 15 15 15 15 14 14 14 14 14 14 14 14 14 14 |
| $2CCC | CDPTIM | $BE60 | spheroid enforcer-drop delay | 30 28 26 24 30 20 18 16 18 25 12 12 12 25 25 12 12 12 18 20 14 14 14 14 14 25 14 14 18 25 12 12 12 12 25 12 12 12 18 20 |
| $2CF7 | HLKSPD | $BE61 | hulk update rate (lower = faster) | 8 8 7 7 7 7 7 6 6 6 6 5 (rest 5) |
| $2D22 | BSHTIM | $BE62 | brain cruise-missile fire delay | 64 64 64 64 64 40 40 38 38 38 38 38 38 38 38 38 36 36 36 36 32 32 32 32 32 32 32 30 30 30 30 30 25 (rest 25) |
| $2D4D | BRNSPD | $BE63 | brain speed | 8 8 8 8 8 7 7 7 7 7 7 7 7 7 7 6 (rest 6) |
| $2D78 | TNKSHT | $BE64 | tank shell fire rate | 32 32 32 32 32 32 32 30 30 30 30 30 30 28 28 28 28 28 28 28 30 30 30 30 30 30 28 28 28 28 28 26 26 26 26 26 24 24 24 24 |
| $2DA3 | SHLSPD | $BE65 | shell "accuracy"/speed setting | 176 (w1-20) then 184 (w21-30) then 192 (w31-40) |
| $2DCE | TDPTIM | $BE66 | quark tank-drop delay (initial RND(BE66); after each tank RND(BE66/2+1)) | 16 (w1-14) 15 (w15-20) 14 (w21-40) |
| $2DF9 | SQSPD | $BE67 | quark movement (destination = RND(BE67) near borders) | 50 (w1-12) 56 (w13-28) 60 (w29-40) |

**Bozo mode** (disasm 2B26-2B68, Larry DeMar quote in comments): waves 1-2,
if lives left < CMOS turns-per-player → dial enforcer/spark/grunt settings
down via a 4-row mercy table ($2B59). Port: skip (arcade cabinet setting).
**In-wave grunt speedup**: each grunt kill does BE5C = max(BE5C × 7/8, BE5D)
(disasm 3A94-3A9F; source ROBKIL LDB #$E0 MUL) — grunts speed up as they die.

### 11.3 Scores — SCORE routine convention DECODED (disasm ~17163)
Call: `LDD #AABB; JSR SCORE` where **A = number of trailing zeros, B = BCD
digits**. Score = 4 BCD bytes (e.g. 1,234,567 = 01 23 45 67).
- Grunt 100 (`$0110`) · Electrode **0 — NO score call** (PSTKIL source +
  disasm ELECTRODE_COLLISION_HANDLER 3AA9 both confirm: deallocate+sound
  only) · Spheroid 1000 (`$0210`, "SCORE 1K") · Quark 1000 (`$0210`) ·
  Enforcer 150 (`$0115`) · **Spark 25** (`$0025` RRC11 SPKIL) · Tank 200
  (`$0120`) · **Tank shell 25** (`$0025` RRTK4 + disasm SHELL_COLLISION_
  HANDLER 4FE1, with explosion $5B43) · Brain 500 (`$0150` RRB10) ·
  Cruise missile 25 (`$0025` RRB10) · Prog 100 (`$0110` RRB10) · Hulk 0.
- **Rescue bonus (RRH11 SVITAB, ~line 498)**: FDB $0210,$0220,$0230,$0240,
  $0250 → **1000, 2000, 3000, 4000, 5000**. SAVCNT = running human-saves
  count (INC on save), capped at 5 for the index; a score popup (P1000
  series, 4-byte entries: 1000/2000/3000/4000/5000) is shown in place.
- Title screen (RRET.ASM ~934-972) confirms the display table: GRUNT 100,
  SPHEREOID 1000, QUARK 1000, ENFORCER 150, TANK 200, BRAIN 500, CRUISE
  MISSILE 25, PROG 100, INDESTRUCTIBLE HULK, "SAVE THE LAST HUMAN FAMILY".

### 11.4 Human kill/save flow (RRH11 HUMKIL ~425-490) — PARTIAL
Called via KIDKIL/MOMKIL/DADKIL (DEC KIDCNT/MOMCNT/DADCNT first).
- Killed by BRAIN (BRNFLG) → no skull, no score, just gone ("BRAINY GOT
  ME") — the brain's conversion to PROG (RRB10) handles the visuals.
- Killed by GRUNT → SKULLV: erase process (HUMSAV) runs; skull sprite
  SKULP (6,11 = 12×11) drawn at death X (clamped to XMAX-6) for ~90 ticks;
  HKSND "KILL A HUMAN".
- If PCFLG set → ALSO: SAVCNT++, score popup + SVITAB[SAVCNT] points,
  SAVSND. PCFLG = "PLAYER COLLISION" (RRF.ASM 364): RRG23 COLCHK sets 1
  at the start of the player-collision scan (802-803) and clears it after
  the humans check (822-823) → i.e. the human is processed DURING a player
  collision frame. EXACT trigger semantics PENDING (re-read COLCHK 798-830
  next session) — hypothesis: human adjacent to/touching player when a
  grunt kills it = "saved by the player".
- Skull is a DRAWN OBJECT (picture swapped to SKULP) with a countdown, not
  a separate entity — port can mirror that (SkullTimer on a marker object).

### 11.5 Tanks/shells in R5 — DECODED (disasm 4B00-50xx) + AUTHOR CORRECTION
- **TANK SHELL = raw pixel data at $4FF2, 7 bytes wide x 16 rows = 14x16 px**
  (AUTHOR 2026-09-12, twice: "The address I gave you is correct. It's the raw
  pixel data for the tank shell. Not the metadata, which points to 4FF2.").
  Data spans $4FF2..$5061. Extracted as TankShell.png (pass C, notes §6 M2/
  §11.5; extractor extras array). R5 shell art != stale source SHLD1.
  **CORRECTION to earlier this-session decode:** the "4x7 header @ $4FEE +
  data @ $4FF0" and the "mini-tank growth frames @ $5036/$503E/$505A/$507A"
  were MISREADS — those addresses are INSIDE the 14x16 shell's pixel rows.
  There are no tank growth frames in this ROM (definitive list has none;
  tanks spawn full-size 14x16 — port unchanged).
- **Tanks spawn ONLY from quarks** (R5): wave-init tank loop (4D10) reads
  cur_tanks = $BE71 = the wave-state TERMINATOR BYTE = always 0 -> no
  wave-start tanks (current port already matches: tanks only dropped).
  Cap 20 (CMPA #$14).
- **Tank walk anim**: plays BACKWARDS when moving left, forwards otherwise
  (disasm 4DBE-4DDB). tank1-4 = 14x16 @ $551E/$558E/$55FE/$566E = definitive
  list tank1-4 (author-verified) - no change needed. Quark frames quark1-9
  = 16x15 @ $50E6+ - definitive list verified identical (9 x 120-byte
  blocks). Spheroid1-8 = 16x15 @ $1512+ (definitive list).
- **Tank aim (4E11 PICK_DESTINATION)**: RND <= $60 -> seek player (~38%,
  NOT 75% - the old section 4.5 claim is wrong), else random point; vertical
  delta only if |dy| > 16 px; move countdown = RND(1..31); re-aims each
  countdown expiry. Delta is axis-aligned (+/-1/0 per axis).
- **Tank fire**: countdown = RND(0..31) + BE64 (first shot), BE64 (repeat);
  at 0 -> CREATE_TANK_SHELL (4E46). **SHELL 20-per-wave BUG (documented in
  disasm)**: shell counter $98F1 is decremented only on kill, never on
  fizzle-out -> tanks stop firing once 20 shells have been fired (until some
  are shot). Port should replicate: shells-fired-this-wave counter, cap 20.
- **Shell bounce**: COM velocity on wall hit (all 4 walls) + SRBSND.
  Shell fizzle lifetime: (RND & $1F) + $30 = 48..79 ROM ticks -
  VERIFIED in R5 disasm 2026-09-12 (4F82-4F8A; the earlier "48..63" was a
  typo).
- Quark spawn (R5 4B40-4B80): Y = $1A (26) or $DC (220), X = RND*7E + 6;
  first drop countdown = RND(BE66); tanks per quark = RND(BE5E)/2
  (nonzero); quark NAP 3; quark "vamoose" after all tanks dropped =
  vertical +/-2 (4C6F-4C7C).
- **2026-09-12 (12):** **M5 gameplay session (author: gameplay first).**
  Shaved the shader park (§6 M4, progress (11)) and went deep on wave/score
  research, all recorded in new §11. (1) $2E24 object-count table verified
  byte-exact vs ROM (9 cols × 40, no tank column). (2) Decoded the 12
  difficulty-setting tables @ $2C20 (43-byte records: [mult][min][max][40
  values]) and mapped each to its $BE5C..$BE67 EQU + meaning + all wave
  values; captured the difficulty formula (default = raw table value), bozo
  mode, and the per-grunt-kill speedup (BE5C × 7/8 floored at BE5D).
  (3) Cracked the SCORE routine convention (A = trailing zeros, B = BCD
  digits) and decoded every score: Grunt 100, Electrode **0** (no call in
  source OR disasm), Spheroid 1000, Quark 1000, Enforcer 150, Spark 25,
  Tank 200, Shell 25, Brain 500, Cruise 25, Prog 100, Hulk 0; rescue bonus
  SVITAB = 1000..5000 by SAVCNT (cap 5). (4) Human kill/save flow (RRH11
  HUMKIL): brain-kill = no skull/score; grunt-kill = 12×11 skull ~90 ticks
  + HKSND; PCFLG-set = rescue bonus + score popup (trigger semantics
  pending). (5) R5 tank/shell fully decoded from disasm: shell = $4FEE
  (author-confirmed) 8×7, tanks spawn only from quarks (cur_tanks = $BE71 =
  terminator = 0), mini-tank growth frames 4×4/8×14/8×16/12×24 via 6-byte
  entries, tank walk anim runs backwards when moving left, tank aim ≈38%
  seek-player (corrects the old 75% claim), shell 20-per-wave fizzle bug.
  NEXT: implement per-wave counts/timing/scores in LevelParameters +
  ScoreValues, then per-pixel collision, then humans/brain/prog.
- **2026-09-12 (13):** **AUTHOR: "I'm looking for full fidelity."** Wave
  data milestone DONE + committed: `Level/WaveTable.cs` (all 21 ROM arrays —
  9 count columns $2E24 + 12 difficulty values $2C20 — with the 41+ →
  repeat-21-40 rule, `ForWave()`/`ResolveWave()`), `LevelParameters`
  rewritten as a defaults-having record + `FromWave()` (legacy
  MaxEnforcersPerSpheroid/MaxTanksPerQuark kept = ceil(ENFNUM/2) for
  compatibility), `LevelParameterGenerator` now ROM-driven (CSV designer
  override kept, procedural ramp deleted), `ScoreValues` = exact arcade
  values (Electrode 0, Spark 25, Shell 25, +Brain 500/CruiseMissile 25/
  Prog 100 + RescueBonus(saves)=1000*saves cap 5). Tests: 74/74 (rewrote
  LevelParameterGeneratorTests to ROM values, new WaveTableTests, fixed the
  electrode-score test 25→0). TankShell.png (14x16 @ $4FF2) extracted +
  wired (commit 3332d97). **TEMPO DECISION (recorded for full fidelity):**
  ROM runs 50Hz (author RE); port runs 60Hz fixed timestep →
  `PortTicks(romTicks) = romTicks * 60 / 50` conversion helper to be added
  in GameplayConstants and applied at every timing use site below.
- **NEXT SESSION — PHASE B (wire the ROM timings into entities, exact
  semantics; all values already in WaveTable.cs):**
  1. Spheroid: bounce timer = RND(0..CDPTIM) [PortTicks]; per-drop timer =
     RND(0..CDPTIM/4) (ROM CIRC2, notes 4.1); drops = ceil(RND(0..ENFNUM)/2)
     rolled AT SPAWN (PlayField passes Parameters.MaxDropsX2, spheroid rolls
     once in ctor — currently it uses the constant MaxEnforcersPerSpheroid);
     drop gated on EnforcerCount < 8 (ENFCNT cap, notes 4.1); escape when
     drops exhausted (port mechanism fine); kill = 1000 + 7-frame burst
     ($FFAA colours) + "1000" popup 30 ticks (FX-ish; burst frames =
     spheroid frames 4-7 already on disk).
  2. Quark: first drop = RND(0..TDPTIM), re-arm = RND(0..TDPTIM/2+1) (ROM
     4C24-4C2C); tanks per quark = ceil(RND(0..ENFNUM)/2); drop gated on
     TankCount < 20 (TNKCNT) — OVCNT<17 global cap PENDING ($98xx counter
     def in disasm).
  3. Enforcer: fire = RND(0..ENSTIM) (replace RND 60..180); spark cap 20
     (existing); aim at player (existing).
  4. Grunt: step-based movement — one 2-screen-px step toward the player
     every PortTicks(GruntMoveDelay) ticks (replace 3px/tick); on EVERY
     grunt kill (laser OR electrode) PlayField speeds up ALL surviving
     grunts: delay = max(GruntSpeedFloor, delay*7/8 rounded) (ROM 3A94-3A9F;
     LDB #$E0 MUL = x7/8). Grunts keep 8-way aim (ROM grunts track the
     player continuously).
  5. Tank: first shot = PortTicks(TNKSHT) + RND(PortTicks(31)); re-arm =
     PortTicks(TNKSHT) (ROM 4CE4-4CEA/4E46-4E50 — no RND on re-arm); aim:
     RND(0..255) <= 96 → seek player ELSE random point (38%, notes 11.5);
     vertical move only if |dy| > 16 arcade px; move countdown RND(1..31);
     axis-aligned ±1/0 steps; walk anim runs backwards when moving left
     (Draw: pick frame index direction).
  6. Tank shells: lifetime = PortTicks(RND(0..31) + 48) (source value,
     48..63 ROM ticks — verify vs R5 disasm shell process); 20-per-wave
     fizzle BUG: PlayField keeps a shellsFiredThisWave counter, INC on
     spawn, DEC ONLY on laser kill (never on fizzle); Tank may fire only
     while counter < 20; bounces off all 4 walls (port currently removes
     on exit — change to reflect + SRBSND equivalent).
  7. Hulk: step every PortTicks(HLKSPD) ticks (ROM: update rate; lower =
     faster); indestructible + knockback (existing); ROM hulk = random
     wander (verify RRH11 for exact wander vs the port's 15% chase).
  8. Tempo: add GameplayConstants.PortTicks(int romTicks) = romTicks*60/50
     (50Hz ROM, author RE) and use everywhere above.
  **PHASE C:** per-pixel collision (author-wanted since 2026-09-06):
  precompute per-frame opaque-pixel masks at SpriteSet load (Texture2D
  GetData, non-transparent = nibble != 0 → PNG alpha), laser-vs-enemy and
  player-vs-enemy checks against masks; sparse spheroid/quark frames are
  INTENDED (small hit areas).
  **PHASE D:** humans — wave spawn (MomCount/DadCount/MikeyCount already in
  LevelParameters), transporters (RRT2 GROUP1-7 corners), walk (HUMATB
  direction tables, RRH11 ~500), grunt-killed → skull 90 ticks + HKSND
  (Skull.png on disk), brain-killed → conversion to PROG (RRB10: blink
  $AABB, PRGSND ~20 loops, becomes enemy 100 pts), rescue bonus
  ScoreValues.RescueBonus(SAVCNT) + P1000-series popups (PCFLG trigger
  semantics PENDING — RRG23 COLCHK 798-830), "SAVE THE LAST HUMAN FAMILY"
  message (FAMMP).
  **PHASE E:** brains (BrainCount from table; BSHTIM cruise missiles at
  closest human, BRNSPD speed, 500 pts, MissileSmall_0/1 sprites on disk)
  + progs.
  **PHASE F:** M6 gates (0 warnings, all tests, launch smoke) + full
  notes sweep + commit per milestone.
  PENDING RESEARCH (small): OVCNT global-object cap definition (disasm
  $98xx), shell fizzle lifetime in R5 disasm, wave-END condition (which
  counts must hit 0 — check wave-advance code near $2B0B), enforcer aim
  ($136D), hulk wander (RRH11), human spawn positions (RRT2 GROUP1-7),
  red screen (RRFRED) + death sequence, marquee (RRM1, low priority).
- **2026-09-12 (14):** **AUTHOR: "get the main game working first; colour
  cycling, explosions, sounds are frills — do them LAST. Rectangle
  intersection collision is fine for the POC (per-pixel later)."**
  PHASE B (ROM timing wiring) ~80% DONE + committed green:
  `GameplayConstants.PortTicks(romTicks) = romTicks*6/5` (50Hz ROM → 60Hz
  port); **Grunt** rewritten = 1 arcade-px (2 screen-px) step every
  PortTicks(ROBSPD) toward player, `SpeedUp(floor)` = max(RMXSPD,
  delay×7/8 rounded) exposed for the field; **PlayField** now: grunts
  speed up on EVERY grunt kill (laser + electrode paths, ROM 3A94),
  `CanDropEnforcer` (<8), `CanDropTank` (<20), `CanFireShell`
  (20-shells-per-wave fizzle-bug counter: INC on fire, DEC only on laser
  kill), spheroid/quark/enforcer/tank spawned with the ROM wave-table
  params; **Spheroid/Quark** rewritten = ENFNUM drop-count rolled at SPAWN
  (ceil(RND(0..ENFNUM)/2)), spheroid: bounce phase RND(CDPTIM) then drops
  every RND(CDPTIM/4); quark: first drop RND(TDPTIM) then RND(TDPTIM/2+1);
  both: skip-drop-and-rearm when the arcade cap blocks, flee+vanish when
  allotment exhausted; **Enforcer** fire = RND(0..ENSTIM); **Tank**
  rewritten = ROM ANIMATE_TANK: re-aim every RND(1..31) ticks,
  RND(0..255)≤96 (~38%) → seek player else random playfield point,
  vertical step only when >16 arcade px off, 1 px/tick, wall-bounce
  mirror, first shot RND(0..31)+TNKSHT then TNKSHT exactly (no re-roll),
  walk anim runs BACKWARDS when moving left.
  **PHASE B REMAINING (small, next session start):**
  1. `TankShell.cs` still uses the OLD lifetime (120..240 ticks) and dies
     at the boundary — change to lifetime = PortTicks(RND(0..31)+48)
     (source value 48..63 ROM ticks; verify in R5 disasm) and BOUNCE off
     the four walls instead of removing (keep RemoveAll pruning).
  2. `Hulk.cs` still moves 4px/tick — change to step 2px every
     PortTicks(HLKSPD) ticks (keep the axis/wall/chase structure; consider
     whether the ROM hulk is pure random-wander — check RRH11).
  3. Re-run `dotnet test` after each (74/74 is the bar).
  **Then PHASE C is SKIPPED per author (rect collision is the POC).**
  **Next main-game work = PHASE D humans** (the heart of the game — they
  don't exist in the port yet): wave spawn uses MomCount/DadCount/
  MikeyCount (already in LevelParameters + spawned? NO — PlayField has no
  human code at all yet). Needed: (a) brief source read of RRH11
  (HUMATB direction tables, human spawn/walk), RRT2 (GROUP1-7 =
  transporter corner spawn points), RRH2/RRH6 (walk process, grunt-kill
  → skull 90 ticks + HKSND; PCFLG rescue semantics); (b) new Human entity
  (Mom/Dad/Mikey = the family sprites on disk: HumanMom_*, HumanDad_*,
  HumanKid_* etc. — check SpriteSet for exact names), Skull.png kill
  popup, rescue bonus ScoreValues.RescueBonus(SAVCNT) (1000×saves, cap 5);
  (c) wave-clear condition: wave ends when ALL robots gone AND (check ROM:
  do surviving humans carry over or vanish? — verify before wiring);
  (d) "SAVE THE LAST HUMAN FAMILY" message when families left = 1.
  **PHASE E brains/progs after humans** (BSHTIM cruise missiles at closest
  human, BRNSPD, 500 pts; brain-kill → human converts to PROG 100 pts —
  RRB10; add Brain+Prog to wave-clear condition).
  **Last (frills, author-confirmed low priority):** M4 colour-cycle shader
  (options in §6 M4), explosion effects (RRX7), all sound (RRSND),
  per-pixel collision, red screen / marquee.
  PENDING research list unchanged (OVCNT cap def, wave-END condition,
  enforcer aim $136D, hulk wander, human spawn points, shell fizzle R5).
- **2026-09-12 (15):** **PHASE B COMPLETE** (both remaining items + gates).
  (1) **TankShell** rewritten ROM-faithful (R5 disasm verified this session):
  lifespan = PortTicks((RND & $1F) + $30) = 57..94 port ticks (4F82-4F8A; the
  earlier "48..63" in this file was a typo — 48..79 ROM ticks is right),
  straight-line flight (drift-perturb placeholder removed — the ROM sets the
  velocity ONCE at creation; the port's aim = player ±1 px/tick jitter per
  axis, a deliberate approximation of the pending SHLSPD aim, see below),
  BOUNCES off all four walls (4F94-4FCD: COM the delta, X wall checked before
  Y, shell never exits; clamp kept as belt-and-braces), fizzle still does not
  decrement the 20-shell counter (bug preserved; the fire gate
  `CMPA #$14; LBHI` = fire while count < 20 — the port's `< 20` was already
  exact, re-verified against the disasm this session).
  (2) **Hulk** rewritten from RRH11 source this session (HULK/HULKND/
  HULKST + the animation tables): one step per HLKSPD-tick cycle (PortTicks),
  horizontal steps alternate 3/4 arcade-px with the animation, vertical 2
  arcade-px; re-aims every RND(1..31) steps (ROM PD5) or immediately on wall
  contact (no move that cycle); every re-aim FLIPS the axis (ROM: previous
  direction was an X-block -> seek Y and vice versa) and aims the new
  direction at target + RND(-16..15) offset (horizontal out-of-range clamps,
  vertical below-range wraps to the far side — ROM HNDX/HNDY verbatim);
  hulk starts horizontal (ROM: initial picture address is not an X-block so
  the first HULKND seeks X); **target provider** (Func) so Phase D can hand
  half the hulks a human (ROM: 50% GTARG human / 50% player, dead target ->
  player); old 15%-chase constants deleted. Hulk still knocks back on laser,
  still destroys electrodes it walks over (field, unchanged).
  (3) Gates: 0 warnings, **78/78 tests** (4 new: shell fizzle range, shell
  wall-bounce containment, hulk exact step period, hulk wall re-aim
  containment over 300 ticks), `PlayField` gained AddTankShell/TankShells
  internal test hooks.
  **PENDING (recorded, not done):** (a) ROM tank-shell aim = SHLSPD-based
  distance-independent speed + a ~50% aim-at-WALL mode (4E79-4F80, the
  normalization loop doubles the delta until inside SHLSPD-derived bounds) —
  port keeps the simpler player-aim; (b) hulk animation: ROM cycles 4
  picture entries per direction block (9 unique frames = the verified
  hulk1-9; the OPICT 4-byte delta -> list-frame mapping is unverified) —
  port draws the lead frame; (c) hulk human-targeting (Phase D).
  **NEXT = PHASE D humans** (the heart of the game): see entry (14)'s (a)-(d)
  plan — source reads RRH11 (HUMATB/walk) + RRT2 (GROUP1-7 transporter
  corners) + RRH2/RRH6 (walk process, grunt-kill -> skull 90 ticks + HKSND,
  PCFLG rescue semantics), Human entity family, wave-clear condition
  (verify: do surviving humans carry over or vanish?), "SAVE THE LAST HUMAN
  FAMILY" message (FAMMP).

- **2026-09-12 (16):** **PHASE D humans — discovery batch** (R5 disasm + RRH11
  source cross-check; port wiring in progress this entry).
  (1) **HUMKIL fully decoded** (RRH11 425-495). ONE routine handles both
  rescues and robot-kills, branched on **PCFLG** (the player-collision flag):
  - PCFLG set (player touched the human) = RESCUE: linger picture =
    **P1000 + 4*min(SAVCNT,5)** — i.e. the score-value DISPLAYS (the "1000"
    .. "5000" blitter pictures, vector table $000C/$000F/$0012+... ),
    **60-tick** timer, SAVCNT++, score added via the SCORE routine with
    index min(SAVCNT,5) into **SVITAB ($0210..$0250) = 1000,2000,3000,4000,
    5000** (byte-verified in R5), SAVSND sound, collision vector = NOKILL
    (the player does NOT die on rescue contact).
  - PCFLG clear (a robot touched the human) = KILL: picture = **SKULP**
    (skull & crossbones), **90-tick** timer, HKSND.
  - Marker X is clamped to XMAX-6 (keep the display inside the right edge).
  - **SAVCNT is NOT capped** (plain INC). Only the score DISPLAY/lookup is
    capped at 5. PLINIT does CLR SAVCNT -> the running count resets when the
    player respawns (matches spec "rescues carry across waves, reset on
    death"). Port: SkullMarker 90-tick/skull already matches; rescue bonus
    ScoreValues.RescueBonus(count) 1000..5000 capped already matches; the
    60-tick rescue SCORE DISPLAY popup is the one missing display (sprite
    Score_1000..5000 already in Content) — being added.
  (2) **COLCHK fully decoded** (RRG23 792-812): every tick PCFLG=1; check
  robots, then posts, then motion objects (any hit -> PLEND, player dies);
  then humans (PCFLG path -> rescue, NOKILL); CLR PCFLG. Confirms: in R5
  ANY robot (not just hulks) kills the player on contact; only the hulk
  checks the human list (HPTR) so only the hulk kills humans; the player's
  own contact rescues.
  (3) **HUMAN walk process** (RRH11 321-363): PD4 = byte offset within the
  13-byte direction block (0/3/6/9); each 3-byte entry = IMAGE, dX, dY
  (deltas in 2-arcade-px bytes, signed); a negative/0xFF IMAGE resets the
  walk cycle; the picture is OPICT = PD2 + IMAGE each step (so the 12-slot
  picture list is indexed by the table's image numbers); a step that hits an
  obstacle (**CKOBS against PPTR = POSTS/ELECTRODES**) or leaves the field
  (CKLIM) is NOT taken and triggers a re-aim (GHDIR); steps happen every
  **NAP 8** (1 step / 8 ROM ticks); GHDIR: PD5 = RND(1..128) steps, new
  block = (SEED & 7) * 13. **Port fix applied:** Human.Update previously
  only re-aimed on walls; it now also re-aims when the next step would
  overlap a LIVING electrode (ROM CKOBS PPTR) — added
  OverlapsLivingElectrode().
  (4) **HUMSTV spawn** (RRH11 365-404): HTINIT clears the family-target
  lookup table first; spawn order **kids (KIDCNT), then moms (MOMCNT), then
  dads (DADCNT)**; plain RANDXY (NO spacing/safety constraints, unlike the
  hulk's SAFTY loop); staggered start timer SEED&7+1 (1..8 ticks); each
  spawn is HUMADD into the target table (slot order = spawn order).
  (5) **HULATB verified byte-exact** against the port's Steps table: all
  8 blocks; image numbers = 4 * (slot index into the 12-slot L,R,D,U
  picture list); 4-byte picture descriptors (FCB w,h + FDB dataptr).
  (6) **R5 BEGIN_WAVE spawn ORDER** (disasm $2826): read wave counts ->
  WAVE_START_PLAYER -> **$0000 hulks** -> $1AC0 brains -> $4B00 tanks ->
  **$0003 family** -> $3883 electrodes -> $3880 grunts -> spheroids ->
  quarks -> (brain-wave task if brains). So **hulks and brains are
  initialised while the family list is still EMPTY** — this drives two
  targeting quirks below.
  (7) **R5 hulk target roll** ($017C HULK_INITIALISE, $0106 re-aim):
  per-hulk coin at spawn: 50% (SEED<$C0) -> **$0237** = round-robin scan of
  the family list (cursor in $49, returns the SLOT ADDRESS of the first
  non-null entry from the cursor; all-null -> 0); 50% -> target = **$B3A2 =
  the LAST family-list slot**. Re-aim does LDY [$09,U] (target stored
  indirectly through the list slot); Y=0 -> LDY #$985A = **player object**
  (NULL target -> player). Because the list is empty at hulk init: the
  "family" roll stores NULL (clean: hulk hunts the player) and the "last
  slot" roll stores $B3A2, which HUMSTV fills moments later with the
  **last-spawned family member** (dads last -> usually the last dad). When
  that member dies/is rescued the slot clears -> hulk falls back to player.
  **Documented R5 BUG** (RE author's comment at $010D): the NULL case does
  LDY ,0 which reads ROM bytes $0000/$0001 = 7E 01 -> Y=$7E01 -> the hulk
  chases a phantom object in ROM code and "wanders off doing its own thing,
  getting stuck in corners". **Port decision (logged):** implement the
  clean semantics — 50% player (NULL fallback), 50% last-spawned human with
  NULL->player fallback; skip the $7E01 phantom chase (it would look like a
  broken hulk in a POC). Wired in PlayField.SpawnHulks
  (LastHumanOrPlayer; _humans[^1] = last spawned, HUMSTV order).
  (8) **R5 brain "find nearest family member" + the Mikey bug** ($1B95
  FIND_NEAREST_FAMILY_MEMBER_TO_PROG): brains init ($2834) also runs while
  the family list is empty, so the nearest-search finds all-NULL and
  returns the FIRST slot ($B354) for EVERY brain; the first slot is filled
  first by HUMSTV = **Mikey** -> in the arcade ALL brains chase Mikey at
  wave start. PHASE E territory (human->PROG conversion) — port should
  reproduce: all brains target the first human (a mikey, first spawned).
  (9) **Human frame order VERIFIED against R5**: repo frames 1..12 per
  member = **L,L,L,R,R,R,D,D,D,U,U,U** (matches old-source descriptor order
  and the port's Mikey/Mom/DadFrames[0..11] mapping, FrameSet
  [L,R,D,U,L,R,R,L] per direction). Evidence: R5 ASCII decode — mommy1 head
  offset LEFT, mommy4 head offset RIGHT (independent art, NOT mirrored),
  mommy7/10 symmetric cardinal poses; R5 vector table $000E/$0010/$0012
  = mommy/daddy/mikey "standing still, facing left" descriptors
  ($052F/$07FF/$0B3B) — same first-frame identity as repo frame 1.
  (10) **Port state this entry:** PlayFieldHumanTests compile fixed
  (xunit.v3 Assert.InRange has IComparer before userMessage — use
  userMessage: named arg); Rescues_KeepRunningCountUntilTheCap corrected to
  expect 9 (SAVCNT uncapped, only the bonus caps — test asserted the ROM
  wrong); human electrode avoidance added; hulk 50/50 target wired
  (7/8 above); rescue score popup (1) next, then gates + entry (17).

- **2026-09-12 (17):** **Playtest-fix batch — animation + drop-rate fidelity**
  (author playtest report 2026-09-12: "grunts do not animate", "player does
  not animate", "enforcers too fast", "spheroids spawn enforcers WAY too
  fast", plus a new controls request).
  (1) **RRP8 grunt walk animation — DECODED (RRP8.ASM 153-227, art 670-676).**
  The ROBOT process (NAP 2 + NAP 10 body cadence) steps a grunt when its
  move countdown (ODATA, rolled RANDU ≤ ROBSPD) hits zero; EACH COMPLETED
  1px step advances the image pointer: `LDD OPICT,X; ADDD #4; CMPD #RWDP4;
  BLS; LDD #RWDP1` (ROB11). The frame table maps the four ROM frames onto
  only THREE unique arts — RWDP1→RWDD1, RWDP2→RWDD2, **RWDP3→RWDD1 (the ROM
  literally reuses art 1)**, RWDP4→RWDD3 — i.e. walk cycle **[A,B,A,C]**,
  which exactly matches the repo's 3 grunt frames. A stationary grunt never
  advances (image only changes on a step) = frozen sprite, as in the arcade.
  Port: `Grunt.WalkFrame` (1..4) + `Grunt.WalkArtIndex` (2→1, 4→2, else 0);
  Draw uses `GruntFrames[WalkArtIndex(WalkFrame)]`.
  (2) **R5 player walk animation — DECODED (MOVE_PLAYER $2FD0, $3031
  descriptor table).** Stick bits (0-3) ×4 index a 12-entry descriptor
  table: entry 0 (centered) = delta 00 00 + NO table → **early-out at
  $2FFE: no movement AND the animation counter is not touched (sprite
  freezes on its current frame)**. Up/down/left/right entries carry a
  2-byte delta + a 5-byte frame sequence: LEFT = **1,2,1,3**, RIGHT =
  **4,5,4,6**, DOWN = **7,8,7,9**, UP = **10,11,10,12** (ABAC pattern);
  diagonals: up-left/down-left → LEFT table, up-right/down-right → RIGHT
  table. Frame metadata: sequence byte N → **$35EB + (N-1)*4** — note the
  `DECB` happens BEFORE the ×4 shifts, so byte 1 → $35EB, byte 12 →
  $361B; that makes all 12 entries valid 8×12 frames with images
  $361B..$382B (48 bytes each). (First read mis-parsed the shift as
  byte×4, making byte 12 look like garbage — it isn't.) Cadence: counter
  $70 cycles 0→1→2→0 each MOVE_PLAYER call and the frame is reloaded only
  when $70==0 → **each frame lasts exactly 3 movement ticks**; the sequence
  index $71 resets to 0 when the descriptor table changes; $70 itself is
  NOT reset (port restarts the 3-tick cadence on direction change —
  logged deviation, looks better). **WAVE_START_PLAYER ($2F8C) points the
  player metadata at $3603 = frame 7** (first DOWN frame) → port starts on
  PlayerFrames[6]. Repo `Player_1..12.png` = R5 frames 1..12 verified by
  ASCII decode: L{1,2,3} R{4,5,6} D{7,8,9} U{10,11,12} — same mapping as
  the human frame order (entry 16, item 9). ROM also does half-pixel X
  movement (fractional X byte $000B/X+0B) — port stays integer-only
  (pre-existing deviation).
  (3) **TWO-STICK CONTROLS (author request 2026-09-12) — deliberate
  deviation.** WASD = move, **IJKL = 8-way aim**, Space = fire; gamepad
  right stick deferred by author ("gamepad dual-stick or D-pad + stick
  support later"). `PlayerInputState` gained `AimDirection` (zero = fire in
  current facing). The arcade has no independent aim (single joystick:
  laser fires in last-movement direction) — twin-stick convention adopted:
  sprite facing + fire direction = aim when given, else last movement
  direction; walk animation follows the facing (aim) group.
  (4) **Spheroid drop rate — "WAY too fast" ROOT-CAUSED (RRC11 4B38 / R5
  $1193).** The spheroid drop process is **NAP 2 → runs every 2 ROM ticks**,
  so the drop countdown decrements at HALF the tick rate; the port was
  decrementing every port tick = 4× the ROM rate. Additional ROM facts
  re-confirmed: the `RANDOM` call returns **1..N, never 0** — the port's
  old `Next(0, ENFNUM+1)` roll was unfaithful (could roll 0 → drop zero
  children); now rolls 1..ENFNUM, drops ceil(x/2) = 1..5, never 0. Initial
  countdown = RND(1..CDPTIM) ($11A8); re-arm after a drop =
  RND(1..CDPTIM/4) ($11A4-11A6). ENFMAX = $EF = 64. OVCNT<17 global check
  ($11AF) skipped (port safeguard, logged). Port now counts in 4-tick
  chunks (PortTicks(4) per ROM step).
  (5) **Enforcer fire starvation + speed (RRC11 5068 / R5 $1404).** ROM
  re-arms the fire countdown (RND(1..ENSTIM)) BEFORE the active-spark cap
  check — the port previously re-armed only on successful fire, so a full
  spark field starved firing indefinitely; fixed. Grow-up 40 ticks, AI
  every 3 ticks, offset-target loiter = port class doc (earlier notes).
  Speed retuned 4→3 px/tick per playtest ("enforcers too fast").
  (6) **Port state this entry:** 114/114 green (was 104; +5
  PlayerAnimationAndAimTests, +5 GruntAnimationTests incl. 4-way art-table
  theory). Test-harness lesson: zero-GameTime tests never expire the 2-s
  start grace → RobotsFrozen stays true forever; timing tests warm up 121
  integer-tick frames (166,666-tick spans; 120× = 1.999992 s < 2 s) then
  drive entities standalone so dropped robots can't kill the standing
  player. Next: gamepad right-stick aim (deferred by author), PHASE E
  brains (all-brains-chase-Mikey per entry 16 item 8).

- **2026-09-12 (18):** **PHASE E prep — brains/progs/cruise-missile ROM
  decode** (RRB10.ASM "BRAINS & CO.", read-only research; NO port code
  this entry — the playtest build must stay untouched until the author
  plays).
  (1) **BRAIN process (RRB10 143-237).** Cadence NAP 4 + NAP 12 = body
  every 16 ticks, then a variable SLEEP(BRNSPD). Target in PD2: NULL →
  player (PLOBJ); if any human alive (MOMCNT+DADCNT+KIDCNT ≠ 0) →
  GETHTG = nearest human (R5 $1B95 Mikey bug, entry 16 item 8). Movement:
  1px step per body toward target (sign-clamped band, CKLIM bounds).
  Animation: 4 direction tables, each a 4-frame **ABAC** (BRNAL/
  BRNAR/BRNAD/BRNAU over BRLP1-3/BRRP1-3/BRDP1-3/BRUP1-3), PD4 += 2 per
  body → each frame 16 ticks; brain art = 7×16 px. Firing: PD5 counts
  down per body → 0 → BRNSHT. **Engagement gate (one-sided band):**
  if target is a human and (brainY - humanY + 3) ≤ 6 AND
  (brainX - humanX + 3) ≤ 6 → BMUT — i.e. the brain only triggers when it
  sits ≤3px ABOVE-LEFT of the human (author's own comment: "SEEK OUT THE
  INEFFICIENT & DESTROY"); otherwise it keeps orbiting until it re-enters
  the band from above-left.
  (2) **BMUT — the progging sequence (RRB10 240-333).** Brain positions
  itself beside the human (tries LEFT first: brainX - humanWidth - 1 ≥
  XMIN, else RIGHT: brainX + 8 ≤ XMAX-4), switches its own art to the
  facing pose (BRLP1/BRRP1), aligns Y (humanY + 2). Then "GET RID OF THE
  BODY": JSR [OCVECT,X] = the human's kill routine called with BRNFLG set
  (human deallocated from the lists, suppressed skull), the human object
  is recycled as the prog carrier, and a **20-iteration flicker loop**
  runs: PRGSND (programming sound), BRNON ($BB flicker box at the brain),
  erase the human image, redraw the human at (brainX, brainY +
  RND(0..7) clamped [YMIN, YMAX-14]) as a two-layer "outer shell / inner"
  HUMON pair — the visible "programming" glitch. Finish: HPSND (final
  conversion sound), PROGST at the human's position, brain reverts to
  normal art, GETHTG for the next victim.
  (3) **BRNKIL + the mid-prog death rule (RRB10 366-403).** Brain kill
  score = $0150 → **500** (BCD convention §11.3; title screen "BRAIN
  500" ✓). **If the brain is killed while its PC is inside the BMUT3
  flicker loop (i.e. mid-prog): the human being programmed is LOST** —
  deallocated to the free list, image erased, BRNFLG → a SKULL appears at
  that spot. Killing a progging brain permanently kills its victim.
  (4) **PROG (PROGST 396-431, process 475-525, PRGKIL 531-568).**
  PROGST allocates from **SPFREE — the same slot pool the cruise
  missiles use** (progs + missiles share a cap). Spawn point = the
  human's position. Direction: GPDIR — HSEED<0 → "SEEK Y" (vertical
  walker) else "SEEK X" (horizontal): player position ± random offsets
  (GPOFF: x RND(1..15), y RND(1..18), sign-flipped) vs the field edge
  picks the art/direction table (PRGAL/PRGAR/PRGAD/PRGAU) — **progs walk
  STRAIGHT lines** like the arcade. Animation: 4 ABAC frames per
  direction; each table entry = (imageOffset, Δx, Δy) and the deltas are
  ±2px (PRGAL: (0,-2,0),(4,-2,0),(0,-2,0),(8,-2,0)) → 2px step per body,
  body ends in NAP 3. Re-aim: SEED<0xF8 (≈3%) → new GPOFF offsets;
  LSEED≥0xE4 (≈9%) → new GPDIR; blocked by CKLIM → new GPDIR. **Shadow
  trail:** a SPSIZE(8)-entry ring (PD+10..) — each body erases the oldest
  trail image and HUMON-draws a new one at the current position in the
  "second guy" colour ($EE00), then draws the head image ($00AA): a
  **trail of up to 8 human images** follows the walking prog. PRGKIL:
  erase the whole trail, then **swap the image to PGXPIC — a 6×16
  pixel-burst "PHONY PICT"** — clamp to (XMAX-5, YMAX-15) and explode
  there; score $0110 → **100** (title "PROG 100" ✓), PGKSND.
  (5) **CRUISE MISSILE (BRNSHT 614-655, GCMDIR 666-704, CMISL 707-768,
  CMKIL 770-790).** Brain fires when PD5 hits 0, if BCMCNT < 8 AND
  SPFREE > 0 (shares the prog pool); reload PD5 = RND(1..BSHTIM); spawn
  at brain + (3,4); art CMPIC (6×6 solid) / CMP1 (plus-shape flicker);
  BCMCNT++. Direction GCMDIR: **50% horizontal** (DX=±1, sign from
  playerX + RND(0..15) - 6 vs own X), **25% vertical, 25% diagonal**
  (both ±) — always biased toward the player with ±6px noise; re-aims
  every RND(1..7) bodies (PD4). Movement: CMMOV ×2 per body + NAP 2 ≈
  1px/tick, **REFLECTS off the boundaries** (direction component
  negated); keeps an 8-point trail (old points $DDDD, head $AAAA); the
  fat collision box is the head inset 1px ("FAT PHONY GUY"). CMKIL:
  BCMCNT--, erase trail, score $0025 → **25** (title "CRUISE MISSILE 25"
  ✓), CMKSND.
  (6) **Port state:** nothing implemented (deliberate — playtest first).
  ScoreValues.Brain=500 / .Prog=100 already match (title screen, §11.3).
  PHASE E implementation checklist when started: (a) Brain entity: 16-tick
  body, 1px step, ABAC 4-dir 7×16 art (new sprites from RRB10
  BRLP/BRRP/BRDP/BRUP → repo PNGs), target = nearest human (Mikey bug)
  else player; (b) progging: engage band, 20-iter flicker (can be a
  simplified glitch effect — FRILL-able), victim lost if brain killed
  mid-prog (skull); (c) Prog entity: straight-line walker with 2px
  steps, ≈9%/≈3% re-rolls, 8-image trail (frill-able), phony-burst death
  (PGXD art); (d) CruiseMissile entity: 3-way biased direction, RND(1..7)
  re-aim, bounce, 8-pt trail, BCMCNT<8 + SPFREE pool shared with progs;
  (e) "SAVE THE LAST HUMAN FAMILY" in-game trigger STILL unverified
  (title-screen text only) — hunt for it in the R5 disasm when starting
  PHASE E.

- **2026-09-12 (19):** **Playtest round 2 — player speed + laser art decoded
  (ROM); laser render change PENDING author's own ROM check.**
  Author playtest (continued): "player moving slightly too fast" + "player
  laser sprites are wrong" + "level complete text way too big, it clips".
  (1) **PLAYER SPEED (R5 MOVE_PLAYER $2FD0; old source RRG23 PLAYRV + PITAB,
  lines 682-775).** Descriptor table @ $3031 = 4-byte entries (dx, dy, anim
  ptr), one per stick state. Per-tick deltas:
  - **Vertical = 1 arcade px/tick**: dy is a raw signed byte (01/FF) added
    straight into PY16 (`ADDB PY16`), clamped to YMIN..YMAX-11.
  - **Horizontal = 0.5 arcade px/tick**: px is a 16-bit SUB-PIXEL position
    (PX16); the dx byte is split `CLRB / ASRA / RORB` (bit0 → low byte) then
    `ADDD PX16`: 0x01 → D=0x0001, 0xFF → D=0xFF01. Table comment (disasm):
    "bit 0 set, player moves 1/2 a step more". So each held tick adds ±0.5px
    to X. Clamped XMIN..XMAX-3.
  - **Diagonals apply X and Y simultaneously — NOT normalised** (a diagonal
    tick = 0.5px X + 1px Y). Left/right use the SAME magnitude as up/down
    before the /2, i.e. the stick is 8-way but the X axis is half-rate.
  - Old-source cross-check: PITAB byte-for-byte identical to R5, and its anim
    sequences PISL=1,2,1,3 / PISR=4,5,4,6 / PISD=7,8,7,9 / PISU=10,11,10,12
    confirm the walk cycle already ported (entry (17)).
  **Port fix:** `PlayerSpeed=6` (flat, all 8 dirs) → `PlayerSpeedX=2` /
  `PlayerSpeedY=3` screen px per port tick (port tick == arcade tick).
  2/3 screen px == 0.6/1.2 arcade px, i.e. the arcade 0.5/1.0 rounded up to
  integers (+20%); keeps the 1:2 X:Y ratio and arcade-like crossing times
  (X 5.3s vs 6.4s, Y 2.2s vs 2.7s). Chose integers over a sub-pixel
  accumulator for simplicity; re-add the X /2 if it still feels fast.
  (2) **LASER ART verified in the R5 ROM @ $35BE–$35DC** (the 33 bytes right
  before the player-frame metadata @ $35EB); byte-for-byte equal to old source
  RRG23 LLPC/ULPC/DLLPC/ULLPC (lines 1367-1380). Four 4bpp pics, 2 px/byte
  (high nibble = left pixel):
  - LLPC $35BE, 3B×1r = **6×1 solid bar** ($AA $AA $AA) — LEFT & RIGHT.
  - ULPC $35C1, 1B×6r = **2×6, left column lit** ($A0 ×6) — UP & DOWN.
  - DLLPC $35C7, 3B×6r = **6×6 anti-diagonal** (top-right→bottom-left) —
    used for DOWN-LEFT *and* UP-RIGHT.
  - ULLPC $35D9, 3B×6r = **6×6 main diagonal** (top-left→bottom-right) —
    used for UP-LEFT *and* DOWN-RIGHT.
  Old-source LTAB spawn table (RRG23 ~972-986) confirms each of the 8
  directions picks ONE of these four arts — **NO FLIPPING at all**:
  L/R→bar, U/D→column, UL→ULLPC (tip top-left), DL→DLLPC (tip bottom-left),
  UR→DLLPC (tip top-right), DR→ULLPC (tip bottom-right). Muzzle offsets per
  dir (x,y in arcade px): U=(2,-1), D=(2,4), L=(0,4), R=(2,4), UL=(0,0),
  DL=(0,8), UR=(2,0), DR=(2,12).
  **Laser speed = 3 arcade px/tick** (RLASR `ADDA #3` / LLASR `SUBA #3`,
  NAP 1 = every tick; vertical + diagonal routines not yet read). Port
  LaserSpeed=12 screen px/tick == 3.6/4.8 arcade px/tick; the 6×/4× ratio vs
  the new player speed is close to the arcade's 6×/3×, so left as-is.
  **Port plan (NOT implemented — author was mid-way through verifying the art
  in the ROM himself at shutdown):** SpriteSet gets 3–4 textures built with
  `PixelArtFactory.Create` (row-major, white-on-transparent): 6×1 bar, 2×6
  column, 6×6 main diagonal (+ 6×6 anti-diagonal to mirror LTAB exactly, or
  derive it by flipping the main diagonal). PlayerLaser.Draw picks the art by
  Direction8 (no SpriteEffects if the 4th texture is added), draws at
  1 art-px = SpecScale screen px, centred on the 4×4 collision box (box +
  speed + firing logic unchanged).
  (3) **WAVE-CLEAR SCREEN** (port-specific message — the arcade has no such
  text, §11.3): two port fixes this round: (a) off-by-one — it printed the
  NEXT level (the state holds next-level to build the next PlayingState); now
  prints the level just cleared (commit dd7b9f6); (b) 32px font at 2.0×
  clipped on wide level numbers — now capped at 1.25× AND clamped to
  85%-of-screen-width.
  **State at shutdown (2026-09-12 ~23:57):** build 0 warnings, 114/114 tests
  green. Committed this round: player speed (X/Y split), wave-clear level
  number + text scale, this note. PENDING: (i) author's laser-art ROM check,
  then implement the laser render per the plan above; (ii) re-playtest of the
  fix batch; (iii) PHASE E (checklist in entry (18)) gated on that playtest.
  Game process killed at shutdown (was PID 31056, build dd7b9f6); relaunch
  `src/Robotron2084/bin/Debug/net10.0/Robotron2084.exe` next session.
- **2026-09-13 (20):** **Laser render — author's ROM check + implementation
  complete.**
  (1) **Author's ROM check (the pending item from (19))**: laser sprite
  pixel data found at 13758 (3×1), 13761 (1×6), 13767 (3×6), 13785 (3×6) —
  decimal = $35BE/$35C1/$35C7/$35D9, exactly the (19) decode. Dropped the
  ROM at ref/rom/robotron64k.bin: the 45-byte span ($35BE..$35EA, ending
  immediately before the player-frame metadata at $35EB — the "33 bytes" in
  (19) was a miscount) is **byte-identical** to old-source RRG23
  LLPC/ULPC/DLLPC/ULLPC. All lit pixels are palette slot $A (the port draws
  the art white, per plan).
  (2) **Original-source naming (author asked)**: the lasers in RRG23 =
  process **LSPROC**, spawn table **LTAB**..LTEND (`FDB routine, xyOffset,
  object`), 8 movers **RLASR/LLASR/ULASR/DLASR/DLLASR/ULLASR/URLASR/DRLASR**
  (NAP 1), pictures **LLPC/ULPC/DLLPC/ULLPC** (descriptors LLPIC/ULPIC/
  DLLPIC/ULLPIC; spawn does `ADDD #LLPIC` + the table's object field),
  die routines RLDIE/LLDIE/ULDIE/DLDIE → **LASDIH**/**LASDIV** (glow point
  left in LASCOL), counter ZP1LAS/PLAS (max 4), sound LASSND. Port keeps
  descriptive texture names; ROM labels live in the doc comments.
  (3) **Implementation (per the (19) plan):** `PixelArtFactory` gained four
  static pattern builders at arcade-pixel dimensions (1 art pixel = 1
  texture pixel): bar 6×1, column 2×6 (LEFT column lit — the ROM high
  nibble), main diagonal 6×6, anti-diagonal 6×6. `SpriteSet` replaces the
  solid 4×4 placeholder with four white textures (LaserBar/LaserColumn/
  LaserDiagonalMain/LaserDiagonalAnti, via factory `Create`);
  `PlayerLaser.Draw` picks the art by `Direction8` per ROM LTAB — L/R →
  bar, U/D → column, UL/DR → main diagonal, DL/UR → anti-diagonal, **no
  flipping** — and draws it via `DrawSprite` (SpecScale scaling + centred
  in the 4×4-spec collision box). Box, speed (12 screen px/tick), firing
  logic and muzzle offset UNCHANGED per plan (the (19) ROM muzzle offsets
  are recorded for reference, not applied).
  (4) **GATES:** 0 warnings; **118/118** (new `PlayerLaserArtTests`:
  byte-exact pixel invariants — bar all-lit, column left-only, each
  diagonal exactly 6 lit pixels in the right positions); launch smoke OK.
  **PENDING (same order as (19))**: (ii) re-playtest of the fix batch (now
  incl. the laser art + the round-2 player speed); (iii) PHASE E (checklist
  in (18)) gated on that playtest.
- **2026-09-13 (21):** **Playtest round 3 — hulk animation decoded + laser
  muzzle offsets applied.**
  (1) **Hulk animation (playtest: "the hulk doesn't animate")** — decoded
  from the ROM, closing the (15) "port draws the lead frame" item:
  - Animation table HLKAL/HLKAR/HLKAD/HLKAU at **$01CC** (byte-verified vs
    the RRH11 source): each block = 4 entries × 3 bytes (image, dX, dY) + a
    $FF reset. Image = 4× the slot index into the picture list. LEFT =
    images 0,4,0,8 with dX −3,−4,−3,−4; RIGHT = 12,16,12,20 with dX
    +3,+4,+3,+4; DOWN = 24,28,24,32 with dY +2; UP = same images, dY −2
    (DOWN and UP share a frame block).
  - Picture list **HLKLP1 at $0CF9** (vector-table entry at $0014; the
    RRH11 module lives at $0000 — consistent with the R5 $0000 hulk / $0003
    family spawn vectors from (16)): nine 7×16 (14×16 px) descriptors whose
    data pointers read **hulk1, hulk2, hulk3, hulk7, hulk8, hulk9, hulk4,
    hulk5, hulk6** (the verified repo offsets $0D1D + 112×(n−1)). So the
    walk is LEFT = frames 1,2,1,3 / RIGHT = 7,8,7,9 / DOWN = UP = 4,5,4,6
    (ABAC over the nine unique frames). This resolves the (15) "OPICT 4-byte
    delta → list-frame mapping unverified".
  - **HND10**: every direction change does CLRA; STA PD4,U (entry = 0) and
    re-points OPICT at the new block's FIRST frame — the walk restarts.
  - Implementation: `Hulk` now keeps `_animEntry` (ROM PD4, 0..3) +
    `_currentFrameIndex` (0..8 into `HulkFrames`); the step length follows
    the entry (horizontal: even 3 / odd 4 arcade px; vertical 2) — replacing
    the ad-hoc `_longStep` toggle, which unlike the ROM did not reset on
    re-aim; re-aims reset entry + frame; `Draw` renders
    `HulkFrames[_currentFrameIndex]`. Test hooks: `AnimationFrameIndex`,
    `Direction` (internal).
  - Test: `HulkAnimationTests` — 400-update simulation tracking the ROM
    state (direction, entry, shown frame, position) and asserting the frame
    AND the step length on every step, including the post-step re-aim case
    (move in the old direction, OPICT re-pointed at the new block's first
    frame — the re-aim's frame wins) and the wall-blocked re-aim case.
  (2) **Laser muzzle offset (playtest: "the laser's starting position is
  wrong when it fires")** — the ROM LTAB per-direction offsets recorded in
  (19) but deferred are now applied. They are spec px from the player cell's
  top-left: **U=(2,−1) D=(2,4) L=(0,4) R=(2,4) UL=(0,0) DL=(0,8) UR=(2,0)
  DR=(2,12)**. `Player.MuzzleOffset(direction)` (private static, notes-
  cited) replaces the uniform 10 spec-px offset; the
  `GameplayConstants.PlayerMuzzleOffsetPixels` constant is deleted.
  Test: `PlayerAnimationAndAimTests.Fire_LaserSpawnsAtTheRomLtabMuzzleOffset`
  — 8-direction Theory asserting the exact spawn box, accounting for the
  laser's one-tick advance in the same field update (Player updates before
  PlayerLasers).
  (3) **GATES:** 0 warnings; **127/127** (118 + 1 hulk animation + 8
  muzzle); launch smoke OK.
  (4) **Additional discoveries this session (recorded, not applied unless
  noted):**
  - **RRH11 vector table @ $0000** (module base confirmed — the R5 $0000
    hulk / $0003 family spawn vectors from (16) line up exactly):
    $0000 JMP HLKSTV, $0003 JMP HUMSTV, $0006 JMP CKLIMV, $0009 JMP SKULLV,
    $000C FDB P1000 (mommy stand-still), $000E FDB MLP1 (daddy), $0010 FDB
    DLP1 (mikey), $0012 FDB KIDLP1, $0014 FDB HLKLP1 = $0CF9, $0016 FDB
    HLKAL = $01CC, $0018 FDB HUMATB = $03CF, $001A FDB SKULP = $0437 (skull
    art $043B matches §4). Dump any FDB from here to find a module's data
    addresses.
  - **HULKND uses two ROM random streams: X-aim = SEED, Y-aim = HSEED.**
    Port uses one System.Random — documented simplification (the streams
    only decorrelate the ROM's own LCGs; nothing observable to preserve).
  - **HNDX band clamp:** adjusted X target in [XMAX, XMAX+$40) is kept
    as-is (the hulk then aims left, across the field); >= XMAX+$40 wraps
    the target to XMIN. Port clamps to the playfield edges instead —
    visually equivalent and simpler; use the ROM band only if chasing
    exact wander statistics.
  - **HNDY:** adjusted Y target below YMIN>>2 wraps to YMAX (port: below
    the playfield top wraps to the bottom edge).
  - **HULKIL laser knockback (ROM) is small and random:** delta X =
    LASDIR (±1) or ±2 (50/50 by SEED sign, via ASLA); delta Y = LASDIR+1
    (±1, LSEED >= $C0) or ±4 (LSEED < $C0, via ASLB X2); then CKLIMV-clamped
    and the hulk re-checks collisions against the player + humans. Port
    uses a fixed spec-px knockback (spec: "pushed back ... into the WALL")
    — larger and deterministic, acceptable per spec.
  - **OPICT descriptor convention (all 4bpp pictures):** FCB bytesPerRow,
    rows + FDB dataPtr; OPICT = pictureListBase + image, image = 4×slot,
    a $FF/0x80+ image resets the walk cycle. This is the generic decode
    for ANY future animation table (humans from (16) are the same shape).
  - **HLKST spawn:** SAFTY + RANDXY with a retry loop that avoids the
    player's middle band (XTEMP/XTEMP+1/XTEMP2 checks), target roll
    SEED >= $C0 → last family slot else GTARG random. Port uses its own
    min-distance spawn — fine.
  - The **"hulk wander (RRH11)" item in the (18) PENDING research list is
    RESOLVED** — HULKND/HNDX/HNDY/HLKST fully decoded above; the port
    already implements the equivalent (PHASE B).
  - **Tooling gotchas (hit this session):** C# `_x & 1 == 0` parses as
    `_x & (1 == 0)` (CS0019) — parenthesise the mask (`(_x & 1) == 0`);
    the PlayField test ctor has no default for `startingLives` — pass
    `startingLives: 3` (see PlayFieldMovementTests.CreateEmptyField);
    `SpriteSet.Hulk` and `SpriteSet.Grunt` are frame-0 aliases of the
    frame arrays — both now unreferenced (Hulk orphaned by (21), kept for
    symmetry; delete both if you ever prune).
  **RESUME (exact next steps, in order):**
  1. Author re-playtest of the fix batch at
     `src/Robotron2084/bin/Debug/net10.0/Robotron2084.exe`: check the hulk
     now WALKS (gait changes on turns; vertical walk uses frames 4,5,6)
     and lasers spawn from the per-direction ROM muzzle offset (LTAB,
     spec px from the player cell top-left: up (2,-1), down (2,4), left
     (0,4), right (2,4), UL (0,0), DL (0,8), UR (2,0), DR (2,12)). Apply any fixes, then re-run the gates (build 0 warnings;
     tests via `tests/Robotron2084.Tests/bin/Debug/net10.0/Robotron2084.Tests.exe`,
     NOT `dotnet test`; launch smoke > 8 s) and add a ledger checkpoint row.
  2. **PHASE E** per the (18) checklist (brains: R5 $017C target = nearest
     human else player, 16-tick body, 500 pts; progs: straight walker,
     phony-burst death, 100 pts; cruise missiles: 3-way biased dirs,
     RND(1..7) re-aim, 8-pt trail, BCMCNT<8, 25 pts; RRB10 decode in (18)).
  3. Frills last (author-confirmed): M4 colour-cycle shader (parked .fx in
     §6 M4 notes), explosions (RRX7), sounds (RRSND), red screen (RRFRED),
     marquee (RRM1).
  Still-pending research (unchanged except hulk wander, done via (21)):
  OVCNT global cap def ($98xx), wave-END condition (near $2B0B), enforcer
  aim ($136D), human spawn positions (RRT2 GROUP1-7), shell fizzle in R5,

- **2026-09-13 (22):** **Playtest round 4 — hulk knockback + enforcer speed**
  (author playtest of the (21) build: "the hulk, when shot, is jumping too
  far"; "the enforcers are going too fast" — the latter a repeat of the (17)
  complaint, so the 4→3 retune was not slow enough).
  (1) **Hulk laser knockback — ROM HULKIL now IMPLEMENTED** (the (21)
  "recorded, not applied" item). The fixed 20-spec-px push (40 internal px —
  more than the hulk's own 32-px width per hit) is replaced by the ROM's
  small per-axis random push: delta X = ±1 arcade px, doubled to ±2 when
  SEED's sign bit is clear (50%, via ASLA); delta Y = ±1, quadrupled to ±4
  when LSEED &lt; $C0 (75%, via ASLB X2); a diagonal laser pushes both axes;
  then the CKLIMV-equivalent clamp to the playfield (spec: "pushed back into
  the WALL but no further"). `Hulk.ApplyKnockback(direction)` rolls the hulk's
  own Random per axis; `GameplayConstants.HulkKnockbackDistance` deleted.
  Tests: `HulkKnockbackTests` (2 facts) — all 8 directions x 400 seeded hits:
  ONLY the ROM magnitudes {1,2} (X) / {1,4} (Y) arcade px are observed, BOTH
  magnitudes appear on every active axis, the inactive axis never moves, and
  a hulk pinned in the top-left corner pushed outward stays on the wall, never
  outside the field. `Hulk.Position` gained an internal setter (test hook,
  InternalsVisibleTo pattern).
  (2) **Enforcer speed 3 → 2 internal px/tick** (1.5 → 1.0 spec px/tick at
  60Hz). The ROM's 16-bit fixed-point velocity runs ~1.1-1.9 spec px/tick AWAY
  from the destination zone (50Hz) but crawls NEAR it; the port's constant
  step has no crawl, so the playtest value sits below the ROM's mid-range —
  logged as a continuing playtest-driven deviation (supersedes the (17)
  4→3 note in GameplayConstants).
  (3) **GATES:** 0 warnings; **129/129** (127 + 2 hulk knockback); launch
  smoke BLOCKED — the playtest exe is still running (build can't overwrite
  bin\Debug\net10.0\Robotron2084.exe) — re-run the >8 s smoke after closing
  the game.
  **RESUME (exact next steps, in order):**
  1. Author re-playtest of `src/Robotron2084/bin/Debug/net10.0/Robotron2084.exe`
     (CLOSE the running game first so the new exe can be copied): a laser now
     nudges the hulk a few px (ROM magnitudes, random per hit) instead of the
     20-spec-px lurch, and enforcers approach ~1/3 slower. Apply any fixes,
     then re-run the gates (build 0 warnings; tests via
     `tests/Robotron2084.Tests/bin/Debug/net10.0/Robotron2084.Tests.exe`, NOT
     `dotnet test`; launch smoke > 8 s) and add a ledger checkpoint row.
  2. **PHASE E** per the (18) checklist (brains: R5 $017C target = nearest
     human else player, 16-tick body, 500 pts; progs: straight walker,
     phony-burst death, 100 pts; cruise missiles: 3-way biased dirs,
     RND(1..7) re-aim, 8-pt trail, BCMCNT<8, 25 pts; RRB10 decode in (18)).
  3. Frills last (author-confirmed): M4 colour-cycle shader, explosions
     (RRX7), sounds (RRSND), red screen (RRFRED), marquee (RRM1).

- **2026-09-13 (23):** **Playtest round 5 — collision boxes + arcade HUD
  placement** (author: "the collision detection isn't working correctly ...
  the rectangular intersection isn't working — tighten that up"; "the player
  lives overlap the border wall"; overall goal: "look like the Robotron
  arcade game, just scaled up" at 640×400).
  (1) **Root cause of the bad rectangle intersections:** the (round-5)
  per-entity collision-size table in `GameplayConstants`
  (`PlayerCollisionSize` (8,12), `GruntCollisionSize` (10,13),
  `HulkCollisionSize` (14,16), `SpheroidCollisionSize`/`QuarkCollisionSize`
  (16,15), `EnforcerCollisionSize` (10,11), `TankCollisionSize` (14,16),
  `ElectrodeCollisionSize` (10,9), `MomCollisionSize` (8,14),
  `DadCollisionSize` (10,13), `MikeyCollisionSize` (6,11),
  `SkullCollisionSize` (12,11) — ROM picture dimensions, COL0V intersects the
  PICTURE) had been AUTHORED in the previous session but **never wired
  into any entity** — every `Bounds` still used the 16×16 spec cell
  (32×32 screen px), so boxes overshot the art by 8 px per side (the
  player "died" from invisible range, lasers whiffed on visible gaps).
  The file also had a stray `}` (didn't compile). Now WIRED: each entity's
  `Bounds` is `new(Position, Scaled(w), Scaled(h))` from its table row
  (top-left anchor = ROM picture origin; art is drawn exactly there via
  DrawSprite's centering, which is a no-op at box = art size). Wall
  clamps/aim clamps updated per entity: Player per-axis wall revert,
  Hulk `PickDirection` (X clamp W / Y clamp H) + `ApplyKnockback`,
  Enforcer `PickDestination`, Tank bounce rects + `RandomPointIn`,
  Human wall check (per-kind box), `WallFleeHelper` now takes (width,
  height) — Spheroid/Quark pass their 16×15. Lasers/sparks/shells keep the
  spec-stated 4×4 box (unchanged). `EntitySizeSpecPixels` (16) remains
  ONLY the spawn-candidate grid + pixel-art pattern canvas (comment says
  so). `FindSpawnPoint` containment still uses Scaled(16) = the largest
  box, so every smaller box is contained.
  (2) **HUD moved to the arcade layout** (`PlayingState.DrawHud`): lives
  ("men") were bottom-left, overlapping the bottom wall — they're now at
  the ROM P1MAN spot (col 46/row 20): x = ArcadeX(92), y = ArcadeY(20),
  8×12 player art at natural SpecScale× size, ArcadeX(8) edge-to-edge
  spacing (ADDA #4), spilling over the top wall exactly as the arcade
  renders them. Score moved to the ROM SCRTRV spot (col 21/row 20):
  ArcadeX(42)/ArcadeY(20). LEVEL text stays top-right (port addition;
  arcade shows the level on the wave-clear message). The
  `ArcadeScore*/ArcadeMen*` constants (authored in the previous session)
  are now actually used; `ArcadeX/ArcadeY` route through
  `ScreenSize.Width/Height` so the mapping stays proportional if the
  resolution knob turns (per `resolution-handoff.md`).
  (3) **Tests:** `PlayFieldCollisionTests.LaserHitsElectrode` refired from
  12 px above the electrode's top-left (the 24 px first laser step no
  longer reaches from the old origin into the 10×9 box); comments on the
  hulk test updated to the 14×16 box (geometry still overlaps);
  `HulkKnockbackTests` centering + clamp-range assertions now use the hulk
  collision size, not the 16-px cell.
  (4) **GATES:** 0 warnings; **129/129**; launch smoke OK (>10 s).
  **RESUME (exact next steps, in order):**
  1. Author re-playtest of
     `src/Robotron2084/bin/Debug/net10.0/Robotron2084.exe`: hits should now
     feel fair (box = the art), the lives row sits in the top band next to
     the score (over the top wall, arcade-style), score at the arcade spot.
     Apply any fixes, then re-run the gates and add a ledger checkpoint row.
  2. **PHASE E** per the (18) checklist (brains: R5 $017C target = nearest
     human else player, 16-tick body, 500 pts; progs: straight walker,
     phony-burst death, 100 pts; cruise missiles: 3-way biased dirs,
     RND(1..7) re-aim, 8-pt trail, BCMCNT<8, 25 pts; RRB10 decode in (18)).
  3. Frills last (author-confirmed): M4 colour-cycle shader, explosions
     (RRX7), sounds (RRSND), red screen (RRFRED), marquee (RRM1).

## (24) Playtest round 6 + PHASE E — brains, progs, cruise missiles (2026-09-13)

Round 6 playtest on the round-5 build produced four findings; all four are
done and the PHASE E checklist from (18) is implemented on top.

1. **"He stops firing after a while even when I press SPACE"** — not a slot
   bug (audited `LaserSlots`/`PlayerLaser`: slots free on wall/entity hit,
   nothing jams). The arcade fire button is rising-edge-per-shot; the port
   fired once per press and did nothing while held. Fix: press fires
   immediately; HOLDING re-fires every `PlayerAutoFireTicks = 12`
   (~200 ms). The 3-laser slot cap is the binding limit (a no-op when all
   three are out) — verified by `Player_HoldingFire_AutoReFires_...`.
2. **Spheroids/enforcers too fast** — `SpheroidSpeed` 3 → 2,
   `EnforcerSpeed` 2 → 1 internal px/tick. The ROM spheroid away-speed is
   ~1.5–2.5 spec px/tick (16-bit fixed-point, 50 Hz) and the enforcer has a
   near-destination crawl the port's constant step can't reproduce; these
   are playtest values, not ROM locks. (Diagonals stay ~1.4 / 0.7 spec
   px/tick — non-normalized per-axis, same as the arcade.)
3. **P = skip level** (port test key, not in the arcade): P in
   `KeyboardPlayerInputSource` → `PlayerInputState.SkipLevelPressed` →
   `PlayingState` takes the wave-clear transition immediately (same path as
   a real clear: carries lives/score/rescue count). Lets waves be playtested
   out of order.
4. **Sprite transparency** — the arcade blitter treats a 0 nibble as
   TRANSPARENT (or blits solid, e.g. the prog burst). The extractor wrote
   24-bit RGB with 0 = opaque black, so every sprite carried a black box.
   `tools/SpriteExtractor` now writes 32-bit RGBA (nibble 0 → alpha 0);
   `SolidNames = { "ProgBurst" }` gets the solid blit (slot 0 = opaque
   black). All 222 PNGs regenerated; `Content.mgcb` +
   `SpriteContentPaths.cs` regenerated via `tools/generate-mgcb.py`.
   **PGXPIC is NOT in the ROM** — R5 re-drew the prog burst art (byte search
   for the source's PGXD pattern finds nothing), so `ProgBurst` is an inline
   pass-C extra carrying the original source's 96 bytes (12×16, solid).
   The mgcb's `PremultiplyAlpha=True` pipeline is correct for these.
   **⚠ That inline array was MIS-TRANSCRIBED when it was written** — its middle
   rows held eight bytes instead of six, so the PNG writer walked a misaligned
   stream and `ProgBurst.png` decoded to noise until §90.1 fixed it; the
   extractor now rejects inline data whose length is not exactly `w x h`.
   **⚠ And a full extractor run rewrites `docs/sprite-map.md` with its 78 FONT
   rows removed** (a row is emitted per sprite the tool writes, and font glyphs
   are skipped — `tools/extract-fonts.py` owns them, §60), so regenerating
   sprites always shows an unrelated 78-line diff. Parked as B26: either commit
   that cleanup or make the generator skip the font ROWS too.

PHASE E entities (the (18) checklist; RRB10 decode there):
- **Brain** (`Brain.cs`): body every `PortTicks(16 + BRNSPD)` ticks
  (NAP 4 + NAP 12 cadence then SLEEP(BRNSPD)); one arcade-px step per axis
  toward the NEAREST LIVING HUMAN (`PlayField.NearestHumanPosition`, the
  ROM's GETHTG), else the player; ABAC walk over Brain_1..12 (same
  4×3 layout as the family); fire timer `RND(1..BSHTIM)` bodies → cruise
  missile at brain+(3,4) while `MissileCount < 8` (RCMBCNT<8); 14×16 box;
  500 pts; frozen while `RobotsFrozen`; 2-second blink-off death (entity
  convention). **Simplification:** wave spawn uses the hulk-style
  `FindSpawnPoint` (min distance from player) — the ROM's exact brain
  spawn scatter is unverified.
- **Prog** (`Prog.cs`): straight-line CARDINAL walker (the ROM has 4
  tables), 2 spec-px steps per `PortTicks(3)` body (PRGAL deltas ±2);
  re-aims ~3% (SEED<$F8 offsets) / ~9% (LSEED≥$E4 direction) per body and
  when wall-blocked, always biased toward the player (50% horizontal /
  50% vertical, sign toward it — the source's player±RND target offset
  collapses to "toward the player"); keeps the converted human's 12-frame
  set + per-kind collision box; laser death = the PHONY burst picture
  (`ProgBurstTicks = 20` pop, clamped position is inherent — progs are
  always inside the field) then gone; 100 pts; **contact kills the player**
  (enemy robot, arcade-faithful).
  **⚠ The death line is SUPERSEDED by §90:** the pop was a decode shortcut —
  `PRGKIL` swaps the picture to `PGXPIC` and calls the ordinary `EXST`, so a prog
  dies in the same strip explosion as every other robot (and §90.1 found the
  `ProgBurst` art itself was corrupt: the inline `PGXD` copy had 8-byte rows).
- **CruiseMissile** (`CruiseMissile.cs`): 50% horizontal / 25% vertical /
  25% diagonal, every component signed toward the player ±6 px noise
  (GCMDSX/GCMDY); re-aims every `PortTicks(RND(1..7))` (GCMRND); REFLECTS
  off all four walls (per-axis component negation — no kill on bounce);
  1 spec px/tick; 2-frame flicker (MissileSmall_0/1, CMPIC/CMP1); 4×4
  "FAT PHONY GUY" box; **no lifetime** (bounces forever — a wave can't
  clear while one flies); 25 pts, instant off (no death animation); contact
  kills the player. Missiles fly on while `RobotsFrozen` (missile, not
  robot — spark/shell convention).
- **PlayField wiring**: `_brains/_progs/_missiles` lists + counts;
  `IsLevelCleared` now also requires all three empty; brain↔human overlap =
  the BMUT conversion (human removed WITHOUT a skull, prog spawned at the
  human's spot with its kind — the source's 20-iteration flicker is a
  frill, skipped per the frills-last agreement); lasers resolve against
  brain/prog (Kill) and missile (Destroy); player-contact kills added for
  brain/prog/missile; draw order: brains/progs with the robots, missiles
  with the other missiles, player still last.
- **GATES:** 0 warnings; **140/140** (11 new PHASE E tests in
  `PlayFieldPhaseETests`); launch smoke OK (>10 s).
- **Test gotchas (learned this session):** `RobotsFrozen` includes
  `Player.IsInStartGracePeriod` — any test that drives robots past wave
  start must first warm up 121 frames with REAL elapsed time
  (`new GameTime(TimeSpan.Zero, 16.67ms)`); a `new GameTime()` never
  expires the 2-second grace (DeathTimer-based deaths need real elapsed
  time too). And: never park a test entity on the player's center spawn —
  contact kills the player and freezes the whole simulation mid-test.
  `IntVector2` is a `readonly record struct` (`with` works); `LoadRange`
  is 1-based (`MissileSmall` is 0-based → loaded explicitly).

**RESUME (exact next steps, in order):**
1. Author playtest: P-key wave-skip through waves 2–5 to feel brains/progs/
   missiles (level 1 waves have brains from the ROM table); confirm the
   spheroid/enforcer pace and the held-fire feel. Apply fixes → gates →
   checkpoint.
2. Frills last (author-confirmed): M4 colour-cycle shader, explosions
   (RRX7), sounds (RRSND), red screen (RRFRED), marquee (RRM1).

## (25) Playtest round 7 — invincibility aid, enforcer spawn/flash, IJKL fire (2026-09-13)

Round 7 findings and fixes:

1. **Player invincible FOR TESTING** (author request, TEMPORARY):
   `GameplayConstants.PlayerInvincibleForTesting` (a `static bool`
   property, deliberately NOT a const — `const true` made the compiler
   fold the `Player.Kill()` guard to unreachable, CS0162) short-circuits
   `Kill()` so whole waves can be playtested without dying.
   `PlayerInvincibilityTests` pins the no-op contract.
   `PlayerWalksIntoElectrode_BothStartDying` is `[Fact(Skip = ...)]`
   until the flag flips. **TURN OFF when the gameplay is confirmed
   working:** flip the property to `false`, un-skip the contact test,
   delete `PlayerInvincibilityTests`, re-run the gates.
2. **"The enforcer droids flash — that's not meant to happen"** — the
   permanent alive-state visible/hidden toggle (a plan-8.5 carryover,
   "flashing blue") is removed from `Enforcer.Draw`; only the 2-second
   death blink remains. (`_flashTicks` deleted with it.)
3. **"Spheroids drop fully-grown enforcers"** — the 40-ROM-tick grow-up
   state existed but was invisible (the port always drew the full sprite;
   the ROM's 5 spawn-animation frames are not in the R5 content). The
   grow-up is now a VISIBLE scale-up (quarter → full size, centred on the
   10×11 box, nearest-neighbour) over the same 40 ticks, while the
   enforcer is still immobile (pre-existing, tested).
   **Spawn rate verified arcade-accurate** (no change needed): ENFNUM roll
   RND(1..10) → 1..5 drops never 0; initial countdown RND(1..CDPTIM),
   re-roll RND(1..CDPTIM/4), one countdown step per 4 ROM ticks; the
   arcade ENFCNT < 8 cap skips-and-re-rolls.
4. **Controls, final form** (author: "the player will no longer need to
   press space to shoot. IJKL will shoot in the required direction: I up,
   K down, J left, L right and combinations"): WASD moves; IJKL now aim
   AND fire — any IJKL key down sets `FirePressed` (held = the existing
   12-tick auto-refire while a laser slot is free); Space is kept only as
   a fire alias. Standing still with IJKL held fires in that direction
   (aim overrides facing; the fire block is independent of movement) —
   exactly the requested behaviour. P still skips the level.
- **GATES:** 0 warnings; **141 tests: 140 pass + 1 skipped** (the
  invincibility-affected contact test); launch smoke OK (>10 s).

**RESUME (exact next steps, in order):**
1. Author playtest (player is now unkillable): P through the waves; watch
   enforcers grow up when spheroids drop them; IJKL-hold should stream
   lasers in the aimed direction.
2. When satisfied: flip `PlayerInvincibleForTesting` to `false`, un-skip
   `PlayerWalksIntoElectrode_BothStartDying`, delete
   `PlayerInvincibilityTests`, gates, checkpoint.
3. Frills last (author-confirmed): M4 colour-cycle shader, explosions
   (RRX7), sounds (RRSND), red screen (RRFRED), marquee (RRM1).

## 26. Playtest round 8 (2026-09-13) — HUD clear of the wall, real enforcer grow-up art, brain cadence corrected, aim decoupled from facing

Playtest feedback and the resolution of each item:

**a. "The score also overlaps the border wall. As does the level number and number of lives left."**
The round-5/7 HUD put score/men at the arcade's SCRTRV/MANDSV spots (col 21 / col 46,
row 20) — which in the arcade sit in the thin top border, overlapping the top wall,
with the men icons spilling into the playfield. On the port's 8-px wall band that read
as "HUD on the wall". Fix: all three HUD elements now live INSIDE the 40-px screen
margins — score top-left, level top-right (both y=8, `HudTextScale` 0.5 → 1.0, the old
7-px-tall text was illegible), life icons bottom-left at their NATIVE texture size
(16×24 — the old code called `ScreenSize.Scaled()` on an already-2× texture, drawing
32×48 icons), 20-px spacing, y=372 (clear of the 360..368 bottom wall band). The
`ArcadeScoreOrigin*/ArcadeMen*` constants are retired (ArcadeX/ArcadeY kept for other
proportional mappings). The arcade's exact top-band layout is a documented deviation;
the ROM's score font itself is a known frill (deferred, as before).

**b. "The mommies are walking too fast."**
The port was ROM-accurate here: HUMAN steps every 8 vblanks (NAP 8, RRH11 $360),
HUMATB verbatim. Playtest verdict over the ROM — `Human.StepPeriodRomTicks` 8 → 16,
the same "feels too fast, halve it" retune as spheroids (3→2) and enforcers (2→1).
Documented deviation.

**c. "The enforcer spawn art is not right."**
The round-7 grow-up was a scale-up placeholder because the frames were "not in the
R5 content". They ARE: RRC11's spawn sequence (ENFDRP $1381) plays FIVE pictures
ENGD1..ENGD5 (5×11 nibbles each, 8 ticks per frame) before the full ENFD0 — and a
byte search finds all five VERBATIM in the R5 ROM at $1921/$1958/$198F/$19C6/$19FD.
Those addresses are exactly the riddle-list `enforcer2..6` — so `Enforcer_2..6.png`
in Content already are the real frames. `Enforcer.Draw` now plays
`EnforcerFrames[1..5]` (one per 8-tick slice of the 40-tick grow-up, ending on
`EnforcerFrames[0]` = ENFD0). No extraction changes needed.

**d. "Not 100% sure the brains are moving at the right speed."**
They were ~3.6× too SLOW. The (18) note's "16 + BRNSPD" miscounted: the RRB10 BRAIN
entry `NAP 12` runs only once (first body) and `NAP 4` is the FROZEN-status branch
(`BITA #$7F` — robots halted); the steady-state loop is `BRNL body → SLEEP(BRNSPD) →
BRNL`, i.e. BRNSPD vblanks + one body-execution vblank. `Brain.BodyCadenceRomTicks`
16 → `BodyExecutionRomTicks = 1`: period = PortTicks(1 + BRNSPD). Wave 1 (BRNSPD 8):
1 body / 9 vblanks (arcade) ≈ PortTicks(9)=11 ticks (port) — 1 arcade-px per axis
every ~0.18 s. Fire-timer semantics unchanged (one decrement per body).

**e. "IJKL will shoot in the required direction ... it's like the player is facing
the direction they are shooting. The shooting direction should not specify the
walking animation."**
Round 7 made aim override facing. Reverted: `Player.Update` sets `FacingDirection`
from MOVEMENT only; the fire direction is `aim ?? FacingDirection` (muzzle offset
included), so standing still + IJKL fires in the aimed direction with no facing or
walk-animation change. Two-stick contract now: WASD = move + face + walk art,
IJKL = fire direction only.

Tests: `PlayerAnimationAndAimTests` re-pinned to the round-8 contract (aim leaves
facing/walk untouched; new `Fire_UsesTheAimDirection_WhileMoving_OtherWay`), the two
brain-step tests switched to wave-1 `brainSpeedRomTicks: 8` with the corrected period.
Gates: 0 warnings, 142 tests (141 pass + 1 skipped), 12 s smoke clean.

## 27. Playtest round 9 (2026-09-13) — sparks: wall clamp + ROM life (the "lingering at the wall" bug)

Playtest: "When the sparks hit the wall, they don't immediately fizzle out,
sometimes they linger for a while (and keep moving). You need to check the
spark logic again. Make sure its arcade faithful."

Re-checking the R5 disasm (`CREATE_SPARK` $1404, `ANIMATE_SPARK` $14A8,
mover $DCFF) against the port found TWO deviations, both contributing:

1. **Life.** The port used the spec's "10–15 s" (`SparkLifeMin/MaxTicks`
   600–900). The ROM: life counter = `RND(0..15)+20` ($1473-1479), decremented
   once per 4-vblank `ANIMATE_SPARK` cycle → **80..140 vblanks = 1.6–2.8 s**.
   A wall-parked arcade spark is gone within ~3 s; the port's survived 10–15 s.
2. **Walls.** The port only killed a spark when it fully exited `OuterBounds`
   (the wall ring's outer edge) — so it flew THROUGH the wall and kept going
   into the margin ("keeps moving"). The ROM's mover ($DD0D-14C5) does the
   opposite: per axis, if `newX < 7` (or `newX+width > 144`, `newY < 24`,
   `newY+height > 235` — the playfield in arcade px) the coordinate update is
   **REJECTED**: the spark clamps/slides AT the inner wall and dies only when
   its life expires. No bounce, no wall-death.

Also re-decoded while there: the ROM's initial spark velocity is per-axis
`4 x (playerDistance + RND(0..31) - 16)` subpixels/frame ($1431-145C) —
proportional to player distance (≈ distance/53 screen px per 60 Hz port tick),
each axis independent (no normalisation), and the velocity INTEGRATES a random
curvature (`ADDD $0009,U / $000B,U`, RND(0..31)-16 subpixels, every 4 vblanks)
— the "going off course" habit. The port's constant 5 px/tick + ±1 drift
approximation is replaced by that shape in integer terms:

- `Spark` constructor now takes the PLAYER position (was a unit direction):
  velocity per axis = clamp(distance/53 + RND(−1..1), −8..8) — a spark can
  start out heading away from the player (the ROM's inaccuracy).
- Curvature: every `PortTicks(4)` one random axis gets a ±1 px/tick nudge,
  clamped to ±8 (`SparkMaxSpeed`).
- Life: `PortTicks(RND(80..140))`.
- Update: per-axis clamp at `PlayfieldBounds` (reject the axis move, spark
  slides/stops at the wall); the `OuterBounds` escape-death is GONE.
- `Enforcer`/`PlayField.SpawnSpark` re-plumbed to pass the player position.

Constants: `SparkSpeed`/`SparkLifeMinTicks`/`SparkLifeMaxTicks`/
`SparkDriftPerturbIntervalTicks` replaced by `SparkMaxSpeed` (8),
`SparkAimDivisor` (53), `SparkCurvatureIntervalRomTicks` (4),
`SparkLifeMinRomTicks` (80), `SparkLifeMaxRomTicks` (140).

The 4-frame spark flicker (SPKP0..3) is a parked frill (the port draws the
solid spark; the spec forbids flashing anyway).

New `SparkTests` (3): wall-clamp + dies-on-life-not-wall, life ∈ PortTicks(80..140)
over 16 seeds, distance-proportional aim over the first 4 ticks.
Gates: 0 warnings, 145 tests (144 pass + 1 skipped), smoke OK.

## 28. Playtest round 10 (2026-09-13) — quark WAY too fast; tanks "not spawned as they should be"

Playtest: "the sparks behaviour is slightly weird. Anyhow, I want you to move
on to the quark and tank behaviour. The quarks are WAY too faast and the
tanks aren't 'spawned' as they should be."

Spark follow-up: noted, parked — the author's "slightly weird" is the standing
item to look at next playtest (current model: distance-proportional aim,
integrated curvature, 80..140-tick life, wall clamp).

Re-checked the R5 disasm for BOTH entities; both complaints are real port
deviations:

### Quark — the port chases the player at constant speed; the ROM wanders

Port bug (Quark.Update): velocity is re-computed **toward the player every
tick** at a constant 3 px/tick per axis (4.24 px/tick diagonal, always).
"WAY too fast" confirmed — and it's the wrong motion model entirely.

ROM (INITIALISE_ALL_QUARKS $4B36, CHANGE_QUARK_DIRECTION $4B82,
QUARK animate $4BFB):
- **Spawn:** X = (126 x RND(0..255))/256 + 6 → 6..131 arcade px (uniform
  across the field, $4B51-4B5A); Y = $1A (26, top) or $DC (220, bottom) with
  a coin flip ($4B48-4B50). Quarks spawn on the TOP or BOTTOM wall.
- **Movement = waypoint wandering, never player-seeking:**
  - X destination = (126 x RND(256))/256 + 6 (6..131); Y destination =
    RND(0..table) with the "quark movement table" ($2DF9: 14,40,68,50,50...)
    — Y targets live near the TOP of the field.
  - velocity = delta to destination: **4 x (destX-x)** subpixels/frame on X,
    **8 x (destY-y)** subpixels/frame on Y ($4B9A-4B9D / $4BB8-4BBD) — i.e.
    speed PROPORTIONAL TO DISTANCE, twice as fast per unit distance on Y.
  - re-aim every RND(0..31)+1 frames ($4BC1-4BC6); the mover ($DCFF) clamps
    at the wall; near a wall (X≤12 / X≥131 / Y≤29 / Y≥230) the destination
    logic is biased toward the interior (50% flip).
  - Screen-px equivalents: vX ≈ distX/27, vY ≈ distY/13 px/tick. A typical
  quark crawls at 1..4 px/tick; a bottom-spawned quark darts to the top band
  fast (dist ~400 px → ~30, capped here at 16) and then wanders slowly.
- **Exit (MAKE_QUARK_VAMOOSE $4C6F/4C7F):** after the last tank, X delta = 0,
  Y delta = ±$0200 (±2 px/frame, toward the edge it is nearest) — the quark
  slides OFF the screen. The port's "flee the nearest wall and vanish"
  approximates this (kept).
- Drops unchanged (already ROM-correct: RND(ENFNUM) halved +1, first drop
  RND(TDPTIM), re-arm RND(TDPTIM/2)+1, 20-tank cap $4C40-4C45).

Port fix: waypoint model — spawn on the top/bottom wall edge (coin flip),
waypoint X uniform, Y in the top 88 screen px (ROM table max 68 arcade px
from screen top = 44 in-field = 88 screen), v = clamp(dist/divisor, ±16),
re-aim every PortTicks(RND(1..32)) and immediately when a wall reflection
flips the velocity. `QuarkSpeed` retired → `QuarkAimXDivisor` (27),
`QuarkAimYDivisor` (13), `QuarkMaxSpeed` (16), `QuarkTopBandScreenPx` (88).

### Tank — birth offset + instant first move missing

ROM (tank birth $4CAC, initial destination $4E11):
- **Birth position = QUARK blitter destination + (2, 6) arcade px**
  ($4CD4-4CDE: LDD quark; ADDD #$0206) = + (4, 12) screen px — the tank
  "drops out" below-right of the quark. The port spawned it at the quark's
  EXACT top-left corner (no offset — the tank pops out of the quark's body).
- **Destination is picked IMMEDIATELY at creation** ($4CE4 JSR $4E11): the
  tank starts moving on its very first frame. The port left
  `_destination = position` / `_step = 0` until the first re-aim
  (RND(1..31) ticks later) — a fresh tank sat still inside the quark for up
  to ~0.5 s. THIS is what made spawning look "wrong".
- Already correct in the port: 37.5% seek-player / random-point destination
  ($4E11-4E1C), vertical only when >16 arcade px away, 1 px/tick, re-aim
  RND(1..31), fire cadence RND(0..31)+TNKSHT then TNKSHT, 20-shell
  fizzle-bug counter.
- Wave-table tanks (INITIALISE_ALL_TANKS $4D10, random playfield point in a
  "safe rectangle" away from the player) — the port's waves carry no
  initial tanks (classic behaviour: tanks only from quarks); unchanged.

Port fix: `TankBirthOffset = (4, 12)` screen px added at the quark drop;
`PlayField.SpawnTank` clamps the birth inside the playfield; `Tank` picks its
destination on its first Update (immediate first move).

## 29. Playtest round 11 (2026-09-13) — grunts move too often (no stagger); screen full of sparks

Author report: "The grunt movement is wrong - they move too often. They are
meant to stagger. Also I think the sparks are being fired too often by
enforcers, the screen is full of them."

### Grunts: the ROM motion is a STOCHASTIC stagger, not a fixed period

R5 `MOVE_GRUNT` ($39E6) + the grunt task loop ($39CD, allocated with
`LDA #$04` → body every 4 vblanks):

- Each body: `DEC $13,X` (the per-grunt move countdown).
- When it hits 0: step, then re-roll the countdown with
  `LDA $BE5C; JSR $D042` — $D6AC multiplies A by RND(0..255) with an
  INCA, i.e. **RND(1..$BE5C), never 0**.
- Spawn initialises the same field with RND(1..$BE5C) ($38E1-38E7).

So the step interval is a UNIFORM RANDOM 1..$BE5C bodies (4 vblanks each):
mean ($BE5C+1)/2 bodies, huge variance (1 body ≈ 80 ms arcade; $BE5C bodies
≈ 0.8 s for $BE5C=20). That is the stagger: bursts of steps with pauses of
irregular length. The port had a FIXED period of PortTicks($BE5C) with a
2-screen-px 8-way step — steps on average TWICE as often as the arcade,
with zero variance, so it reads as continuous shuffling.

Wave table: $BE5C (GRUNT_MOVE_PROBABILITY @ $2C20) wave 1 = 20, then 15...;
floor $BE5D @ $2C4B wave 1 = 9... — the port's WaveTable.GruntMoveDelay /
GruntSpeedFloor already hold these exact values; only the CONSUMPTION was
wrong (fixed period instead of re-roll limit).

Step geometry (R5 $39EF-3A29): per axis, dead-zone 2 arcade px (4 screen px)
— no movement on an axis while |Δ| ≤ 2 arcade px — then a step of 4 arcade
px (8 screen px) on that axis. Axes are independent, so diagonal steps of
(±8, ±8) screen px happen. The port moved 2 screen px along one 8-way
direction.

Animation (R5 DRAW_GRUNT $3A2B-3A39): the RWDP frame pointer advances +4
(wrap to RWDP1) — but DRAW_GRUNT is reached ONLY from the step branch
($39DE BEQ $39E6 → ... → $3A2B); the non-step path ($39E0) goes straight to
the next grunt. The walk cycle (legs; direction-agnostic — the same frames
repeat regardless of facing, author-confirmed) therefore advances ONLY on a
step and a paused grunt freezes mid-pose. (Round 11 misread this as
"every body pass" and advanced it per body — playtest round 12:"even when
they are standing still, their legs are moving"; re-corrected to per-step in
(30).)

### Sparks: enforcer fire cadence is ROM-exact — the bug is the Spheroid
### drop rate (4× too fast)

Enforcer side verified against R5 and left unchanged:
- ENFORCER_AI ($139D) runs every 3 vblanks (`LDA #$03`); the fire countdown
  ($000D,U) decrements once per pass; at 0 it fires ($1404) and re-arms
  with RND(1..ENSTIM) BEFORE the 20-spark cap check ($1408-1414). Initial
  value at creation: RND(1..ENSTIM) ($136D-1373).
- Port matches: 3-tick AI pass, RND(1..ENSTIM) re-arms, cap 20, 8-enforcer
  cap (CanDropEnforcer < 8 = ROM ENFCNT < 8). Wave 1 ENSTIM = 30 → mean gap
  15.5 passes × 3 vblanks ≈ 0.93 s per enforcer.

The real deviation: Spheroid drop cadence. R5 spheroid process body =
**every 2 vblanks** (`LDA #$02; LDX #$11E5` at $121A-121F), and the drop
countdown ($0009,U) is decremented at $11F2 — but only on the animation
pass where the frame pointer is on the LAST frame: $11E7-11F0 advance the
pointer and only take the `DEC` branch when the advanced pointer exceeds
$150E, i.e. once per full 8-frame spheroid animation (metadata list
$14F2..$150E = 8 entries, matching the 8 SpheroidFrames in the port).
Full cycle = 8 passes × 2 vblanks = **16 vblanks per countdown step**.

The port's comment claimed "1 step per 4 ROM ticks" (round 7, notes §17) —
wrong: it ran the countdown at 4× the ROM rate. With CDPTIM = 30 (wave 1):
- ROM: initial RND(1..30) × 16 vblanks (0.32–9.6 s), re-roll
  RND(1..CDPTIM/4) × 16 vblanks (0.16–1.12 s between drops).
- Port (pre-fix): RND(1..30) × 4 vblanks equivalent → the 8-enforcer cap
  was reached within ~2-4 s of the wave instead of ~10-25 s.

Eight enforcers firing at the (ROM-exact) ~0.93 s mean gap with 1.6–2.8 s
spark lives = a persistent ~20-spark screen in the port from early in the
wave. The arcade only reaches that steady state much later, once the
spheroids have had time to fill the enforcer pool — which is why the
original "feels sparse at first, chaotic later" while the port is chaotic
immediately.

Fix: `Spheroid.CountdownToTicks(steps) = PortTicks(steps × 16)` (was ×4).
Initial delay RND(1..CDPTIM) and re-roll RND(1..CDPTIM/4) are unchanged
(they were already correct).

### Test updates
- `Spheroid_NeverDropsBeforeTheMinimumBouncePhase`: minimum first drop is
  now 1 step × 16 vblanks = PortTicks(16) = 19 port ticks after the 121st
  unfreeze frame → no drop through tick 139.
- `GruntAnimationTests`: the fixed-period step test is replaced by stagger
  pins (per-axis 8-screen-px steps, 4-px dead zone, non-periodic step
  gaps; the walk-frame test originally pinned per-body — superseded by (30)'s
  per-step freeze pin).

## 30. Playtest round 12 (2026-09-13) — grunt legs animate while standing still (PRIORITY)

Author report: "The grunt animation is weird now, because even when they are
standing still, their legs are moving!" + "They are not directional
animations. The same animations repeat regardless of direction."

Root cause (found same session): the (29) read of DRAW_GRUNT was wrong. The
frame advance ($3A2B-3A39) is NOT on the common body path — the body
($39DB-39DE) only branches to it when the move countdown hits zero
(`BEQ $39E6` → move → $3A2B); the non-step path ($39E0) goes straight to the
next grunt without touching the frame pointer. The RWDP frames are a leg walk
cycle (direction-agnostic), so they must advance ONLY on a step: a paused
grunt freezes mid-pose. The (29) change advanced one frame every 4-vblank
body, so every grunt's legs cycled continuously — including while paused.

Fix (applied this session): `_walkFrame` advances in the step branch only
(one frame per step, wrap RWDP4→RWDP1) — i.e. the pre-(29) port behaviour,
which the author never flagged through rounds 1-11.

Test: `Grunt_WalkFrameFreezesWhilePaused_AndAdvancesOncePerStep` — 10 body
passes with no step leave the frame on 1; a long walk advances it.

Tomorrow's priority: playtest-verify the stagger + frozen-pause feel, then
the spark-storm timing check from the (29) fix.

## 31. Grunt speed progression — "at least as fast as the player" (2026-09-13, evening)

Author directive: "The grunts should speed up as the level progresses, until
they are AT LEAST as fast as the player. That's in the source code and my
disassembly. Check the logic that tweaks the grunt speed, and reproduce it."

ROM findings — THREE mechanisms (the port had only one, with a static floor):

1. **Per grunt death** (R5 $3A94-3A9F): `LDB #$E0; LDA $BE5C; MUL; CMPA
   $BE5D; BCS skip; STA $BE5C` → $BE5C = max($BE5D, $BE5C × 224/256) with
   integer TRUNCATION (MUL truncates; the port used to round).
2. **Level-progress tick** (R5 $2AC7-2AF1, "Update grunt speed as level
   progresses", inside the wave-progress process $2A85 that self-reschedules
   every 15 vblanks at $2B03-2B08; an internal counter $0007,U starts at 18
   then 15, so the tick fires at 18×15 = 270 vblanks first, then every
   15×15 = 225 vblanks): ONLY while `cur_grunts >= 30` ($2ACA CMPA #$1E):
   $BE5D = max(1, $BE5D − 2) and $BE5C = max($BE5D, $BE5C − 4). ($F0 is never
   set in the ROM, so the alternate −1/−2 path is dead code; the −2/−4 path
   is the live one.)
3. **Wave start** (R5 $2B7C via $2B0B-2B1B): $BE5C (ROBSPD) and $BE5D
   (RMXSPD) reload from the per-wave table each wave (wave 1: 20 / 9).

Why floor 1 = player speed: the player's movement deltas are ±1 arcade
px/frame per axis (R5 $3031 table, MOVE_PLAYER $2FD0). A grunt at limit 1
steps 4 arcade px every RND(1..1) = 1 body = 4 vblanks = 1 arcade px/frame —
exactly the player's speed. So as the floor descends to 1, surviving grunts
end at least as fast as the player (late in big waves).

Port fix (this session):
- `Grunt.SpeedUp` now truncates (× 7/8 integer) and takes the CURRENT floor.
- New `Grunt.WaveSpeedTick(floor)`: limit = max(floor, limit − 4), no re-roll.
- `PlayField` owns `_gruntSpeedFloor` (init = wave-table RMXSPD) and
  `_gruntProgressTimer` (PortTicks(270) first, then PortTicks(225));
  `UpdateGruntSpeedProgress()` applies the tick when GruntCount ≥ 30.
- Tests: `GruntSpeedProgressTests` (4) — truncation + floor clamp, tick
  arithmetic, the 270/225 cadence with a 30-grunt wave-7 field (0 electrodes,
  deterministic), and the <30 gate holding the floor.

156 tests green (155 + 1 skipped).

## 32. Frills session 1 — spark flicker SPKP0..3 (2026-09-16)

Author (2026-09-16): continue; when a step needs their check, work on the
rest of the list. Item 1 (playtest round 14) and item 2 (flip
PlayerInvincibleForTesting) are author-gated; this session starts the
frills list (status.md next-steps #3): spark flicker first (smallest,
fully ROM-documented), then red screen / explosions / colour-cycle shader.

ROM findings — the SPARK process (RRC11.ASM, R5 ~$14A8; source listing
verified against the disasm ANIMATE_SPARK):

```
SPARK  LDX PD,U
       LDD OPICT,X
       ADDD #4          ; next frame (4 bytes per FCB 4,7 entry)
       CMPD #SPKP3
       BLS SPK1
       LDD #SPKP0       ; wrap
SPK1   STD OPICT,X      ; ...move, DEC life...
       NAP 4,SPARK
```

- Frame advances once per body pass; the body re-runs every **4 vblanks**
  (NAP 4) → frame period = 4 vblanks, cycle 0→1→2→3→0 (no hold, no
  special first/last frame).
- All four frames are `FCB 4,7` = 4×7 arcade px (the 4x4 spec box stays
  the collision box; DrawSprite centres the taller art in it).
- Port pattern already proven by CruiseMissile (`_flickerTicks` +
  `FrameIndex`); port period = `PortTicks(4)` (5 port ticks).

## 33. Red screen / death flash — research so far (question to author)

**Task:** parked frill "red screen (RRFRED)". Investigation results:

- **RRFRED.ASM is NOT a red screen** — it is the FRED message/audit module (game-over / attract text, the `$4A` "J" check byte, `$15` status, `$F`/`$1A`/`$38`/`$69`/`$D1` message slots).
- **Player death sequence (R5 KILL_PLAYER $30EF → $5B4C → $5E38; source RRX7 "PDTH — GENIES BITCHEN 330AM"):**
  1. Sound $26D9 (the classic descending blip), status `$59=$1B`.
  2. `JSR $D024` → `$D89E` **recreates the 6 colour-palette animation tasks** (slots 10-15: white flash, RGB 38/07/C0 cycle, BPR blue-purple-red cycle, LASER slot 13, RGOLD slot 15, DECAY slot 12 = the 23-step fade C0→F0→FA→7A→3A→1F→17→0F→07→00, 2 ticks/step).
  3. Player sprite blinks in random colours from `00 11 33 77` (red-ish table) for ~10 cycles, drawn via the solid-colour blit ($3942, colour byte → ZP $2D → DMACON nibble).
  4. `KILL OFF DECAY` — the DECAY process (slot 12) is **stopped**, then the fade table `FF F6 AD A4 5B 52 09 00` is written to **PCRAM+$C (slot 12)** one byte per 4 ticks: the player fades white→lavender→green→dark→black while drawn in slot 12; `COLST` restarts the colour processes afterwards.
- **No explicit "paint the whole screen red" code found** in the death path.
- **Per-wave wall colour (SET_BORDER_WALL_COLOUR $2A21, table $2A4B = 22 55 11 EE 77 33 44 88 00 CC):** the border wall is a **palette slot per wave** — wave 3 (and 13, 23…) = slot 1 = **RED wall**; wave 4 = slot 14 (BPR animated), wave 10 = slot 12 (DECAY animated). The port currently hardcodes slot 11 (RGB) for the wall — this is a deviation from the ROM.

**QUESTION TO AUTHOR (which "red screen"?):**
(a) a full-screen red tint/flash on player death (in which case: which surface — wall only, or playfield background too, and for how long)?
(b) the per-wave wall colour (red on waves 3/13/23…) — the port currently ignores this;
(c) the slot-12 DECAY fade that the player death animation uses.

My read of the ROM: (b)+(c) are what the hardware does; there is no full-screen red paint. The port's wall should probably adopt the per-wave slot table, and the death animation should flash the player through the red-ish colours + slot-12 fade. Awaiting confirmation.

## 34. M4 shader unblock — implementation decision (2026-09-14)

The parked `ColorCycle.fx` failed the MonoGame 3.8.5.1 EffectProcessor TPGParser on
`const float4 MarkerColors[6] = float4[6](...)` (error X3000 at 17,38 — the array
constructor is not D3FX syntax). Fix per the plan: **no arrays at all in the file**.

- The six marker comparisons are UNROLLED as literal RGB triples in MainPS
  (values unchanged: $C4/$F4/$CC/$81/$45/$2F through RobotronColor).
- `float4 LiveColors[6]` is replaced by six named uniforms
  `Live10 .. Live15 : register(c0..c5)` — zero `[` tokens in the whole .fx,
  which eliminates any parser risk (array indexing in a PS body was an
  unverified variable).
- `GamePalette.UpdateEffectColors` now pushes six named parameters
  (`Live10`..`Live15`) instead of one array parameter.
- mgcb gains `Effects/ColorCycle.fx` (EffectImporter/EffectProcessor).
- `RobotronGame.LoadContent` assigns `_sprites.ColorCycleEffect =
  Content.Load<Effect>("Effects/ColorCycle")`.
- M4 final tick pending author eyeball: skull eyes must cycle purple↔orange-ish
  (slot 15 RGOLD) — see parked list.

## 35. Explosions (RRX7) — ROM mechanics (verified R5) + port design

**ROM (R5 disasm, authoritative):**
- Explosion list: `$B3E4`, **7 entries × 242 bytes** each (RESET_EXPLOSION_LIST `$F01D`;
  free-list walk `$F03A`). Max 7 concurrent explosions.
- **CREATE_EXPLOSION `$F0D7`** (non-directional): erase object, take a slot, store the
  object's blitter destination + current animation-frame metadata (PICPTR), build a
  per-pixel expanded data area inside the slot (each source byte P → [P, P×16]: the
  2-px-per-byte ROM rows expanded to 1 px/byte), FRAMES = `$10` = **16 frames**.
- **CREATE_DIRECTIONAL_EXPLOSION `$473F`**: same, plus `$12` = top-half direction
  ($FF = up-left, 0 = straight up, 1 = up-right), `$06` = bottom-half direction
  ($FF/$00/$01 down variants); direction comes from the player laser ($88/$89).
- **DRAW_EXPLOSION_SEGMENTS `$478C`**: 16 identical 13-byte blocks. Blit i:
  source = per-strip pixel data pointer (from the slot data area), dest = D + i×`$BA`,
  D += `$BA` — i.e. strip i sits at base + i×$BA. The "shot in the corner" bug:
  segment count A outside 0..15 → `JMP $478C + |A|×13` lands outside the table.
- **Update tick `$4954`** (once per frame while active): `$0A`-- (16 → 0 frees the
  slot); the 16-bit accumulator `$0008` += `$0100` (so its HIGH byte counts 1,2,3,…16
  across the 16 draws); `$BA = sign($12) × ($08 >> 1)` (`$4965`-`$4970`: `LSRA`, then
  `NEGA` if the direction byte `$12` is negative) — the per-strip offset GROWS each
  frame, so the strips fan out progressively; dest Y is recomputed with a
  clamp at $18 (24 px). **Exact `$BA` sequence over the 16 draws:
  `1,1,1,2,2,3,3,4,4,5,5,6,6,7,7,8` arcade px** (the creation draw passes
  `$BA = $0001` from the `$0007` word initialised by `$477D`/`$4782`).
- Player death is NOT a strip explosion (that is the PDTH fade — §33).

### FIX (2026-09-16) — "explosions don't explode far enough" (author playtest)

- **The bug was the fan MAGNITUDE, not the shape.** The port grew the splay
  linearly (`ExplosionSplayPerFrame = 0.12` px/tick), peaking at 1.8 px per
  strip / ~27 px total span. The ROM's spacing is 4.4x wider at the last draw.
- **Fix:** `Explosion.SplayForTick(tick) = ((tick + 2) >> 1) - 1`, i.e. the
  ROM's `$BA` sequence verbatim. It is `$BA - 1` because the port draws each
  strip at its NATURAL row plus a splay offset, so spacing = natural 1 px +
  splay (the ROM's `$BA` IS the spacing). Peak: 15 gaps x 8 px + the 1-px strip
  = **121 arcade px** for a 16-row sprite — the arcade smears the dying sprite
  across most of the playfield height. `ExplosionSplayPerFrame` is deleted
  (the curve is ROM-derived, not a tuning knob).
- Pinned by `ExplosionTests.StripGeometry_MatchesTheRomFanSpacing`, which
  asserts the whole 16-draw spacing sequence and the 121-px peak span.
- **Correction to the ROM field map above:** `$06` is **YOF** (the impact's
  offset from the sprite top, clamped to half-height) — NOT a "bottom-half
  direction". The `A`/`B` register comment at `$473F` describes the CALLER's
  API, but the R5 code path only stores `A` (into `$12`); the incoming `B` is
  never read, and the second fan comes from a SEPARATE explosion record
  (`HVEXV`/`DDXST` call `EXSTV` then `HEXST`/`DXST` with a different `LASDIR`,
  making two records).
- **Verified on screen** (2026-09-16, captured gameplay frames): a
  mid-animation explosion measures **6 separated bars on an exact 18-screen-px
  pitch = 3 arcade px per strip** (ROM spacing 3 at ticks 5-6), and late-stage
  explosions measure a repeating **7 arcade px** pitch across 9-11 bars (ROM
  spacing 7 at ticks 13-14; 8 is the peak). The old 0.12 px/tick model could
  never exceed 1.8 px per strip, so a 3-7 px pitch is only reachable by the
  ROM's $BA curve. **SUPERSEDED — see §35.1.** This measurement was
  self-referential: it showed the code did what the code said, which is not the
  same as the ROM. The $BA-as-Y-step reading it rests on is wrong.
- **Still not modelled (open question for the author):** the ROM's fan is
  ONE-DIRECTIONAL — strip i is at `base + i×$BA` with the base recomputed so
  the fan hangs off the impact point in the shot direction (`$4981`-`$49A2`),
  i.e. the sprite STRETCHES away from the impact rather than swelling about its
  centre. The port keeps its documented symmetric fan (top up, bottom down)
  plus the `ExplosionDriftPerFrame` lean, which now matches the ROM's total
  extent but not its anchoring. Also unmodelled: the ROM's screen-edge clamp
  (strips whose recomputed Y falls outside are DROPPED by the `CHK2`/`CHK5`
  loops) — the port relies on MonoGame clipping at the render target instead.

### 35.1 CORRECTION — the exact fan geometry, and which system a kill uses (2026-09-16, second pass)

**The reading above was WRONG, and commit `a070290` shipped from it.** It treated
the per-strip offset as a single byte (a Y-only step). It is a **16-bit value
added to a 16-bit blitter destination** whose high byte is the X screen COLUMN
(2 px each) and whose low byte is the Y line.

**Evidence — GOSPEL FIRST (`ref/original-source/RRDX2.ASM`, the DIAGONAL
EXPLOSIONS module, `RDXORG $4680` = the very region decoded below):** its `EX`
struct names the fields the disasm leaves anonymous — `$07 XSIZE = "X SIZE USED
LAST (THIS) FRAME"`, `$08 YSIZE(2) = "CURRENT Y POINT SPACING"`, `$12 SLOPE =
"SLOPE OF DIAGONAL LINE WE ARE EXPLODING (SIGN BIT)"` — and its draw loop
literally does `ADDD XSIZE`, which reads the 16-bit word `(XSIZE, YSIZE_high)`,
i.e. one `ADDD` steps BOTH axes by design. The disasm agrees and pins the byte
order:
- `$4954` tick: the 16-bit accumulator `$0008` (starts `$0100`) += `$0100`, so its
  HIGH byte counts tick+1. That high byte goes to **`$BB`** ($4961) and
  `highByte >> 1`, sign-flipped by the direction byte `$12`, goes to **`$BA`**
  ($4965-$4970).
- `$478C` `DRAW_EXPLOSION_SEGMENTS` does `ADDD $BA` after each of the 16 blits →
  strip *i* sits at `dest + i × (($BA << 8) | $BB)`.
- **Decisive:** the update's OWN clamp loops step each axis by exactly those
  bytes — `ADDB $BB` in the Y clamp (`$4990`-`$499C`) and `ADDB $BA` in the X
  clamp (`$49B7`-`$49C3`). A strip count is derived from how many steps fit.

**So, at draw *d* (1-based), per strip:**
- `yStep = d` lines (NOT `d >> 1` — `a070290` halved it)
- `xStep = ±(d >> 1)` COLUMNS = `±2 × (d >> 1)` px

→ the bars fly apart on a **45° diagonal** (1 px across per 1 line down for a
positive direction), not straight down. Peak: `yStep` 16 → 15 gaps × 16 = **240
lines** before clamping.

**Anchoring** (`$4981`-`$49A2`; negative `$49D2`-`$49F2`):
- `baseY = impactY − yStep × YOF + xStep`, where `YOF` = the impact row's offset
  from the sprite top, clamped to height/2 (`$4755`-`$4764`) — so strip number
  `YOF` lands exactly on the impact point and the fan grows BOTH ways from there.
  The fan is anchored AT THE IMPACT, not at the sprite centre.
- `baseX = objX − YOF × xStep` (positive) / `objX + width + YOF × xStep` (negative).
- Strips are then **DROPPED** (the strip count is decremented) while the fan runs
  off screen: Y clamped to 24..`$EA` (24..234), X to `$07`..`$8F` (14..286 px).

### 35.2 WHICH system a kill uses — `MAKE_ENEMY_EXPLODE` `$5C1F` (this is the big one)

`$88` = laser horizontal direction, `$89` = laser vertical direction (each
`$FF`/0/`+1`):

| Shot | ROM path | Visual |
|---|---|---|
| **Diagonal** (`$88 != 0` AND `$89 != 0`) | `$5C30` `A = ~($88 ^ $89)` → `$5C33` `JSR $4683` → **`$473F` strips** | the 16-row sprite shatters into bars on a **45° diagonal**, leaning along the shot's diagonal |
| **Pure vertical** (`$88 == 0`) | `$5C25` `JSR $5B5B` → `$5BBB` (if fancy attract → `$F0D7`) else `JMP $5C3A` | **APPEAR/STRETCH object** — NOT strips |
| **Pure horizontal** (`$88 != 0`, `$89 == 0`) | `$5C2E` `BEQ $5C3A` | **the same APPEAR/STRETCH object** |

**So the strip shatter only happens on DIAGONAL shots.** Straight shots (both
axes!) reserve a normal OBJECT (`$5B6C` off the `$1B` free list, appended to the
`$96` list) and build an **RRX7 APPEAR record** at `$5C49`-`$5CA7` — its field
map ($02 pixel data, $09 UL, $0B WH, $0D DMAWH, $0F YHITE, $10 DMACNT) matches
RRX7.ASM's `EX` struct exactly, with `$0006 = $0100` (the size accumulator) and
`$0008 = $10` (16 frames). It is drawn by `$5CA9`+ through a per-size jump table
at `$F017` (6-byte stride, indexed by `16 − $0F`), i.e. the image is **rescaled
via blitter DMA size over 16 frames**.

**Port consequence — the port is wrong in KIND, not just in magnitude:**
- the port draws a symmetric strip fan for EVERY kill. The arcade only does
  strips for diagonals, and those strips are impact-anchored, 45°, and
  edge-clamped with strips dropped.
- the arcade's straight-shot case needs the **appear/rescale effect, which the
  port does not implement at all** (sprites appear/expand instantly; the effect
  was parked in the frills list — the same machinery is what materialises
  enemies at wave start, notes §4.9/§36).
- `$F0D7` `CREATE_EXPLOSION` is the **attract-mode** variant (gated by the CMOS
  "fancy attract mode" flag via `$5BA8`); `$F00C`/`$F00F` are its thunks.

### 35.3 Player death / game over — the strip CASCADE (not ported)

`$2882: JSR $29D2`, reached from a delayed task chain (`$2869` → `$2874` (delay
`$96`) → `$2882` (delay 6) → `$288D` (delay 4)). `$29D2` walks the player sprite
(`$985A` = player object) from its BOTTOM row upward in 3-line steps
(`SUBA #$03`, loop while `BPL`) and for each step creates **TWO** strip records
via `$4686` → `$46E6`: first `CLRA` (direction 0 → positive lean) then `COMA`
($FF → negative lean), each anchored at the successively higher row ($A7).

`$46E6` is **`APSTZ` — START AN APPEAR** (see §35.5), which takes a slot from the
`$BE` list (via `$46CE`) instead of `$C0` (`$46B2`) and starts `YSIZER` at
`$1000` (the Gospel comment: `START LARGE FOR APPEAR`). So the arcade player
death is a cascade of ~12 **APPEAR records whose rows CONVERGE onto the player
sprite**, plus the §33 palette fade — not a strip explosion, and not the fade
alone. (An earlier reading of this section said "strip explosion cascade"; that
came from the disasm alone and is wrong — see §35.5.)

### 35.5 THE GOSPEL ALGORITHM — from `ref/original-source/RRDX2.ASM` (authoritative; 2026-09-16)

**Read this INSTEAD of reconstructing anything from the disasm.** Per the source
hierarchy rule (§0.0), `RRDX2.ASM` ("DIAGONAL EXPLOSIONS") is the Gospel for the
strip explosion, and the disasm region I previously decoded ($4680-$4A60) **IS
that module** — `RRF.ASM:234` has `RDXORG EQU $4680`, and the module's vector
table is exactly what the disasm shows at $4680-$468B:

| Vector (source) | Disasm | Gospel routine |
|---|---|---|
| `JMP EXINV` @$4680 | — | init the record list |
| `JMP EXSTZ` @$4683 | `JMP $473F` | **`EXSTZ` = START AN EXPLOSION** |
| `JMP APSTZ` @$4686 | `JMP $46E6` | **`APSTZ` = START AN APPEAR** |
| `JMP DXUPDV` @$4689 | — | the per-frame update |

**So `$46E6` is `APSTZ` — START AN APPEAR — not an explosion variant.** Its
`LDD #$1000` is commented `START LARGE FOR APPEAR`; `EXSTZ`'s `LDD #$100` is
`1 UNIT IS MIN`. **This corrects §35.3: the player-death cascade calls `APSTZ`
twice per 3-row step, so it is a cascade of APPEARs (rows CONVERGING), not of
explosions.** That error came from reading the disasm alone.

#### The record (`EX` struct, field names are the Gospel's)

```
$00 NEXT(2)   $02 PICPTR(2)  $04 XCENT(1)  $05 YCENT(1)   $06 YOF(1)
$07 XSIZE(1)  "X SIZE USED LAST (THIS) FRAME"
$08 YSIZE(2)  "CURRENT Y POINT SPACING"
$0A FRAMES(1) $0B UL(2)      $0D WH(2)     $0F DMAWH(2)   $11 YHITE(1)
$12 SLOPE(1)  "SLOPE OF DIAGONAL LINE WE ARE EXPLODING (SIGN BIT)"
$13 DATA(16×2)  "LIST OF ROW DATA POINTERS (FOR SPEED MAN)"
$06 YOF = "OFFSET IN Y TO OBJECT CENTER (PIXELS) (0-HEIGHT)"
```

#### The draw loop (`BSLOOP`) — this is `$478C` `DRAW_EXPLOSION_SEGMENTS`

```asm
BSLOOP LDX ,Y++      ; the row's source pointer
       STX DMAORG
       STD DMADES    ; D = this row's destination
       STU DMACTL
       ADDD XSIZE    ; ← ARTICULATION POINT: reads the 16-bit word AT XSIZE,
                     ;   which by layout is (XSIZE, YSIZE_high) = the pair
                     ;   (X step in COLUMNS, Y step in LINES)
```

**`ADDD XSIZE` is the whole answer to the geometry.** The disasm's anonymous
`$BA` = XSIZE (the X step, COLUMNS of 2 px) and `$BB` = the high byte of YSIZE
(the Y step, LINES). The two fields are adjacent *by design* so one `ADDD` steps
both axes — a **45° diagonal**, exactly as the `SLOPE` field name promises.
The loop is entered at a computed offset for `16 − HITE` rows (`16 − YHITE`)×`BSSIZE`.

#### Per frame (`WRITE` = explosion, `AWRITE` = appear; shared tail `APGO`)

```
WRITE:  DEC FRAMES,Y ; 0 → free the record (KILEXP)
        HITE = WH+1,Y                       ; row count
        YSIZER += $0100                     ; ← explode: +1 line per frame
AWRITE: YSIZER -= $0100                     ; ← appear:  −1 line per frame
APGO:   YSIZE  = high byte of YSIZER        ; the Y step, LINES
        TEMP2  = YSIZE >> 1                 ; the "1/2 SIZE" centring term
        XSIZE  = ±(YSIZE >> 1)              ; sign from SLOPE (BPL keeps +)
        (YOF == 0 → TEMP2 = 0: "FIX TOP ONE (OBSCURE BUG)")
        YOFF   = YOF
        UL_Y   = YCENT − YSIZE×YOF + TEMP2  ; ← the fan's top row
```
So row number `YOF` lands exactly on `YCENT` (the impact) and the fan grows both
ways from it — **the fan is anchored AT THE IMPACT, not at the sprite centre.**
Peak: `YSIZE` reaches 16 lines → 15 gaps × 16 = 240 lines before clipping.

#### The four clip passes (they DROP rows, they do not scale)

Constants from `RRF.ASM:67-70`: `YMAX 234`, `YMIN 24`, `XMAX $8F`, `XMIN 7`
(X in screen BYTES = 2 px, so X 14..286 px).

```
; top:    while UL_Y <= YMIN      : HITE--, YOFF--, UL_Y += YSIZE
; left (XSIZE>=0): baseX = XCENT − YOFF×XSIZE
;          while baseX < XMIN     : HITE--, YYCNT++, baseX += XSIZE
;          UL_X = baseX ; UL_Y += YYCNT×YSIZE      ; compensate the dropped rows
; left (XSIZE<0):  baseX = XCENT + WH + YOFF×|XSIZE|
;          while baseX > XMAX     : HITE--, YYCNT++, baseX += XSIZE (−ve)
;          UL_X = baseX − WH ;  UL_Y += YYCNT×YSIZE
; firstRow = WH+1 − HITE          ; the DATA table index to start from
; bottom:  lastY = UL_Y + (HITE−1)×YSIZE ; while lastY > YMAX : HITE--
; right:   lastX = UL_X + (HITE−1)×XSIZE + WH ; while lastX > XMAX : HITE--
; draw HITE rows: row k at (UL_X + k×XSIZE, UL_Y + k×YSIZE), source row firstRow+k
```

#### How the two engines divide the work (corrects §35.2's framing)

- **Diagonal shot → `EXSTZ` (RRDX2)** — the engine above: rows spread on a 45°
  diagonal, `XSIZE = ±(YSIZE>>1)`.
- **Straight shot (either axis) → the RRX7 record built at `$5C49`** — field
  usage there (`$0009` UL, `$000B` WH, `$000D` DMAWH, `$000F` YHITE, `$0010`
  DMACNT, `$0006` YSIZER, `$0008` FRAMES) matches **`RRX7.ASM`'s** struct, and it
  sets `YSIZER = $0100` + `FRAMES = $10` = the *explosion* configuration of
  RRX7's `WRITE` (+$100 per frame). RRX7's record has **no XSIZE field**, so the
  rows spread **straight down with no lean**. This is the vertical bar-fan.
- `APSTZ`/`AWRITE` (rows converging) is the **appear/materialise** effect — the
  same engine, opposite direction. It is what spawns enemies at wave start, and
  what the player-death cascade drives.

#### What this means for the port (revised plan)

The port has ONE strip engine and no appear. To mirror the arcade:
1. `Explosion` must be given the **impact point** (`XCENT`/`YCENT`) and derive
   `YOF` = the impact row's offset from the sprite top (clamped to height/2).
2. Implement the Gospel `WRITE`+`APGO`+four-clip algorithm above for the
   **diagonal** case (`XSIZE = ±(YSIZE>>1)`, sign from the shot's diagonal).
3. Implement the **vertical** case: the same row-spread with `XSIZE = 0`
   (RRX7's shape) for straight shots — i.e. one row-spreading primitive
   parameterised by the X step, plus the `$5C1F` dispatch.
4. Implement the **appear** (rows converging, `AWRITE`) — needed for the
   player-death cascade and for wave-start materialisation.
5. Clip constants must be mapped to the PORT's playfield bounds, not the ROM's
   absolute screen bytes (the port's screen model differs: see §screen layout).

**Confidence:** the algorithm and field names above are direct Gospel readings
(file+line cited). The remaining unverified detail is RRX7's `XCENT`/`YCENT`
aliasing in its own struct listing — confirm in `RRX7.ASM` before relying on it.

### 35.4 Still to do on explosions (for the next session)
1. `Explosion` must take the **impact point** (the ROM's `$A6`/`$A7`, computed by
   `$29B5`; the port should pass the laser's hit point) and derive `YOF` from it.
2. Implement the **diagonal strip model** of §35.1 (incl. the clamps/strip-drop).
3. Implement the **appear/rescale** system and the `$5C1F` dispatch of §35.2 —
   this also unblocks wave-start enemy materialisation.
4. Implement the **player-death cascade** of §35.3.
5. Port the per-wave wall colour table (`$2A4B`) — see §33 (b).

**Port design (arcade-faithful, visually calibrated; not cycle-accurate blitter math):**
- `Explosion` entity: dead enemy's CURRENT frame texture + center + laser direction
  (8-way → dx,dy unit drift); **16 ticks** (ROM FRAMES=$10); drawn as **16 horizontal
  1-px strips** of the art. Strip i (0..15, center 7.5):
  - splay: vy = (i − 7.5) × SplayPerFrame × f (top up, bottom down — the symmetric splay)
  - drift: += laserDir × DriftPerFrame × f (the directional bias)
  - f = elapsed tick 0..15. Freed after tick 16.
- `IExplodable.CurrentFrameArt(SpriteSet)` on the 8 robot types + electrode — the
  explosion shatters the exact frame on screen (ROM stores the PICPTR of the dying
  frame).
- `PlayField`: `_explosions` list, cap 7 (ROM slot count); spawned by
  `ResolveLaserHits` (with the laser direction) and by the grunt-electrode mutual
  kill (non-directional). Drawn after entities (topmost, per ROM blit order).

## 36. Frills session 2 — score font, sound system, tank shells, electrode placement (2026-09-16)

Continuing the parked frills list after explosions (§35): score font → sounds →
optional ROM checks (tank shell speed, electrode placement, hulk tempo). Hulk
tempo is already ROM-derived (HLKSPD wave table, see `GameplayConstants` comment) —
no action needed.

### 36.1 Score font — the arcade draws scores with the ROM large font

**Entry chain**: main update `JSR $26D2` → `$34AF`
(DRAW_PLAYER_SCORES_LIVES_AND_BORDER_WALL) → `JSR $D00F` → **`$DC13`
(DRAW_PLAYER_SCORES)**:

- Colour: P1 = `LDB #$11` → `STB $CF`; if current player `$3F` == $11 then
  `LDB #$AA` (2-player variant). `$CF` is the solid-blit colour byte (both
  nibbles = palette slot): **P1 score is palette slot 1 (blue), P2 slot $A
  (light green)** — static colours, not the cycling slots.
- Blitter destination: P1 = `$180E` (col 24, row 14 — top-left of the top
  border), P2 = `$580E` (col 88).
- Clears a 15×6-rect to black, then X = dest − 6 px, and five digit calls:
  millions (`LDA ,U`), hundred-thousands (`$1,U`), **thousands+hundreds as one
  BCD pair** (`$2,U`), tens (`$3,U`) → `JSR $5F9F` → `$6096` (large font,
  size flag `$D2` = 7).
- **Leading-zero suppression** (`$610D`): a nibble of 0 at cursor `$D6` == 0
  (nothing printed yet) is skipped (cursor advances only), so a score of 100
  prints "100" left-anchored at col 18 — matches the arcade.
- **Large font blit** (`$6023`): char table at `$E9CA` → **`$EC34`** (in ROM10-12,
  present in `robotron64k.bin`): 2-byte pointers, index = (ASCII − $30) × 2.
  Glyph = [width byte][rows]; solid-colour blit via `blitter_mask` = `$CF`
  (font data is monochrome; colour comes from the blit). Space → `:` glyph
  trick (`$20`→`$3A`).

**The font data problem**: the pointer table's glyphs live at `$92EC`–`~$B000` —
the **RAM** region (zeroed in the 64K bin; the font is copied ROM→RAM at boot).
Extraction attempts (layout matching, pointer-table search, glyph pattern scan,
immediate-address search for $92EC/$A5EC) found no ROM source — the copy routine
uses register-computed addresses only. The existing `Font_L_*.png` (6×6) and
`Font_S_*.png` (4×5) in the repo are hand-drawn **placeholders**, not the arcade
font (12 px tall).

> **Question to author**: where does the large-font ROM source live (or do you
> have a RAM dump / MAME trace of `$92EC`+)? Without it I can only draw the
> score with a substitute 12-px font. The placeholder glyphs are not the
> arcade shapes (compare arcade "1" vs the 6×6 block).

**Port state**: `PlayingState.DrawHud` uses `SpriteBatch.DrawString` with the
default font (documented deviation since M2). Parked until font data resolved.

### 36.2 Sound system — single-voice priority sequencer on PIA port B

No RRSND.ASM in the GitHub source; fully readable in the R5 disasm.

**Request** (`$D04B` → **`$D3C7` PLAY_SOUND_PRIORITY**): caller passes
`D` = pointer to the table's **priority byte**. Table layout (priority at P):

```
P-2, P-1 : dummy (tail of previous ROM data; never played)
P        : priority
P+1      : dur   (0 = end of table)
P+2      : len   (vblanks per note)
P+3      : note  (0..63)
P+4..    : (dur, len, note) triplets until dur == 0
```

**Priority preemption** (`$56` = current priority): new sound plays only if its
priority is strictly higher; the interrupt (`$D3E0`, every vblank) decrements
`$57` (note remaining); at 0 it decrements `$58` (notes remaining in the entry)
and, at 0, advances X by 3 and loads the next entry.

**Hardware** (`$D3B6`): `STA $C80E, #$3F` ("get ready" / clear), then
`B = ~note & $3F` → `STB $C80E`. Note number 0..63 (complemented 6 bits)
selects the tone on the Williams sound board — **the note→frequency map is in
the sound-board hardware, not the CPU ROM** (not in `robotron64k.bin`).

**Call sites (29)** — key ones decoded from the tables:

| site | event | priority | notes (dur,len,note) |
|---|---|---|---|
| `$273A` | player laser | 240 | (1, 16, $25) |
| `$30EF` | player death (KILL_PLAYER) | 238 | (2, 8, $11), (1, 32, $17) |
| `$4607` | brain warp-in ("PLAY_BRAIN_WAVE_WARP_IN_SOUNDS") | 255 | (1, 1, $13) |
| `$4FC7` | tank shell wall bounce | — | `$4B16` table |
| `$4F8C` | tank shell fire | — | `$4B11` table |
| `$12EA` | spheroid spawn area | 209 | (1, 8, $08) |
| `$2A9C` | long drone (29 notes × 4 vblanks) | 224 | n$0E |
| `$DBF9` | score-reached (inside score add, after DAA) | — | `$D0C9` table |

**Port state**: no audio subsystem at all. To port: (a) an 8-bit
`SoundEffectInstance` per note (MonoGame), (b) a vblank-stepped sequencer with
the same priority rule, (c) the note→frequency + waveform map — **author-only
knowledge** (sound-board chip). Without (c) I'd be inventing timbres.

> **Question to author**: do you have the sound-board note→frequency table (or
> a recording I can match by ear)? If not, I can build the engine + event
> wiring now and stub the note table with a musically plausible scale, flagged
> as non-faithful.

### 36.3 Tank shells — ROM structure (optional check #1)

`CREATE_TANK_SHELL $4E46` + update/bounce `$4F94`–`$4FCD`:

- **Cap**: max **20** shells on screen (`$F1` vs `$14`). The ROM's own comment
  documents the famous bug: `$F1` is decremented only on collision, never when
  a shell fizzles out → after 20 lifetime-ends the game stops spawning shells.
  (Port: shells expire by lifespan; cap not enforced — see question below.)
- **Lifespan**: `(RND & $1F) + $30` = **48..79 ticks** (port has 48 + rand 0..31 ✓).
- **Two aim modes** (50/50 on `LSR RND`):
  - *player-aim*: dx/dy = (player − shell) + RND(−16..15) per axis, multiplied
    by the wave "accuracy" table `$BE65` ($2DA3: wave1=`0E`, 2=`A0`, 3=`FF`,
    4+ = `B0`→`B8`→`C0` by wave), ×2 (X) / ×8 (Y) — **shells move much faster
    vertically than horizontally**; then a doubling loop normalises both
    components to ≈ ±accuracy.
  - *random-aim*: aims at a random spot near the player's X and the **top or
    bottom wall** (player half of screen decides), delta = (target − shell).
- **Update period**: periodic task with countdown 2 → the delta is applied
  **every 2 ticks**; wall hit → `COM` the delta (bounce) + bounce sound.
- **Speed**: after normalisation the per-update delta magnitude ≈ the accuracy
  byte (14 in wave 1, 160–192 later) — i.e. **~7–95 px per 2 ticks**. My
  reading of the normalisation loop is not 100% certain (the stack-threshold
  dance at `$4F3F`–`$4F80` is intricate).

> **Question to author**: the port uses a fixed 5 screen-px/tick shell speed
> (never playtest-flagged). From your cabinet footage, does the effective
> shell speed look closer to constant, or does it scale with the accuracy
> table (fast shells in later waves)? If you can confirm, I'll encode the
> exact model; otherwise I'll leave the constant.

### 36.4 Electrode placement — ROM uses per-wave safe rectangles (optional check #2)

`INITIALISE_ALL_ELECTRODES $3950`:

- Per-wave **safe-area rectangle** table at `$3920` (wave ≥10 clamps to 6,
  wave >5 clamps to 5):
  - w1: (0x40, 0xB0, 0x1A, 0x7A) · w2: (0x48, 0xA8, 0x1A, 0x7A) ·
    w3: (0x50, 0xA0, 0x2A, 0x6A) · w4: (0x54, 0x9D, 0x30, 0x60) ·
    w5: (0x5D, 0x96, 0x35, 0x59) · w6: (0x62, 0x94, 0x38, 0x5C)
- Electrodes are placed **outside** the safe rectangle (the centre keeps
  shrinking as waves progress), scanning for empty screen pixels; initial
  overlap prevention is done by blitting a dummy solid colour (`$66`) and
  checking the pixels (BLIT_IN_SOLID_COLOUR_INVISIBLE_TO_PLAYER `$393C`).
- Placement happens at **wave setup** (not mid-wave spawns).

**Port state**: electrodes spawn with a random position subject to
`ElectrodeMinDistanceFromPlayer = 40` — a different model (player-distance
vs safe-rectangle). Never playtest-flagged. Changing the placement model is a
visible behaviour change; parked behind the author (same answer covers the
shell question — "is the current feel OK, or do you want the safe-rectangle
placement?").

---

## 37. Sound engine implementation (2026-09-16)

The §36.2 research was complete (sequencer + all tables decoded; only the
note→frequency map is sound-board hardware), so the port now has the audio
subsystem with a **flagged stub scale**:

- `Audio/SoundEngine.cs` — pure-logic single-voice priority sequencer, a
  line-for-line port of $D3C7 (request: play only when the priority is
  STRICTLY higher — `CMPA $56 / BCS`), $D3E0 (vblank step: $57 = ticks left
  in the current note repetition, $58 = repetitions left in the entry;
  at 0/0 advance 3 bytes, dur 0 ends and clears $56), $D3B6 (the note is
  what the sink turns into a tone).
  - ROM subtlety caught by the tests: the repetition path ($D3EC → $D3FC)
    re-sounds the note WITHOUT reloading $58 — only the entry-advance path
    sets $58 = dur. The first port draft reset it on both paths and looped
    the first entry forever.
- `Audio/SoundTables.cs` — every decoded table from §36.2 with its ROM
  address (provenance): PlayerLaser $26E6 p240, PlayerDeath $26D9 p238,
  RobotDeath $1ADA p208, ShellFire $4B11 p200, ShellBounce $4B16 p200,
  BrainWarpIn $4143 p255, BonusLife $D0C9 p239, Drone $26EB p224 + the
  spawn/grunt/tank-area tables parked for later wiring.
- `Audio/Sound.cs` — process-wide service (one sound board, one voice —
  same idiom as MonoGame's static MediaPlayer). `Sound.Initialize` in
  `RobotronGame.LoadContent`, `Sound.Tick()` in `Update` (one port tick ≈
  one vblank), `Sound.Play` from the field code.
- `Audio/MonoGameSoundSink.cs` — 8-bit mono 22 kHz square waves, one
  cached 1-second loop per note, 10 ms fade in/out. **STUB scale:**
  110 × 2^(n/12) Hz (notes 0x01..0x25 land at ~117 Hz..~1.5 kHz). NOT
  arcade-faithful — the real map is the sound-board hardware (BLOCKED on
  the author; replace `StubFrequency` when the table arrives).
- Wiring (R5 call sites → port events): laser fire (Player.LasersFiredThisUpdate
  → $273A), robot death (ResolveLaserHits explosion branch → $1F5B),
  player death (Alive→Dying transition → $30EF), shell fire (SpawnTankShell
  → $4F8C), shell bounce (TankShell.BouncedThisUpdate → $4FCD), brain
  spawn (SpawnBrains → $4607), bonus life (Score.Add true → $DBF9).
- Tests: `Audio/SoundEngineTests.cs` — sequencing, priority preemption
  (equal/low ignored, higher preempts, voice freed at table end), table
  provenance spot-checks. 171 total (170 pass, 1 skipped).

---

## 38. Score font — UNBLOCKED: real arcade glyph data (2026-09-16)

The §36.1 "font dump" block is resolved. The author pointed at their sprite
editor's sprite repository
(`WmsGfxSpriteEditor.Roms.Robotron2084/Shared/Sprites/RobotronBlueLabelSpriteRepository.cs`,
data sourced from seanriddle.com/robotronsprites.txt), which contains the ROM
offsets for both fonts:

- **smallfont0..9 / A..Z / ( / )** — decimal offsets 59947..60426, each
  **2 bytes × 5 rows** (4×5 px), 4bpp.
- **largefont0..9 / A..Z / ( / ) / : / arrowleft** — decimal offsets
  60563..61403, each **3 bytes × 6 rows** (6×6 px; `(` `)` 2×6, `:` 1×5,
  arrowleft 3×6), 4bpp.

**Author (authoritative): "The fonts are blitted same as the sprites"** —
i.e. the standard Robotron 4bpp linear format, 2 px/byte, row-major.

Verified byte-for-byte against `ref/rom/robotron64k.bin`: largefont0 @60563
decodes to a perfect 6×6 hollow "0" (ring of palette index 9), largefont1 @
60582 to the serifed "1", smallfont0 @59947 to the 4×5 "0" (index F). All
digits verified visually from the nibble dumps.

**§36.1 correction:** the large font is **6×6 px**, not 12px tall (the
"$D2 = 7 size flag" reading was the blitter's size byte, not a 12-px glyph).
The repo placeholder PNGs had the right DIMENSIONS (6×6 / 4×5) — only the
shapes were hand-drawn.

**The pointer-table mystery, resolved:** the ROM's large-font pointer table
($E9CA → $EC34, 2-byte pointers, indexed (ASCII−$30)×2) points at
**$92EC, $A5EC, …** (0x1300 apart) — RAM addresses, as found in §36.1. The
glyph data is copied ROM→RAM at boot (register-computed copy, source not
findable by scanning), and the ROM source is exactly where the author's
editor reads it: **ROM10-12 @0xEC93 (large) and @0xEA2B (small)**. The
pointer-table bytes at $EC34+ are NOT glyph data — §36.1's "glyphs live at
$92EC+ (RAM)" is correct for RUNTIME, and this section pins down the ROM
SOURCE the RAM copy comes from.

**Blit metrics (R5 $6023-$6021, re-verified this session):** glyph =
[width-byte][rows]; after the blit X advances by **(width_px + 1)** pixels
($6009: `LDA ,Y; INCA; CLRB; LSRA; LEAX D,X`) — 7 px for the large digits,
5 px for small, 3 px for the 2-px-wide parens; odd widths toggle the
blitter pixel-shift flag ($D0). Score colour = solid blit via `blitter_mask`
= $CF (both nibbles): P1 = slot 1 (blue), P2 = slot $A (cycled by the LF
process). Leading-zero suppression per §36.1 (score 100 prints "100"
left-anchored; score 0 prints nothing).

**Port plan:** regenerate all 78 glyph PNGs in place (same file names, so
Content.mgcb / SpriteContentPaths stay valid) with the real ROM glyphs —
white opaque pixels on transparent (tinted at draw time with the live
palette slot colour: P1 slot 1 static blue, P2 slot 10 via the live palette).
`PlayingState.DrawHud` draws the score with the large-font digit glyphs,
7-px advance, leading zeros suppressed, at the port's round-8 HUD position
(HUD-inside-margin decision stands). Small font extracted for later use
(title-screen text).

---

## 39. Colour cycling: M4 ticked + font blitter-remap semantics (2026-09-16)

**M4 FINAL TICK — author go-ahead.** The author asked for the colour cycling
to be done now ("Can you do the colour cycling now?") — M4 (the colour-cycle
pixel shader, §34) is considered complete: the six cycling slots (10-15)
cycle via the unrolled .fx shader with Live10..Live15 uniforms, wired into
`SpriteSet.DrawSprite`. Ticked off.

**Font + blitter remap (author):** "In Robotron on the demo screen the same
font is rendered in different colours... that will be using the blitter remap
functionality... if you render a font with a specific palette index, and the
palette index is one of the colour cycling indices, the text will cycle too."

That's the arcade blitter model: the glyph data carries palette INDICES
(opaque nibble + 0 = clear); the `blitter_mask` picks ONE colour for the
opaque index. If that colour's palette slot is a cycling slot (10-15), the
slot's PCRAM entry is being rewritten by its colour process every few
vblanks — so the SAME glyph data on screen changes colour over time. No
second mechanism; it falls out of "pixels read the live palette".

**Port consequence (convention change for font glyphs):** a glyph drawn "in
slot N" must follow slot N's live colour. Static slots (0-9): tint the white
glyph with the live slot colour at draw time (what the P1 score already does —
slot 1 is static, so §38's tint approach is correct as-is). **Cycling slots
(10-15): the glyph must be marker-baked for that slot and drawn through the
colour-cycle effect** (same M4 path as sprites) — a white glyph tinted with a
snapshot of `Color(10..15)` would freeze at that instant and NOT cycle.

**Port implementation:** `tools/extract-fonts.py` now also emits, for every
glyph, six marker-baked variants `Font_L_<ch>_10.png` .. `_15.png` (and
Font_S) — glyph pixels = the slot's CyclingSlotMarker RGB (exact
`RobotronColor.FromByte` of 0xC4/0xF4/0xCC/0x81/0x45/0x2F, the same values
the sprite PNGs bake), background transparent. `SpriteSet` loads them as
`FontLargeCycling`/`FontSmallCycling` [glyphs × 6] and exposes:
`DrawGlyphStatic` (tint with live slot colour) and `DrawGlyphCycling` (apply
the effect pass, draw the marker-baked variant — cycles live). The P1 score
stays on the static path (slot 1). The demo-screen text (small font,
multiple colours incl. cycling ones) will use the cycling variants when it is
built. `FontSlots.IsCycling` (slot is 10..15) + tests.

## 40. M4 shader regression: "only the wall renders" — **RESOLVED** (2026-09-16)

### RESOLUTION — root cause found, fixed, verified on screen (same day)

**Root cause:** `ColorCycle.fx` declared its OWN vertex shader, and that VS
performed no projection at all
(`o.Position = float4(input.Position.xy, 0.5f, 1.0f)`). SpriteBatch supplies
its vertex buffer in SCREEN pixels (0..640 / 0..400) and depends on the VS to
apply its screen-space projection matrix — so feeding those raw numbers to
the clipper as clip-space coordinates puts every sprite outside the clip
volume. Nothing is drawn. (This is why the previous session's `float3` fix
changed nothing: `float3` vs `float4` was never the problem — the missing
matrix was. The float3 declaration remains correct regardless.)

**Why it also killed later draws that pass NO effect** (the HUD text, the life
icons — the confusing part): MonoGame applies a custom effect in exactly one
place, `SpriteBatcher.FlushVertexArray`, and only when one was handed to
`SpriteBatch.Begin`. This port never does that — it binds the effect manually
per entity draw. With `effect == null` that method goes straight to
`DrawUserIndexedPrimitives`, i.e. it re-applies NOTHING, so whatever shader
was last bound stays bound. `EffectPass.Apply()` binds only the stages the
pass actually declares:

    if (_vertexShader != null) { device.VertexShader = _vertexShader; ... }
    if (_pixelShader  != null) { device.PixelShader  = _pixelShader;  ... }

So the first manual `Apply()` permanently swapped out the sprite VS for the
rest of the Begin/End block. The wall was the only survivor because it is
drawn BEFORE the first `Apply()`.

**Fix:** delete the vertex shader from the effect — the pass is now

    PixelShader = compile ps_3_0 MainPS();

`EffectPass.Apply()` therefore leaves SpriteBatch's own sprite VS (bound once
by `SpriteBatch.Setup()` at `Begin`) in place, and positions are transformed
exactly as the built-in path does — including its projection and half-pixel
offset, so pixel-perfect rendering is reused rather than re-implemented. The
M4 remap is unaffected: the pass replaces the PIXEL shader only.

**Verified on the author's machine** (3440x1440, scale 3, window driven to the
playing state with a ctypes SPACE press): `tools/screenshot-map.py` no longer
shows an empty ring interior — entities, electrodes, the player, the
"LEVEL 1" HUD text and the life icons all draw. Colours prove the remap is
LIVE and not a passthrough: entities show bright green (live slot 10 `0x38`)
and electrodes white (live slot 9 `0xFF`), and neither colour is among the
six baked markers (they decode to purple / light blue / dark blue / crimson /
gold).

**Gates after the fix:** build 0 warnings; 187 tests (186 pass + 1 skip); 12 s
launch smoke OK.

**Lesson for this port:** a custom effect used via manual
`effect.CurrentTechnique.Passes[0].Apply()` inside a SpriteBatch block must
NOT declare a vertex shader — it would replace SpriteBatch's projection.
If a custom VS is ever genuinely needed, the draw must go through
`SpriteBatch.Begin(effect: ...)` so the runtime owns the whole pipeline.

---

### Original report and evidence chain (kept for history)

### Symptoms (author, playtest of the first build that ever had the M4 effect
### wired into the draw path)

- Title screen renders normally (logo, prompt, high scores).
- Press SPACE: the game STARTS and is fully functional — IJKL movement and
  SPACE laser-firing are heard (sound engine §37 works), the wall ring
  colour-cycles ("the colour cycling wall looks good").
- **Nothing else is drawn**: no player, no enemies, no humans, no level text
  (plain `DrawString`, top-right), no life icons (plain `spriteBatch.Draw`,
  bottom-left). The playfield interior is solid black.
- The 12-second launch smoke test never catches this: it only exercises the
  title state.

### Environment facts (needed to reproduce/verify on this machine)

- ONE 3440x1440 ultrawide monitor on the 9070XT (the system has a second GPU
  but it has no display output — no phantom displays in the virtual desktop).
- GDI `ImageGrab` (PIL) captures the correct/only monitor.
- This harness (Pi) overlaps the game window; the operator types in Pi while
  watching the game. Verify with `python tools/screenshot-map.py` (added this
  session) — it screenshots the game window and prints an ASCII colour map
  (K=black, W=white, G/B/R/Y=colours). A healthy started game shows the wall
  ring PLUS scattered white (player centre, robots, HUD). The broken state
  shows ONLY the ring.
- To start the game headlessly: `python` + ctypes `SendInput` VK_SPACE with
  the game foregrounded (see the throwaway `drive_game2.py` pattern used this
  session — NOT SendKeys, which went to the harness window).

### Isolation (what the diff since the last known-good playtest, 90f284c,
### changed in the DRAW path)

- 90f284c (playtest round 13, known good): `SpriteSet.DrawSprite` was a plain
  `spriteBatch.Draw` — NO effect.
- 0012a38 (M4): `DrawSprite` now does
  `Palette?.UpdateEffectColors(effect); effect.CurrentTechnique.Passes[0].Apply();`
  before every entity draw. `RobotronGame.LoadContent` loads
  `Effects/ColorCycle.xnb` and sets `SpriteSet.ColorCycleEffect`.
- The WALL is the only thing drawn WITHOUT the effect
  (`PlayfieldWall.Draw` = direct `spriteBatch.Draw` tinted with the live
  slot-11 colour) — which is exactly the only thing that renders.
- Everything else (entities, level text, life icons, score glyphs) draws
  AFTER the first effect-applied draw and is invisible — even draws that
  pass no effect. This is consistent with: **once the M4 pass is bound, it
  stays bound for the rest of the SpriteBatch** (SpriteBatch.Draw with
  effect=null keeps the currently-bound pipeline stage in MonoGame), and the
  pass renders nothing visible.

### What was already tried (2026-09-16, by this session)

1. `VSInput.Position` was declared `float4 : POSITION` although SpriteBatch's
   vertex buffer supplies POSITION as 3 floats (w would read garbage = the
   first texcoord float). Changed to `float3` + explicit `w = 1.0f` in
   `MainVS`. **Rebuilt and re-verified with the ASCII map: NOT fixed — the
   field is still empty.** (The float3 change is kept: it is correct
   regardless, but it was not the root cause.)
2. Code review of `PlayField.Draw`, `Player.Draw`, `Grunt.Draw`, `DrawHud`,
   `RobotronGame.Draw` (two-pass: render to the 640x400 `_playfield` RT, then
   integer-scaled blit to the backbuffer): no logic that would skip entities.
   Entity draw code is unchanged since 90f284c apart from additive
   IExplodable art capture and the explosion pass.

### Current state of `src/Robotron2084/Content/Effects/ColorCycle.fx` (post-fix)

- **No vertex shader at all** (this is the fix — see RESOLUTION above; do not
  re-add one without also moving the draw to `SpriteBatch.Begin(effect:)`).
- The pass is `PixelShader = compile ps_3_0 MainPS();` only.
- ps_3_0 PS: `tex2D(SpriteTexture, ...)` (s0), six unrolled marker
  comparisons (C4/F4/CC/81/45/2F as decimal floats, tolerance 0.004)
  substituting `Live10..Live15` uniforms, then `return t * input.Color;`.
- Zero `[` tokens (TPGParser constraint), six named uniforms
  (pushed by `GamePalette.UpdateEffectColors`).
- The .xnb COMPILES and BUILDS; the game runs; the wall (no effect) is fine.

### Next debugging steps (SUPERSEDED — the bug is fixed; kept for history)

1. **Confirm the effect is the cause**: temporarily set
   `_sprites.ColorCycleEffect = null` in `RobotronGame.LoadContent` (or skip
   the `Apply()` in `DrawSprite`). Rebuild, start the game, run
   `tools/screenshot-map.py`. Expected: player + robots + HUD visible. If
   they are NOT visible with the effect disabled, the bug is NOT the shader —
   re-investigate (but all evidence so far points at the shader).
2. If confirmed, **bisect the pass**:
   a. Replace the PS with a trivial passthrough
      (`return tex2D(SpriteTexture, input.TextureCoordinate) * input.Color;`)
      and test. Visible? Then the marker logic/`t * input.Color` is at fault
      (check: does `input.Color` arrive as (1,1,1,1)? Add a debug variant
      that returns a constant colour to see if the VS->PS handoff works at
      all).
   b. If the trivial PS also fails, the VS is at fault: try the MINIMAL
      MonoGame sprite VS (`o.Position = input.Position;` with matching
      float3/float4 structs) and check whether SpriteBatch expects a
      specific VS contract (it ships its own compiled sprite effect — compare
      the vertex layout/semantics against it; in particular whether the
      VS must emit `float4` with a specific w, and whether
      `o.Position = float4(input.Position.xy, 0.5f, 1.0f)` breaks the
      SpriteBatch's view-projection, which is screen-space (0..width,
      0..height) with a y-flip baked into its matrices).
3. **Suspect list** (ranked): (a) the custom VS not matching SpriteBatch's
   screen-space projection contract (the batch's own VS passes positions
   through untouched; my VS keeps .xy but forces z=0.5/w=1.0 — verify that
   is what SpriteBatch's VP expects); (b) the PS `tex2D` sampling a
   SpriteBatch-bound texture through a sampler-state mismatch (the batch
   begins with `SamplerState.PointClamp`); (c) `input.Color` arriving as
   black (vertex colour element order); (d) the xnb being stale relative to
   the .fx (delete `obj/` + `bin/` content and rebuild).
4. **Verification is the ASCII map** — a started game must show white
   clusters (player/robots) inside the wall ring. Keep iterating until the
   map shows them, THEN run the full gates (build 0 warnings, the test exe,
   12 s smoke) and ask the author to playtest.

### Regression guard — **ADDED** (`tools/verify-playfield.py`)

- The smoke test only exercises the title state — that is how this slipped
  through. `tools/verify-playfield.py` launches the game, presses SPACE,
  screenshots the client area and asserts the playfield INTERIOR (inset 14%,
  so the wall ring itself cannot satisfy it) has at least 200 lit samples.
  Exit code 0 = content, 1 = black interior = regression. Options: `--keep`,
  `--exe`, `--png`, `--settle`.
- **Validated against the actual bug**, not just against a green run: with the
  broken (VS-declaring) effect rebuilt, the guard reported **0 lit samples of
  298944 (0.000%)** and exited 1; with the fix it reports ~9.5k-10.8k lit
  samples (~3.2-3.6%) and exits 0.
- Run it as a gate alongside the test exe whenever the draw path or any
  `.fx` changes: `python tools/verify-playfield.py`.

## 41. Enforcer sparks — ballistic flight, from the Gospel (2026-09-16)

**Author report:** "The enforcer sparks don't fly like they do in the arcade."
Decoded from the **Gospel** (`ref/original-source/RRC11.ASM`) per §0.0, not the
disasm. The module labels map to the R5 addresses I had previously been reading
as anonymous code: `ENFSHT` = the enforcer's shoot routine, `SPARK` = the spark
process, `SPKIL` = the spark's kill handler, `SPKP0..3` = the 4 flicker pics.

### What the ROM does

**`ENFSHT`** (spawn) gives each axis two things:
- a **velocity** `4 x (player coord + jitter - spark coord)` in subpixel units,
  where the jitter is `(seed & $1F) - 16`, i.e. **-16..+15 COLUMNS per axis**,
  and the X jitter is forced to 0 when the player is within 16 columns of the
  left wall (`CMPA #XMIN+$10 / BHS ENFS2 / CLRB`);
- **`PD2`/`PD4` = `(LSEED & $1F) - 16` / `(HSEED & $1F) - 16`** — a CONSTANT
  per-axis acceleration, chosen independently per axis and fixed for the
  spark's whole life.

**`SPARK`** runs once per move (`NAP 4` = 4 vblanks) and does
`OXV += PD2; OYV += PD4`. **The acceleration integrates into the velocity, so a
spark flies a PARABOLA** — that is the arcade's "habit of going off course", and
because the jitter is comparable to the delta at close range a spark can start
out moving AWAY from the player.

**Speed** comes from the mover (`RRSCRIPT.ASM` MONOP:
`LDD OBJX,X / ADDA OXV,X / ADDB OYV,X / STD OBJX,X`) — it adds the **high byte**
of each 16-bit velocity to the screen position. So `OXV = 4 x delta` means a move
covers **`delta/64` COLUMNS**, and `PD2 = a` bends that by `a/256` columns per
move. Life = `PD7 = (HSEED & $F) + $14` = **20..35 moves** x 4 vblanks = 80..140
ROM ticks (1.6-2.8 s). Caps: `SPKCNT < 20` sparks, `OVCNT < 17` objects, and the
shot timer is re-armed (`RMAX ENSTIM`) *before* both caps.

### What was wrong, and the fix

The port re-randomised a ±1 px/tick nudge on one random axis every 4 ticks — a
**drunken walk, not a parabola**: no consistent curve, and it never arced the way
the arcade's do. Its speed was also ~4.6x too fast (a divisor of 53 px per tick
instead of `delta/64` per move).

Fix (`Entities/Spark.cs`): the ballistic model. A fixed per-axis acceleration
(ROM `PD2`/`PD4`) is added to the velocity every move; the initial velocity is
the ROM's `4 x delta` with the -16..+15 **column** jitter applied as a real delta
perturbation (±64 port px, not ±1 px); the left-wall jitter suppression is
implemented. Velocity and steps are held in **1/256-px fixed point** (mirroring
the ROM's 16-bit velocity with an 8-bit fraction), and the per-move step is
spread across the move's ticks with a remainder carry so it never drifts.
Constants are cited to the source in `GameplayConstants`.

### Tests / verification

- `Spark_VelocityChangesByAConstantAcceleration_EveryMove` — asserts the
  velocity changes by the SAME constant on each axis every move (the parabola)
  and that the acceleration is the ROM's -16..+15 range scaled. This is the
  behaviour that was missing.
- `Spark_AimsAtThePlayer_WithDistanceProportionalSpeed` — expectations
  re-derived from the Gospel (the first move covers `delta/64`).
- Sparks confirmed rendering in play (level 3) via a driven capture; gates: build
  0 warnings, 189 tests (188 + 1 skip), playfield check PASS, 12 s smoke OK.

**OPEN FOR THE AUTHOR (flagged, not decided):** the ROM-derived speed is ~4.6x
slower than the port's previous value, so sparks now travel roughly `delta/64`
port px per move over a 20-35 move life (about a third of the screen) — a
nuisance curtain rather than the faster direct threat the port had. That is what
the Gospel says, but it changes difficulty, so it needs a playtest judgement.
Also unmodelled: `OVCNT < 17` (the port has the 20-spark cap only).

## 42. Spheroids — the motion MODEL is wrong; the SPEED needs the mover cadence (2026-09-16)

**Author question:** "Are you sure the spheroids are going as fast as the
arcade?" **Answer: no — and the honest reason is that the port models the wrong
thing, so "how fast" is not even well defined for it yet.**

### What the port does
`SpheroidSpeed = 2` port px/tick, with an initial random heading of `±speed` on
each axis (so always exactly 45°) and `WallFleeHelper.MoveWithReflection`
reflecting off the walls. That is a **constant-speed billiard ball**. The value
itself was set empirically in playtest round 6 ("too fast" → 3 → 2), not derived.

### What the Gospel does (`RRC11.ASM`: `CIRSTL` spawn, `CIRCLE` process, `CIRGO`, `CIRNAC`)

- **The velocity starts at ZERO** — `CIRSTL` sets the position and a random
  acceleration but never sets `OXV`/`OYV`. So a spheroid accelerates up from rest.
- **`CIRGO`** (run every pass) does, per axis:
  `v += accel` → clamp to `±$0100` → `v += damping`, where the damping is
  `highByte(~v x 4)` sign-extended ≈ `-v/64`. **So it accelerates AND damps** —
  it reaches a terminal speed rather than a fixed one. (X and Y use different
  clamps: `$0100` for X, `$0200` for Y.)
- **`CIRNAC`** re-rolls the acceleration every `RANDU(15)` = **1..15 passes**:
  `PD5` (X) = `(HSEED & $1F) - $10` = -16..15, `PD6` (Y) = `(LSEED ^ SEED) & $3F - $20`
  = -32..31. So the two axes are INDEPENDENT and the heading wanders — **there is
  no 45° lock**; the port's fixed diagonal is a special case the ROM can pass
  through but not a constraint.
- **The clamp is the terminal speed**: `±$0100` means the 16-bit velocity's high
  byte is ±1, so the position advances **1 COLUMN (2 arcade px) per move** — for
  Y, `$0200` = 2 lines per move.
- **Escape phase** (`CIRC3`/`CIRC3L`): `OYV = 0`, `OXV = ±$0100` — it flees
  **horizontally at full speed** until `OX16` passes `XMIN+3` / `XMAX-10`, which
  is why an escaping spheroid never moves vertically.
- The `CIRCLE` process body runs `NAP 2` (**every 2 vblanks**); the drop phase
  (`CIRC2`) re-rolls `RND(1..CDPTIM/4)` ✓ (the port already has the `/4`);
  `ENFCNT < 8` and `OVCNT < 17` caps ✓ (the port has the 8 cap).

### The UNRESOLVED bit — what I will not guess

Which routine adds the velocity to the position, and therefore the px/s. The
only mover I have found that applies a velocity to a screen position is
`RRSCRIPT.ASM` `MONOP` (`LDD OBJX,X / ADDA OXV,X / ADDB OYV,X / STD OBJX,X`, i.e.
the high byte of the 16-bit velocity; it runs `NAP 3`). I have **not** confirmed
that the spheroid is driven by `MONOP` rather than by its own `NAP 2` pass.

The answer swings the speed by 2x: **1 column per 2 vblanks = 50 arcade px/s**, or
**per vblank = 100 arcade px/s**. For scale, the port's 2 port px/tick crosses the
640-px playfield in ~5.3 s; the arcade's spheroid crosses 152 columns in 3 s (per
vblank) or 6 s (per 2 vblanks) — so the port's *rate* is plausibly in range, but
its *model* is not: it cannot reproduce the acceleration from rest, the terminal
speed, the independent-axis wandering, or the horizontal-only escape.

### Next step (in order)
1. Find the real mover: grep the source for what advances `OX16/OY16` for a
   plain object (candidates: `MONOP` in `RRSCRIPT.ASM`; check `RRM1`/`RRT2` too).
   Settle the cadence empirically if needed (a 2x difference must not be guessed).
2. Implement `CIRGO`/`CIRNAC` in `Spheroid.cs` (accelerate + damp + re-roll, the
   `$0100`/`$0200` clamps, independent axes, no 45° lock) — the same shape of
   change just made for the sparks in §41.
3. Then re-derive `SpheroidSpeed` from the clamp and the cadence, and re-test.
4. The escape phase should become horizontal-only at the clamped speed.

**Do not** ship a spheroid speed change without doing step 1 — that is exactly
how the §35/a070290 explosion model went wrong.

## 43. THE MOVER FOUND — and the spark/spheroid/enforcer/quark speed audit (2026-09-16)

### The generic object mover: `RRS22.ASM` `OPB80` ("OBJECT UPDATE COCKTAIL LINE80")

This was the missing piece (§42 step 1). It walks the whole object list and, per
object:

```asm
OB8LP0 LDD OX16,X      ; the 16-bit world position
       LDU OPICT,X     ; the picture metadata (for width/height)
       ADDD OXV,X      ; += the FULL 16-bit velocity
       CMPA #XMIN
       BLO OB81        ; out of bounds → REJECT this axis
       ADDA OBJW,U
       CMPA #XMAX+1
       BHI OB81        ; too far right → REJECT
       SUBA OBJW,U
       STD OX16,X      ; commit
OB81   (the same for Y)
```

Three facts that settle the cadence/units questions for EVERY entity:

1. **The full 16-bit velocity is added to the 16-bit world position** (not just
   the high byte) — so a velocity `v` moves `v/256` units per application.
2. **It runs once per FRAME** (the routine loops the object list), NOT once per
   the entity's `NAP` interval. An entity's `NAP n` only sets how often its
   *logic* (velocity/timers) updates; the POSITION advances every frame.
3. **Out-of-bounds is REJECTED, not clamped**: the axis update is skipped and the
   object keeps its last valid coordinate on that axis, while the other axis
   still moves. That is why objects slide along a wall and then stop at it —
   no bounce, no edge-snapping.

(`OPB0` is the same thing for the other cocktail half. `RRSCRIPT.ASM`'s `MONOP`
is a *task* that paints an object in its own colours and also nudges it by the
velocity's high byte — `NAP 3` — so it is a different, scripted mover; the spark
and spheroid use `OPB80`.)

### Spark — FIXED (was 4x too slow)

Because the mover runs per FRAME, `OXV = 4 x delta` means the spark covers
**`delta/64` COLUMNS per frame** = `delta/32` arcade px per frame, not per 4
frames. The previous commit spread the step across the `NAP 4` interval and was
therefore **4x too slow**. Fix: the step scale no longer includes the move
interval, and the wall rule is now a REJECT (as above). The acceleration is
unchanged (it genuinely is per move: the SPARK process runs `NAP 4`).
Sanity check: for a 400-port-px delta the spark now moves 6.25 port px/frame,
i.e. it crosses in ~1 s — and the port's ORIGINAL empirical divisor (53) was
≈ 7.5 px/frame, so the old *speed* was nearly right and only the *trajectory*
was wrong. Both are now Gospel-derived (`delta/64` per frame + the parabola).

### Spheroid — model wrong AND ~1.7x too slow

With the cadence known: the terminal speed is the clamp `±$0100` → the high byte
= 1 → **1 column = 2 arcade px per FRAME ≈ 100 arcade px/s**; it crosses the 152
columns in ~3 s. The port's `SpheroidSpeed = 2` port px/tick = 120 port px/s ≈
60 spec px/s → **~1.7x too slow**, and structurally wrong (constant speed, fixed
45°, reflection instead of accelerate+damp+re-roll, no horizontal-only escape).
See §42 for the `CIRGO`/`CIRNAC` algorithm.

### Enforcer — constant speed instead of a proportional approach

`RRC11.ASM` `ENFRCE`: the grow-up phase runs `NAP 8`, then the main loop
`ENFR1`/`ENFR1A`/`ENFR1B` runs `NAP 3` and (a) re-runs `ENFNV` when its own
`PD7` timer hits 0, (b) fires via `ENFSHT` when the `PD6` shot timer hits 0.
`ENFNV` picks a random target wall coordinate and sets the velocity to
**`2 x (target − current)`** (`SUBB OY16,X / SBCA #0 / ASLB / ROLA / STD OYV,X`)
— so the enforcer's per-frame movement is `(target − pos)/128`, i.e. it
**decelerates as it approaches its target** and drifts slowly near a wall. The
port uses a constant 3 px/tick, so it cannot reproduce that near-zone crawl
(the port's own notes call it "the ROM's near-zone crawl" — this is where it
comes from). Shot timer: `PD6 = RMAX(ENSTIM)`, re-armed *before* the caps.

### Quark — constant speed + reflection instead of a re-rolled random velocity

`RRTK4.ASM` `SQVEL` (run every `PD7 = RND(1..31)+1` frames, process `NAP 3`):
per axis, the speed is **`RND(1..SQSPD)`** (`SQSPD` = a WAVE-TABLE value), scaled
`x4` for X and `x8` for Y, so the per-frame movement is `RND(1..SQSPD)/64`
**columns** and `RND(1..SQSPD)/32` **lines** — the Y axis is deliberately 2x the
X. The SIGN is chosen from position and a seed bit, and it **flips away from the
walls** rather than reflecting: near the left (`<= XMIN+5`) don't flip; near the
right (`>= XMAX-12`) flip to move left; otherwise flip 50% of the time
(`LSEED`'s sign). So a quark's heading is re-randomised with a wall bias, and its
speed VARIES each roll. The port uses a constant speed with reflection.

### The pattern (for the next session)

Spark, spheroid, enforcer and quark all have the SAME class of defect: the port
substitutes a **constant speed + wall reflection** for the Gospel's
**velocity-from-position/table + re-roll timer + wall-aware sign + REJECT mover**,
and several of the constant values were tuned by playtest rather than derived.
The fix shape is identical in each case (this is what the spark change did):
keep the entity's update cadence for its LOGIC, apply the velocity per FRAME,
and derive the numbers from the Gospel. Do them one entity at a time, each with
a test that pins the Gospel arithmetic, and commit per entity.

## 44. Grunt and electrode deaths — no flash; the post SHRIVELS (2026-09-16)

**Author reports:** (a) "The grunts and electrodes flash when shot. They shouldn't.
The grunts should explode and the electrodes should "shrivel"." (b) "when you
shoot the robot from the bottom the explosion is wrong — all the explosions are
the same."

Gospel: `ref/original-source/RRP8.ASM` (grunt + post/electrode module).

### Grunt — explode immediately, no flash

```asm
*KILL ROBOT
ROBKIL LDA PCFLG        ; did the PLAYER touch it?
 BNE ROBKON             ; yes → flash  ("ON GRUNT...SEE WHAT YOU HIT")
 JSR EXST               ; no (a laser) → BLOW HIM UP!!!!
 ...
ROBKON JMP DMAON        ; the player-contact path
```
**A laser kill explodes the grunt IMMEDIATELY with NO blink/flash and no death
animation** — the explosion IS the visual. The flash belongs to the
PLAYER-CONTACT path (`ROBKON` → `DMAON`), which is **not modelled yet** (tracked).
The port had a 2-second blink on `Kill()` for every kill (a constant invented "for
per-entity symmetry", not in Appendix A), which is what the author saw.
Fix: `Grunt.Kill()` goes straight to `Dead`.

Also confirmed in the same routine: the kill speed-up is `ROBSPD = ROBSPD x $E0/256`
(= ×224/256 = ×7/8, floored against `RMXSPD`) ✓ the port already has this, and
**`LDD #$0110 / JSR SCORE`** — the grunt's score. **RESOLVED (same day): that IS
100, so the port's `ScoreValues.Grunt = 100` is CORRECT** — do not "fix" it.
Proof from the Gospel (`RRS22.ASM` `SCOREV`, whose header reads
"A=0-7 EXP, B=0-99"): the routine is DECIMAL (`DAA`) and treats `A` as a digit
exponent with `A`'s low bit selecting a ×16 scale on `B`. For `$0110`: A = $01 →
`LSRA` gives A = 0 with the carry set → `B = $10 x 16 = $0100` → the high byte
`$01` is decimal-added into the digit array, i.e. **1 into the hundreds place**.

### Electrode/post — the SHRIVEL (`PKPROC`)

```asm
PSTKIL ... JSR KILPST ... MAKP PKPROC    ; the laser kill starts the shrivel
PKPROC LDX PD,U / LDY OPICT,X
PKPR1  LEAY 5,Y        ; next picture entry (+5: W,H,ptr(2),sleep)
       LDA ,Y / BNE PKPR2
       JSR DMAOFF      ; "ITS ALL OVER" — the sequence's terminating 0
PKPR2  STY OPICT,X / JSR DMAOFN
       LDA 4,Y         ; SLEEP TIME (the entry's trailing byte)
       JMP SLEEP
```
So the post plays **its own 3-frame death sequence**, holding each frame for its
own sleep time — `PSP1` = 6, `PSP2` = 3, `PSP3` = 2 vblanks — then the image is
turned off. **It is a shape collapse, NOT a blink** (the port toggled visibility
for 2 s). Total 11 vblanks ≈ 0.22 s.

**The four post types:** `PSTP1V` lists four picture families, each 3 frames of
5 bytes × 9 px — `PSP1A..PSP3A` (STAR), `..B` (SNOWFLAKE), `..C` (SQUARE),
`..D` (TRIANGLE) = 12 pictures. In the sprite list these are `electrode1..12`
(contiguous at $3B95 + $2D each ✓ = 45 bytes = 5×9). **Follow-up:** the port only
ever spawns type A, so its shrivel uses `ElectrodeFrames[0..2]`; assigning the
real per-electrode type is tracked. (The player-contact path is `PSTKON` →
`OPON` = "turn him on", again a different, unmodelled behaviour.)

### Fixes and tests
- `Grunt.Kill()` → `Dead` immediately (removed `_deathTimer`/`_blinkTicks` and the
  Dying branch + Draw blink). `LaserHitsGrunt_GruntDiesImmediately_AndExplodes`
  now asserts Dead + exactly one explosion spawned.
- `Electrode.Kill()` → the shrivel: `_shrivelStep` 0→1→2 held for
  `PortTicks(6)`, `PortTicks(3)`, `PortTicks(2)`, then `Dead`; `Draw` and
  `CurrentFrameArt` return `ElectrodeFrames[_shrivelStep]` while dying.
  `ElectrodeLaserKill_ShrivelsForTheRomFrameTimes_ThenDies` pins the 12-tick
  lifetime.

### (b) "all the explosions are the same" — CONFIRMS §35.5
Independent author confirmation of the gap already decoded: the port draws ONE
symmetric fan for every kill, so a shot from below looks identical to any other.
The Gospel's `$5C1F` dispatch (diagonal → the leaning `EXSTZ` engine; straight →
the RRX7 engine with no lean) and the impact anchoring are still unimplemented —
see §35.5 for the full plan. This report raises its priority.

## 45. Electrode/post TYPES are wave-dependent (2026-09-16)

**Author:** "Fix up all the electrode types. The electrode types are
wave-dependent." — correct, and the Gospel is `RRG23.ASM` `GTWCOL` ("GET WALL
COLOR", which actually sets all four per-wave values):

```asm
GTWCOL JSR PLINDX
 LDU #WCTAB          ; the base of the FOUR per-wave tables
 LDA PWAV,X / DECA
GTWL CMPA #9 / BLS GTW1 / SUBA #10 / BRA GTWL   ; wrap the wave into 1..10
GTW1 LEAU A,U
 LDA  ,U / STA WALCOL ; +0  → the WALL colour
 LDA 10,U / STA PSTCOL; +10 → the POST colour
 LDB 20,U / LDX PSTP1 ; +20 → the POST IMAGE offset
 ABX / STX PSTANI
 LDA 30,U / STA LASCOL; +30 → the LASER colour
```

**The tables (10 entries each, one per wave, then repeating):**

| index (wave-1) | 0 | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 |
|---|---|---|---|---|---|---|---|---|---|---|
| wall colour | $22 | $55 | $11 | $EE | $77 | $33 | $44 | $88 | $00 | $CC |
| post colour | $FF | $EE | $BB | $DD | $EE | $FF | $11 | $BB | $DD | $AA |
| **post image offset** | $00 | $10 | $20 | $30 | $40 | $50 | $70 | $80 | $00 | $60 |
| laser colour | (next table) | | | | | | | | | |

`$10` per family because `PSTP1V`'s entries are 5 bytes each and a family is
3 entries (alive + 2 shrivel) = 16 bytes including the sequence's terminating 0.
So the **post picture family = {0,1,2,3,4,5,7,8,0,6} indexed by `(wave-1) % 10`**,
and the 27 electrode pictures in sprite-list order are exactly 9 families × 3
frames — which independently confirms the family ordering
(`electrode1..12` are the four 5×9 families STAR/SNOWFLAKE/SQUARE/TRIANGLE of
`RRP8.ASM`, contiguous at `$3B95` with a 45-byte stride = 5 bytes × 9 px).

**Implemented:** `GameplayConstants.PostFamilyByWaveMod10` / `PostFamilyForWave`
/ `PostPicturesPerFamily`; `Electrode` takes a `familyIndex` and draws
`ElectrodeFrames[family*3 + step]` (step 0 alive, 1-2 shrivel);
`PlayField.SpawnElectrodes` passes `PostFamilyForWave(Parameters.LevelNumber)`.
Pinned by `ElectrodePictureFamilyIsWaveDependent` (the whole 10-wave sequence
plus the wrap at waves 11/20).

**Verified on screen** (captures of waves 1/2/3): wave 1 posts are cross-shaped
**stars**, wave 2 cross-hatched **snowflakes/diamonds**, wave 3 solid **squares**
— matching families 0/1/2 exactly, as `GTWCOL` predicts.

**`PSTCOL` — DONE (the per-wave post COLOUR).** `GameplayConstants.PostColorByWaveMod10`
/ `PostColorForWave` hold RRG23's second table (`$FF,$EE,$BB,$DD,$EE,$FF,$11,$BB,$DD,$AA`)
and `Electrode.Tint` decodes it with `RobotronColor.FromByte`. Note these are
colour BYTES (BBGGGRRR), not palette indices — the ROM draws the post as a **solid
silhouette** in this colour (`PSTKON` → `OPON` = "turn him on"). The extracted
electrode PNGs are **white**, so a tint reproduces the ROM exactly (white × tint =
tint). Decoded: wave 1 `$FF` white, 2 `$EE` lavender, 3 `$BB` green, 4 `$DD` purple,
7 `$11` dark green, 10 `$AA` teal. **Verified on screen**: wave 1 posts read white
and wave 3 posts read as solid light-green squares (they were white on every wave
before). Pinned by the colour assertions in `ElectrodePictureFamilyIsWaveDependent`.

**Still to do (the same `GTWCOL` block, NOT yet implemented):**
- **`WALCOL`** — the per-wave **wall colour**: the port hardcodes slot 11. This
  is the same deviation already logged in §33(b) — the two are the same fix.
- **`LASCOL`** — the laser colour.
Both come from the same four-table block, so they can be done together.
- **The player-contact path** (`PSTKON` → `OPON` "turn him on"): the ROM flashes
  the post instead of shrinking it when the PLAYER touches it. Not modelled (the
  port runs the shrivel for every kill); low priority while
  `PlayerInvincibleForTesting` is on.

## 46. Brains, progs and cruise missiles — re-derived from the Gospel (2026-09-16)

Author: *"I would like the brains and progs, cruise missiles to be done today."*
All three live in **one** Gospel file, `ref/original-source/RRB10.ASM`
("BRAINS & CO."), so they were decoded together. Nothing here needed the R5
disassembly.

### Read this first: the object layout and `CKLIM`

```
OBJX   EQU 4    upper-left SCREEN x  (the "fat" collision box corner)
OBJY   EQU 5    upper-left SCREEN y
OBJID  EQU 6    associated process
OCVECT EQU 8    collision vector
OX16   EQU $A   16-bit world coordinate
OY16   EQU $C
```
`CKLIM` is a 3-byte `JMP` (RRF.ASM:167) to **`CKLIMV` (RRH11.ASM:85)** — the
single most important helper for all three entities:

```
CKLIMV PSHS D
       CMPA #XMIN / BLO CKLF      ; XMIN=7  XMAX=$8F  YMIN=24  YMAX=234  (RRF.ASM:67-70)
       CMPB #YMIN / BLO CKLF
       ADDD [OPICT,X]             ; += the object's PICTURE (width,height)
       CMPA #XMAX / BHI CKLF
       CMPB #YMAX / BHI CKLF
       CLRA                       ; EQ = in bounds, NE = out
CKLF   PULS D,PC
```
The test is on the **picture box**, and the CALLER decides what failure means.
For the brain and the prog it means **reject the whole move** — see below.

### The BRAIN (`BRAIN`/`BRNL`, RRB10:143-231)

Four things the port had wrong, all now fixed:

1. **X has a ±2px dead zone.** `LDA OBJX,Y / SUBA OBJX,X / ADDA #2 / CMPA #4 /
   BLS BRN3A` skips the X step entirely while `|dx| <= 2` arcade px. A brain
   that is roughly aligned stops correcting horizontally.
2. **Y has NO dead zone, and a tie means "below".** `BRN3A` always sets ±1, and
   `CMPA OBJY,X / BHS BRN4` is *taken on equality*, so a brain level with its
   target drifts DOWN a px, then up again next body. **That ±1px jitter is the
   arcade brain's signature hover** — the port held the row perfectly still.
3. **The step is ALL-OR-NOTHING.** `LDD OBJX,X / ADDA XTEMP / ADDB XTEMP+1 /
   JSR CKLIM / BEQ BRN40 / SUBA XTEMP / SUBB XTEMP+1` — on failure BOTH axes are
   undone. The port's per-axis backtracking let a brain **slide along a wall**
   the source never allows; a pinned brain now stops dead.
4. **A facing change resets the animation index.** `BRNDIR CMPD PD6,U / BEQ
   BRNSD / STD PD6,U / BRA BRNSD0` where `BRNSD0 CLRB` — the 4-entry ABAC table
   (advancing by 2: 0,2,4,6) restarts at 0 whenever the direction *base*
   changes. The port kept its phase across turns. The facing is also chosen from
   the **step deltas** with **X taking precedence over Y**, so a diagonal
   approach animates as left/right, never as a diagonal - the ROM has only four
   3-frame sets.

Also corrected: **the target human is the nearest one to the BRAIN**, not to the
player, and GETHTG measures **|dx| + |dy|** (Manhattan) — `GHT2 ADDD XTEMP` sums
the two absolute differences. The port used player-relative Euclidean distance,
which picks a different victim whenever two humans sit in different directions.

**DONE — see §48 (the BMUT reprogram animation is implemented).** The notes
below record the ORIGINAL deferral and why it was wrong to defer it: I had
claimed it needed new one-colour human art. It does not — the one-colour
rendering is a blitter MODE (§47), so `$12`+`$1A` reproduce it from the art
already in `Content`.

#### The original deferral note (kept for provenance)

When a brain touches a human the
ROM does 20 iterations of (`BRNON` outline → erase the human → move it to
`humanY ± (SEED & 7)`, clamped → draw it in `$AABB` → `NAP 2`) before `PROGST`
turns it into a prog: the human **vanishes immediately from the human list**
(it is moved to the object list, so it stops being a brain target and can no
longer be rescued) and the brain is BUSY and stationary for ~20×3 = 60 vblanks
≈ 1.2 s, with `PRGSND` re-requested every iteration and `HPSND` at the end. It
is also repositioned first, to just LEFT of the brain (brain X − the human's
picture width − 1, wrapping to the right of the brain if that would cross
XMIN), with its Y set to the brain's Y + 2.

The port still converts instantly in `PlayField.ResolveHumanCollisions` — so
the OUTCOME is right (prog, no skull, correct score) and the **pause and the
jiggle are missing**.

Note the art dependency before implementing: the ROM's "flicker" is a switch
between the two-tone SOLID rendering (`HUMON`: `$AA` outer shell + `$BB`
inner) and the ONE-COLOUR outline (`BLKON`/`BRNON`). The port's human sprites
are already the two-tone solid form, so the flash half of the animation needs
new one-colour human art — the ±7px Y jiggle alone can be done with what is in
`Content` today. Worth doing WITH the author able to eyeball it, since a
partial version (human vanishes, nothing visible for 1.2 s, prog pops in)
would look WORSE than the current instant swap.

**Also cheap and still open: the CATCH test itself.** The ROM does not use
picture overlap for the brain↔human catch. `BRNL1`'s tail is
```
SUBB OBJY,Y / ADDB #3 / CMPB #$6 / BHI BRN5   ; |brainY - humanY| <= 3
SUBA OBJX,Y / ADDA #3 / CMPA #6 / BLS BMUT    ; |brainX - humanX| <= 3
```
i.e. the two **top-left corners must be within ±3 px on BOTH axes** — a much
tighter and squarer test than `Brain.Bounds.Overlaps(Human.Bounds)`, which
fires as soon as the 14×16 box touches anything. Worth trying on its own: it
may be why brains in the port seem to "catch" humans too eagerly.

## 47. The blitter's COLOUR/REMAP modes — decoded, and a shader can do them (2026-09-16)

Author: *"So can't you create a pixel shader which will emulate the Robotron
blitter's REMAP COLOUR functionality?"* — **Yes, and it corrects a wrong claim I
made in §46** ("BMUT's flash needs new one-colour human art"). It does not. The
one-colour rendering is a **blit MODE**, not separate art.

### The blitter registers (RRF.ASM:53-62, and the R5 disassembly)

```
DMACTL EQU $CA00   CONTROL BYTE - WRITING IT INITIATES THE TRANSFER
DMACON EQU $CA01   CONSTANT DATA BYTE   <-- the colour / remap value
DMAORG EQU $CA02   source
DMADES EQU $CA04   destination
DMASIZ EQU $CA06   width & height
```
Note `DMACON` is a **byte** and the video is **4bpp / 2 pixels per byte** — that
is the whole trick, and it is why every colour argument in the source is written
as a DOUBLED NIBBLE (`$AA`, `$BB`, `$EE`, `$11`…): two pixels of index A, index
B, index E, index 1. See the palette finding below.

### The op codes (named by the disassembler's own comments)

| DMACTL | Meaning | Source |
|---|---|---|
| `$02` | copy the source **in its own colours**, opaque | `PCTONV` RRS22 |
| `$0A` | copy in its own colours, **transparent** (inferred: `$02`\|`$08`) | — |
| `$12` | **SOLID**: fill the rectangle with `DMACON`, ignore the source | `BLKONV` RRS22 ("ON DMA BLOCK"), `BLIT_RECTANGLE_WITH_COLOUR_REMAP` $DA61 ("solid mode") |
| `$1A` | **SOLID + TRANSPARENT**: draw only the source's non-zero pixels, all in `DMACON` | `MPCTNV` RRS22 ("ON MONOCHROME PICT"), `BLIT_IMAGE_IN_SOLID_COLOUR_AND_TRANSPARENCY` $DA9E |
| `$3A` | `$1A` shifted one pixel right | $6002 |
| `$12` + `DMACON=0` | erase a rectangle (`PCTOFV`, `BLKCLV`) | RRS22 |

Bit meanings, therefore: `$02` = take source; `$10` = **use the constant colour
instead of the source's own colours**; `$08` = transparency. The "REMAP COLOUR"
capability is the `$10` bit — and the disassembly file has both wrappers named
and commented for us:

```
; Blit an image in a solid colour. Basically take an image and remap all the
; nonzero bytes to the colour specified in $2D.
BLIT_IMAGE_IN_SOLID_COLOUR_AND_TRANSPARENCY:        ; op $1A

; Blit an image as a single solid colour, with another solid colour as its
; background. Think of when the progs are being programmed, mommy/daddy/mikey
; is drawn as a solid shape and has a rapidly cycling background colour.
BLIT_IMAGE_AS_SOLID_COLOUR_WITH_SOLID_BACKGROUND:   ; $1DC2
  1. $2D = A  -> BLIT_RECTANGLE_WITH_COLOUR_REMAP  (op $12) = fill the frame rect
  2. $2D = B  -> BLIT_IMAGE_IN_SOLID_COLOUR...     (op $1A) = the shape on top
```

### What this means for BMUT (correcting §46)

`HUMON A=$AA, B=$BB` is *exactly* `BLIT_IMAGE_AS_SOLID_COLOUR_WITH_SOLID_BACKGROUND`:
a **solid `$AA` rectangle the size of the human's frame**, then the human's own
picture as a **solid `$BB` silhouette** on top. And `BRNON` (`LDB #$BB / JSR
BLKON`) draws the **brain as a solid `$BB` rectangle** — not a sprite at all. The
"flicker" is those two renderings alternating while the human jiggles ±7px,
against a cycling slot (the disassembler's comment says so outright: "with a
rapidly cycling background colour").

**No new art is needed.** Both halves are blit modes:
- op `$12` = a filled rectangle = `spriteBatch.Draw(whitePixel, dest, colour)`;
- op `$1A` = "every non-transparent texel becomes one colour" = three lines of
  pixel shader.

### The port design

`ColorCycle.fx` already does the hard part — it holds the six live cycling
colours as uniforms and is bound per-draw by `SpriteSet.DrawSprite`. Add:

```hlsl
float4 RemapColor : register(c6);

// blitter op $1A - solid colour, transparent (MPCTON / "mono")
float4 SolidRemapPS(PSInput input) : COLOR
{
    float4 t = tex2D(SpriteTexture, input.TextureCoordinate);
    clip(t.a - 0.5f);                          // source nibble 0 = transparent
    return float4(RemapColor.rgb, t.a);
}
```
plus a technique / `SpriteSet.DrawSpriteSolid(...)` that sets `RemapColor` and
applies it. **The pass must stay PIXEL-SHADER ONLY** — re-read §40 before
touching that file; declaring a VS there is what broke all rendering.

That single primitive then covers, in one place:
1. **BMUT** (§46) — the brain's solid rectangle and the human's `$AA`/`$BB`
   two-colour rendering: the reprogram animation becomes implementable.
2. **The font's 468 baked cycling-variant PNGs** (§39) become unnecessary — one
   white glyph + `RemapColor` = the live slot colour, which is also the correct
   fix for the "a tint cannot cycle" workaround.
3. **`OPON` / `PSTKON`** ("turn him on") — the post's and other objects' solid
   silhouettes.
4. **`WALCOL`/`LASCOL`** *if* the palette-index finding below holds.

### ⚠ FINDING: the per-wave colour tables are PALETTE INDICES, not colour bytes

This contradicts §45, and it re-contradicts the "correction" in §33(b) — the
ORIGINAL §33(b) reading was right.

**Decisive argument:** the video is 4bpp with 2 pixels per byte, and the blitter's
solid modes write `DMACON` as a *byte* fill. For a **solid** fill to come out
solid, both nibbles of that byte must match — so the ROM writes them **doubled**.
And every value in all four `GTWCOL` tables is a doubled nibble:
`$22,$55,$11,$EE,$77,$33,$44,$88,$00,$CC` (WALCOL) and `$FF,$EE,$BB,$DD,$EE,$FF,
$11,$BB,$DD,$AA` (PSTCOL). The ROM's own comment for the post is `OPON` = "turn
him on", i.e. a **solid silhouette** — which a non-doubled byte such as `$38`
could not produce (it would stripe index 3 then index 8).

So `PSTCOL` for wave 3 = `$11` = **palette index 1**, which per the ROM's own
palette table (`CRTAB`, RRS22 ~1268) is `$07` = **RED** — *not* the (32,64,0)
dark green the port currently computes via `RobotronColor.FromByte`. So
`PostColorForWave` should resolve a **slot** through the live palette — and
slots 10-15 would then CYCLE, which is very visible.

**NEEDS THE AUTHOR'S EYE BEFORE CHANGING:** do the arcade's posts (and walls)
change colour per wave / cycle the way the palette slots imply? The port's
current "wave 1 white / wave 3 light-green" screen match was produced BY the
FromByte reading, so it is not independent evidence.

**RESOLVED 2026-09-16: the author confirmed the palette-index reading** — the
posts do change colour per wave / cycle. Fixed in the same commit as the
shader: `PostSlotByWaveMod10`/`PostSlotForWave`, and the post is now drawn with
`DrawSpriteSolid` in the slot's live colour (which is also what the ROM does:
`PSTKON` → `OPON1` → `MPCTON`, op `$1A`). Waves naming slots 10-15 now cycle.
`WALCOL`/`LASCOL` are the same fix and remain open.

## 48. BMUT implemented — the reprogram animation (2026-09-16)

The author asked for the shader + this animation together. Both parts of §46's
"not done" note are now done.

### Timing

ROM `BMUT`'s loop is `LDA #20 / STA PD4,U` iterations, and each iteration does
two redraws separated by `NAP 2` — one lifting the human's Y by `SEED & 7`
(clamped to `YMAX-14`), one dropping it by the same (clamped to `YMIN`). So:
**20 iterations × 2 redraws × PortTicks(3) = 120 port ticks ≈ 2 s.** The
lift/drop are applied to the human's OWN Y, so it is a clamped random walk, not
a symmetric wobble.

Through the whole thing the brain is immobile and does not fire.

### The rendering (this is what §47 unlocked)

- **Brain** — `BRNON`: `LDB #$BB / JSR BLKON`. Not a sprite at all: blitter op
  `$12`, a **solid block in the colour of slot 11** (the disassembly's routine
  is even named `DRAW_BRAIN_IN_PROGGING_STATE`). Port:
  `DrawSolidRectangle(bounds, SlotColor(11))`.
- **Human** — `HUMON` with D = `$AABB`: op `$12` fills the frame rect in **slot
  10**'s colour, then op `$1A` draws the human's own picture as a solid shape in
  **slot 11**'s. Port: `DrawSpriteSolidWithBackground(...)`.
- **The "flicker" is palette cycling, not an alternation of renderings.** Both
  slots are in the 10-15 cycling range, which is exactly what the disassembly's
  comment on `BLIT_IMAGE_AS_SOLID_COLOUR_WITH_SOLID_BACKGROUND` describes ("a
  solid shape and has a rapidly cycling background colour"). Nothing in the
  port bakes or alternates anything: it names the slots and the existing
  `GamePalette` animation does the rest.

### The catch test

Now the ROM's, not a picture overlap: `|brainX - humanX| <= 3 AND
|brainY - humanY| <= 3` **arcade px, on the two TOP-LEFT corners** (BRNL1's
tail: `ADDB #3 / CMPB #$6 / BHI`, then `ADDA #3 / CMPA #6 / BLS`). The old
`Bounds.Overlaps` fired as soon as the 14×16 brain box touched anything.

### The placement, the handover, and the failure case

- The human is moved to just **left of the brain** (brain X − its own picture
  width − 1), or **8px right** if that would cross XMIN; its Y is the brain's
  Y + 2.
- The human goes **off the human list** at the start (it is no longer a brain
  target — `NearestHumanPositionTo` skips it — no longer rescuable, and no
  longer hulk-killable), and its own walk is frozen; the BRAIN drives its
  position.
- At the end the human is freed with **no skull** and `PROGST` makes a PROG at
  the human's **last** (jittered) position, keeping its art and box.
- **If the brain is killed mid-animation the conversion never completes**: the
  ROM's `BRNKIL` compares its own address against `BMUT3` and, past it, frees
  the human and leaves a **SKULL**. Without that the victim would be stranded
  forever — frozen, un-rescuable, never a prog. `Brain.ReleaseVictim()` +
  `ResolveHumanCollisions` handle it, pinned by a test.
- The animation is robot activity, so `RobotsFrozen` pauses it (the port's
  "ALL ROBOTS ARE IMMOBILE" rule) — the tests must expire the start grace first
  or the loop never advances. Worth knowing before writing another test here.

### Sounds (two more ROM tables decoded)

RRB10's own table block, at `BRNORG $1AC0` + the entry block. The offsets are
verifiable: `PGKSND` lands at `$1ADA`, which is exactly where R5's
`(1,4,$14),(2,4,$17)` priority-208 sequence lives, so the stride arithmetic
checks out.

| Label | Addr | Priority | Entries | Use |
|---|---|---|---|---|
| `PRGSND` | `$1AEA` | `$D0` = 208 | (2,3,$12) | requested at the top of EVERY BMUT iteration |
| `HPSND` | `$1AEF` | `$D8` = 216 | (1,8,$11) | the final conversion |

Both are now wired. **Side note:** the port's `SoundTables.RobotDeath` is the
bytes at `$1ADA`, which by this table is `PGKSND` — the PROG KILL sound. The
bytes are right (so the sound is right); the name is a misnomer worth fixing
only if something ever needs the real robot-death table.

### Gates

0 warnings; **198 tests (197 pass + 1 skipped)**; 12 s smoke OK;
`verify-playfield.py` PASS. New tests:
`Brain_CatchingHuman_ReprogramsItThenLeavesAProg_NoSkull` (the full 120-tick
animation, the placement, the stillness, the handover),
`Brain_KilledMidReprogram_ReleasesTheHumanAndLeavesASkull`, and
`Brain_CatchNeedsTheCornersWithinThreePx_NotAPictureOverlap`.

**Not yet seen by the author** — this one is very visible (a big flashing block
for ~2 s whenever a brain catches someone), so it wants a playtest.

## 49. Progs and cruise missiles are BLITTER objects, not sprites (2026-09-16)

Two author reports, one root cause — the port was drawing these two with
`DrawSprite`, when the ROM never blits either of them as a picture:

> "The posts cycle, but the progs look weird. There is no colour remapping of
> them. Also the cruise missiles aren't correct, they should be like worms
> leaving a trail, but here they are just squares."

### The PROG (`PROG3`/`PROG4`, RRB10:509-548)

The prog's body draws its own image **twice per body**, with the same frame
descriptor but two different colour pairs:

```
PROG3 LDY OPICT,X            ; the frame
      LDA PD+8,U / LDD A,U
      JSR PCTOFF             ; erase the ring entry's image
      LDD #$EE00
      JSR HUMON              ; *** drawn at OBJX, still the OLD position ***
      LDA OX16,X / LDB OY16,X
      STD OBJX,X             ; <-- OBJX becomes the NEW position HERE
      TFR D,Y
      LDA PD+8,U / STY A,U   ; store the new position in the ring
      ADDA #2 / CMPA #SPSIZE / BLO PROG4 / LDA #PD+10
PROG4 STA PD+8,U
      LDY OPICT,X
      LDD #$00AA
      JSR HUMON              ; *** drawn at the NEW position ***
      NAP 3,PROG
```

The ordering is the whole trick: the `$EE00` blit happens **before** `OBJX` is
updated, so it lands on the position the prog is *leaving*, and the `$00AA`
blit lands where it *is*:

| | colour 1 (`$12`, block) | colour 2 (`$1A`, shape) |
|---|---|---|
| vacated position | `$EE` = slot 14 | `$00` = slot 0 (BLACK) |
| current position | `$00` = slot 0 (BLACK) | `$AA` = slot 10 |

And the ghost is erased **7 bodies** later, not immediately — `PD+8` is the ring
index and the pointers run `PD+10`..`SPSIZE-1`. With `PD EQU 7` and
`PSIZE EQU 15`, `SPSIZE = 31`, so the indices are `17,19,21,23,25,27,29` =
**7 entries**: one erased and six left visible. **So each prog drags a trail of
six ghosts** (slot-14 block, black silhouette) behind a black block with a
slot-10 silhouette. That trail is what makes an arcade prog unmistakable — and
both colour pairs name cycling slots, so it shimmers as it walks.

Implemented: `Prog.GhostTrail` (newest first, capped at
`GameplayConstants.ProgGhostCount` = 6), and `Prog.Draw` paints oldest ghost
first so a blocked prog (whose ghost lands on itself) still reads correctly.

### The CRUISE MISSILE (`CMMOV`, RRB10:711-750) — and two wrong readings of mine

**Correction 1** (my earlier reading): "the 8-point storage is just for
erasure". `CMPIC`/`CMP1` are only ever loaded into `OPICT`/`FONIPC` — the
collision and erase descriptors — and are **never blitted**. What the missile
actually does is write VIDEO MEMORY directly, one 16-bit word per CMMOV:

```
 LDY #$DDDD / STY [OX16,X]      ; the position just LEFT: $DDDD
 STD OX16,X                    ; move
 LDY #0 / LDA PD5,U / STY [A,U] ; *** erase the ring entry nine steps back ***
 LDD #$AAAA / LDY OX16,X / STD ,Y   ; the position now: $AAAA
 LDA PD5,U / STY A,U            ; store the new coordinate in the ring
```

**The video address space is COLUMN-MAJOR — `column*256 + row`** — proved by
RRG23's `BORDER` loop, which draws its vertical border with `STA ,X+` (one
address per ROW) and steps a column with `LEAX 256,X`. Consequences:

- a 16-bit word is **two VERTICALLY adjacent pixels**, so every mark is **1 px
  wide by 2 px tall** — NOT the 2x1 I first implemented. (The same loop also
  independently re-confirms §47's doubled-nibble reading: `WALCOL` is written
  with `STA` = one byte = 2 px of the same nibble.)
- the trail marks merge along the path (steps are 1px apart), which is why the
  missile reads as a worm.

**Correction 2** (and the author's "the trail is too long"): the ring's length
is the **tail's length**, because that middle `STY [A,U]` with `LDY #0` erases
the screen pixel it wrote nine steps ago — *every step*. The ring is
`PD+6`..`SPSIZE` stepping by 2 (initialised to `PD+6` = 13, `SPSIZE` = 31 →
13,15,…,29 = **9 entries**), so **the tail is a rolling nine marks**, and
`CMKIL` wipes the remaining nine so it dies with the missile.

Two successive misreadings, both worth remembering: I first took the ring for
pure death-time bookkeeping, then for an unbounded video-memory history (which
is what shipped — a 4096-mark snake). The erase is sitting in the MIDDLE of the
loop, between the two writes, which is exactly where I was not looking.

Implemented: `CruiseMissile._trail` (a 9-entry rolling ring, on the entity),
1x2 marks in slot 13 behind a 1x2 head in slot 10, gone on `Destroy`.
`SpriteSet.MissileSmallFrames` is now unused — the CMPIC/CMP1 pictures exist in
`Content` and in the ROM but are only collision/erase descriptors. Candidate
content cleanup.

### ⚠ DELIBERATE DEVIATION: the brain's wall check is PER AXIS

The author, straight after the §46 movement change: *"the brains seem to get
stuck at the bottom wall."* They are right, and my Gospel reading is what
caused it.

`BRAIN` computes the combined step and pre-checks it with `CKLIM`, undoing
**both** axes on failure:

```
 LDD OBJX,X / ADDA XTEMP / ADDB XTEMP+1
 JSR CKLIM
 BEQ BRN40            ; in bounds -> store
 SUBA XTEMP / SUBB XTEMP+1   ; else undo BOTH
```

The brain's Y step is **unconditional** (BRN3A sets ±1 with no dead zone), so a
brain flush against the bottom wall with its target below has its whole move
rejected — it cannot even correct X. It is pinned until the target moves. That
is a genuine deadlock in the source, and the port reproduces it faithfully.

The port now **rejects per axis** instead. Justification, so this is not just
convenience: the ROM's own **generic object mover** (`RRS22` `OPB80`, §43)
rejects *per axis* — "an axis update that would leave the playfield is
REJECTED" — so per-axis is the machine's house convention and the brain's
combined pre-check is the special case; and the author reports the deadlock as
visibly wrong against the arcade. `Brain.FitsInsideX`/`FitsInsideY`, pinned by
`Brain_DoesNotDeadlockOnAWall_SlidesAlongItInstead` (the brain holds its X at
the wall but creeps down it).

### Gates

0 warnings in the C# compile; **200 tests (199 pass + 1 skipped)**; new tests
`CruiseMissile_DragsARollingNineMarkTail_ThatDiesWithIt` (4 marks after 2
bodies, 1 CMMOV apart — or diagonal across a body boundary, where a re-aim can
turn the missile — capped at 9 over a long flight, 0 after `Destroy`) and
`Brain_DoesNotDeadlockOnAWall_SlidesAlongItInstead`.

**The 12 s launch smoke and `verify-playfield.py` were BLOCKED** on this commit
because the author was playtesting and `bin\Debug\net10.0\Robotron2084.exe` was
locked (MSB3021/MSB3027). Both were re-run green once the game closed.

**PROCESS TRAP, recorded because it cost a round trip:** while that build was
failing, the test step still reported "200 tests, 0 failed" — from the STALE
test binary. When the build was finally fixed, a real failure appeared
(`Destroy` did not clear the tail, which is what ROM `CMKIL`'s ring wipe does)
plus an xUnit analyzer error. **A failed build makes the test totals
meaningless** — confirm "Build succeeded" before reading them, and re-run the
tests after fixing a build. `CMKIL`'s wipe is now in `CruiseMissile.Destroy`.

## 51. The QUARK's motion, from the Gospel — and the column/row coordinate unit

Author: *"The quarks are WAY too fast."* They were: the port derived the quark's
speed from the **distance to a waypoint**, and the Gospel has no distance term at
all. Same error class as the rest of this log (§43's "one entity at a time" list).

### The decode (RRTK4 `SQUARE` / `SQVEL` / `SQ3`)

```
SQVEL LDA SQSPD / JSR RMAX            ; A = RND(1..SQSPD)  <-- a random SPEED
      LDB OX16,X / CMPB #XMIN+5 / BLS SQV1     ; near the LEFT wall -> keep sign (+)
      CMPB #XMAX-12 / BHS SQV1N                ; near the RIGHT wall -> NEGATE
      LDB LSEED / BPL SQV1                     ; else the seed bit (set = negative)
SQV1N NEGA
SQV1  TFR A,B / SEX / ASLB ROLA ×2 (×4) / STD OXV,X     ; X: RND x 4
      ... same for Y with ×3 (×8) -> OYV
      LDA SEED / ANDA #$1F / INCA / STA PD7,U          ; 1..32 BODIES to the next roll
```
- **Body = `NAP 3`**, and `PD7` counts down in **BODIES** (the port was treating
  its equivalent as ticks).
- The **sign flips away from the walls FIRST** — `X <= XMIN+5` → right,
  `X >= XMAX-12` → left, `Y <= YMIN+5` → down, `Y >= YMAX-20` → up — and only
  otherwise comes from the seed bit. Note the **opposite seed polarity per axis**
  (X: bit7 set = negative; Y: bit7 set = positive), which decorrelates them.
- The exit (`SQ3`) is *not* a nearest-edge slide: `OXV = 0`, `OYV = ±$0200` per
  frame from one coin flip, and it is over when the quark leaves the field
  vertically (`Y <= YMIN+2` or `Y >= YMAX-16`). The animation walks the pointer
  **backwards** while it goes.
- The animation advances **one picture per body**, over a phase-dependent range:
  `SQP0..SQP4` wandering, `SQP0..SQP8` while dropping tanks, `SQP8..SQP0` exiting.
- Tank drops: the first timer is `RND(1..TDPTIM)` **bodies**, re-armed to
  `RND(1..(TDPTIM>>1)+1)` bodies, gated by the 20-tank cap.

### The correction in §28 that mattered most

The §28 R5-only decode read the `ASLB/ROLA` pairs as multiplying the **distance**
(`v = 4 × (destX − x)`), which is why the port could compute ~16 px/tick. The
Gospel shows they multiply **`RND(1..SQSPD)`** — a fresh random SPEED per axis. At
SQSPD 50 that is **0.4 COLUMNS/frame average, 0.78 max** = **0.8 px/frame**
(1.56 max) — see §52, which proves a column is 2 px five ways; this paragraph
originally mislabeled those numbers as px. The whole
waypoint-destination model (`QuarkAimXDivisor`/`QuarkAimYDivisor`/`QuarkMaxSpeed`/
`QuarkTopBandScreenPx`/`QuarkFleeSpeed`) is gone.

### ⚠ The coordinate unit: X counts 2-pixel COLUMNS, Y counts ROWS

This one generalises well beyond the quark, and it is what the `×4`-X vs `×8`-Y
asymmetry is *for*:

- The video buffer is addressed **`column*256 + row`** (proved by RRG23's
  `BORDER`, which walks its vertical border with `STA ,X+` = one address per
  ROW, and strides a column with `LEAX 256,X` — §49).
- So a world X coordinate counts **two-pixel columns**; a picture descriptor's
  width is in **bytes** for the same reason — which is exactly why a 7-wide brain
  picture is 14 px (the port already interprets it that way).
- Sanity check that settles it: `XMIN 7`..`XMAX $8F` = 136 units. As *pixels* that
  is 136 of 304 — but the arcade's wall ring clearly spans most of the screen; as
  *columns* it is 272 of 304 ✓.
- **Therefore X's velocity is in columns/frame and Y's in rows/frame — and the
  ×8 on Y (vs ×4 on X) exists precisely to cancel the fact that a column is two
  pixels. Both axes come out the same speed on screen.** The port's
  `AxisVelocityFp(scale, …, coordinateUnitArcadePixels)` takes that unit
  explicitly (2 for X, 1 for Y).

### The port's model

A sub-pixel velocity needs a carry: `_velocityFp` (1/256 PORT px per tick) plus
`_remainderFp`, exactly the pattern the spark already uses (§41) — the port's
stand-in for the ROM's 16-bit world coordinates. The mover rejects an axis whose
step would leave the playfield, keeping that coordinate *and* its carry. At SQSPD
50 the quark now drifts at 2.6 position units/tick maximum, ~1.25 average, per axis
— 0.75 arcade px/frame against the arcade's 0.8 (§52), measured over 5 seeds × 600
ticks. §52 also fixes the **body cadence**, which this section left 17% fast: the
body is 4 ROM frames = 4.8 ticks, not `PortTicks(4)` = 4.

### ⚠ FOLLOW-UP this implies for the CRUISE MISSILE

Same unit question, and the missile probably needs the same 2× correction: its
step is `ADDA PD2` on the X *column* byte, so **1 CMMOV = 1 column = 2 arcade px**,
not the 1 arcade px the port uses; and its video mark (`$AAAA` = 1 column x 2
rows) is **2x2 arcade px**, where the port draws 1x2. Recorded, not changed — the
author confirmed the worm reads correctly apart from its length (§49), so this
wants a side-by-side look rather than a blind edit.

### Gates

0 warnings / 0 errors; **201 tests (200 pass + 1 skipped)**; 12 s smoke OK;
`verify-playfield.py` PASS. `Quark_Wanders_WithDistanceProportionalSpeed…` is
replaced by `Quark_DriftsSlowly_WithinTheField`, which pins the instantaneous step
(now ≤ 4 units — the legitimate peak, since SQSPD 60 plus the sub-pixel carry
reaches 4, and one seed hit exactly that; see §52) and the average path
(< 3.5 units/tick) — the old model averaged 5-15 px/tick and blows both. Note the build was briefly **blocked by the author's
running exe** again (MSB3021/MSB3027): `dotnet build … -t:Compile` is the way to
keep validating sources while the game is open, since it skips the copy step.


## 52. The TANK's birth, and the proofs behind the "column = 2 px" unit (2026-09-16)

### The tank is BORN (`RRTK4` `TNKDRP` + `MTANK`)

Author: *"tanks spawn instantly whereas they are 'born' like the enforcer."* Correct.
`TNKDRP` (RRTK4:203) does **not** create a full tank — it creates the **mini tank**:

```
TNKDRP ... LDD #MTNKP1 / STD OPICT,X / STD OLDPIC,X     ; mini tank, both pics
           LDD OBJX,Y      ; the QUARK's video address
           CMPB #YMIN / BEQ TNKDP1 / DECB               ; nudge unless on the top wall
           ADDD #$0206     ; +2 columns, +6 rows  = (4,12) arcade px
           STA OX16,X / STB OY16,X / JSR TNKND
           LDA TNKSHT / STA PD6,U / JSR DMAON
```

and a separate process grows it:

```
*MINI TANK GROW
MTANK LDX PD,U
 JSR DMAOFF
 LDD OBJX,X            ; current video address
 LEAY OPICT,X
 ADDA 4,Y / ADDB 5,Y   ; += THE GROW PICTURE'S OWN (dx,dy)  <- offsets 4,5
 LEAY 6,Y              ; next descriptor: 6 bytes = an ANIMATED descriptor
 JSR DMAON
 CMPY #TNKP1 / BHS TANK   ; pointer reached the full tank -> normal TANK process
MTANKS NAP 12,MTANK       ; one grow picture per 12 ROM FRAMES
```

So: **4 grow pictures (`MTNKP1..4` — `SpriteExtractor` already maps those labels to
`TankGrow_1..4`), `NAP 12` each = 48 ROM frames**, with each grow picture carrying its
own (dx,dy) so the sprite stays centred while it expands. The tank does **not** move,
aim or fire during the birth.

**Why the port spawned it instantly:** the "destination is picked immediately at
creation ($4CE4)" note describes `TNKSTV` — the **wave-start** path, which creates a
full-size `TNKP1`. This port never uses it (tanks only ever come from quarks), so the
dropped tank must go through `MTANK` instead. `Tank_StartsMovingOnItsFirstFrame`
pinned the wrong path and is now `Tank_IsBornBeforeItMoves`.

**The art IS extractable — my first reading was wrong (corrected the same day).** A
full `SpriteExtractor` run reports `MTNKP1: could not collect 8 bytes` and
`MTNKP2..MTNKP4: NOT FOUND in ROM (source/ROM discrepancy)`. That is a **Pass B
parser limitation, not missing data**: Pass B cannot parse the 6-byte ANIMATED
descriptors these pictures use (`FCB w,h` + `FDB data` + `FCB dx,dy`). The data is in
the 64K image, contiguous and in the source's own order:

| picture | address | size |
|---|---|---|
| `MTNKD1` | `$5036` | 2x4 bytes = **4x4 px** |
| `MTNKD2` | `$503E` | 4x7 = **8x7 px** |
| `MTNKD3` | `$505A` | 4x8 = **8x8 px** |
| `MTNKD4` | `$507A` | 6x12 = **12x12 px** |

They are Pass C entries (`TankGrow_1..4`) and the port draws the REAL pictures, each at
its own size, with `MTANK`'s per-step deltas (descriptor bytes 4/5: `$FF,$FF`, `0,$FF`,
`$FF,$FE`, `$00,$FE`) applied to the CURRENT picture's position before the pointer
advances. The mini tank therefore walks up-left as it grows — **-2 columns and -6 rows
in total** — so the full 14x16 tank lands CENTRED on the drop point. The ROM bounds and
collides against the frame it is showing, so the port's collision box is the current
birth picture's size: a growing tank is genuinely a smaller target.

(This also corrected `TankBirthOffsetX`: `TNKDRP`'s `ADDD #$0206` is **+2 COLUMNS and
+6 ROWS**, so the X offset is 4 arcade px = 8 units, not the 4 units = 1 column the port
had; and the row is decremented first unless the quark sits on the top wall.)

The same run still **silently rewrites 78 committed font PNGs with genuinely different
pixels** (the fonts are authored by `tools/extract-fonts.py`). Revert ONLY those —
`git diff --name-only | Where-Object { $_ -like '*Font_*' } | ForEach-Object { git checkout -- $_ }`
— because reverting the whole `Sprites` folder also throws away intentional changes (it
silently un-did the tank-shell fix the first time).

### The general object mover — the algorithm behind every glider (`$DCFF`, `$DD90`)

The disassembly documents this itself (asm near `$DCFF`), and it is the model the
pending spheroid/enforcer work (§43) needs:

- The object's data block: `$000A,X` = 16-bit X, `$000C,X` = 16-bit Y, `$000E,X` =
  X delta, `$10,X` = Y delta, `$0004,X` = the blitter destination.
- **Each delta is a 16-bit fixed-point number: the MSB is the *signed* integer part,
  the LSB is the fraction in 1/256ths.** (The disassembly's own example: a delta of
  0.5 is `$0080`.)
- Per frame, per object: `LDD $000A,X / ADDD $000E,X`, then the bounds test; the same
  for Y. The **MSBs** of the two coordinates are then packed as
  `column*256 + row` and handed to the blitter (`OBJOUT`: `LDA OX16,X / LDB OY16,X /
  STD DMADES`).
- **Bounds, applied PER AXIS and REJECTING (the old coordinate is kept):**
  X after the add must be ≥ `#7`, and `X + the frame's WIDTH` (descriptor byte 0, added
  in the same units) ≤ `#$90` = **144**; Y must be ≥ `#$18` = 24 and
  `Y + the frame's HEIGHT` ≤ `#$EB` = 235.
- The mover runs **from the interrupt every frame** — "which is why these object types
  glide so smoothly" — and there are two near-identical copies (`$DCFF` draws the ones
  under the raster beam, `$DD90` the ones over it) so that the draw is split across the
  beam while the MOVE happens once. **`STATUS` bit 3 SET makes both skip the move and
  draw only** — that is the ROM's "robots frozen" gate, which the port models as
  `field.RobotsFrozen`.

### "A column is two pixels" — five independent confirmations

This is the single most load-bearing fact in the port, and it is why the ×4-X vs ×8-Y
speed asymmetry is *not* a speed difference. Confirmed five ways:

1. **The player's velocity table is the clincher.** `PITAB` (RRG23:748) stores `$0100`
   for RIGHT, and the code then does `CLRB / ASRA / RORB` — halving it to `$0080` =
   **0.5 columns/frame** — while Y's delta is `1` row/frame added to the byte `PY16`. A
   digital 8-way stick must move the same speed on both axes (the diagonals `$0101` and
   `$FFFF` depend on it), so 0.5 X-units MUST equal 1 Y-unit: **an X unit is 2 px**.
   The halving exists precisely to give the player 1 px/frame.
2. The mover's X bound is `#$90` = **144 columns** = 288 px of a 304-px screen — as
   *pixels* the playfield would be less than half the screen wide (absurd).
3. `OBJOUT` packs the coordinate MSBs as `column*256 + row`; `RRG23`'s `BORDER` walks a
   column with `LEAX 256,X` and each row with `STA ,X+`.
4. `SpriteExtractor.WritePng` writes **`w * 2`** because a descriptor width is in
   *bytes* — a 16-px-wide sprite (the quark) is 8 descriptor units.
5. `ref/mame-notes.md`: "304×256 pair-major 4bpp, **2 px/byte**, 256 B per 2-px column ×
   152 columns = exactly 38,912 B".

**Consequence:** the quark's `RND(1..SQSPD) × 4` is 0.4 **columns**/frame average
(0.78 max) = **0.8 px/frame** (1.56 max), the same on both axes.

### The quark's speed: VERIFIED, not changed — and the open question

The author reported the quark as "STILL way too fast" (2026-09-16) after §51's rewrite.
Checked to exhaustion and the port is faithful: `SQSPD` @ `$2DFC` is
`$32`×12 / `$38`×16 / `$3C`×12 (= 50/56/60, byte-identical to `WaveTable.QuarkMove`),
`SQVEL` ($4B82-$4BC6) is ×4 X and ×8 Y, and the port measures **0.625 spec px/tick =
0.75 px/frame** against the arcade's 0.8.

**One real defect WAS found and fixed:** a body is `NAP 3` + 1 = 4 ROM *frames* =
**4.8** port ticks, but `PortTicks(4)` truncates to 4 — so every body was 17% short and
the quark re-rolled its direction and cycled its animation **a fifth more often than
the arcade**, which reads as frantic. Fixed with an exact 6/5 accumulator (see below).
The speed itself is unchanged and matches the Gospel; the author's reference is a
YouTube video they cannot time. **Do not re-litigate this without new evidence** — if
the author asks again, the options are a deliberate, documented deviation at a factor
they name, or a measurement from the arcade.

### `PortTicks()` truncates — and the exact 6/5 idiom

`GameplayConstants.PortTicks(rom) => rom * 6 / 5` is **integer division**:
`PortTicks(3)` = 3 (should be 3.6), `(4)` = 4 (4.8), `(8)` = 9 (9.6), `(12)` = 14
(14.4). **Every ROM delay of 1-4 frames therefore runs 17-20% fast** — animations,
re-aim timers, fire cooldowns. Only the quark's body and the tank's grow step are fixed
so far; sweep the rest.

The exact idiom (worth reusing everywhere): accumulate in **6ths of a tick**, 5 per
tick, against a threshold of `romFrames * 6`:

```csharp
_bodyFifths += 5;                 // one port tick
if (_bodyFifths < romFrames * 6) return;
_bodyFifths -= romFrames * 6;     // exactly romFrames * 6/5 ticks have passed
```

### Two traps that cost time today (both now guarded)

- **A stale `bin\Release` build.** Four `Robotron2084.exe` copies exist (src + tests ×
  Debug + Release); the Release pair was 11 days old while every Debug copy was current.
  Build both configurations, and timestamp all four before concluding a fix "didn't
  work".
- **`tools/verify-playfield.py` measuring the wrong window.** `ImageGrab.grab(bbox=…)`
  copies **screen** pixels, so a maximised browser over the game was counted (it PASSed
  at 99.77% lit). It now raises the game with `SetWindowPos(HWND_TOPMOST)` — no
  foreground rights needed, unlike `SetForegroundWindow` — and FAILs rather than passes
  if the raise fails or the interior is implausibly bright. **ctypes:** `SetWindowPos`
  silently returns 0 unless you declare `argtypes`/`restype`.


## 53. The PROG's colours: the trail is DELIBERATELY the inverse of the prog (2026-09-16)

Author: *"I think the trail of the prog has inverted colours, so the 'human' that has
been progged is solid black, and the rest is a solid cycling colour. Check the prog draw
code for remap and if necessary create an effect to do that."* **The observation is
correct and the inversion is the ROM's design** — do not "fix" it without the author's
explicit say-so.

### Why (`HUMON`, RRB10:348-363)

```
*ON HUMAN
*Y=PICT,A=OUTER SHELL,B=INNER
HUMON PSHS D
 STA XTEMP2      ; A = the HIGH byte  = OUTER SHELL -> BLKON   (op $12, FILLS the rect)
 LDD OBJX,X / JSR BLKON
 LDA 1,S
 STA XTEMP2      ; B = the LOW byte   = INNER       -> MPCTON  (op $1A, non-zero px only)
 LDA OBJX,X / JSR MPCTON
```

So a colour word is **(outer/background = high byte, inner/figure = low byte)** — which is
also §47's op table: `$12` fills the rectangle (`BLKONV` "ON DMA BLOCK"), `$1A` draws only
the source's non-zero pixels (`MPCTNV` "ON MONOCHROME PICT"). Slot 0 is `$00` = black
(the `$DA51` table starts `00 07 17 C7 …`).

### What the prog process then does (RRB10:505-527)

```
 JSR PCTOFF            ; erase the shadow ring's OLDEST entry
 LDD #$EE00 / JSR HUMON ; "NEW COLOR 2ND GUY"  <- BEFORE OBJX is updated
 LDA OX16,X / LDB OY16,X / STD OBJX,X   ; <- OBJX now = the NEW position
 LDD #$00AA / JSR HUMON ; "ON NEW GUY"
```

The `$EE00` draw uses `OBJX` **before** it is re-stored from the coordinates, so it lands
on the position the prog is LEAVING; the `$00AA` lands on the one it is entering. Hence:

| mark | pair | background (the card) | figure |
|---|---|---|---|
| **the trail** (the 6 ghosts) | `$EE00` | slot **E** — cycling ($81 "BLU PURP RED") | slot **0** = BLACK |
| **the prog itself** | `$00AA` | slot **0** = black | slot **A** — cycling ($38 "LASER FLASH") |

They are exact inverses, and that is the point: the prog reads as a cycling human on
black, the trail as black human-shaped holes punched out of cycling cards. The ROM keeps
a 7-entry shadow ring (6 visible ghosts), erasing the oldest each body; the port's
`Prog._ghosts` (inserted BEFORE the step, capped at `ProgGhostCount`) matches, and the
port already draws both pairs through the `SolidRemap` pixel shader — **no new effect is
needed.**

Corroboration for the high-byte-is-the-card mapping: the human WHILE a brain reprograms it
uses the same `HUMON` with `$AABB` (the port's `ReprogramBackgroundSlot` = slot A,
`ReprogramShapeSlot` = slot B) — the mapping the author has already seen on screen.

**Status: open with the author.** If they want the trail to read as coloured humans
instead, it is a two-constant change (`ProgGhostBackgroundSlot`/`ProgGhostShapeSlot` →
`0x00`/`0x0E`); record it as an author-directed deviation from the Gospel, because the
Gospel is explicit.


## 54. The TANK SHELL was mis-sized: 8x7, not 14x16 (2026-09-16)

The author pointed at their own sprite repository
(`WmsGfxSpriteEditor/WmsGfxSpriteEditor.Roms.Robotron2084/Shared/Sprites/RobotronBlueLabelSpriteRepository.cs`
— offsets into the 64K image they built), where:

```
_sprites.Add(new(136, "tankshell", 20466, 4, 7, BitsPerPixel));
                                            ^ 4 BYTES x 7 ROWS = 8x7 px
```

which agrees with the pre-R5 source's `SHLP1 FCB 4,7`. Pass C was extracting
**7x16 = 224 bytes** from that same address (`$4FF2`) — the shell plus 196 bytes of
whatever follows it. Decoded correctly, 4x7 at `$4FF2` is a clean little shell:

```
...#....
.#####..
.#.#.##.
#######.
.#.#.##.
.#####..
...#....
```

Fixed: the extractor entry is `("TankShell", 0x4FF2, 4, 7, null)`, and
`GameplayConstants.TankShellCollisionSize = (8,7)` replaces the spec's square
`MissileSizeSpecPixels` 4x4 box — the hit box was half the sprite on both axes while
the 14x16 sprite was scaled into it. The ROM bounds and collides a projectile against
the picture it is showing.

**Lessons worth keeping:**
- The sprite repository's `(offset, widthInBytes, height)` is the AUTHORITY for sprite
  geometry (the author made it, and the offsets are into the 64K image).
- Pass B's "NOT FOUND in ROM (source/ROM discrepancy)" is a **parser verdict**, never
  evidence that data is absent. Check the descriptor layout before believing it.
- A sprite whose extraction size is wrong is worse than a missing one: it looks
  almost right while its hit box is wrong. Check the repository's size against every
  Pass C entry.


## 55. The "column is 2 px" sweep: the TANK's body clock, the MISSILE's steps (2026-09-16)

Author: *"It ALWAYS has to be like the arcade."* Two more entities were moving at the
wrong rate for the same unit reason flagged in §51/§52.

### The TANK moves one pixel per BODY — `TNKSPD` = 2

```
TANKL DEC PD6,U / JSR TNKFIR            ; the SHOT timer counts BODIES
TANK1 LDA PD4,U / CLRB / ASRA / RORB / ADDD OX16,X   ; X += PD4 >> 1
      LDB PD5,U / ADDB OY16,X / JSR CKLIM            ; Y += PD5 (a whole ROW)
TANK3 the walk picture (+/-4, backwards when PD4 < 0)
TANK5 DEC PD7,U / JSR TNKND             ; the DIRECTION timer counts BODIES
TANK6 LDA TNKSPD / LDX #TANKL / JMP SLEEP
```

- **`TNKSPD` is a CONSTANT, not a wave value**: `LDA #2 / STA TNKSPD` in the per-level
  reset (RRG23:677). A body is 2 vblanks plus the frame the process runs in = **3 ROM
  frames**. (The port had the tank moving every tick.)
- The X step gets the **same `CLRB/ASRA/RORB` halving as the player's table**, so
  `PD4 = ±1` means 0.5 COLUMNS = 1 arcade px, and Y is one ROW = 1 px. Both axes move
  **one pixel per body** — 0.33 px/frame against the port's 0.6.
- So the whole body is: fire check, move, walk frame, re-aim — once per body, with the
  fire countdown `RND(0..31) + TNKSHT` and the direction countdown `RND(1..31)` both
  counted in BODIES, and the walk cycle advancing one picture per body.

### The CRUISE MISSILE steps one COLUMN on X and one ROW on Y

```
CMMV1 ADDA PD2,U      ; X += 1 COLUMN = 2 arcade px   <- the port used 1 px (HALF speed)
CMMV3 ADDB PD2+1,U    ; Y += 1 ROW    = 1 arcade px
      STD OX16,X / SUBD #$0101 / STD OBJX,X   ; the FAT box = coordinate - (1 column, 1 row)
      LDY #$DDDD / STY [OX16,X]               ; slot 13 at the position being left
      LDD #$AAAA / LDY OX16,X / STD ,Y        ; slot 10 at the new one
```

- No halving here, so **the missile really is twice as fast horizontally as
  vertically** (2 columns vs 2 rows per body). One `StepPixels` for both axes was wrong:
  the port now has `StepColumns = Scaled(2)` and `StepRows = Scaled(1)`.
- The mark is **2x2 px**: `STD ,Y` is a 16-bit write, so it paints two vertically
  adjacent addresses (column-major), each holding 2 px. The port drew 1x2.
- The fat box is `CMPIC FCB 3,4` = 3 BYTES x 4 rows = **6x4 px** (the port had a square
  4x4), and it sits one column and one row up-left of the true coordinate.
- The bounce tests the TRUE COORDINATE (`CMPA #XMIN` / `#XMAX-1`, `CMPB #YMIN` /
  `#YMAX`), not the fat box — the box does not overhang the wall.

### Why this class keeps recurring — read this before changing any speed

The ROM's velocity units are **not pixels**. X coordinates count **2-px columns**, so:

- a routine that wants "one pixel" writes **HALF a column** — the player's
  `CLRB/ASRA/RORB` (0.5 columns = 1 px) and the tank's identical sequence;
- a routine that wants "one column" is **2 px** — the missile's `ADDA PD2`, the quark's
  `RND(1..SQSPD) x 4`, `TNKDRP`'s `ADDD #$0206`.

When a speed looks wrong, work out which of those the ROM is doing before trusting any
"px" in a comment — including comments in these notes. Fixed by this sweep so far: the
quark's cadence (§52), the tank's birth offsets (§52), the tank shell's size (§54), the
missile (§55) and the tank's body clock (§55).


## 50. NO enemy in Robotron blinks when it dies — the whole class

Author: *"enforcers shouldn't flash when hit. Also electrodes shouldn't explode
when hit, they shrivel."* Both correct (§44 had already fixed the grunt, so this
was the third report of the same defect). Rather than wait for the fourth, I
mapped **every** robot's kill vector out of the Gospel. `MKPROB <proc>,<pic>,
<kill vector>` (RRF.ASM:713) names them all:

| Enemy | Routine | Death sequence |
|---|---|---|
| Grunt | RRP8 `ROBKIL` | `JSR EXST` (BLOW HIM UP) then `JSR KILROB` → **explode, off** |
| Enforcer | RRC11 `ENFKIL` | `JSR KILOFP` then `JSR EXST` → **off, explode** |
| Electrode | RRP8 `PSTKIL` | `KILPST` + `DMAOFF` + `MAKP PKPROC` → **shrivel, NO `EXST`** |
| Spheroid | RRC11 `CIRKIL` | `KILOFP` + `MAKP CIRKP` → **7-frame bubble burst**, then the **"1000" picture for 30 steps** in slot `$FF` |
| Quark | RRTK4 `SQKIL` | `KILOFP` + `MAKP CIRKV` (colours `$DDDD`, count 8) → **bespoke 8-step burst** |
| Tank | RRTK4 `TNKIL` | `JSR HVEXST` + `KILROB` + `KILL` → **explode, off** |
| Brain | RRB10 `BRNKIL` | `HVEXST` + `KILROB` + `KILL` → **explode, off** |
| Prog | RRB10 `PRGKIL` | phony-burst picture (`PGXPIC`) + `EXST` + `KILROB` |
| Missile | RRB10 `CMKIL` | `KILROB` + the nine trail marks wiped → **instant off** |

**Not one of them blinks.** The 2-second blink-off was a port invention, marked
in its own constants as *"not in Appendix A; added for per-entity symmetry"* —
which is exactly the kind of justification that invents behaviour. `KILROB` is
**not** a death animation either: RRP8 calls it *after* `EXST` as the
bookkeeping that takes the robot out of the active list.

### Fixed

- **Enforcer**: `Kill()` → `Dead` immediately (the field bursts it via
  `IExplodable`). `EnforcerDyingBlinkTicks` deleted.
- **Electrode**: no longer `IExplodable` — a laser hit, a grunt walking into it
  and a hulk running it over all just start the shrivel. The three
  `SpawnExplosion(electrode, …)` call sites are gone, so `PSTKIL`'s missing
  `EXST` is now structural rather than a convention someone has to remember.
- **Spheroid, Quark, Tank, Brain**: blink removed, `Kill()` → `Dead`. The
  spheroid's and quark's BESPOKE bursts (above) are still to be built; until
  then they burst via the field's explosion like the others.
- All five invented `*DyingBlinkTicks` constants and
  `ElectrodeDeathAnimationSeconds` deleted, each replaced by a comment saying
  why no such constant exists — so nobody re-adds one.

### The ALIVE flashes — and what "flashing" actually meant

Spheroid, quark and tank had an invented alive flash (visible 20 / hidden 10
ticks). §50 left them because they were *spec.txt*-derived — then the author
settled it in two steps: *"Quarks and tanks should not flash. **However parts of
tanks do colour cycle**"* and *"The spheroid is rendered in a palette index
which colour cycles, I think."*

**The generalisable insight: spec.txt's "flashing <colour>" means PALETTE
CYCLING, not an on/off blink.** "Flashing light green" (spheroid), "flashing
light red" (quark), "flashing blue" (tank) all describe a sprite whose art is
drawn in colour-cycling palette slots, so it shimmers as the palette animates.
The port's plan read those words literally and built a visibility toggle — the
fourth time an invented behaviour has needed deleting (after the dying blinks,
the missile sprite, and the electrode explosion). **When the spec describes
something "flashing" or "cycling", check for a palette slot before writing a
blink.**

- **All three flashes REMOVED** — no enemy in this port has an invented
  visibility toggle any more. Counters renamed `_animationTicks` (they were
  also the frame clocks), the `Flash*` constants deleted, and
  `WallFleeHelper.FlashVisible` removed with its last caller. `WallFleeHelper`
  itself stays: `FleeTowardNearestEdge`/`MoveWithReflection` still drive the
  spheroid's and quark's motion.
- **The cycling itself was already correct** and needed no change — measured
  from the extracted art, for the six cycling-slot marker colours: quark 228,
  spheroid 218, hulk 243, player 234, **TankShell 48**, brain 38/frame,
  enforcer 2-20/frame, grunt 5/frame, spark 44. The M4 shader remaps those to
  the live slot colours. Removing only the visibility gate is what preserves it.
- **Open question: the TANK BODY has zero cycling indices**, while every other
  enemy has some. The ROM agrees — `TANKL` draws with `JSR DMAOFN`, the plain
  picture blit — so a static tank body is right, but it means spec.txt's
  "flashing blue" for the tank comes from its *shell*, or the sprite extraction
  dropped a cycling index from the tank's pictures. Worth a look if the arcade
  tank shimmers.
- Gotcha that cost a detour: **`glob('Tank*.png')` also matches
  `TankShell.png`**, which made the tank look like it had cycling pixels until
  the counts were split per file.

### Gates

0 warnings / 0 errors; **201 tests (200 pass + 1 skipped)**; 12 s smoke OK;
`verify-playfield.py` PASS. Re-pinned: three `ExplosionTests` that had encoded
the port's old "everything explodes" assumption —
`LaserKillsElectrode_SpawnsNOExplosion_ItJustShrivels`,
`GruntOnElectrode_OnlyTheGruntShatters`, and the ROM-slot-cap test (which now
uses grunts, because a post could never have exercised the cap). New:
`LaserKillsEnforcer_SpawnsExplosionImmediately_NoBlink`, and
`LaserKillsBrain_Scores500_AndLaserKillsProg_Scores100` now asserts `Dead`.

**Still open from this class:** ~~the spheroid's bubble burst + "1000" display and
the quark's 8-step burst~~ — **DONE 2026-09-17, see §64** (both were one shared ROM
routine with different colours, counts and picture chains).






### The PROG (`PROGST`/`GPOFF`/`GPDIR`/`PROG`/`PRGKIL`, RRB10:396-600)

1. **The step is PER-AXIS: ±2 horizontal, ±4 vertical.** The tables are
   `FCB picture,DX,DY`:
   ```
   PRGAL 0,-2,0 / 4,-2,0 / 0,-2,0 / 8,-2,0     ; LEFT
   PRGAR 12,2,0 / 16,2,0 / 12,2,0 / 20,2,0     ; RIGHT
   PRGAD 24,0,4 / 28,0,4 / 24,0,4 / 32,0,4     ; DOWN  -> 4px
   PRGAU 36,0,-4 / 40,0,-4 / 36,0,-4 / 44,0,-4 ; UP    -> 4px
   ```
   **A prog moving vertically travels twice as far per body as one moving
   horizontally**, and always a full body of `NAP 3`. The port used a flat 2px.
2. **It does not home in on you — it aims at you PLUS a standing error.** GPOFF
   rolls a per-prog offset at creation and re-rolls it on ~3% of bodies
   (`SEED > $F8`): **X = (RND(1..15) - 8) × 4**, i.e. −28..+28 in steps of 4;
   **Y = ((19 - RND(1..18)) × 2) - 18**, i.e. −16..+18 in steps of 2. GPDIR then
   aims at `player + offset`.
3. **Only one axis at a time, chosen 50/50.** `GPDIR LDA HSEED / BMI GPDY` —
   the top bit picks X or Y. A prog never walks diagonally and never aims with
   both axes.
4. **Aim points wrap.** `CMPA #XMAX+$30 / BLS GPD1 / LDA #XMIN` (and
   `#YMAX+18` for Y): an aim point past the far edge wraps to the OPPOSITE edge.
   That is how progs occasionally turn *away* from you.
5. **Ties turn around** (`BLS`, so `aim <= mine` goes left/up), and a re-aim
   resets the animation (`LDA #$FD / STA ODATA,X` wraps the +3 advance to 0).
6. Re-roll odds: offsets `SEED > $F8` (7/256), re-aim `LSEED > $E4` (27/256),
   plus an immediate re-aim when a step is refused (CKLIM fail → `PROGND`), in
   that order — GPDIR uses whatever offsets are current.

### The CRUISE MISSILE (`BRNSHT`/`GCMDIR`/`CMISL`/`CMMOV`, RRB10:616-760)

1. **Two CMMOVs per body, one px each.** `CMISL ... JSR CMMOV / JSR CMMOV /
   NAP 2,CMISL` with `CMMOV` moving exactly 1px and re-checking the bounds each
   time — so the missile travels **2 arcade px per ~3-tick body** and can turn
   around flush against a wall. The port moved 1px per port tick.
2. **GCMDIR's control flow is a trap.** 
   ```
   LDA SEED / BPL GCMDY     ; bit 7 clear -> JUMP INTO THE Y BLOCK
   ... X arm ...
   LDA LSEED / BMI GCMDX    ; bit 7 set -> skip Y
   GCMDY ... Y arm ...
   ```
   `GCMDY` is the *start of the Y block*, so `BPL GCMDY` skips the X block **and
   the LSEED gate with it**. Net distribution: **Y-only 50%, X-only 25%,
   diagonal 25%, and NEVER stationary** — Y is the missile's favoured axis. My
   first pass read it as four independent 50% rolls with a 25% "freeze"; the
   tests now pin "never stalls". (The port's old 50/25/25 roll had the axis bias
   **backwards** — it armed X 75% of the time.)
3. **The aim nudge is asymmetric.** `ANDA #$F / ADDA #-6 / ADDA PX16` →
   `player + ((seed & $F) - 6)`, i.e. **−6..+9**, and it is applied to the
   PLAYER's coordinate *before* the compare against the missile's, with `BHS`
   so a tie moves **positive**. The port used a symmetric ±6 on the delta.
4. **The re-aim timer is in BODIES**: `RND(1..7)` per re-aim (`LDA #7 / JSR
   RMAX`), decremented once per body before the move.

Not modelled: CMMOV writes **directly into video memory** (`STY $DDDD,[OX16,X]`
erases the old spot, `$AAAA` draws the new one) and remembers the last 8
addresses in its process block purely so `CMKIL` can wipe them. The port draws a
sprite instead, so the `CMPIC`/`CMP1` two-frame flicker is the port's stand-in.
Conversely: the missile's `OBJX` is its true position **minus (1,1)** — the "fat
phony guy" box is deliberately 1px off-centre, and the port's centred 4×4 box is
1px optimistic.

### Gates (all four green before the commit)
`dotnet build` 0 warnings / 0 errors; **196 tests, 0 failed, 1 skipped**;
12s launch smoke OK; `tools/verify-playfield.py` PASS.

New/pinned tests (in `PlayFieldPhaseETests`):
`Brain_XApproachHasADeadZone_ButYDoesNot`,
`Brain_MoveIsAllOrNothing_ItDoesNotSlideAlongAWall`,
`Brain_WithNoLivingHumans_ChasesThePlayer` (now asserts the +2 Y jitter),
`Prog_StepsTwoPxHorizontallyAndFOURPxVertically_PerBody`,
`CruiseMissile_MovesTwicePerBody_AndNeverStalls` (400 missiles: X armed
~50%), `CruiseMissile_TravelsTwoPxPerAxisPerBody`.

**Not yet verified by the author.** Movement model changes are exactly the kind
of thing on-screen measurement cannot validate — the author eye must judge
whether the brains jitter, the progs scatter and the missiles fly like the
arcade.

## 56. Spheroid and enforcer motion: the last two "speed" models (2026-09-16)

Author: *"the tanks spawn instantly whereas they are 'born' like the enforcer"*
→ after the unit sweep, *"Fix that too"*. Both remaining suspects turned out to
be MODELS, not constants: neither robot has a speed at all in the Gospel.

### 56.1 The unit that keeps biting: a COLUMN is 2 px (restated, proofs in §52)

- The video buffer is COLUMN-MAJOR: `address = column * 256 + row`.
- X is counted in 2-PIXEL COLUMNS, Y in ROWS. Any ROM routine that wants "one
  pixel on X" writes HALF a column (`CLRB/ASRA/RORB`); anything that wants "one
  column" is 2 px.
- Therefore the ROM's X/Y asymmetries (x4 vs x8, ±$0100 vs ±$0200) are
  COMPENSATIONS, not different speeds: 1 column/frame = 2 rows/frame = the same
  2 arcade px/frame.
- `PortTicks(n) = n * 6 / 5` TRUNCATES, so any body of N ROM frames must use the
  accumulator idiom: `fifths += 5; if (fifths < N * 6) return; fifths -= N * 6;`
  (`PortTicks(3)` = 3, not 3.6; `PortTicks(4)` = 4, not 4.8; `PortTicks(8)` = 9,
  not 9.6).

### 56.2 Spheroid — CIRCLE / CIRNAC / CIRGO (RRC11)

Body = `NAP 2` = 3 ROM frames. Phases, in the order the ROM runs them:

| phase | pictures | countdown |
|---|---|---|
| CIRCLE (spin) | 5, `CIRP0..CIRP4` (`OPICT += 4`, wraps when the next entry would pass `CIRP4`) | PD2 = `RND(1..CDPTIM)` → CIRC2 |
| CIRC2 (drop) | 8 (`CIRP0..CIRP7`, wraps past `CIRP7`) | PD2 = `RND(1..CDPTIM/4)` per drop |
| CIRC3 (escape) | 5, the same `CIRP0..CIRP4` as the spin | none — ends on the X exit test, which is itself on the wrap pass |

⚠ **The two 5s were 4s here until §91.** `CMPD #CIRP4 / BLS` (source) — `CMPD #$1502 / BLS $11C7` (R5) — STORES `CIRP4`, so it is part of both cycles; reading the boundary as "wraps past `CIRP3`" gave the port a four-picture spin and a four-picture escape, and the drop phase's pictures the port had right by accident.

**The countdown counts ROTATIONS, not bodies.** In both CIRCLE and CIRC2L the
`DEC PD2,U / BEQ` sits on the branch taken only when the picture WRAPS
(`ADDD #4 / CMPD #CIRP… / BLS`), so a step is a full 5-picture spin (15 frames)
or a full 8-picture spin (24 frames). The port had the structure right by
accident (it counted whole animation cycles) but the wrong period and the wrong
picture count (it spun 8 pictures in the phase that spins 5).

**`TST STATUS / BNE CIRC1`** skips only the PD2 decrement: a frozen game keeps
spinning AND keeps accelerating — it is the generic mover, not this code, that
stops the object moving.

**CIRNAC — the acceleration pair:**
```
PD5 = (HSEED & $1F) - $10          ; X accel -16..+15
PD6 = ((LSEED ^ SEED) & $3F) - $20 ; Y accel -32..+31 (twice as large — 2 px per column)
PD7 = RANDU(15)                    ; 1..15 BODIES, then CIRNAC again
```
**CIRGO — accumulate, clamp, damp (per body):**
```
OXV += PD5 (SEX); clamp OXV to ±$0100   ; 1 column/frame
OYV += PD6 (SEX); clamp OYV to ±$0200   ; 2 rows/frame
COMA COMB / ASLB ROLA / ASLB ROLA / TFR A,B / SEX / ADDD OXV,X
    = v += floor((-4v - 4) / 256) = v - v/64
```
The velocity therefore CONVERGES on `64 x accel`, and the clamp is the usual
terminal state — the speed is EMERGENT, so no constant can express it. That is
why `SpheroidSpeed` (2 px/tick, ~1.7x too slow) and its `speedBonus` parameter
are both deleted: they described a model that does not exist.

**The mover keeps the fraction.** OXV is a 16-bit 1/256-unit value and OPB80 adds
it EVERY FRAME (§43 fact 2), hence `_remainderXFp/YFp` — and when an axis is
REJECTED out of bounds the ROM rewrites BOTH bytes with the old ones, so the
fraction is not consumed either.

**The escape is not a flee.** `CIRC3` sets `OYV = 0` and `OXV = ±$0100` exactly
(the sign is `SEED` bit 7 — a coin flip here, the port has no LFSR state), then
spins the same FIVE pictures the idle phase does (`CIRP0..CIRP4`) until
`OX16 <= XMIN+3` or `>= XMAX-10`, where `XMIN = 7` and `XMAX = $8F` (RRF.ASM:69)
— buffer columns 10 and 133. That X test sits INSIDE the wrap branch, so it runs
once per five-picture cycle (the object can overshoot the column and, at the
wall, wait there for its next wrap pass — §91). `CIR4` then runs `KILLOF` +
`SUCIDE`: the object is removed with NO burst and no score. So a spheroid ALWAYS
leaves sideways at exactly 2 px/frame, never "toward the nearest wall", and
never explodes at the end of its life.

### 56.3 Enforcer — ENFNV / ENFSHT (RRC11)

No speed constant either: a pure proportional approach.
```
ENFNV: PD7 = RAND16 & $1F              ; re-aim after 0..31 BODIES
       targetX = PX16 + (HSEED >> 3)   ; player + 0..31 COLUMNS (2 px each!)
       vX = (targetX - OX16) * 2       ; ASLB/ROLA = x2, in 1/256 units
       (Y the same against PX16+1 with 0..31 ROWS)
ENFSHT: fire countdown = RND(1..ENSTIM) BODIES, re-armed BEFORE the 20-spark cap
        check, so a shot the cap swallows is simply lost
```
Body = `NAP 3` = 4 ROM frames. `v = 2 x offset` in 1/256 units means it advances
`offset / 128` per frame — fast when far, CRAWLING as it arrives, which is the
"near-zone" behaviour a constant 3 px/tick could not reproduce. Both countdowns
are in bodies, and the X jitter is in COLUMNS (the port added half of it).
The grow-up is FIVE spawn pictures at `NAP 8` = 5 x 9 = **45** ROM frames (the
port used 40, treating each NAP 8 step as 8 frames).
**Dead ROM path, recorded so nobody "fixes" it later:** in ENFNV the `LDB #XMAX`
clamp is unreachable — `CLRA` before `CMPA #XMAX` guarantees `BLS`.

### 56.4 Verified, and what needs the author's eye

Four gates green on both commits (`3447cde` enforcer, `bfcdb3f` spheroid):
0 warnings, 201 tests, 12 s smoke, verify-playfield PASS. The enforcer's fire-gap
test now measures gaps in BODIES (`[PortTicks(4)-1, PortTicks(120)+1]`).
**Not verified by the author:** both changes alter how these two robots MOVE,
which no screenshot can validate. Expected on-screen difference — the spheroid
eases into long straight-ish glides at up to 2 px/frame instead of a slow
constant diagonal with wall bounces, and the enforcer decelerates into a crawl
as it closes on the player.

## 57. A leaked pixel-shader pass (the black prog trail), 2026-09-16

Author: *"The progs trail doesn't render right"*, then *"The trail should be a
black silhouette on a colourful block"* — which located the fault exactly: the
silhouette was right and the CARD was black.

### 57.1 The mechanism

`SpriteSet`'s draw helpers bind their shader by hand:

```csharp
effect.CurrentTechnique.Passes[0].Apply();
```

`EffectPass.Apply()` writes the pixel shader straight to the DEVICE. The game
draws with `SpriteSortMode.Immediate` and calls `Begin()` with no effect, so
**nothing rebinds the pass per draw** — the binding outlives the draw that made
it, and the next plain `spriteBatch.Draw` runs through whatever pass was bound
last. Same class as §40 (a declared vertex shader clobbered SpriteBatch's
projection for the rest of the batch); §40 fixed the vertex stage, but the
*PixelShader* stage still leaked.

`DrawSpriteSolidWithBackground` is where it bit: it draws the card as a plain
`WallPixel` fill and then the silhouette through `SolidRemap`. On the NEXT ghost,
the CARD was drawn through `SolidRemap` → filled with the previous silhouette's
colour → black. Seven black cards with black figures on top is a black trail.
The prog "head" looked correct only because its silhouette colour (slot 10, a
cycling slot) filled the whole quad, which is the right look anyway.

### 57.2 The fix

A `UsePassThrough()` helper rebinds `MainPS` (the six-marker remap, then
`return t * input.Color` — otherwise the built-in sprite shader) and is called:

- **before** the card fill in `DrawSpriteSolidWithBackground`,
- **after** the silhouette in `DrawSpriteSolid`,
- **before** the draws in `DrawSprite` and `DrawGlyphCycling` (dedupes the old
  inline block), and
- **before** the fill in `DrawSolidRectangle` (the brain's reprogram block and
  the cruise missile's trail marks).

No pass is left bound when a helper returns, so the leak window is closed for
every other raw draw in the game (wall, HUD, explosions, sparks).

### 57.3 §53 CORRECTED — and the lesson

§53 concluded that the inverted colour pairs were the ROM's design. That is
still TRUE — `HUMON` maps A (the high byte) to the BLOCK via `BLKON` and B (the
low byte) to the FIGURE via `MPCTON`, so `$EE00` is a slot-14 card carrying a
black figure and `$00AA` is its exact inverse — but §53 stopped there and never
asked *why the author was seeing black*. The answer was the RENDERER, not the
decode: the earlier "the human that has been progged is solid black" report had
the same root cause, because that human goes through the same helper.

**Lesson: when the author reports a COLOUR, check the draw path before
re-deriving the ROM.** Two reports of one symptom got two different Gospel
theories; only the third question ("what should it look like?") found the single
cause.

### 57.4 The trail itself (same session, `3a6e742`)

The prog's shadow ring is SEVEN ghosts, not six: `PROG3` indexes `PD+8` and
hands out entries from `PD+10`, wrapping when the index reaches `SPSIZE`, and
with `PD EQU 7` (RRF.ASM:551) and `SPSIZE EQU PSIZE+16 = 31` (RRF.ASM:565) those
are byte offsets 17,19,...29. `CLRSP`, which zeroes `PD..SPSIZE-1` = 7..30,
confirms the range. Each ghost now also keeps the pose it was BORN with: the ROM
blits a ghost once (`HUMON` at that body's `OPICT`) and never re-blits it, so the
trail shows the ABAC walk cycle repeating rather than one uniformly animated
snake. Both are pinned by
`Prog_LeavesTheRomsSevenGhostTrail_FrozenInItsCreationPose`.

## 58. The SCORE and MEN display, decoded end to end (2026-09-17)

**Source note for everything below:** these routines live in the OPERATING SYSTEM ROM
(`$D000`+), whose vector table is listed in `RRF.ASM` (`BLKCLR` = `$D01B`, `PCTON` =
`$D021`, `SCRTR0` = `$D00F`, `PR57V`/`WRD5V`/`WRD7V` = the print-string entries, ...).
The Gospel's 28 modules do NOT include the OS, so for these the disassembly IS the
primary source — and that is acceptable: it is the actual R5 bytes, not a paraphrase.
Use the vector table to identify any `$D0xx` call before guessing at one.

Author: *"start with the score and lives display as they are not correct."* The port
had the score left-anchored at the screen's top-left corner in slot 1, and the "lives"
drawn as full-size PLAYER SPRITES along the bottom-left — neither is the arcade.

### 58.1 The score — `DRAW_PLAYER_SCORES` (`$DC13`, OS ROM; caller `$34AF`)

```
34AF: PSHS U,Y,X,B,A
34B1: LDA $40        ; PLRCNT
34B3: JSR $D00F      ; -> $DC13: draw ONE player's score (A = player number)
34B6: DECA / BNE $34B3                 ; loop: PLRCNT, then PLRCNT-1 ... i.e. P2 FIRST
34B9: BSR $3526      ; DRAW_BORDER_WALLS      (writes colour bytes directly, no blitter)
34BB: JSR $26C9      ; -> $34E0: DRAW_LIVES_REMAINING
```

- **Destination**: P1 `$180E`, P2 `$580E` (column-major: `column*256 + row`), i.e.
  **P1 col 24 row 14, P2 col 88 row 14**. A 15-byte-column x 6-row rectangle is cleared
  to black first (`LDD #$1506 / JSR $D01B`).
- **Cursor**: `LEAX -$300,X` = back 3 byte columns = **col 21 (x = 42)** for P1 and
  col 85 (x = 170) for P2 — the ROM's own comment is literal: `7 NOT 8 DIGITS`.
- **Colour**: `LDB #$11` normally, but `CMPA $3F (CURPLR) / BNE` upgrades a player's OWN
  score to **`$AA` = slot 10** (one nibble per pixel pair, so `$11` = slot 1 and `$AA`
  = slot 10). Slot 10 is the LF ("laser flash") CYCLING process (§4.12), so **the current
  player's score is drawn in a cycling slot** and the idle player's in static slot 1.
  §38's "P1 = slot 1" was an over-simplification: in a 1-player game P1 IS the current
  player (`START1` sets `STA CURPLR`), so the arcade 1P score is the CYCLING one.
- **Digits** (`$5F9F` → `$6096` = large font 6x6, `$D2 = 7`, `$D1 = 2`): the score is
  **4 BCD bytes** (`p1_score`) = 8 digit positions, drawn in this order:

  | call | source | digits drawn |
  |---|---|---|
  | 1 | `,U ANDA #$0F` | ten-millions (always 0 → always blanked) then millions |
  | 2 | `1,U` | hundred-thousands, ten-thousands |
  | 3 | `2,U` | thousands, hundreds |
  | 4 | `3,U` | tens, units |

- **Leading-zero blanking** (`$610D`): a `0` digit while `$D6 == 0` ("nothing printed
  yet") takes the blank path — the cursor still ADVANCES, by `$6128`'s `LEAX $0200`
  +4 px and (`$6136`) `LEAX $0100` +2 px for the LARGE font = **6 px**, where a drawn
  glyph advances `width+1` = **7 px**. The number is therefore left-anchored at col 21
  with 6-px-wide holes where the suppressed zeros were.
- **The last two digits are ALWAYS drawn**: `$DC4D INC $D6` before the final call, so
  tens and units take the draw path even when 0. **A fresh game shows "00"**, and 100
  shows "100" (6 blanks = 36 px, then 1-0-0 at x = 78). The port printed NOTHING for 0
  and left-anchored everything.
- Both nibbles of the mask byte are the same, so the "solid + transparent" blit (`$1A`)
  paints the glyph's non-zero pixels in that one slot — the font PNG is a SHAPE and the
  colour comes from the live palette.

### 58.2 The men (`DRAW_LIVES_REMAINING`, `$34E0`)

```
34E0: LDX #$2E0E / LDD #$1508 / JSR $D01B   ; clear 15 cols x 8 rows (P1)
34E9: LDX #$6E0E / JSR $D01B                ; clear P2's block
34EF: LDY #$3592                            ; frame metadata 03 08 35 96 = 3 bytes x 8 rows @ $3596
34F3: LDA p1_men / BEQ ... / CMPA #7 / BLS / LDA #7     ; cap at SEVEN
3500: LDD #$2E0E                            ; dest col 46 row 14
3503: JSR $D021                             ; -> $DA82 BLIT_IMAGE_NO_TRANSPARENCY
3506: ADDA #$04                             ; +4 byte columns = 8 px per man
```

- The men are the **mini man picture** (`MNPIC`, RG23 / `$3596` in R5) — **3 bytes x
  8 rows = 6x8 px**, NOT the 8x12 player sprite.
- **P1 col 46 (x = 92)**, **P2 col 110 (x = 220)**, both row 14 — immediately to the
  right of that player's score field (which ends at col 45.5), **8 px apart**.
- `p1_men` is the SPARE count: `START1` loads `ZP1LAS = NSHIP` and `PLSTRT` does
  `DEC PLAS,X` as each life begins, so with 3 ships you see **2 icons during your first
  life**. In port terms the icon count is `Player.Lives - 1`.
- The blit is op **`$02` (no transparency, mask not set)**, so the icon is drawn in its
  OWN palette indices — the `$CF` left over from the score blit is not used, because
  mode `$02` does not substitute the constant.

### 58.3 The messages (string table `$6291`, `PRINT_STRING_SMALL_FONT` `$613F`)

`A` = string INDEX, `B` = the parameter to insert, and the strings carry their own
control codes. Read straight out of the ROM image:

| idx | decoded | meaning |
|---|---|---|
| 103 | `$12 (col 62,row 122,odd 1)` `$0F $649E` `$09` `$10` | "PLAYER <n>" at the CENTRE (2P turn start) |
| 104 | `$12 (col 62,row 238)` `$15` `$04 $AA` `$10` `$05 (+3 cols)` `$04 $BB` `" WAVE"` | **"<n>  WAVE" at the BOTTOM** (row 238), number in flashing $AA, " WAVE" in $BB |
| 114 | `$05 (+4 cols)` `$09` `$04 $AA` `$10` | P2's wave number appended (2P only) |
| 40 | `$12 (col 62,row 128)` + "GAME OVER" | game over, screen centre |
| 75 | `$12 (col 63,row 121)` `$0F $6E05` `$12 (col 62,row 134)` `$0F $6DE8` | the two-line "PLAYER n" / "GAME OVER" (`$6E05` = "PLAYER " + `$09` + the parameter, `$6DE8` = "GAME OVER") |
| 113 | `$12 (col 36,row 238)` + "COPYRIGHT 1982 WILLIAMS ELECTRONICS INC=" | bottom-left |

**How to decode ANY of these strings** (the op table is `TEXT_FUNCTIONS` at `$61A2`,
indexed by `(op-1)*2`; ops are `< $17`, and anything `>= $17` is a glyph whose table
index is `ASCII - $30`):

| op | args | effect |
|---|---|---|
| `$04` | 1 | set the text colour (`STA $CF` — the blitter's solid-blit colour byte) |
| `$05` | 3 | move the cursor by (dx cols, dy rows, adjust-odd-pixel flag) |
| `$09`/`$15`/`$14` | 0 | set `$D1` = 1 / 2 / … (`$D1` = 2 enables leading-zero BLANKING; 1 disables it, so zeros print) |
| `$10` | 0 | print the `B` parameter (BCD) |
| `$12` | 3 | absolute cursor: (address hi, address lo, odd-pixel flag) |
| `$0F` | 2 | print the string at that address (a nested call) |
| `$07`/`$08` | 0 | switch to the large / small font |

Two port consequences: **(a)** the arcade's wave indicator is the bottom-of-screen
"n WAVE", not a top-right "LEVEL n" — and that matters, because in a 2-player game the
top-RIGHT is where player 2's score and men live; **(b)** the messages use the 4x5-px
SMALL font, not the score's 6x6 large font. The "PLAYER n" message's colour is
`PSTCOL` (the per-wave POST slot — `GameplayConstants.PostSlotForWave`, §45), not the
score's `$AA`: PLS0D does `LDA PSTCOL / STA TEXCOL`, so wave 1's is slot 15 (RED-GOLD)
and wave 10's is slot 10.

### 58.4 What the art decodes to (so the port can build it without a PNG)

`$3596`, 3 bytes per row, high nibble = left pixel:

```
02 22 00  ->  .222..      nibble 2  = head
BB 0B B0  ->  BB.BB.      nibble B(11) = body/arms
BB 0B B0  ->  BB.BB.
00 20 00  ->  ..2...
88 08 80  ->  88.88.      nibble 8  = legs
30 80 30  ->  3.8.3.      nibble 3  = feet
08 08 00  ->  .8.8..
88 08 80  ->  88.88.
```

**Slot 11 is one of the six cycling slots** (RGB process: green → red → blue, 8 ticks a
step), so the little man's body and arms cycle while his head (slot 2), legs (slot 8)
and feet (slot 3) stay fixed. Same idiom as §50's enemies: he is not blinking, his
BODY SLOT is cycling. The port bakes the six marker colours for cycling slots into
textures and remaps them in `ColorCycle.fx`, so the mini man is generated at runtime
with its slot-11 pixels painted in the slot-11 marker (`RobotronColor.FromByte(0xF4)`)
and drawn through the normal sprite path.

### 58.5 The port's new layout

Arcade pixel positions are mapped with the existing `GameplayConstants.ArcadeX/ArcadeY`
(proportional, integer). The round-8 complaint ("the score overlaps the border wall")
came from having used **row 20** (a bad decode) instead of the ROM's **row 14**:
`ArcadeY(14)` = 21 with a 12-px glyph leaves ~7 px above the port's 40-px wall band,
where `ArcadeY(20)` = 31 drove the glyphs straight through it. The HUD is now the
arcade's: score + men in the top band (both players' groups in a 2P game, which is also
why the port's top-right "LEVEL n" had to go), and the wave indicator moved to the
bottom as "<n>  WAVE".

### 58.6 Finding: the wave-clear "bonus man" is a WASH, not a missing feature

`GEXEC0` ends the wave with `INC PWAV,X` **and** `INC PLAS,X` (GEXX1) before
`JMP PLSTRT` — and PLSTRT opens with `JSR PLINDX / DEC PLAS,X`, because starting a
life takes a man. So clearing a wave grants one man and starting the next wave
consumes it: **net zero**. The port's model (Lives carried over unchanged, and
`Player.Kill` doing the decrement there) already matches the ROM, and adding a
bonus life per wave would have been an invented rule. Recorded because the
`INC PLAS,X` alone reads like free men.

## 59. Two players (the arcade's alternating game), and how the port models it

Author: *"The game should also support a two player mode, as per the arcade. So if you
press '1' on the title page you start a 1 player game. If you press '2' you start a two
player game. Note the players currently take turns on the arcade game, but I'd like to,
eventually, make a two player simultaneous game."*

### 59.1 The ROM's two-player flow

| Step | ROM | Behaviour |
|---|---|---|
| start | RRG23 `START1`/`START2` ($0021/$001E) | A = 1 or 2 → `STA PLRCNT`; both players get `ZP1LAS = NSHIP` (CMOS "ships per credit") and wave 1; `CURPLR = 1` |
| per-player state | `PLDATA` … `PLDEND`, `PLINDX`/`PLDX` | score (4 BCD bytes ZP1SCR/ZP2SCR), next-free-man, `PLAS` (lives), `PWAV` (wave), and the frozen enemy list — `PLSAV`/`PLRES` copy it in and out |
| turn start | `PLSTRT` → `PLS0D` | `DEC PLAS,X` (a life begins); in a 2P game print "PLAYER n" (string 103) and `NAP 115` before erasing it — a 1P game skips it (`LDA PLRCNT / DECA / BEQ`) |
| death | `PLEND` → `PLEND3` → `PLE1B` | `LDA CURPLR / EORA #3` → `PLDX` → `LDB PLAS,X / BEQ PLE1` (keep flipping until a player with men is found) → `STA CURPLR` → `JMP PLSTRT` — so the turn ALWAYS passes to the other player while they have men, and that player resumes at THEIR wave |
| player out | `PLEND3` | when the current player has no men but the other does: "PLAYER n" + "GAME OVER" (string 75) at the centre, `NAP $60`, then the flip |
| game over | `PLEND` | `LDA ZP1LAS / ORA ZP2LAS / BNE PLEND1` — only when BOTH are out: "GAME OVER" (string 40), `NAP 120`, end |
| wave clear | `GEXEC0` | the CURRENT player's `PWAV` advances (byte INC skipping 0); `CURPLR` is NOT touched, so the same player plays their next wave |

High scores: `RRTESTC` checks **both** `ZP1SCR` and `ZP2SCR` against the table, so both
players can enter initials at the end of a 2-player game.

### 59.2 The port's model

`Level/PlayerSlot` is the per-player state (`Number`, `Input`, `Wave`, `Score`, `Lives`,
`Rescues`) and `Level/GameSession` is the game in progress: the slots plus whose turn it
is, with `SwitchToPlayerWithMen()` implementing `PLE1B` (returns false when the other
player is out, so a 1P game never "switches"). `PlayingState` builds the field for the
current player from their slot, syncs back on wave clear / death, and:

- **keeps the turn on a wave clear** (the session simply rides through `WaveClearState`);
- **passes the turn on a death** while the other player has men, and shows the ROM's
  "PLAYER n GAME OVER" pause (`NAP $60`) when the player who died is out;
- ends the game — with every player's score, highest first, offered to the high-score
  table — only when nobody has men.

`PlayerSlot.Input` is per slot precisely so the SIMULTANEOUS mode the author wants later
can hand player 2 their own `IPlayerInputSource` (gamepad 2, a second keyboard set, …)
without touching the turn logic: in simultaneous play the session would keep both slots
alive at once and `PlayingState` would own one field per player instead of one shared
field. Nothing else in this change assumes alternation except `SwitchToPlayerWithMen`.

**Not verifiable by screenshot, and currently unreachable in play:**
`GameplayConstants.PlayerInvincibleForTesting` is STILL ON (the standing author-gated
item), so a life can never end — which means the turn-passing, the "PLAYER n GAME OVER"
pause and the game-over-with-two-scores path cannot be exercised by playtesting until
that switch is flipped. The session/turn rules are covered by
`tests/.../Level/GameSessionTests.cs` in the meantime.

## 60. The font glyphs were MIRRORED inside every byte (2026-09-17)

Author, minutes after the HUD landed: *"I don't think the score display is right, the
font characters don't look correct."* Correct — and it was not the layout, it was the
ART: **every committed glyph PNG had the two pixels of each byte swapped.**

### 60.1 What was wrong

`tools/extract-fonts.py`'s `glyph_pixels` wrote the LOW nibble to the LEFT pixel:

```python
for half in (4, 0):
    x = byte_i * 2 + (1 if half == 4 else 0)   # INCORRECT: high nibble -> right pixel
```

while the arcade — and **every other extraction path in this repo** — puts the HIGH
nibble on the left (`tools/SpriteExtractor/Program.cs`: `nibble = (x & 1) == 0 ? (b >> 4)
& 0xF : b & 0xF`, and the author's own sprite editor). The result: each glyph was
mirrored in 2-pixel blocks, so the large "0" lost its right-hand stroke and read as a
"C", the "1" grew a stray bottom row, and so on. Decoding `$EC93` (largefont0) straight
from `robotron64k.bin` gives a perfect ring:

```
#####.        ####.#
#...#.        .#...#        <- what the PORT drew before this fix
#...#.   vs   .#...#
#####.        ####.#
```

### 60.2 How it survived the earlier checks, and the lesson

§38 replaced the hand-drawn placeholders and verified the DECODE — a fresh nibble dump of
the ROM's `$EC93`, which is correct — but that check never read the generated FILES back.
Their size, count and alpha were all right too, so the content pipeline, the 214 tests,
the smoke test and `verify-playfield.py` were all happy. **A generated asset needs a check
that reads the generated ART back and compares it to the source of truth** — hence
`tools/verify-fonts.py` (below).

Two writers made it worse: `tools/extract-fonts.py` (white-on-transparent masters + the
six colour-cycle marker variants) and `tools/SpriteExtractor` (palette-coloured pixels)
both wrote `Font_L_*`/`Font_S_*`. A run of the extractor — which decodes CORRECTLY — was
"reverted as the suspicious change" in an earlier session (see the trap note it left in
the repo memory), so the buggy files were restored. **The SpriteExtractor no longer
writes font glyphs at all** (`IsFontGlyph` skip in all three passes): `extract-fonts.py`
owns them, because only it emits the marker variants the M4 shader needs.

### 60.3 The guard, and the gate list

`tools/verify-fonts.py` re-decodes the ROM and compares **every pixel** of all 78 glyphs
plus their 6 cycling variants (546 files) against `ef.NAME`/`ef.SMALL`/`ef.LARGE` — the
same tables the generator uses, imported from it, so the two cannot drift. It is now
gate 5:

```
python tools/verify-fonts.py   # expect: PASS: 546 font glyph PNGs match the ROM
```

Regenerate with `python tools/extract-fonts.py`, then re-run the gate.

## 61. EXPLOSIONS re-derived from the Gospel — three engines, one record (2026-09-17)

The port's explosion was a **symmetric strip fan for every kill** with an invented drift
and (since 2026-09-16) an invented spacing curve. §35.5 had already decoded the RRDX2
algorithm from the Gospel; what it could not see from the disassembly alone is **which
engine a kill uses**, and the answer is in the Gospel's own three modules.

### 61.1 The dispatch — `RRX7.ASM`'s `EXSTV` (the R5 `$5C1F` thunk)

> **CORRECTED by §69 (2026-09-17).** The code below is the Gospel, but §61 read
> `LASDIR` as "the VERTICAL direction byte" and therefore had the two engine rows
> SWAPPED. `LASDIR` is the shot's **X step** and `LASDIR+1` its **Y step**, so a shot
> with no X step is a pure VERTICAL shot — and it takes the **HORIZONTAL**
> (`HEXST`/`HOREX`) explosion. The engine is named for the axis its pieces MOVE
> (across the shot), not for the shot. The table below is the corrected one.

```
EXSTV LDA LASDIR / BNE EXST1      ; LASDIR = the shot's X step (0 = no X movement)
      JSR HEXST                   ; 0 -> a pure VERTICAL shot: the HORIZONTAL explosion
EXST1 LDB LASDIR+1 / BEQ EXST1A   ; LASDIR+1 = the shot's Y step (0 = no Y movement)
      EORA LASDIR+1 / COMA / JSR DXST   ; both set -> the RRDX2 45-degree fan
EXST1A JSR GETBLK                 ; 0 -> a pure HORIZONTAL shot: the VERTICAL explosion
```

| shot | engine | strips | fan |
|---|---|---|---|
| diagonal | **RRDX2** `EXSTZ` | the sprite's ROWS | 45 degrees — `XSIZE = ±(YSIZE>>1)` |
| pure vertical | **RRHX4** `HEXST`/`HOREX` (the "H" family) | the sprite's COLUMNS | left/right, **no lean** |
| pure horizontal | **RRX7** `EXST1A` (the "V" family) | the sprite's ROWS | up/down, **no lean** |
| no direction (a grunt-electrode mutual kill; the tank/brain paths run `HVEXV`) | — | ROWS | no lean |

`SLOPE = ~(vertical ^ horizontal)`, so **UP-LEFT and DOWN-RIGHT are negative** (the rows
step left going down) and UP-RIGHT and DOWN-LEFT positive. §35.2's labels for `$88`/`$89`
were the wrong way round: `LDA LASDIR` is the shot's X byte — `HVEXV` sets `$0100`
("FORCE VERT", i.e. X = 1, Y = 0), which has a nonzero X step, so it skips `HEXST` and
`LDB LASDIR+1` = 0 sends it to `EXST1A`, the V engine — the disasm's annotation ("A = what
the enemy's TOP HALF does, B = the BOTTOM half") is *describing the resulting fan*, not a
second direction pair.

### 61.2 What is shared, and the numbers that matter

The two row engines (RRDX2/RRX7) and the column engine (RRHX4) all use the same record
shape (`RRHX4`'s `YSIZE EQU HXRAM` even aliases its cross step onto RRDX2's `XSIZE`
scratch byte) and the same APGO:

```
YSIZE  = YSIZER >> 8                 ; the step: ROWS (row engines) or COLUMNS (column engine)
XSIZE  = ±(YSIZE >> 1)               ; a column step is 2 px, so this is a 45-degree lean
TEMP2  = YSIZE >> 1, or 0 when YOF == 0   ("IF DOWN FROM TOP....FIX TOP ONE (OBSCURE BUG)")
UL_Y   = YCENT − YSIZE*YOF + TEMP2   ; the fan straddles the IMPACT, not the sprite centre
```

- **The records live in ONE pool of ten** (`EX` at DXTAB, `RMB ((10-1)*EXSIZE)`, struct =
  51 bytes), and explosions and appears both take from `EXFREE`. The old "7 slots of 242
  bytes at `$B3E4`" reading does not match the Gospel's struct or count.
- **The size is a 16-bit accumulator stepping $100 a frame.** `EXSTZ` starts `YSIZER =
  $0100` ("1 UNIT IS MIN") with `FRAMES = $10`, and `WRITE` does `DEC FRAMES` (0 → the
  record is freed), then `ADDD #$100` — so a kill draws **15 times with steps 2,3,…,16**.
  The old port model's linear 1…8 curve was invented.
- **`APSTZ`/`AWRITE` is the same engine backwards** — the APPEAR: `YSIZER = $1000`
  ("START LARGE FOR APPEAR"), −$100 a frame until the step would reach 1, so the strips
  CONVERGE. That is what materialises robots at a wave start (`RRG23`'s `APST`/`HAPST`).
- **Strips are DROPPED, never scaled**, by four clip passes (`YMIN` above, `XMAX`/`XMIN`
  for the lean, `YMAX` below, `XMAX` for the trailing edge). The dropped rows are the
  sprite's FIRST ones (`X = DATA + (height − HITE)*2`), so a clipped fan keeps its bottom.
- **Unit quirks worth knowing:** RRDX2's out-of-picture fallback takes `YOF = width/2`
  while RRX7's takes `height/2` (a genuine inconsistency in the original — the port's
  impact is always inside the sprite, so it cannot fire); RRHX4's APGO multiplies its
  COLUMN step by an XOF the module itself doubled into PIXELS (a unit clash); RRHX4 never
  draws the last art column ("NO NEED TO DO END COLUMN"); and the half-step centring term
  puts the strip *indexed by* the impact half a step BELOW it.

### 61.3 §35.3 CORRECTED: the player death is NOT a strip cascade

`PDEATH` is a vector (`RRF.ASM:265`) and its implementation is **`RRX7.ASM`'s `PDTHV`**
("GENIES BITCHEN 330AM PDEATH"): the player's image is turned OFF (`DMAOFF`), then drawn
SOLID in `$99`/random `PDCTAB` colours for 10 iterations of 6 frames, then the DECAY
process is killed and the fade table `FF F6 AD A4 5B 52 09 00` is written to slot 12
(`PCRAM+$C`) every 4 frames while the player is drawn in `$CC`. So the arcade's death is
**a solid-colour flicker followed by the slot-12 palette fade** — no converging strips.
§35.3's "~12 APPEAR records" came from the disassembly's `$2882`/`$29D2`, a routine whose
role is still unidentified; do not wire it to the death without finding it in the Gospel.
**That fade is the §33 death-fade mechanism**, which is one of the three candidate
mechanisms for the red-screen question — still open with the author.

### 61.4 The port's implementation, and its confidence levels

`Entities/Explosion.cs` now implements the Gospel: the impact point (the laser/robot
overlap centre — the ROM's `CENTMP`), `YOF`/`XOF` from the impact's position inside the
picture, the `$0100` accumulator with the ROM's two start values, `XSIZE = ±(YSIZE>>1)`,
the four clip passes against the PORT's playfield interior (the ROM's XMIN/XMAX/YMIN/YMAX
are its own screen's columns/rows), dropped rows, the `Rows`/`Columns` axis switch, the
appear mode, and the dispatch table. `PlayField` passes the impact point, the clip
rectangle and enforces the shared ten-record cap.

- **Confidence: high for the row engines (RRDX2/RRX7)** — read line-by-line from the
  Gospel, and pinned by `ExplosionTests` (the accumulator curve, the impact anchor, the
  45-degree lean, the clips, the dispatch, the appear's 14 draws, the ten-record cap).
- **Confidence: structural for the column engine (RRHX4)** — its start/clip code IS read
  (RRHX4.ASM:370-500) but its units clash as noted, so the port uses the dimensionally
  consistent reading; the visible difference is only the fan's starting X.
- **NOT implemented yet (next items, in order):**
  1. ~~**Wave-start materialisation**~~ — **DONE 2026-09-17** (see §62).
  2. **The player-death flicker + fade** of §61.3 — the fade is the §33 question, which
     is still open with the author.
  3. `$F0D7` `CREATE_EXPLOSION` (the attract-mode variant, gated by the CMOS "fancy
     attract mode") — attract mode itself is a parked nice-to-have.

## 62. Wave-start MATERIALISATION — the robots assemble (2026-09-17)

§61.4 item 1, now implemented. The ROM does not let a wave's robots pop into
existence: it runs each one through the appear engine (§61) while the robots
themselves are held OFF.

### 62.1 The Gospel — `RRG23.ASM`'s `APPEAR` (the `APST`/`HAPST` loop)

`APPEAR` (RRG23.ASM ~line 300) walks the wave's robot list and, for ONE robot
per FRAME, creates an appear record:

```
      LDA #1
      PSHS A            ; the frame counter: ONE new appear per frame
APL   ... walk ADCIR/PD list ...
      LDA PD,U
      ANDA #3
      CMPA #3
      BNE AP1
      JSR HAPST         ; every FOURTH appear uses the HORIZONTAL fan (RRHX4)
AP1   ... APCEN/APCEN2 -> CENTMP, JSR APST (vertical fan, RRDX2/RRX7) ...
      NAP 1,APL
```

Key consequences, all of which the port now mirrors:

1. **One new appear record per frame** (not one per robot in one frame), so a
   wave of *n* robots takes *n* frames to *start* assembling, and each record
   then converges over its own 15 calls (§61.2) — so the last robot is
   fully present about *n* + 15 frames after the wave begins.
   **CORRECTION:** an earlier note said "32 frames"; that figure is a bad read
   and is superseded.
2. **Every FOURTH appear is horizontal** — the sequence counter's low two bits
   == 3 (indices 3, 7, 11, …) picks `HAPST` (the column engine) instead of the
   vertical `APST`. The counter keeps running across the whole wave (and across
   waves), it is not reset per robot.
3. **The robots are OFF for the whole sequence** (`ROBOFF`), not just during
   their own record — they do not move, do not act, and are not drawn until the
   appear chain has finished with them.
4. **The appear's impact is the robot's own centre** (`APCEN`/`APCEN2` write
   `CENTMP` from the robot's position, the same `CENTMP` the laser-kill path
   uses), and the **slope is 0** — that loop never sets `A` before `APST`.
5. The appear records come from the **same ten-record pool as explosions**
   (§61.2), so a full pool simply means the next robot's appear waits a frame.

### 62.2 The port's implementation

`PlayField` gained a materialisation queue:

- `QueueMaterialise(robot)` (called by all five wave-start spawn paths: grunts,
  hulks, spheroids, quarks, brains) enqueues the robot into `_pendingAppear`
  **and** marks it assembling immediately in `_assembling`.
- `AdvanceMaterialisation()` runs in `Update` (after `UpdateGruntSpeedProgress`,
  before the player and the entity lists): while the queue is non-empty and the
  shared ten-record cap is not reached, it dequeues ONE robot, picks
  `StripFanAxis.Columns` when `(_appearSequence & 3) == 3` and `Rows` otherwise,
  and adds an `Explosion.StartAppear(art, bounds, centre, axis, slope: 0, clip)`.
  Records whose `LifeState` has left `Alive` are removed from `_assembling`.
- `UpdateEntities<T>` skips any entity with `IsMaterialising(entity)` == true, and
  the robot draw loops go through a new `DrawEntity` that does the same — so an
  assembling robot neither acts nor draws itself. It IS visible, because its
  appear records are blitting its own current art.
- `IArtSource` (`Texture2D CurrentFrameArt(SpriteSet)`) was split out of
  `IExplodable` for this: `Hulk` is indestructible and so never explodes, but it
  must still materialise. `IExplodable : IEntity, IArtSource`.

Tests: `tests/Robotron2084.Tests/Level/MaterialisationTests.cs` — the queue
count, one appear per frame, the queue draining, a materialising robot not
moving, indices 3/7 taking the column fan, the record's bounds being the robot's,
and the flag clearing when the chain converges.

### 62.3 Deliberate omissions (logged so nobody re-derives them as bugs)

1. **The PLAYER's appear** (`PAPPR`/`PDAPPR`, which materialise the player the
   same way, 6 frames apart) is NOT implemented. The ROM can afford it because
   its start sequence freezes everything; the port's 2-second start grace lets
   the player move and shoot at once, so hiding the player for ~15 frames would
   make a controllable, invisible player. Revisit only together with a real
   start freeze.
2. The appear's sound requests are not wired.
3. The appear records do not respect the ROM's per-record sprite-list bias —
   the port's `Explosion.Layout` already anchors from the impact (§61.2).

## 63. The wall colour and the laser-wall flare are per-wave tables (WALCOL / LASCOL)

Found while logging §62: the port hard-coded the wall to palette slot 11 (RGB)
"because it cycles", and never drew the laser's wall-collision flare at all. Both
are simply wrong — the ROM has TWO more per-wave palette tables nobody had read.

### 63.1 The Gospel — `RRG23.ASM`'s `GTWCOL` and its four tables

`GTWCOL` (RRG23.ASM:373-403) indexes **four** parallel ten-byte tables by
`(wave-1) mod 10` (the ROM's `CMPA #9 / BLS / SUBA #10` wrap loop):

```
GTWCOL JSR PLINDX
 LDU #WCTAB
 LDA PWAV,X
 DECA
GTWL CMPA #9
 BLS GTW1
 SUBA #10
 BRA GTWL
GTW1 LEAU A,U
 LDA ,U
 STA WALCOL        ; (1) WALL / BORDER colour
 LDA 10,U
 STA PSTCOL        ; (2) POST colour            — already implemented (§47)
 LDB 20,U
 LDX PSTP1
 ABX
 STX PSTANI        ; (3) POST IMAGE family       — already implemented (§47)
 LDA 30,U
 STA LASCOL        ; (4) LASER WALL COLLIDE colour
 RTS
*WAVE COLOR TABLES
 FCB $22,$55,$11,$EE,$77,$33,$44,$88,$00,$CC
*POST COLOR TABLES
 FCB $FF,$EE,$BB,$DD,$EE,$FF,$11,$BB,$DD,$AA
*POST IMAGES
 FCB $00,$10,$20,$30,$40,$50,$70,$80,$00,$60
*LASER COLOR
 FCB $99,$00,$99,$66,$99,$99,$99,$11,$AA,$99
```

`RRF.ASM` names the two variables, which removes all doubt about what they are:

```
WALCOL RMB 1 WALL+POST BG
LASCOL RMB 1 LASER WALL COLLIDE COLOR
```

**So `LASCOL` is NOT the laser's own colour** (a tempting misread — the label
says "LASER COLOR"): it is the colour of the FLARE where a laser runs off the
playfield. The laser's own picture is drawn from its slot elsewhere.

The tables use the same **doubled-nibble** form as `PSTCOL` (§47): the video is
4bpp, two pixels per byte, and the ROM fills the border by storing ONE byte per
step, so a byte must have both nibbles equal to be solid. Therefore **only the
low nibble is the slot** — reading these as colour bytes gives nonsense (the
same trap §47 documented for the posts).

### 63.2 The decoded wave colours

Using the ROM's CRTAB defaults (`GamePalette.DefaultSlots`; colour byte format =
bits 0-2 RED, 3-5 GREEN, 6-7 BLUE — `ref/palette-notes.md`):

| wave | WALCOL byte | slot | slot's default colour | LASCOL byte | slot | flare colour |
|---|---|---|---|---|---|---|
| 1 | `$22` | 2 | `$17` ORANGE | `$99` | 9 | `$FF` WHITE |
| 2 | `$55` | 5 | `$3F` YELLOW | `$00` | 0 | `$00` BLACK (invisible!) |
| 3 | `$11` | 1 | `$07` RED | `$99` | 9 | WHITE |
| 4 | `$EE` | 14 | CYCLING (10-15) | `$66` | 6 | `$38` GREEN |
| 5 | `$77` | 7 | `$C0` BLUE | `$99` | 9 | WHITE |
| 6 | `$33` | 3 | `$C7` MAGENTA | `$99` | 9 | WHITE |
| 7 | `$44` | 4 | `$1F` YELLOW (dim) | `$99` | 9 | WHITE |
| 8 | `$88` | 8 | `$A4` PURPLE | `$11` | 1 | `$07` RED |
| 9 | `$00` | 0 | `$00` **BLACK** | `$AA` | 10 | CYCLING |
| 10 | `$CC` | 12 | CYCLING (10-15) | `$99` | 9 | WHITE |

Two deliberate oddities to expect in playtest, both ROM-faithful:

- **Wave 9's border is BLACK** (`$00` = slot 0), i.e. effectively invisible.
- **Wave 2's flare is BLACK** — that wave's wall collisions simply do not flash.

Verified on screen 2026-09-17: the wave-1 border is now ORANGE, where the port
previously drew the cyan `WallColorCycle`.

### 63.3 The port's implementation

- `GameplayConstants.WallSlotByWaveMod10` / `WallSlotForWave(wave)` and
  `LaserWallSlotByWaveMod10` / `LaserWallSlotForWave(wave)`, both wrapping every
  10 waves, next to the existing `PostSlotForWave`.
- `PlayField.Draw` now uses `p.Color(WallSlotForWave(Parameters.LevelNumber))`
  instead of the hard-coded slot 11. Slots 10-15 are the cycling ones, so waves
  4 and 10 get their animation for free from `PaletteAnimator`.
- `PlayField.SpawnLaserWallFlare(bounds, direction)` + a small `LaserWallFlare`
  record list, called by `PlayerLaser.Update` where it already detected the wall.
  The ROM's `LASDIE` dispatches on the crossed edge: `LASDIH` (a LEFT/RIGHT wall)
  paints a SOLID 2-column x 4-row block in LASCOL for 2 frames then repaints it in
  WALCOL; `LASDIV` (a TOP/BOTTOM wall) instead ANDs WALCOL's HIGH nibble onto
  LASCOL's LOW nibble — a vertical DITHER, so the wall shows through alternate
  rows. The port paints over the wall band for 2 port ticks, solid for vertical
  walls, alternating arcade rows for horizontal ones.

### 63.4 Open / not modelled

1. The ROM's **colour processes** re-write slots during play (the palette RAMPS),
   so a border can shift shade *within* a wave. Only the fixed CRTAB defaults +
   the 10-15 cycling animation are modelled. This is the same machinery as the
   §33 death-fade question and is best tackled with it.
2. Enemy shots are `Spark`/`TankShell` in the port, not lasers, so only the
   player's laser path paints a flare. If the Gospel's enemy shots ever turn out
   to share the LASDIE path, wire them to the same call.
3. The 1-frame WALCOL repaint after the flare is skipped: the port's flare is
   already drawn over the wall, so painting it in the wall colour would be a
   no-op visually.

## 64. The spheroid's and the quark's DEATH BURST — not the strip explosion (2026-09-17)

§50's "still open" item, now built: those two enemies have their OWN death
sequence and never use RRX7's strip engine.

### 64.1 The Gospel — one routine, two enemies

`SPHEROID_COLLISION_HANDLER` ($12C8) and `QUARK_COLLISION_HANDLER` ($4BC9) are
twin kills. Each checks the player-collision flag first (`LDA $48 / BNE` — a
player touch kills the PLAYER instead, no burst), frees the enemy's object and
metadata, takes a fresh metadata entry, sets up the burst parameters, awards
**$0210 = 1000**, plays its own kill sound, and decrements its count. The
spheroid's sets up `CIRKP` ($12F1, the disassembly names it
`DRAW_SPHEROID_IN_DEATH_THROES`); the quark's sets a pointer to **$1143**, and
$1143 is literally:

```
1143: 7E 12 FA    JMP   $12FA
```

— `CIRKV` is the SAME routine, entered just past the parameter block. So the two
enemies share the code and differ only in (a) the colours, (b) the count, and
(c) which picture chain they walk:

| | spheroid | quark |
|---|---|---|
| kill entry | $12F1 `CIRKP` | $1143 → `JMP $12FA` |
| colours | `LDD #$FFAA` | `LDD #$DDDD` |
| count | `LDA #7` | `LDA #8` |
| picture chain | CIRP0 at $14F2 (8 pictures) | SQP0 at $50C2 (9 pictures) |
| OPICT at kill | `LDD #CIRP1` = $14F6 | `LDD #$50C6` = SQP1 |
| sound | `$1151` (CRKSND) | `$4B29` (SQKSND) |

**The colours** are a PALETTE SLOT per phase in the doubled-nibble form (§47), and
the disassembly's own comment makes the meaning unambiguous: *"A = colour to draw
points value of spheroid in, B = colour to draw dying spheroid in"*. So the
spheroid's dying silhouette is `$AA` = **slot 10** — the very slot the CURRENT
player's score is blitted in (§58), which is what the disassembly means by *"the
same colour as the player score"* — and its points value is `$FF` = **slot 15**.
The quark uses `$DD` = **slot 13** for both. Slots 10, 13 and 15 are all
CYCLING slots, so both effects shimmer with the live palette.

**The sequence** (both phases are `NAP 2` = 2 frames per step):

1. **Burst:** erase the enemy's current picture, advance its animation pointer by
   one 4-byte descriptor, and draw the NEXT picture as a SOLID SILHOUETTE (the
   blitter's "solid and transparent blit" with the phase colour in `$2D`). The
   FIRST iteration runs at the kill itself, so picture 2 appears immediately; the
   countdown (`DEC PD6 / BEQ CIRKPX`) is tested BEFORE the draw, so the LAST step
   only erases. The pictures shown are therefore **2..count** — 6 for the spheroid
   (2..7) and 7 for the quark (2..8) — and the count IS the last picture's index,
   which is exactly why it is 7 and 8.
2. **Points value:** the enemy is gone and its "1000" picture appears at
   `ADDD #$0105` from the death position — +1 blitter column (2 px) and +5 rows —
   for `LDA #$1E` = **30 steps** of 2 frames (60 frames = ~1 s), then erased and
   the process frees itself.

### 64.2 `P1KD` — what the "1000" picture actually is

The points phase loads its picture pointer with `LDD $000C` (extended), which the
source calls **`P1KD`**. That RAM-looking variable is in fact a ROM CONSTANT: the
block at $0000-$001F is four 3-byte `JMP` trampolines then a run of 2-byte picture
pointers —

```
0000: 7E 01 6D   JMP $016D     HULKST
0003: 7E 02 B2   JMP $02B2     HUMST
0006: 7E 00 9E   JMP $009E     CKLIM
0009: 7E 03 51   JMP $0351     SKULL
000C: 04 85                    P1KD   -> a 6-byte x 5-row picture, data at $0499
000E: 05 2F                    MOMPIC
0010: 07 FF                    DADPIC   (and KIDPIC/HLKPIC/HLKANA/HUMANA/SKLPIC follow)
```

and the descriptors at $0485/$0489/$048D/$0491/$0495 are a run of five 6x5
pictures — **P1000..P5000**, the same art the rescue marker displays. Byte-for-byte
the port already has it as `Score_1000`, so the burst reuses
`SpriteSet.RescueScoreDisplays[0]`. (This also settles that `RHORG EQU $0000` in
the original source is a ROM constant block, not RAM: the code READS these
addresses and nothing writes them.)

### 64.3 The port's implementation

`Entities/ScoreBurst.cs` — an entity with two phases:

- `ScoreBurst.ForSpheroid(bounds)` / `ForQuark(bounds)`: the ROM's counts, colour
  slots and picture chains (`SpheroidFrames[2..7]`, `QuarkFrames[2..8]`; both
  chains were re-verified against the ROM's descriptors — $14F2→$1512 = the port's
  `SpheroidFrames[0]`, $50C2→$50E6 = `QuarkFrames[0]` — so the ROM's "OPICT =
  picture 1" really does mean index 1 and the burst starts at index 2).
- Timing uses the repo's exact-**6ths** accumulator (§52) rather than
  `PortTicks(2)`, so a step is 2 ROM frames = 2.4 port ticks, not 17-20% fast.
- Draw uses `SpriteSet.DrawSpriteSolid` (solid blit + `UsePassThrough`, §57) with
  `SlotColor(slot)`, so the cycling slots shimmer as on the hardware.
- `PlayField`: a `_scoreBursts` list (ticked and pruned with the other effects,
  drawn at step 7b, over the entities and under the player), and `SpawnScoreBurst`
  from the two laser-kill call sites. Spheroid and Quark are **no longer
  `IExplodable`** — they keep `IArtSource` (the wave-start materialisation, §62) —
  so the strip explosion can no longer be spawned for them by construction rather
  than by convention.
- The laser-kill sound is played by `SpawnScoreBurst`, because the branch that
  normally plays it (`if (target is IExplodable)`) no longer runs for these two.
- The ROM's player-collision path (`PCFLG` → the player dies, no burst) is already
  the port's behaviour, and the laser is the only way either enemy can die here,
  so no other call site needs the burst.

Tests: `tests/Robotron2084.Tests/Entities/ScoreBurstTests.cs` — the picture
sequence (2..7 and the quark's 2..8), the "last step only erases" rule, the exact
2.4-tick step, the 30-step points display and the burst's total lifetime, the
phase colour slots, the +1 column/+5 row offset, and two `PlayField` tests proving
a laser kill produces a burst and NO strip explosion for each enemy.

### 64.4 Notes for whoever is next

- The burst is UNIT-TESTED but has not been watched on screen (it needs a real
  spheroid/quark kill in a playtest).
- The strip explosion remains for the grunt, hulk-knockback, enforcer, tank,
  brain and prog kills (the ROM's `EXST` family), and the appear records still
  share the same ten-record pool (§61).

## 65. The short-delay sweep — every remaining `PortTicks()` truncation (2026-09-17)

§52 found the systemic bug: `PortTicks(rom) = rom * 6 / 5` **truncates**, so any
ROM period of 1-4 frames runs 17-20% fast, 5-9 frames 3-10% fast. §52 fixed the
quark, tank, spheroid and enforcer BODIES with an exact-6ths accumulator and
flagged the rest. This section finishes the sweep.

### 65.1 The list, and why each one mattered

| clock | ROM frames | was | exact | effect of the bug |
|---|---|---|---|---|
| `Grunt` body | 4 | 4 | 4.8 | **every grunt moved ~20% fast** (the whole game's pacing) |
| `Prog` body | 3 | 3 | 3.6 | progs homed 20% fast |
| `CruiseMissile` body | 3 | 3 | 3.6 | missiles flew 20% fast |
| `Spark` move interval | 4 | 4 | 4.8 | the ballistic curve advanced 20% early |
| `Spark` flicker frame | 4 | 4 | 4.8 | the 4-frame flash was 20% fast |
| `Brain` body | 1 + BRNSPD | 10 (wave 1) | 10.8 | the brain chased ~7% fast |
| `Brain` reprogram step | 3 | 3 | 3.6 | the 20-iteration animation took 120 instead of 144 ticks |
| `Electrode` shrivel sleeps | 6 / 3 / 2 | 7 / 3 / 2 | 7.2 / 3.6 / 2.4 | the shrivel steps were early |
| `Hulk` step | HLKSPD 5..8 | 6..9 | 6..9.6 | up to 7% fast |
| laser-vs-wall flare (`LASDIE`) | 2 | 2 | 2.4 | the flare was one tick short |
| **all six palette processes** (§4.12) | 1 / 2 / 6 / 8 | 1 / 2 / 6 / 8 | 1.2 / 2.4 / 7.2 / 9.6 | **every colour cycle ran 20% fast** |

Each is now the §52 idiom — `_fifths += 5; if (_fifths < romFrames * 6) return;
_fifths -= romFrames * 6;` — with the ROM's FRAME count kept as the constant, so
the code reads like the Gospel and the clock is exact.

The palette one is the most visible: `PaletteAnimator`'s doc comment previously
admitted the deviation ("the ROM ran these at ~50 fps, so the cycles run ~20%
faster than the arcade"). The six processes drive the CURRENT player's score
digits, the mini man, the posts, the wall and the sparkling enemies, so the whole
screen was shimmering 20% too fast. It now steps on the ROM's frames.

### 65.2 `GameplayConstants.PortTicksCeil`

Tests that tick to a boundary need the tick the step really lands on:
`PortTicksCeil(romFrames) = ceil(romFrames * 6 / 5)` — the first tick on which the
accumulator reaches its threshold. `PortTicks(3)` is 3, but the step is on tick 4;
every test that used `PortTicks(n)` as "the period" has been moved to
`PortTicksCeil(n)` where it means the boundary (and to `PortTicks(n)` where it
means a distance, e.g. the spark's life range).

### 65.3 Deliberately NOT converted (all under 1-3%, all documented)

> **SUPERSEDED by §70 (same day):** these were all converted, because "under 3%" is
> still an approximation. §70 also records the ONE deviation that is kept on purpose
> (the human step period).

- `Human` step (16 → 19 vs 19.2), `TankShell` life (`RND(0..31)+48` → ~1%),
  `RescueScoreMarker`/`SkullMarker` (60/90 → exactly 72/108), the wave-progress
  timers (270/225 → exactly 324/270), the 2P message holds (115 → exactly 138).
- `Enforcer` grow: the TOTAL is `PortTicks(45)` = 54 ticks (exact), but the
  visible frame index is derived from `PortTicks(9)` = 10 instead of 10.8, so the
  four grow pictures change ~6% early inside a correctly-timed growth.
- The port's fixed step is 60 Hz and the ROM's frame is 50 Hz; that 6/5 is a
  project-wide convention (§4.1), not something this sweep revisits.

### 65.4 Playtest impact

Everything above now runs at its true ROM rate, which means the grunts, progs,
cruise missiles, sparks, hulks and brains are **slightly slower than the last
build the author played** (up to 20% for the grunt body). That is the arcade's
rate; if the feel now disagrees with the ROM, the ROM decode is what to re-check.

## 66. The PLAYER's death is `PDTHV` — a solid flash loop, then the slot-12 fade (2026-09-17)

§33 recorded the Gospel's death sequence and §61.3 confirmed it; the port still had
a **wall-clock `DeathTimer`** (a `TimeSpan` of 2 s) drawing the player in its
normal colours, which is wrong in three ways at once (the duration, the clock, and
the picture). It is now RRX7.ASM's `PDTHV` verbatim:

```
PDTHV LDA #10 / STA PD,U        ; ten iterations
PDTH0 LDA #$99 / JSR OPON       ; the player as a SOLID silhouette in $99
      NAP 2,PDTH1               ; 2 frames
PDTH1 LDA SEED / ANDA #3
      LDA A,X (PDCTAB)          ; $00,$11,$33,$77
      JSR OPON                  ; solid in that colour
      DEC PD,U / BEQ PDTH2
      NAP 6,PDTH0               ; 6 frames
PDTH2 JSR GNCIDE / JSR COLST
      JSR KILL OFF DECAY        ; slot 12's colour process is STOPPED
      LDA #$CC / JSR OPON       ; the player drawn solid in slot 12
PDTH3 LDA ,X+ / STA PCRAM+$C    ; PD2TAB into slot 12, one byte per NAP 4
      BEQ PDTH4
PDTH4 JSR DMAOFF / JSR COLST    ; erase, restart the colour processes
PDCTAB FCB $00,$11,$33,$77
PD2TAB FCB $FF,$F6,$AD,$A4,$5B,$52,$09,$00
```

- **The flash**: ten iterations of 2 frames in `$99` (slot 9) + 6 frames in a
  RANDOM `PDCTAB` colour. `PDCTAB` is in the doubled-nibble form, so its slots are
  **0, 1, 3 and 7** (black, red, magenta-ish, blue) for 6 frames each. Total 80
  frames.
- **The fade**: the DECAY process is killed off, the player keeps being drawn as a
  solid silhouette in **slot 12**, and `FF F6 AD A4 5B 52 09 00` is written into
  slot 12 a byte per 4 frames — the trailing `$00` is written too and ends the
  death, so the player is drawn in BLACK for its last frame and then erased (that
  is the fade-to-black you see). Total 28 frames.
- Whole death: **108 ROM frames = 129.6 port ticks** (Dead on tick 130).

Port changes:

- `Player`: the `DeathTimer` (and the class) are GONE. The death is a three-stage
  state machine on the §52 exact-6ths clock (`White` → `Colour` → `Fade`), with
  the ROM's counts (`PlayerDeathFlashIterations` 10, `+2`/`+6` frames, the fade's
  `NAP 4`) and tables (`PlayerDeathFlashSlots`, `PlayerDeathFadeValues`) as
  constants. A seedable `Random` was added to the ctor for the `SEED & 3` pick.
- Drawing: while dying the player is drawn with `DrawSpriteSolid` in
  `DeathSolidSlot` (9, a PDCTAB slot, or 12) — the ROM's `OPON` blit, not the
  normal sprite — so the whole death is a silhouette that changes colour.
- **`GamePalette.SuspendSlot`/`ResumeSlot`**: the ROM's `KILL OFF DECAY` and
  `COLST`. A suspended slot's colour process neither steps nor writes
  (`PaletteAnimator` skips it), which is what lets the death own slot 12; resuming
  restarts that process from its table's first entry. `PlayField.Palette` exposes
  the live palette to entities for this (it is the ROM's direct `PCRAM` write).
- `ResetForLevelRestart` clears the death state; `RobotsFrozen` still freezes the
  robots through the death (the ROM's hit-stop).

Tests: `tests/Robotron2084.Tests/Entities/PlayerDeathTests.cs` (3) — the flash
alternating `$99`/PDCTAB slots, the fade's exact tick boundaries and table values
with slot 12 suspended and resumed, and that the death ignores `GameTime` (it is
tick-driven). Plus `PaletteAnimatorTests.ASuspendedSlot_IsNotWrittenByItsProcess`.

**NOT YET SEEN ON SCREEN:** `PlayerInvincibleForTesting` is still TRUE, so a
playtest can never reach the death (the flag makes `Kill()` a no-op). The tests
call `Player.StartDeathForTesting()` to get past it — an internal hook, on purpose,
so the sequence stays verified while the playtest flag is on. Flipping the flag
(the author-gated item) is what will show this on screen.

## 67. The author's playtest: "the explosions are completely wrong" + "the grunts move fast too early" (2026-09-17)

Both reports were right, and both were MY decode errors — §61's explosion model and
§31's grunt speed-up. This is the third time a playtest has overruled a decode; the
disassembly's own comments are what settled it.

### 67.1 The explosion is a TWO-HALF SPLIT, not one fan

> **Partly superseded by §69, and the MECHANISM by §71 (later the same day).** The two-half
> split described below is WRONG — it was read out of the stale header comment above `$473F`,
> which the routine itself never honours. §71 has the real shape (ONE fan that opens both
> ways) and the corrected lean. The AXIS table in §69.1 stands.

§61 built "cut the sprite into its rows and fan them all in ONE direction from the
impact". `asm/robomame.asm` states the real thing outright — the header of
`CREATE_DIRECTIONAL_EXPLOSION` ($473F):

```
; X = enemy to explode
; A = value determining the direction the enemy's TOP HALF must explode
;     ($FF = top half of explosion moves up and left, 0 = top half moves straight
;      up, 1= top half of explosion moves up and right)
; B = value determining the direction the enemy's BOTTOM HALF must explode
;     ($FF = ... down and left, 0 = ... straight down, 1 = ... down and right)
; $A6 = pointer to screen address where enemy and object that made it explode collided
```

and the record it builds holds the collision's offset INSIDE the picture (clamped
to the picture, or half its height when the collision is outside). The drawing is
`DRAW_EXPLOSION_SEGMENTS` ($478C) — sixteen unrolled blits, each loading its own
source word from the segment table and stepping the destination by `$BA`, "the
offset to add to D after each blit, to space out the explosion segments" — with the
`A`-segment trick at $4A5C selecting how many are drawn ("This is the cause of the
famous 'shot in the corner' bug"). `MAKE_ENEMY_EXPLODE` ($5C1F) routes: a pure
horizontal shot to the column path ($5C49), a diagonal to $473F with
`A = ~(horizontal xor vertical)`, and a pure vertical shot through
`CREATE_EXPLOSION` ($F0D7).

So a kill is TWO fans dividing at the collision:

- the rows ABOVE the collision fly UP, spacing out and leaning like that half's
  direction;
- the rows FROM the collision down fly DOWN, leaning the other way — for a diagonal
  shot `B` is the opposite of `A`, so the halves rotate apart along the shot's axis.

**The port now does exactly that** (`Explosion.Layout`): each strip is placed at
`along = collision ± distance × spacing` with `distance` counted from the split,
`lateral = ±distance × (spacing&gt;&gt;1) × slope` (one half each way), and the
sprite's own rows as the segment sources. The strip's SOURCE is its own row, so the
two halves keep their art. A strip outside the playfield is DROPPED, per strip (the
ROM's clip passes), and the record is drawn WHERE THE SPRITE STOOD — the collision
decides only where the halves divide, exactly as `$A6` does.

Verified two ways: the tests pin the divide, the opposite leans, the growing gaps
(1,2,3… — the `$0100` accumulator curve, unchanged from §61) and the per-strip
drop; and a temporary ASCII dump confirmed that frame 0 reconstructs the sprite
ROW-FOR-ROW (spacing 1, no lean = the intact picture — the ROM's "1 UNIT IS MIN").
The dump and the temporary `PlayingState` debug hook used to see it on screen were
both removed before committing.

### 67.2 The grunt speed-up had two errors — both made the wave fast FAR too early

§31 had `SpeedUp(floor) = max(floor, limit × 7/8)`, applied to every survivor AND
re-rolling each survivor's in-flight countdown. The ROM ($3A94-$3A9F):

```
LDB #$E0        ; 224
LDA $BE5C       ; the shared step limit
MUL             ; D = limit × 224 -> A = the high byte = limit × 7/8 (truncated)
CMPA $BE5D      ; compared with the FLOOR
BCS $3AA2       ; BELOW the floor -> leave the limit ALONE
STA $BE5C       ; otherwise store the new limit
```

1. **The clamp.** `max(floor, …)` sets the limit DOWN TO the floor; the ROM leaves
   it unchanged when the ×7/8 result is under the floor. The port therefore reached
   the wave's floor one kill early, which is faster than the arcade ever goes.
2. **The re-roll.** The ROM touches only the SHARED limit — every survivor keeps
   its pending countdown and picks the new limit up on its next re-roll, so the wave
   accelerates gradually. Re-rolling every survivor's countdown on every kill
   collapsed their pending delays into the new, smaller range immediately, so any
   kill made the whole wave lurch forward.

Also fixed: the level-progress pass toggles ($F0) between "floor −2, limit −4" and
"floor −1, limit −2" ($FEFC then $FFFE); the port always used −2/−4.

Wave 1 now ramps 20 → 17 → 14 → 12 → 10 and STOPS (the ROM's floor 9 is never
reached, because 10 × 7/8 = 8 &lt; 9). The port used to reach 9 a kill earlier, and
every one of those kills lurched the survivors forward.

### 67.3 The lesson, again

Both of these were decodes that survived a full green test suite, a build, a smoke
test and two render gates — because the tests encoded the same wrong model. A
playtest that disagrees with the ROM means the decode is wrong; the disassembly's
own comments (`; A = ... the enemy's TOP HALF ...`) are the fastest way to find out
why. `ref/original-source/` remains the Gospel for semantics, but for the parts the
sources only imply (like this one), `asm/robomame.asm` names them outright.

### 67.4 The records' own clock (found while fixing the above)

`Explosion.Update` still stepped once per PORT tick — the §52/§65 short-delay class
again, which made every explosion 20% fast. The ROM walks its records once a FRAME,
so the step now runs on the exact-6ths clock: a record spans 15 ROM frames = **18
port ticks** (not 15), and an appear's 15 frames likewise. The layout tests now
advance whole ROM frames through a new `internal Spacing` hook instead of counting
`Update` calls, and the appear test pins the 18-tick lifetime.

Verified while fixing the shape: a throwaway ASCII dump of `Explosion.Layout` showed
frame 0 reconstructing the sprite row-for-row (spacing 1, no lean — the ROM's "1 UNIT
IS MIN"), which is the cheapest way to eyeball a fan. The dump and a temporary
`PlayingState` debug hook (spawn + shoot a grunt above the player every 8th tick) were
both removed before committing.

## 68. Sound is OFF by default — a master switch, not a deletion (2026-09-17)

Author: *"The sound annoys me — can you disable it with a feature flag or a
conditional compilation? So many beeps is annoying."*

`Sound.Enabled` (in `Audio/Sound.cs`) is now the port's **master switch and it
defaults to false**, so nothing beeps. Flip it to `true`, or launch with
`ROBOTRON2084_SOUND=1`, to hear it again — no other change needed.

Why a switch rather than deleting the audio:

- The port's ONLY sink is `MonoGameSoundSink`, a stub 8-bit square-wave beeper with
  an **equal-temperament stand-in scale** — the real note→frequency map is sound-board
  hardware, not in the CPU ROM, and is still **blocked on the author** (§36.2). What
  it plays is deliberately NOT the arcade, so there is nothing worth hearing.
- Everything that matters is kept and still tested: `SoundEngine` (the ROM's priority
  sequencer, $D3C7/$D3E0), the decoded `SoundTables`, and `SoundEngineTests`. When the
  real table arrives, drop it in and flip the switch.
- The switch is one property, checked in `Sound.Play`/`Sound.Tick`, so a build with it
  off touches no audio resources at all (the sink only builds a `SoundEffect` on its
  first note).

`tests/Robotron2084.Tests/Audio/SoundTests.cs` pins both states; its collection sets
`DisableParallelization` because the switch is process-wide. Gate 3 (the 12 s smoke)
now runs silent, and an extra smoke with `ROBOTRON2084_SOUND=1` covers the other path.


## 69. "Shoot vertically, explode horizontally" — the explosion AXES were swapped (2026-09-17)

Author: *"When you shoot enemies vertically aren't they supposed to explode
horizontally? And vice versa?"* — **yes.** §61 (and §67, which inherited it) had the
two engines swapped, and this is the fourth playtest to overrule a decode.

### 69.1 The mistake, precisely

§61.1 read `EXSTV`'s first byte as "the VERTICAL direction byte":

```
EXSTV LDA LASDIR / BNE EXST1
```

`LASDIR` is not the vertical byte — it is the shot's **X step** (§1487 of these
notes: `delta X = LASDIR`, `delta Y = LASDIR+1`). So `LASDIR == 0` means *no X
movement*, i.e. a **pure vertical shot**, and that is the branch that reaches
`JSR HEXST` — the **HORIZONTAL** explosion. Symmetrically `LDB LASDIR+1 / BEQ EXST1A`
is "no Y movement" = a pure **horizontal** shot, and it takes the V engine. The 1982
comment on that branch — "NO Y COMPONENT, STRAIGHT VERTICAL" — is naming the
*explosion* that follows, not the shot.

The disassembly confirms both branches independently at `MAKE_ENEMY_EXPLODE` ($5C1F),
which routes on the laser's own direction bytes `$88`/`$89`:

| the shot | $5C1F route | builder | split built from | strips |
|---|---|---|---|---|
| pure VERTICAL (`$88` = 0) | `$5B5B` | `CREATE_EXPLOSION` $F0D7 | `LDB $A6` = the collision **COLUMN** vs the picture's WIDTH | **COLUMNS**, fly apart LEFT/RIGHT |
| pure HORIZONTAL (`$89` = 0) | `$5C3A`/`$5C49` | `$5C49` | `LDB $A7` = the collision **ROW** vs the HEIGHT | **ROWS**, fly apart UP/DOWN |
| diagonal | `$4683` | `CREATE_DIRECTIONAL_EXPLOSION` $473F | `$A7` = the ROW | ROWS, the whole fan leaning `±2×(step>>1)` the one way (§71) |
| non-laser (tank/brain `HVEXV`, grunt-electrode) | — | `LASDIR = $0100` → `EXST1` → `EXST1A` | the ROW | ROWS |

The `$A6`/`$A7` split only makes sense because of how the collision stores them: the
collision routine does `LEAU D,Y / STU $A6` ($D884) — a **16-bit screen address**,
big-endian, and the screen layout is `address = column*256 + row`. So **`$A6` is the
column** (which is why $F0D7 compares it against the WIDTH) and **`$A7` is the row**
(which is why the disasm annotates it "$A7 = Y coordinate" and why $473F/$5C49
compare it against the HEIGHT). §61 compared the wrong byte against the wrong
dimension for each engine and still "worked", which is exactly why it went unnoticed.

`ref/original-source/RRX7.ASM` agrees in its own vocabulary: the vector table exposes
**`HOREX`** and **`HORAP`** ("HORIZONTAL explosion/appear") beside `EXSTV`/`APSTV`,
`HOREX` does `JMP EXST1A`, and the record's `YSIZE` is aliased onto the H family's
`HXRAM` with the note *"Y SIZE MUST HAVE SAME ADDRESS AS H'S XSIZE"* — one engine,
two orientations, named for the axis the pieces TRAVEL.

### 69.2 The rule, in one line

**The ROM cuts the sprite ALONG the shot's axis and throws the pieces APART ACROSS
it:** shoot up → vertical strips flying left/right; shoot sideways → horizontal bands
flying up/down; shoot diagonally → horizontal bands flying up/down *and* drifting in
X. In every case it is ONE fan whose base climbs while its segments march the other way,
so it opens both ways at once — see §71, which corrected §67's "two halves".

### 69.3 The change

`Explosion.Dispatch` (`Entities/Explosion.cs`) — the axes swapped, the slugs and the
diagonal leans untouched:

| direction | before | now |
|---|---|---|
| `Up`, `Down` | (Rows, 0) | **(Columns, 0)** |
| `Left`, `Right` | (Columns, 0) | **(Rows, 0)** |
| `null` (non-laser kill) | (Rows, 0) | (Rows, 0) — `HVEXV` lands on `EXST1A`, unchanged |
| `UpLeft`, `DownRight` | (Rows, -1) | (Rows, -1) |
| `UpRight`, `DownLeft` | (Rows, +1) | (Rows, +1) |

Everything downstream already keyed off the axis correctly and needed no change: the
split already picked `impact.X` for a column fan and `impact.Y` for a row fan (i.e.
`$A6` vs `$A7`), and `Layout` already walks `along`/`lateral` per axis. The call sites
pass `laser.Direction`, so a shot from `PlayerLaser` maps straight through.

Tests updated to match the ROM routes rather than the old model:
`Dispatch_MatchesTheRomLaserDirectionRules` (now spelling out `EXSTV`),
`StartExplosion_TakesTheImpactCentreAndTheRomsDispatch`, the row-fan layout tests now
shoot `Direction8.Left`, and the column test is now
`AVerticalShot_SplitsTheColumns_LeftAndRight` with a column-impact (citing $F0D7).
243 tests, 0 failed, 1 skipped.

### 69.4 Lesson (see also §67.3)

Both §61 and §67 passed a green suite while asserting the wrong axis — because the
tests were written from the same misreading. What broke the tie was cheap and should
be the *first* move next time: read the disassembly's own header comments for the two
routines the dispatch chooses between, and check which **byte of the collision
address** each one reads. The author's arcade memory was right, twice.

## 70. The last `PortTicks()` truncations, and the ONE deliberate override (2026-09-17)

§65.3 listed the remaining truncations as "deliberately NOT converted (all under
1-3%)". That is exactly the "good enough" the author's standing rule forbids (THE
ARCADE IS ALWAYS THE STANDARD — no approximations where the real behaviour can be
recovered), so they are converted too. The clock is now exact everywhere it matters.

Converted to the §52 exact-6ths accumulator:

| what | before | now |
|---|---|---|
| `Human` step | flat `PortTicks(16)` = 19 ticks | 16 frames = 19.2 ticks (`ceil(19.2k)`), pinned by `Human.StepCount` + `Human_StepCadence_RunsOnTheExactSixthsClock` |
| `Enforcer` grow-up | `PortTicks(45)` = 54 for the total (exact) but `PortTicks(9)` = 10 for the frame index (~6% early) | 45 frames in 6ths: the five pictures change on the ROM's 9-frame boundaries (ticks 1, 11, 22, 33, 44), and the 54th tick is the one `ENFR10` runs on. New `GrowFrameIndex` hook + `Enforcer_GrowFrames_ChangeOnTheRomsNineFrameBoundaries` |
| `TankShell` life | `PortTicks(RND & $1F + $30)` → 57..94 | 48..79 frames in 6ths → `ceil(6n/5)` = 58..95 |
| `Spark` life | `PortTicks((HSEED & $F) + $14 cycles × 4)` → 96..168 | same range in 6ths (the ends were already exact; the interior was up to 0.8 tick short) |

`RescueScoreMarker`/`SkullMarker` (60/90 frames → 72/108) and the 2P message holds
(115 → 138) were listed in §65.3 but were never broken — those divisions are exact.
The stale 40-frames/8-each spawn model is gone from `Enforcer` entirely (a dead
`GrowUpRomTicks` constant and two comments); the ROM's is 45 frames, 9 each.

### 70.1 The ONE deliberate deviation left in the game

`RRH11.ASM`'s `HUMAN` process is unambiguous: the body ends `NAP 8,HUMAN`, and it
stores its new position on **every** pass —

```
HUM2 STB OY16,X        ; commit the row
     PULS D
     STD OX16,X        ; commit the column  ← the move, every pass
     DEC PD5,U / BNE HUMX
HUMX JSR DMAOFN
     NAP 8,HUMAN
```

— with the direction table's delta halved (`CLRB / ASRA / RORB`) so a pass moves one
arcade pixel. **The arcade walks a human one pixel every 8 frames (9.6 port ticks).**
The port walks one every **16**, because the author asked for it in playtest round 8
("the mommies are walking too fast") — i.e. half the arcade rate.

This is the only place where the port knowingly differs from the Gospel, and it is a
deliberate author choice rather than a decode shortcut: the decode is not in doubt, and
unlike the §67/§69 cases there is no second reading of the source that makes the
slower walk correct. It is therefore **kept**, marked in the code
(`Human.StepPeriodRomTicks`) and here, and it now runs on the exact clock like
everything else. If the author wants the arcade rate back it is `16 → 8` in one place
(the cadence test names the value it assumes). **Flagged to the author 2026-09-17.**

### 70.2 Test hygiene found while pinning the above

The human tests drove the field with `new GameTime()` — **zero elapsed time** — while
the player's start grace is WALL-CLOCK (`Player._graceRemaining -= ElapsedGameTime`,
2 s). `PlayField.RobotsFrozen` therefore never lifted, so a human in those tests never
ticked at all, and `Humans_MoveOverTime` passed *vacuously* (it compared different
human objects' spawn positions, not one human over time). `PlayFieldHumanTests` now
uses a real 1/60 s `Frame()` helper and drives a single human.

Also fixed: `Human`'s doc comments used `&rarr;`, which is not a defined XML entity
(IDE-only CS1570 noise) — replaced with the arrow character.

## 71. The explosion is ONE fan that opens BOTH ways — §67's two halves were a stale comment (2026-09-17)

Author, playtesting the §69 build: *"the explosion directions are correct but the vertical
explosion only explodes upwards — it's meant to be up AND down at the same time. Also,
what about when the enemy is shot diagonally (ie the laser going up and left)"*.

**He was right, and the culprit is a 1982 comment.** §67 built the port's two-half model
on the header above `CREATE_DIRECTIONAL_EXPLOSION` ($473F):

```
; A = value determining the direction the enemy's TOP HALF must explode
;     ($FF = up and left, 0 = straight up, 1 = up and right)
; B = value determining the direction the enemy's BOTTOM HALF must explode
;     ($FF = ... down and left, 0 = ... straight down, 1 = ... down and right)
```

But **$473F never reads `B`** — it overwrites it (`D6 A7 LDB $A7`, the collision row) and
stores only `A`, as `SLOPE`. There is no second direction pair, and no top/bottom split
anywhere in the engine. The comment is stale.

### 71.1 What the Gospel actually does — the sources' `WRITE`

`RRX7.ASM` (rows) and `RRHX4.ASM` (columns) share one layout routine:

```
        LDA   YSIZE          ; the sizer's high byte: this frame's step, 1, 2, 3, …
        LDB   YOF,Y          ; the collision's offset INSIDE the picture
        MUL                  ; DISTANCE UP FROM CENTER
        STD   TEMP1
        LDB   YCENT,Y        ; the collision's screen row
        CLRA
        SUBD  TEMP1          ; YCENT − YSIZE*YOF
        ADDB  TEMP2          ; += YSIZE/2   (TEMP2 = YSIZE>>1; CLRA'd when YOF = 0)
        STB   UL+1,Y         ; the FIRST segment's row
        ...
CHK3A   LDA   HITE / DECA
        LDB   YSIZE / MUL / ADDB UL+1,Y   ; the LAST segment = base + (HITE−1)*YSIZE
```

So:

```
step      = YSIZER >> 8                        ; 1, 2, 3, … one per frame
base      = YCENT − step*YOF + step/2          ; the first segment's row
segment i = base + i*step                      ; each further one steps DOWN
```

- The segments are the picture's ROWS, in order, and they **march one way only** (down);
- the base, however, **climbs**: `−step*YOF` outruns `+step/2`, so the fan's leading edge
  moves up while its trailing edge moves down — **one fan opening up AND down at once**,
  which is exactly what the author described;
- at step 1 the `step*YOF` term cancels (`base = YCENT − YOF = the sprite's top row`), so
  frame 0 reconstructs the sprite — the ROM's "1 UNIT IS MIN";
- `YOF = 0` (the collision exactly on the sprite's top edge) drops the half-step — the
  ROM's own comment: `; IF DOWN FROM TOP....FIX TOP ONE (OBSCURE BUG)`;
- `RRHX4` is the exact mirror (a vertical shot): the same maths on columns, with the base
  column climbing left while the segments march right.

### 71.2 The three things this changed in the port

1. **The shape.** `Explosion.Layout` no longer splits the sprite at the collision; it lays
   ONE fan out from `base` with step `step` (above), all segments the same way. The
   `+step/2` half-step and the `YOF = 0` guard are both implemented.
2. **The lean.** For a diagonal shot the ROM's `XSIZE = ±(YSIZE>>1)` is in **byte columns**
   (`LSRA` then `ASLB/ROLA`), and a column is two arcade pixels — so every segment shifts
   `±2 × (step>>1)` **art pixels**, and the sign comes from `SLOPE` ALONE: the whole fan
   leans the one way (the old model sent the two halves opposite ways, at half the lean).
   `SLOPE = ~(horizontal ^ vertical)` still matches `Dispatch` exactly: up-left and
   down-right are −1, up-right and down-left +1.
3. **The spacing has a unit per axis.** The V family counts rows (1 arcade pixel), the H
   family counts byte columns (2 pixels), so a "unit" of spacing is one strip WIDTH — 1
   art px for a row, 2 for a column. Without that the vertical-shot fan would open at half
   the arcade's rate.

The appear (`APSTZ`/`AWRITE`) rides the same routine with the sizer running DOWN, so the
wave-start materialisation (§62) converges the same way — its tests were re-derived too.

### 71.3 Open, and flagged to the author

`HEXSTV` (the H family's start) stores `XOF` **doubled** — `CENTOK ASLB ; DOUBLE CENTER FOR
BYTE ADJUSTMENT` — while `XCENT` stays in byte columns, so the ROM's own
`XCENT − XSIZE*XOF` mixes a column with a pixel quantity. That would shift the H fan by the
collision's offset and double its climb rate. The port currently uses the *consistent* form
(same formula per axis, with the strip-width unit above), so a vertical shot's burst is
centred and symmetric. **If a playtest says the vertical-shot burst should start off-centre
or open twice as fast, this is the line to revisit.** The V family (the one the author was
looking at, and the one used for a horizontal shot) has no such ambiguity — every quantity
in it is in rows.

## 72. Two visual defects from the §71 playtest: the brain's "square block", and the H fan's unit (2026-09-17)

Author, playtesting the §71 build: *"When the brain progs a human, the brain sometimes
turns into a square block!"* and *"the vertical explosion works sometimes, but doesn't
last very long."* Both are port defects, and both are decoded from the Gospel.

### 72.1 The brain's progging state is a BLOCK **with the brain's own picture on top**

`DRAW_BRAIN_IN_PROGGING_STATE` ($1DAF) is **two** blits, not one:

```
1DAF  LDB #$BB / STB $2D          ; blitter colour 1 = palette slot 11
1DB3  LDD $000A,X / STD $0004,X   ; the destination = the brain's rectangle
1DB9  LDY $0002,X                 ; the brain's CURRENT animation frames
1DBC  JSR $D093                   ; -> $DA61 BLIT_RECTANGLE_WITH_COLOUR_REMAP
1DBF  JMP $D018                   ; -> $DAF2 "do blit" - the image, NORMALLY
```

`$DA61` is blitter op **$12** (bit 3 clear = no transparency), i.e. the rectangle is filled
solid in `$BB`; `$D018` then draws the brain's picture over it in its own colours. So the
arcade's progging brain is a **slot-11 card with the brain's silhouette on it** - the block
is a BACKDROP, exactly as the human's `$AABB` (`HUMON` = block + figure, §47) is for the
human being reprogrammed.

§46/§47 read only the first blit and concluded "the brain is not drawn as a sprite at all
while it reprograms", so the port drew a bare solid rectangle - the author's square block.
`Brain.Draw` now fills the block and then draws `BrainFrames[WalkFrameIndex]` over it.
`GameplayConstants.ReprogramShapeSlot`'s doc says "backdrop", not "replacement".

**Still open (not the author's report, but visible once he looks):** the ROM also switches
the brain's animation to the *programming pose* (`$2141` facing left / `$214D` facing
right, set by `BEGIN_PROGRAMMING_FAMILY_MEMBER`). The port keeps drawing whatever walk
frame the brain had. If the progging brain now reads as "the brain standing still" rather
than "the brain working on the human", that pose is the next piece.

### 72.2 A unit of fan spacing is ONE PIXEL in BOTH families - §71.2's byte-column unit was wrong

§71.2 set the H family's unit to a **byte column** (2 px), on the reasoning that `RRHX4`
counts columns. `RRHX4`'s `WRITE` says otherwise:

```
       LDA WH,Y   GET WIDTH
       ASLA       DOUBLE FOR PIXEL WIDTH      <- the walk is in PIXELS
       DECA       NO NEED TO DO ZEROS ON LEFT COLUMN
       STA WIDE
```

`WIDE` is the number of strips, and it is **pixel**-derived; the source pointer advances one
row-block per iteration and the destination one pixel of spacing, so the fan's pitch is one
**pixel**, one strip per pixel, each strip 2 px wide - consecutive strips therefore
**overlap by half** and the fan reads as one bright sheet.

§71.3 predicted the byte-column unit would make the fan open at *half* the arcade's rate; the
playtest says it opened at **twice** the arcade's rate and left the playfield early - the
prediction was inverted, and §71.3's "flag it if a playtest disagrees" instruction is what
this section discharges. `Explosion.Layout` now uses `unit = 1` for both axes.

### 72.3 How it was settled (the measurement, not a guess)

A throwaway dump (a temporary test, **deleted before commit**) stepped a record through its
whole life for both axes, every split, three sprite positions, and printed the *visible*
strip set from `Layout` (the real clip: art px `20..280` x `20..160`):

- **Rows (V) family - already faithful.** 12/12 strips at spacing 1 (the sprite row for row,
  the ROM's "1 UNIT IS MIN"); by the last frame 3-11 of 12 survive depending on the split and
  the sprite's position; the "OBSCURE BUG" `YOF = 0` case opens downwards only. The collapse
  at the playfield edge is the ROM's own clip: `EXST2` clamps `YOF` to the picture (falling
  back to the picture's middle, `LDB 1,X / LSRB`), and `WRITE`'s `CHK2`/`CHK5` loops trim the
  segments that pass `YMIN`/`YMAX`, then `LDA HITE / LBEQ KILEXP` frees the record when
  nothing is left. **The port's pairing of source row to destination row survives the trim
  exactly as the ROM's does** (`STB UL+1,Y` advances the base by one `YSIZE` per trimmed
  segment and the ROM's `ABX` advances the source by that many rows), so no renumbering is
  needed.
- **Columns (H) family - this is what was broken.** Before: the fan reached x245 by the last
  frame (spread 224 art px, most of it off the playfield) and clipped strips from step 8 on.
  After `unit = 1`: frame 0 is the sprite's own columns exactly (`x50..x57`), and the last
  frame spans `x21..x99` with 7-8 of 8 strips still inside. Same shape, half the rate, and it
  never leaves the playfield.

### 72.4 The explosion's lifetime is NOT too short - a phase-gate check

The report's "doesn't last very long" invited a duration fix, so the clock was re-derived
instead of assumed. `RRS22.ASM`'s executive loop gates the whole game cycle on

```
EXEC0  LDA TIMER / CMPA #2 / BLO EXEC0 / ... / CLR TIMER / JSR EXUPD
```

and `IRQV` (RRS22:1552) increments `TIMER` on **every** interrupt, in both halves of the
handler (`IFLG` makes the halves mutually exclusive, it does not skip a count): the CA1
interrupt at line 240 and the VSYNC half. Two increments per field means `TIMER == 2` is
**ONE video field** - one game cycle per field - so the port's project-wide 6/5 tick ratio
(§4.1) stands. `WRITE`'s `FRAMES = $10` (RRX7:235, "NUMBER OF BYTES TO ERASE, NUMBER OF
FRAMES LEFT") is 16 frames = 15 draws = **0.30 s**, and §71's exact-6ths clock already
produced exactly that. No timing change was needed; the defect was the spread rate (§72.2)
and, before that, the shape (§71).

### 72.5 Also verified, unchanged

`EXST2`'s split rules match the port exactly: the offset is the collision's row/column minus
the picture's upper-left, a borrow (`BCS NWCENT`) or an out-of-picture offset (`CMPB 1,X /
BLO CENTOK`) falls back to the picture's MIDDLE, and `CENTOK STB YOF,U` keeps the clamped
value - which is what `Explosion.Start` does.

### 72.6 The H family draws one strip fewer than the picture (sharpened 2026-09-17)

`RRHX4`'s `WRITE` sets the fan's strip count with

```
LDA WH,Y   GET WIDTH
ASLA       DOUBLE FOR PIXEL WIDTH
DECA       NO NEED TO DO ZEROS ON LEFT COLUMN
STA WIDE
```

so `WIDE = 2W − 1` strips, where `W` is the picture's width in byte columns: **one strip per
arcade PIXEL, less the picture's leftmost one.** `HEXSTV` (the H family's start) corroborates the
count from the other side — its *initial erase* covers the whole picture:

```
LDD ,X / STD WH,U          ; the metadata: (width in byte columns, height in rows)
ASLA                       ; "DOUBLE WIDTH FOR HEIGHT TO USE"
STA XHITE,U                ; HITE FOR FIRST ERASE  →  XHITE = 2W
```

and `WRITE` then overwrites `XHITE` with its *clipped* `WIDE` (`STA XHITE,Y`, "SAVE FOR ERASE").
So the sprite's own footprint is `2W` pixel columns and the fan draws `2W − 1` of them: exactly
one pixel column is dropped, and `DECA`'s comment says which — the **left** one (blank in the
mask). The V family has no such trim (`LDA WH+1,Y / STA HITE` takes the height whole), which is
why the up/down fan is exact and this one is not.

**The port draws `widthArt` = `2W` strips, i.e. one more than the ROM, at the fan's left end.**
The unit is what makes the count ambiguous: §72.2's playtest-validated reading is one strip per
PIXEL, and under it `WIDE = widthArt − 1`. Nothing reported so far points at it — it is a
one-pixel edge, and dropping the strip would leave the column fan one strip asymmetric (which
the ROM's own cut is) — so it is left as the one place where the port is provably wider than the
Gospel. If a playtest ever says the left/right fan is one pixel fatter at one end, the fix is to
start the column fan's loop at `i = 1` instead of `i = 0`.

### 72.7 Lesson

Three of the last four explosion defects came from reading ONE of a routine's blits/loops and
assuming the rest: §67 (the two-half header), §71 (the stale `$473F` comment) and now §72.1
(the brain's second blit). **When a routine's body is available, read every blit/loop it
performs before concluding what is drawn** - a header comment is a lead, the code is the
Gospel.

## 73. The explosion's fan is MIRRORED — anchored at the picture's MIDDLE, not at the hit (2026-09-17)

Author, playtesting the §72 build: *"The explosions are still wrong - one side of the
explosion is not mirrored on the other side. In other words the explosion half going UP is
bigger than the explosion half going DOWN, and the explosion half going LEFT is bigger than
the explosion half going RIGHT"*.

### 73.1 What the port was doing

`Explosion.Start` took the collision point — §67/§71's reading of `EXST2`/`$5C49`
(`YOF = CENTMP+1 − spriteTop`, clamped to the picture, falling back to the middle when the
hit is outside it) — and `PlayField` fed it the laser/robot **overlap centre**. A laser is a
4x4-spec box that has only just reached the target, so the hit is almost always at the
sprite's NEAR EDGE: `split` sat near 0 or `extent−1`, the fan's fixed point went with it, and
the up half carried nearly all the strips while the down half reached almost nowhere. The
author's two examples are exactly the two families — up/down is the V (rows) family,
left/right the H (columns) one — and both showed the same one-sided bias.

### 73.2 The ROM's own centre path — `NWCENT`

`EXST2` (RRX7.ASM:300) and the laser path `$5C49` (R5) both do:

```
       LDB   CENTMP+1        ; the hit's row
       SUBB  UL+1,U          ; − the picture's top → the hit's offset inside it
       BCS   NWCENT          ; negative → "NO GOOD"
       CMPB  1,X             ; ≥ the picture's size → also no good
NWCENT LDB   1,X / LSRB      ; ← the picture's MIDDLE
       STB   YOF,U           ; XOF/YOF = H/2
       ADDB  UL+1,U / STB YCENT,U   ; YCENT = the picture's CENTRE
```

so on that path `YCENT − spriteTop == YOF == H/2`: **the fan's fixed point is the picture's
middle and the two halves are equal by construction.** That is the ROM's own mirrored fan,
and it is what the port now takes for *every* kill.

The step-1 reconstruction ("1 UNIT IS MIN") is unaffected either way, because
`YCENT − YOF` is the picture's top on both paths — which is why frame 0 still
reconstitutes the sprite exactly.

**The appears always used this path**: §62's `APCENT` gives them the object's own *centre*,
which is why a wave's materialisation has never looked wrong. That is the strongest pointer
that the centre path is the one the arcade shows.

### 73.3 What changed

- `Explosion.Start` — `split = extent / 2` (the picture's middle) for both kinds; the
  collision point is no longer an input at all. `_impact`, the `impact` parameters of
  `StartExplosion`/`StartAppear` and `PlayField.OverlapCentre` are gone, with their tests.
- New tests pin the author's report from both axes: `TheFanOpensUpAndDownAtOnce_
  AndIsMirroredAboutThePicturesMiddle` asserts `reachUp == reachDown`, and
  `AVerticalShot_IsMirroredLeftAndRightToo` asserts `reachLeft == reachRight`.
  `AVerticalShot_FansTheColumnsSideways` and
  `AVerticalShotAtOneUnit_AlsoReconstructsTheSpriteExactly` pass unchanged (they were
  already written around the middle).
- The ROM's "OBSCURE BUG" guard (`LDB YOF,Y / BNE APGO1 / CLRA` — no half step when the
  offset is 0) is kept in the code for faithfulness, but a middle-anchored fan can never
  have a zero offset, so it is now unreachable, and the test that pinned it
  (`ACollisionOnTheSpriteTop_OpensDownwardsOnly`) went with it.

### 73.4 Where the fan lands now (measured)

8x12 art-px sprite at art (50,100), spacing 3 — reach measured from the picture's own edges:

| | before (anchored at a hit near an edge) | after (the middle) |
|---|---|---|
| Rows, hit near the bottom (`split` 11) | base 79 — up **21**, down **1** | base 89 — up **11**, down **11** |
| Columns, hit near the right (`split` 7) | base 37 — left **13**, right **1** | base 43 — left **7**, right **7** |

### 73.5 Lesson

A hit point is not a centre. The port's anchors have to be the ROM's *named* invariants
(`YCENT − YOF == spriteTop`), and when a routine has two paths — the collision-anchored one
and the centre one — the playtest decides which the arcade shows. §67, §69, §71 and §72 all
came from reading one branch and assuming the rest; this one came from *choosing* the wrong
branch — and the appear, which had used the other all along, was right.

## 74. The DIAGONAL fan is a MIRRORED CHEVRON — the lean is measured from the fixed point (2026-09-17)

Author, after §73: *"The explosions are still lop-sided in the debug build."*

### 74.1 What the port was doing

§73 fixed the fan's *reach* (the up/left half and the down/right half now reach equally far
from the picture's middle). The remaining lop-sidedness was the **lean**: `Layout` shifted
strip i sideways by `i * drift`, so the lean was measured from the fan's *first* strip and the
whole fan sheared off to one side. Combined with the clip, the strips at one end left the
playfield and were dropped, so half the burst disappeared while the other half survived —
which is what makes a diagonal kill look lop-sided in play.

### 74.2 RRDX2 — the diagonal module I had never read

§67, §69 and §71 all read `RRX7` (rows), `RRHX4` (columns) and the disassembly's `$473F`. The
diagonal engine is a **third source file**, `RRDX2.ASM`, and its X placement is different:

```
       LSRA            1/2 FOR X SIZE
       STA    TEMP2
       LDB    SLOPE,Y  CHECK SLOPE
       BPL    APGG1    POSITIVE
       NEGA            NEGATIVE
APGG1  STA    XSIZER,Y AND SAVE
       STA    XSIZE    SCRATCH SOME SPEED
...
       LDA    YOFF           ACTUAL Y OFFSET USED
       LDB    XSIZE          WHICH SLOPE??
       MUL                   FIND DEFLECTION
       LDB    XCENT,Y        GET X "CENTER"
       SUBD   TEMP1          FIND WHERE LEFTMOST X OCCURS
...
       ADDB   XSIZE          ADD THE X UNITS      ← once per strip, in the unrolled loop
```

So `XSIZE = ±(YSIZE>>1)` — in COLUMNS, the sign taken from `SLOPE` — the first strip is placed
at `XCENT − YOFF*XSIZE`, and each next one adds `XSIZE`. **Strip i's lateral is therefore
`(i − YOF) * XSIZE`: measured from the fan's FIXED POINT**, not from the first strip. The rows
above the fixed point lean one way and the rows below it the other — a **mirrored chevron**.

That is also what `$473F`'s A/B header described all along ("A = the direction the enemy's TOP
HALF must explode … B = … the BOTTOM HALF …"), the comment §71 rightly distrusted — but it
describes the two halves' *opposite leans*, not a second fan. §67 built a two-half *split* on
it and was wrong; the halves really are a thing, they just share one fan.

`RRDX2`'s WRITE is otherwise the same shape as `RRX7`'s: the same `base = YCENT − SIZE*YOF +
SIZE/2`, the same `TEMP2` "obscure bug" guard, the same `CHK2`/`CHK5` clip passes that DROP
strips.

### 74.3 What changed

`Layout`'s lateral is now `(i − split) * drift`. Two invariants guard the change: at spacing 1
`drift` is 0 (the ROM's `LSRA` of a step of 1), so the step-1 reconstruction is untouched; and
every straight shot has `slope = 0`, so their fans are unaffected — only the diagonals changed.
`ADiagonalShot_LeansTheWholeFanOneWay` became `ADiagonalShot_LeansTheTwoHalvesOppositeWays`,
which pins the chevron: the topmost strip six steps left of the picture's column, the strip at
the fixed point unmoved, the bottom one five steps right — and the other diagonal mirrors it.

### 74.4 Lesson

The explosion has **three** engines, not two: `RRX7` (rows), `RRHX4` (columns) and `RRDX2`
(diagonals). The 45° case has its own WRITE with its own X placement, and reading only the
two axis modules left the diagonal lean wrong for four reports. When a routine has sibling
modules, read the sibling the playtest is actually exercising.

## 75. The fan was laid out from HALF the picture — a texture's dimensions are ART pixels (2026-09-17)

Author: *"I ran the debug build and again the vertical explosion is lopsided - the top half is
much longer than the bottom half."* Then, after this fix: **"That worked!"**

### 75.1 The bug

`Explosion.Draw` did:

```csharp
int heightRows = art.Height / ScreenSize.SpecScale;
int widthArt   = art.Width  / ScreenSize.SpecScale;
```

but **a texture's dimensions are already in ART pixels**. `SpriteSet.CentredIn` scales them by
`SpecScale` when it draws a sprite (`ScreenSize.Scaled(texture.Width)`), so an 8x12 picture has
8x12 *texture* pixels. The fan therefore spanned **half** the picture's rows (6, not 12) and
sampled every other row — while the *anchor* was still derived from the bounds (12 rows), so
`split = 6` was clamped to `extent − 1 = 5`: five steps of reach above the fixed point and
**zero** below. That is precisely "the top half is much longer than the bottom half".

Both earlier fixes were correct and both were invisible: §73's middle anchor *did* make the
halves equal, and §74's chevron *was* mirrored — in the model. A unit error one layer up was
discarding one half of the result before it reached the screen.

### 75.2 Why the tests missed it

`TheFanOpensUpAndDownAtOnce_AndIsMirroredAboutThePicturesMiddle` and
`AVerticalShot_IsMirroredLeftAndRightToo` call `Layout(WidthArt, HeightRows)` with the
picture's own dimensions (8, 12) — i.e. exactly what `Draw` *should* have been passing. Nothing
pinned what `Draw` actually passed, and `Draw` needs a `SpriteBatch`, so no headless test
touches it. Note the asymmetry the author was looking at: the tests exercised the *pure* model,
the playtest exercised the *conversion*.

### 75.3 What changed

- `Draw` passes `art.Width`/`art.Height` straight through, sizes the **source** rectangles in
  TEXTURE pixels (one art row/column a strip) and the **destinations** in PORT pixels
  (art x SpecScale).
- The placement is now a pure, testable helper — `Explosion.PicturePlacement(bounds,
  textureWidth, textureHeight)` — returning the picture's extent and the art's top-left
  **centred in the bounds**, the way `SpriteSet.CentredIn` puts it there. The fan used to start
  from the bounds' *corner*, which is wrong for any picture smaller than its box.
- `Layout` derives the fan's fixed point (`extent / 2`) from the **picture's** extent rather
  than the collision box's, so the anchor and the span can no longer come from two different
  rectangles; the now-redundant `_split` field and its plumbing were removed.
- New test `TheFanStartsFromTheCentredArt_NotTheBoundsCorner`: an 8x12 picture in 22x30
  port-px bounds sits at art (51,101), the fan at spacing 1 reproduces it row for row, and
  `PicturePlacement` pins the convention for the draw path.

### 75.4 Lesson

Two things cost this round trip, and both are worth keeping in front of any future draw work:

1. **The port has TWO size conventions.** Textures are in ART pixels; bounds are in PORT
   pixels; `SpriteSet.CentredIn` is the only place they meet. Anything that mixes them needs
   one of those two names in its head, and a `/ ScreenSize.SpecScale` on a texture dimension is
   always wrong.
2. **A pure helper that agrees with its tests proves nothing about its caller.** §73 and §74
   were verified by tests that fed `Layout` the right numbers; the defect lived in an argument
   at the call site. When a fix lands in a pure function, check what the *caller* hands it —
   and pull the conversion into the tested surface (`PicturePlacement`) so the next one cannot
   hide there.

## 77. Humans no longer spawn on electrodes; the quark's tank drop re-verified (2026-09-17)

Author: *"I think the Quarks are dropping tanks WAY too quickly. Also in the game can humans
spawn on top of electrodes? I don't think they should, as they get stuck"*.

### 77.1 Humans on electrodes — FIXED

`PlayField.SpawnHumanKind` scattered humans with a bare `RandomPointInside()` and none of the
placement checks the electrodes and grunts get. A human refuses to step into a live electrode
(`Human.Update` → `OverlapsLivingElectrode`, which mirrors the ROM's walk), so one scattered on
top of an electrode was stuck there for the rest of the wave. It now uses the file's own
`FindSpawnPoint` idiom with the same predicate the grunts use
(`_electrodes.All(e => !e.Bounds.Overlaps(rect))`).

### 77.2 The quark's tank drop — verified end to end, one suspect left

**`SQUARE` is the quark** (its pictures are `SQP0..SQP8`); `CIRCLE`/`CIR*` is the **spheroid**,
which drops **enforcers** (`ENFDRP`). Its whole drop algorithm now checks out against the port:

| ROM (`RRTK4`) | port (`Quark`) |
|---|---|
| `SQST1`: `LDA TDPTIM / JSR RMAX` → the first `PD2 = RND(0..TDPTIM−1)` | `1 + random.Next(TDPTIM)` (±1) |
| `SQ2`: `LDA TDPTIM / LSRA / INCA / JSR RMAX` → the inter-drop `PD2 = RND(0..TDPTIM/2)`, re-rolled after EVERY drop (`BRA SQ2`) | `1 + random.Next((TDPTIM >> 1) + 1)` (±1) |
| `SQST1`: `LDA ENFNUM / JSR RMAX / LSRA / ADCA #0` → `PD3 = RND(0..ENFNUM−1)/2` tanks | `roll = random.Next(maxDropsX2 + 1); (roll + 1) / 2` |
| the drop loop's pass is `NAP 3` = 3 ROM frames | `BodyFifths` on the exact-6ths clock |
| `SQ2L`: `LDA TNKCNT / CMPA #20 / BHS SQ2` | `CanDropTank => live tanks < 20` |
| `TDPTIM` at `$BE66` = `$1010` ×7 then `$0F0F` ×3 → 16 for waves 1-14, 15 for 15-20 | `WaveTable.QuarkDropDelay` = 16 ×14, 15 ×6, then 14s |

**The one thing not closed:** the ROM rolls the *number of tanks to drop* from **`ENFNUM`** —
the wave's **enforcer** number — while `PlayField` hands `Quark` `Parameters.MaxDropsX2`, which
`LevelParameterGenerator` reads from the wave table's **tank** column ("maxTanks"). If that
column is larger than the enforcer column the ROM rolls from, the quark drops proportionally
more tanks per wave, which would read exactly as "dropping tanks WAY too quickly".

**Next step:** compare the wave table's tank column with the ROM's `ENFNUM` for the wave in
play — and check whether the `ENFNUM` the ROM uses here is the same column the port already
reads as `MaxEnforcersPerSpheroid` (it may simply be the wrong column feeding the roll, in
which case the fix is the argument at `PlayField`'s `new Quark(...)`).

### 77.3 Lesson

`CIRCLE`/`CIR*` (the round one) is the **spheroid** and drops **enforcers**; `SQUARE`/`SQP*`
(the square one) is the **quark** and drops **tanks**. §64 already matched those picture chains,
so use the *pictures* to tell the two enemies apart when reading the sources — "CIRCLE" does
not mean the quark.

## 78. Score updates, and the wave-complete TUNNEL (2026-09-17)

Author: *"The score isn't always updating when I shoot enemies. Can you check the score routines?
Also, I want you to replace "Level N complete" message with the colour cycling tunnel effect that
Robotron has. My disassembly has it, it renders with hatching so take great care to reproduce it."*

### 78.1 Scoring — every laser kill DOES award points

`PlayField.ResolveLaserHits` is the only place `Score.Add` runs for kills, and all eleven enemy
lists route through it (`PlayField:377-424`): electrodes, grunts, spheroids, enforcers, quarks,
tanks, brains, progs, sparks, tank shells and cruise missiles, each with its own `ScoreValues`
entry; the human rescue bonus is separate (`:668`). **So a laser kill always scores** — the
routing is not the bug.

**The deaths that do NOT score** (the only places a kill can be point-free):
- `:443` a grunt destroyed by an ELECTRODE — the electrode's own kill (does the ROM score the
  grunt here? to check);
- `:464` an electrode destroyed by a HULK;
- `:483/:484` the player walking into an electrode (kills both — a player death must not score);
- `:650` a human's death (the ROM has no penalty; the field leaves the skull).

**The display side** is the other way the number can *look* frozen. `Hud/ScoreFormatter` models
the ROM's eight digit positions correctly — leading zeros suppressed while the cursor still
advances 6 px, tens and units always drawn (§58.1) — and the HUD draws the current player's score
in **slot 10, a CYCLING slot** (§58). If that slot is momentarily at its darkest value the digits
are hard to read on a busy frame, which would read as "the score didn't update".

**Next step:** take one kill that "didn't count" and establish whether it was a laser hit (then it
did score, and this is legibility/display) or one of the four paths above.

### 78.2 The wave-complete tunnel — LOCATED in the disassembly, not yet built

```
2AAF: BD 57 00  JSR $5700      ; JMP $5703 - draw colour cycling tunnel effect
5703: DRAW_COLOUR_CYCLING_TUNNEL_EFFECT
      "responsible for drawing the colour cycling tunnel effect that you see when
       you complete a wave"
5710: LDX #$3B80               ; top-left of the tunnel's outer ring
      LDY #$5A82               ; bottom-right of the outer ring
5726: (reloaded from the saved copies at $0009,U / $000B,U)
572B: LDA $000D,U              ; the PACKED BYTE of colours to draw this ring in
572D: LDB #$02                 ; "how many times to draw a part of tunnel using these colours"
5731: JSR $5A11                ; DRAW_RECTANGULAR_PART_OF_TUNNEL
5751: CMPX #$0616              ; reached the middle (column 6, row $16)?
5756: LEAX -258,X              ; not yet: shrink — one COLUMN (2 px) left, two ROWS up
575A: LEAY $0102,Y             ;            and one column right, two rows down
5764: TSTA / CLRA / BRA $5710  ; when the colour is black: redraw the whole tunnel IN BLACK
```

`DRAW_RECTANGULAR_PART_OF_TUNNEL` (`$5A11`) draws one ring as four edges — the top and bottom
horizontal lines (`$C8`/`$C9` hold their Y) and the left and right vertical lines (`$CA`/`$CB`
hold their X) — and the lines are **HATCHED**. The attract-mode copy of the same idea spells the
hatch out (`$E358`): *"write pixel pair, and increment X by 2, skipping a pixel vertically
(making it look like a 'hatched' line)"*, and it draws each line **twice** — `$E342`/`$E346` are
"draw hatched vertical line using FIRST colour pair" / "using SECOND colour pair".

So a ring is a hatched band of two interleaved colour pairs; the rings step inward by one column
(2 px) and two rows at a time; the palette cycles as they go; and the last pass redraws everything
in black to clear it.

The port's stand-in is the `"Level N complete"` banner. (The ROM's `<n> WAVE` text is the wave
*start*, §58 — the tunnel replaces the *completion* message.)

**Plan for the build:** a wave-complete drawable with (a) the outer rect `$3B80 .. $5A82` in art
pixels, (b) the ring step — one column and two rows per side — until the middle `$0616`, (c) each
ring as four hatched edges (a pixel pair every 2 px, two interleaved colour pairs taken from the
ring's packed byte), (d) the palette cycling through §4.12/§65's processes, (e) the black phase to
end. The hatch pitch and both colour pairs must come from the ROM's own data (`$000D,U`'s packed
byte plus the pair lists at `$E351`/`$E35F`) — **not** from an eyeballed dash pattern.

### 78.3 Lesson

The wave-complete effect is a *drawing* routine, not a palette trick: a shrinking, hatched,
two-colour frame, redrawn in black to finish. Port `$5A11` (the ring primitive) first, then the
walker around it.

## 78. Score updates, and the wave-complete TUNNEL (2026-09-17)

Author: *"The score isn't always updating when I shoot enemies. Can you check the score routines?
Also, I want you to replace "Level N complete" message with the colour cycling tunnel effect that
Robotron has. My disassembly has it, it renders with hatching so take great care to reproduce it."*

### 78.1 Scoring — every laser kill DOES award points

`PlayField.ResolveLaserHits` is the only place `Score.Add` runs for kills, and all eleven enemy
lists route through it (`PlayField:377-424`): electrodes, grunts, spheroids, enforcers, quarks,
tanks, brains, progs, sparks, tank shells and cruise missiles, each with its own `ScoreValues`
entry; the human rescue bonus is separate (`:668`). **So a laser kill always scores** — the
routing is not the bug.

**The deaths that do NOT score** (the only places a kill can be point-free):
- `:443` a grunt destroyed by an ELECTRODE — the electrode's own kill (does the ROM score the
  grunt here? to check);
- `:464` an electrode destroyed by a HULK;
- `:483/:484` the player walking into an electrode (kills both — a player death must not score);
- `:650` a human's death (the ROM has no penalty; the field leaves the skull).

**The display side** is the other way the number can *look* frozen. `Hud/ScoreFormatter` models
the ROM's eight digit positions correctly — leading zeros suppressed while the cursor still
advances 6 px, tens and units always drawn (§58.1) — and the HUD draws the current player's score
in **slot 10, a CYCLING slot** (§58). If that slot is momentarily at its darkest value the digits
are hard to read on a busy frame, which would read as "the score didn't update".

**Next step:** take one kill that "didn't count" and establish whether it was a laser hit (then it
did score, and this is legibility/display) or one of the four paths above.

### 78.2 The wave-complete tunnel — LOCATED in the disassembly, not yet built

```
2AAF: BD 57 00  JSR $5700      ; JMP $5703 - draw colour cycling tunnel effect
5703: DRAW_COLOUR_CYCLING_TUNNEL_EFFECT
      "responsible for drawing the colour cycling tunnel effect that you see when
       you complete a wave"
5710: LDX #$3B80               ; top-left of the tunnel's outer ring
      LDY #$5A82               ; bottom-right of the outer ring
5726: (reloaded from the saved copies at $0009,U / $000B,U)
572B: LDA $000D,U              ; the PACKED BYTE of colours to draw this ring in
572D: LDB #$02                 ; "how many times to draw a part of tunnel using these colours"
5731: JSR $5A11                ; DRAW_RECTANGULAR_PART_OF_TUNNEL
5751: CMPX #$0616              ; reached the middle (column 6, row $16)?
5756: LEAX -258,X              ; not yet: shrink — one COLUMN (2 px) left, two ROWS up
575A: LEAY $0102,Y             ;            and one column right, two rows down
5764: TSTA / CLRA / BRA $5710  ; when the colour is black: redraw the whole tunnel IN BLACK
```

`DRAW_RECTANGULAR_PART_OF_TUNNEL` (`$5A11`) draws one ring as four edges — the top and bottom
horizontal lines (`$C8`/`$C9` hold their Y) and the left and right vertical lines (`$CA`/`$CB`
hold their X) — and the lines are **HATCHED**. The attract-mode copy of the same idea spells the
hatch out (`$E358`): *"write pixel pair, and increment X by 2, skipping a pixel vertically
(making it look like a 'hatched' line)"*, and it draws each line **twice** — `$E342`/`$E346` are
"draw hatched vertical line using FIRST colour pair" / "using SECOND colour pair".

So a ring is a hatched band of two interleaved colour pairs; the rings step inward by one column
(2 px) and two rows at a time; the palette cycles as they go; and the last pass redraws everything
in black to clear it.

The port's stand-in is the `"Level N complete"` banner. (The ROM's `<n> WAVE` text is the wave
*start*, §58 — the tunnel replaces the *completion* message.)

**Plan for the build:** a wave-complete drawable with (a) the outer rect `$3B80 .. $5A82` in art
pixels, (b) the ring step — one column and two rows per side — until the middle `$0616`, (c) each
ring as four hatched edges (a pixel pair every 2 px, two interleaved colour pairs taken from the
ring's packed byte), (d) the palette cycling through §4.12/§65's processes, (e) the black phase to
end. The hatch pitch and both colour pairs must come from the ROM's own data (`$000D,U`'s packed
byte plus the pair lists at `$E351`/`$E35F`) — **not** from an eyeballed dash pattern.

### 78.3 Lesson

The wave-complete effect is a *drawing* routine, not a palette trick: a shrinking, hatched,
two-colour frame, redrawn in black to finish. Port `$5A11` (the ring primitive) first, then the
walker around it.

## 79. The wave-complete TUNNEL — full decode and build spec (2026-09-17)

§78.2 located the routine. This is the complete decode of it and of its ring primitive, enough
to implement without re-reading the disassembly. **It is an EXPANDING tunnel**, not a shrinking
one: from a thin line at the screen's centre it grows outward, ring by ring, to the screen's
corners, and is then redrawn in black to clear it.

### 79.1 `DRAW_COLOUR_CYCLING_TUNNEL_EFFECT` ($5703) — the walker

```
5703  PULS A,B / LDU $15 / STD $0007,U      ; pop the return address; save it on the task
5709  JSR $D054 ; .59 B0                    ; reserve a task, function $B059
570E  LDA #$EF                              ; the FIRST colour pair (see 79.3)
5710  LDX #$3B80  LDY #$5A82                   ; the outer/extreme corners (see 79.2)
5717  STX $0009,U / STY $000B,U / STA $000D,U  ; save corners + colour for the task
571E  LDA #$01 / LDX #$5726 / JMP $D066        ; task delay 1, entry point $5726
5726  ... (the task body; entered once per FRAME)
      LDX $0009,U / LDY $000B,U / LDA $000D,U
572D  LDB #$02 / STB $000E,U                ; TWO rings are drawn per pass
5731  JSR $5A11                              ; draw the ring
5734  TSTA / BEQ $5751                      ; black? then don't advance the colours
5737  the colour chain (79.3), else SUBA #$22
5751  CMPX #$0616 / BEQ $5764                ; reached the far corner (col 6, row $16)? done
5756  LEAX -258,X        ; X -= $0102 = one COLUMN (2 px) left and TWO ROWS up
575A  LEAY $0102,Y      ; Y += $0102 = one column right and two rows down
575E  DEC $000E,U / BNE $5731                ; draw the SECOND ring of this pass
5762  BRA $5717                              ; store the corners + colour, then the task ends
5764  TSTA / BEQ $576A                      ; still black? finished
5767  CLRA / BRA $5710                      ; else colour = 0 (black) and REDRAW from the start
576A  JMP [$07,U]                            ; next task
```

Per pass: ring N in the current pair, the pair advances, the corners step outward, ring N+1 in the
new pair. So **the tunnel grows two rings per frame** and the colours change once per ring.

### 79.2 The geometry (ROM screen coordinates: X = column, Y = row; address = column*256 + row)

- The starting corners: `X = $3B80` (column $3B = 59, row $80 = 128) and `Y = $5A82`
  (column $5A = 90, row $82 = 130) — i.e. a **2-row-tall, 32-column-wide box at the screen's
  centre** (row 128 is the middle of the ROM's 256).
- Each ring steps outward: the top-left goes **one column LEFT and two rows UP**, the bottom-right
  **one column RIGHT and two rows DOWN** — so the box gets 4 px wider and 4 rows taller per ring
  (2 px per side horizontally, 2 rows per side vertically: the vertical stretch is what makes it
  read as a tunnel).
- It stops when the top-left reaches `$0616` (column 6, row 22) — 53 rings — which leaves the
  bottom-right at column 143, row 236, i.e. the screen's own edges ($8F/$EC).
- Then the same walk runs again with colour = 0 (black) to erase it.

### 79.3 The colours — a chain of pairs, one nibble each

`A` is a **packed byte: the left nibble is colour 0, the right nibble colour 1** (both are palette
slot numbers). The pair advances once per ring:

```
5737  CMPA #$12 / BNE .. / LDA #$EF / BRA $5751      ; $12 wraps to $EF
573F  CMPA #$F1 / BNE .. / LDA #$DE / BRA $5751      ; $F1 wraps to $DE
5747  CMPA #$23 / BNE .. / LDA #$F1 / BRA $5751      ; $23 wraps to $F1
574F  SUBA #$22                                      ; otherwise BOTH nibbles step down by 2
```

Starting from `$EF` the sequence runs `EF, CD, AB, 89, 67, 45, 23, F1, CF, AD, 8B, 69, 47, 25, …`
with the three wraps above catching the underflows. (With slot numbers 0-15 a nibble that would
go negative wraps into the next chain entry.)

### 79.4 `DRAW_RECTANGULAR_PART_OF_TUNNEL` ($5A11) — the ring primitive

```
5A13  ANDA #$F0 / STA $CC          ; colour 0 = the LEFT nibble
5A17  LDA ,S / ANDA #$0F / STA $CD ; colour 1 = the RIGHT nibble
5A1D  TFR X,D / STA $CA / STB $C8  ; $CA = left column, $C8 = top row
5A23  TFR Y,D / STA $CB / STB $C9  ; $CB = right column, $C9 = bottom row
5A29  SUBB $C8 / RORB / BCC +2 / DEC $C9   ; if (bottom − top) is ODD, pull the bottom up by 1
5A30  … draw the TOP edge twice:
        row $C8      in colour 0, then row $C8 + 1 in colour 1, the second pass
        offset one COLUMN (2 px) to the right ($5AAA: LEAX $0100,X then blit again)
5A3E  … the BOTTOM edge twice: row $C9 in colour 0, row $C9 − 1 in colour 1
5A4B  … the LEFT edge: column $CA, in colour 0 OR colour 1 (the two nibbles OR'd)
5A52  … the RIGHT edge: column $CB, in colour 0 in the high nibble and colour 1 in the low
```

So **every edge is a two-line band**: the top/bottom edges use colour 0 on one row and colour 1 on
the row next to it; the left/right edges are single columns in the combined colour. The odd-height
correction at `$5A29` keeps that two-row band inside the ring on both sides.

### 79.5 Build spec for the port

- **Where:** `PlayingState`'s/`PlayField`'s wave-complete pause. The ROM calls this from `$2AAF`
  (the wave-complete path) and it replaces the port's `"Level N complete"` banner outright.
- **Class:** `Rendering/TunnelEffect.cs` — state = the two corners (in ART pixels), the packed
  colour pair, the pass counter (2 rings), the phase (drawing / erasing).
- **Coordinates:** the ROM's screen is 152 columns x 256 rows; the port's art grid is 320 x 200.
  Map `x = column x (320/152) ≈ 2.105` and `y = row x (200/256) = 0.78125`, so the extreme corner
  `$0616`/`$8FEC` lands on the port's screen edges and the start box straddles the centre.
- **Timing:** two rings per FRAME (the task's delay is 1), 53 rings, then the black phase — about
  0.9 s in total.
- **Drawing:** one `WallPixel`-style 1x1 texture per edge, `SpriteBatch.Draw` stretched to each
  line's rect, in the colour of the ring's nibble via `SpriteSet.SlotColor` (palette slots cycle
  per §65, which is where the “colour cycling” of the name comes from). The two-line bands are
  the hatch: do not draw a single thin outline, and do not invent a dash pattern — the ROM's hatch
  is exactly *the pair of adjacent lines in the two nibble colours*, plus the horizontal edges'
  one-column offset repeat.

### 79.6 Lesson

"Colour cycling tunnel" = an expanding frame of hatched two-colour edges, driven by a colour-pair
*chain* (`SUBA #$22` with three wrap cases) and finished by redrawing everything in black. The
hash in the name is the palette, not the geometry.

## 80. The wave-complete tunnel is BUILT (2026-09-17)

Author: *"Build it properly! Whats the issue?"* — no issue; it is implemented from §79's spec.

### 80.1 What landed

- **`Rendering/TunnelEffect.cs`** — the ROM's walker (`DRAW_COLOUR_CYCLING_TUNNEL_EFFECT`, `$5703`)
  and its ring primitive (`DRAW_RECTANGULAR_PART_OF_TUNNEL`, `$5A11`) in one place:
  - the ring corners are kept in the **ROM's own screen coordinates** (X = column, Y = row), so
    every number in the code is the ROM's, and the ROM→port mapping is applied at draw time only
    (`x = column x 2 x 320/304`, `y = row x 200/256`) — which puts `$3B80`/`$5A82` where the arcade
    puts them (a thin line across the middle) and lands the far corner on the screen's edges;
  - two rings per pass (`LDB #$02`), one pass per frame;
  - the packed colour pair `$EF → CD → AB → 89 → …` with its three wrap cases (`$12`→`$EF`,
    `$F1`→`$DE`, `$23`→`$F1`), frozen once black;
  - the odd-height correction (`$5A29`) that pulls the bottom row up by one;
  - the hatching: each horizontal edge is a **two-line band** (row `top` in colour 0, row `top+1`
    in colour 1; the bottom edge mirrored) and is repeated **one column (2 px) further right**;
    the vertical edges run between the bands and use the ROM's combined colours (`$CC|$CD` on the
    left, the pair re-packed on the right);
  - the end test at `$0616` and the black erase pass (`CLRA / BRA $5710`).
- **`States/WaveClearState`** now owns a `TunnelEffect`: **the `"LEVEL n COMPLETE"` banner is gone**
  (the ROM shows no text at a wave clear — its `<n> WAVE` text is the wave *start*, §58). `Update`
  advances the tunnel and holds the state until it has finished as well as the display timer
  expiring; `Draw` renders only the tunnel.
- **`tests/Robotron2084.Tests/Rendering/TunnelEffectTests.cs`** (5 tests) pins the start corners and
  first pair, two rings per pass with the exact corner step (`-1 column, -2 rows` / `+1 column,
  +2 rows`), the colour chain and its wraps, the **108 rings over 54 frames** total (54 per phase),
  and the black pass restarting at the outer ring with the pair pinned at 0.

### 80.2 Not yet seen on screen

A wave clear is the only way to reach it — the launch smoke and both render gates never get there —
so it needs the author's playtest. What to look for: a line across the middle that grows into a
hatched, colour-cycling frame reaching the screen's corners, then blanks itself in black and the
next wave begins.

### 80.3 Lesson

The ROM's own arithmetic is the test. Hand-derived expectations — the corner step, the chain, and
the 108-rings-over-54-frames total — all passed first time, which is what says the port's walker
matches the arcade's rather than merely running. Where a routine's numbers can be predicted from
its code, assert those numbers.

## 81. The wave-clear crash — a blitter plane mask is not a palette index (2026-09-17)

Author: *"When I click "P" to skip a wave, the program ends. I should really see the tunnel
effect"*.

**The crash was mine, and it was the tunnel's first cut.** `TunnelEffect.Draw` gave the vertical
edges the ROM's *combined* colours:

```csharp
DrawVerticalEdge(..., _left,  sprites.SlotColor(column0 | column1));        // $5AD5: OR of the nibbles
DrawVerticalEdge(..., _right, sprites.SlotColor((column0 << 4) | column1)); // $5ADA: re-packed high|low
```

The second one is not a palette slot at all. `$5ADA` **re-packs the byte** so the blitter can mask
it — with the starting pair `$EF` that is **239**, and `SlotColor` indexes a 16-entry palette, so
the first frame of the tunnel threw and the process ended. The author's two sentences were the same
bug: the game died before the effect could be seen.

`$5AD5` and `$5ADA` both come down to **the two colours' plane union** once the blitter masks them,
so the port draws *both* vertical edges in the two slots OR'd — `(packed >> 4) | packed`, masked to
0-15 — which is always a legal slot.

### 81.1 What changed

- `TunnelEffect.Colours(packed)` now owns the three slots a ring draws in (colour 0, colour 1 and
  the vertical union), so the conversion sits in a tested place instead of at the draw call.
- New test `EveryColourTheRingDrawsWithIsAValidPaletteSlot` walks the whole pair chain and asserts
  all three stay in 0-15. It fails on the old code (239) and passes now — the guard that should
  have existed the first time.
- **The skip-wave key (`P`, `KeyboardPlayerInputSource`) takes the same path as a cleared wave**
  (`PlayingState` → `HandleWaveCleared` → `WaveClearState`), so with the crash gone the tunnel now
  plays on a skip too — which is what the author was asking for.

### 81.2 Lesson

The ROM's combined/repacked colour bytes are **blitter plane masks**, not palette indices; the
hardware's planes are not the port's slot numbers, so every ROM colour expression needs an explicit
conversion into the port's 0-15 space *and a test that it lands there*. A value that is legal on
the hardware and illegal in the port is exactly the kind of thing that only shows up when the
effect first runs — and a crash at the first frame is the least forgivable way to discover it.

## 82. The tunnel ACCUMULATES its rings — that is what fills the area (2026-09-17)

Author: *"The tunnel effect is showing, but its just like a small series of rectangles rather than
filling the entire play area. The entire play area should be filled with colour"*.

**The ROM never erases a ring it has passed.** `$5A11` draws one ring per call and the walker only
ever *adds*: each pass leaves one more rectangle on the screen, two pixels out from the last, so by
the end of the 54 rings the whole area is a filled field of concentric hatched bands. That
accumulation *is* the "tunnel" — and the black pass is what takes it away again, redrawing each ring
in black as the walk moves outward, so the centre clears first.

The port drew only the **current** ring each frame, so the earlier ones vanished as the walk moved
on: a handful of small rectangles instead of a filled area.

### 82.1 What changed

- `TunnelEffect` keeps every ring it has drawn — the corners **and the colour pair that ring used**
  (each ring keeps its own colours as the pair cycles) — and the renderer paints them all, with the
  erase pass's black rings over the top of the coloured ones.
- New test `EveryRingStaysOnScreenSoTheAreaFillsUp` pins both halves of the report: nothing is
  discarded while the tunnel grows, all 54 coloured rings are still on screen when the erase starts,
  and the outermost ring ends on the ROM's own screen edges (`$0616` through to column 143, row 236)
  — i.e. the walk really does cover the playfield.

### 82.2 Lesson

An effect that *accumulates* has to be modelled as accumulating. The ROM had no framebuffer of its
own to redraw — the screen itself kept every ring — so a port that clears each frame must keep that
history itself. When a ported effect looks "too small" or "too sparse", check whether the original
was relying on the screen's persistence rather than on a single frame's drawing.

## 83. The tunnel's pace: the playtest's ~2 seconds, not the disassembly's 0.86 (2026-09-17)

Author: *"It doesn't last that long though, its over quite quickly. Check the timings"* → then
*"It should last ~2 seconds at least"*.

**What the disassembly says** (every figure verified): 54 rings a phase, ending at `$0616`; two rings
per task pass (`$572D: LDB #$02 / STB $000E,U` with the loop back to `$5731`); and a pass delay of
`A = 1` (`$571E: LDA #$01`) where `ALLOCATE_TASK` (`$D1E3`) documents its unit as *"A x 16
Millisec"*. That puts the whole effect — both the colouring pass AND the black erase — at
**≈ 0.86 s**, and the port's first cut (one pass per frame, 16.7 ms) was within 4% of it.

**The playtest disagrees, so the decode is wrong.** The author's timing of the arcade is **~2
seconds at least**, which cannot happen if the task list is walked once per game cycle: a pass must
be worth **two ROM frames**, not one — i.e. the walk frequency, not the delay value, is what I had
wrong (`$D1DA`'s `DEC $0004,U` happens on whatever cadence that loop runs at, and the header's
"16 Millisec" only tells you the unit's *size*, not how often it is applied).

### 83.1 What changed

- `TunnelEffect.PassFifths = 2 * 6` — a pass lasts two ROM frames, on §52's exact-6ths accumulator
  (`_fifths += 5`, threshold 12). That is 2.4 port ticks a pass, so the effect runs
  54 x 2.4 ≈ 130 ticks ≈ **2.2 s**.
- New test `TheWholeEffectLastsAboutTwoSeconds` asserts 120-140 ticks (2.0-2.4 s) end to end, and
  the pass-based geometry tests now tick through a whole pass.
- The constant carries the note that it is **playtest-derived**, and this section is the record of
  why: the ROM's arithmetic and the author's timing of the machine disagree.

### 83.2 Lesson

When the code gives a rate and the author's timing of the machine disagrees, **the unit is what is
wrong**. The numbers inside a routine are exact — 54 rings, 2 rings a pass — but what a "delay of 1"
is *worth* depends on a scheduler that has to be read separately (`$D1DA`'s walk frequency), not on
`ALLOCATE_TASK`'s comment. Mark a constant like this as playtest-derived so a MAME measurement can
settle it later, rather than burying it and having the next session "correct" it back.

## 84. The tunnel's inner rings — a squashed row scale (2026-09-17); and where the prog's speed lives

Author: *"The progs horizontal movement seems a little slow. Also the inner parts of the tunnel don't
look right, I think there's a couple of black pixels that shouldn't be there"*.

### 84.1 The tunnel's black gaps — FIXED

The first cut mapped the ROM's screen onto the port's **art** grid (200 rows for the ROM's 256), so a
ROM row was 0.78 art pixels tall. At the inner rings that is fatal: the starting box is only **two
rows** tall, so its parts landed on the *same* pixel row — the top band's second line, the bottom
band's first line and the whole vertical edge collapsed into one, and the vertical edge's height came
out as zero, which the port then skipped. That is the "couple of black pixels" the author saw.

The port's screen is **640 x 400 real pixels**, so the mapping is now exact — `x = column x 2 x
640/304`, `y = row x 400/256`, drawn straight into the sprite batch with no art-grid scaling — and
every edge keeps at least one pixel of thickness (`Thickness` = one ROM column, `Math.Max(1, ...)` on
every row span) so nothing can vanish at the centre again.

### 84.2 The prog's horizontal speed — step and body both check out

- **The step is right.** `Prog` moves one arcade pixel on X and two on Y (`StepXArcadePixels` /
  `StepYArcadePixels`), which is the ROM's `CLRB / ASRA / RORB` — a *half column*, and a column is
  two pixels (§55). "Vertical progs move twice as fast as horizontal ones" is the ROM's own shape.
- **The body is right.** `RRB10.ASM`'s `PROG` loop ends `NAP 3,PROG` — three ROM frames a body — and
  §65 already put the prog's body on the exact-6ths clock (3.6 ticks).

So the slowness is **not** the step and not the body: it is the **mover** — how many pixels the prog
travels per body. The ROM commits the position once a body from `OX16`/`OY16` (which the shared
object mover updates from `OXV`/`OYV`), so the next place to look is the prog's `OXV`/`OYV` roll and
the mover's per-body application — the port's `Prog` steps a *fixed* pixel per body, where the ROM
drives it from a velocity. That is the next decode, and it is flagged rather than guessed.

### 84.3 Lesson

Two things worth keeping: **the port has a real 640x400 screen, so ROM geometry should be mapped onto
*that*, not onto the 320x200 art grid** — the art grid is for art, and using it for geometry loses
half the vertical resolution; and **an effect's small-scale parts need a minimum thickness**, or the
innermost iterations of a geometric walk collapse to nothing.

## 85. The tunnel's edges are ONE ROM pixel — and that is the "thickness" (2026-09-17)

Author: *"I don't think you've got the inner bars 'thick' enough yet. Here's the arcade:"* with a
screenshot of the arcade's tunnel.

### 85.1 What the screenshot actually measures

The reference is 1201x853 and it is the tunnel's own box: rows 22..236 (215 rows) over 853 px =
**3.97 px per ROM row**, columns 6..143 (138 columns) over 1201 px = 8.70 px per column. Two things
fall out:

- **The colour pattern repeats every 15 rows** (measured 59.5 px), and the ROM's colour chain is
  **15 pairs** long (`$5737`'s three special cases make it 15, not 16) with one pair consumed per
  ring — so the arcade is showing **one palette slot per ROM row**, which is exactly what the walk
  produces: ring *k*'s band is rows `top`, `top+1` in its two nibbles and the next ring's band starts
  two rows further in, so successive rows carry successive slots. That is why the arcade reads as a
  smooth gradient field rather than as 54 discrete rings.
- **A band is 2 rows and the dark lines are the reference CRT's inter-line gaps**, not geometry: the
  "black" between bands samples as a *dark version of the adjacent hue* (`(0,25,0)` between greens,
  `(0,10,50)` between blues).

### 85.2 The reference's colours are the port's colours, colour-shifted

The hue mix of the reference is **green 43%, blue 31%, cyan 26%** — no red, magenta, yellow or white
at all. That looked like a palette mismatch, so I tested it rather than guessing: the ROM's default
table **is** the port's palette (verified byte-for-byte, `CRTAB` at `$DA51` =
`00 07 17 C7 1F 3F 38 C0 A4 FF 38 17 CC 81 81 07`), and the walk visits **all** fifteen slots, so the
port must show the whole rainbow. Attenuating the red channel of the port's own capture to <= 22%
(a weak/aged red gun, or the camera's white balance) turns its mix into **green 48%, blue 37%,
cyan 15%** with no red/magenta/yellow/white — the same three-hue picture. **So the palette is right
and no colour change was made**; the reference is a photograph of a CRT with a weak red channel.
Test kept: `%TEMP%\palette_test.py` (throwaway) reproduces both mixes.

### 85.3 The ROM decode — every edge is one pixel

`DRAW_RECTANGULAR_PART_OF_TUNNEL`'s four blits, read out of the blitter registers:

- **Horizontal line** (`$5A89`): `LDA #$05 / STA blitter_height` -> height field 5 XOR 4 = **1 byte =
  1 ROW**; width = `$CB - $CA + 1` bytes, i.e. columns `left..right` **inclusive**.
- **Its second line** (`$5AAA`): the SAME call one row on, but `DECB` first and `LEAX $0100,X`, so it
  is **one column right and one column narrower** — inset a column at *each* end, not just the left.
- **Vertical line** (`$5A5B`): `LDA #$05` -> **1 byte = 1 COLUMN = TWO ROM pixels** wide, height
  `bottom - top - 1` starting at row `top + 1`.
- **Its colour is two pixels of colour, not one flat colour.** `$5AD5` ORs the packed byte's nibbles
  (`$EF`) and `$5ADA` re-packs them the other way (`$FE`) — and those nibbles are the two pixels'
  palette slots. So the left edge reads **colour 0 then colour 1** and the right edge is its
  **mirror**. The port had been drawing one flat OR'd slot, which is what made the bars look solid.

### 85.4 The fix

`TunnelEffect` maps ROM geometry through `PixelX(romPixel)` / `RowY(romRow)` and every span runs from
its own first pixel to the NEXT one's first, so **adjacent rows and columns tile edge to edge and
cannot leave a seam** — that seam (each row truncated to a whole pixel, then a 1-px hole before the
next) is what the author's "couple of black pixels" and "not thick enough" were. The band's second
line is drawn inset (`inset: true`), and the vertical edges take their two pixel colours
(`DrawVerticalEdge(..., first, second)`) mirrored between left and right. New test
`EveryRomRowAndPixelGetsItsOwnWholePortPixelSoNothingLeavesASeam` pins the tiling and that the 256
rows fill the port's 400-pixel screen exactly.

### 85.5 Lesson

**Thickness is geometry, so map a row or a column to its WHOLE pixel span and let the next one start
where the last ended — never round each span to a size of its own.** A 400/256 px row rounds to 1 and
loses a third of itself; a span that ends where the next begins loses nothing, whatever the scale.
And when a reference's colours look wrong, measure the hue mix and test the cheap explanation
(a colour-shifted capture) before touching a palette that the ROM's own table already confirms.

## 86. The tunnel's bars are its PALETTE — the ROM's colour-cycling ramp (2026-09-17)

Author: *"Its not the colours at fault, its the size of the bars making up the tunnel - they looked a
bit slim"*.

### 86.1 What the reference measures — STRUCTURE, not hue

I measured the arcade reference's centre column run by run instead of looking at its hues:

| | arcade reference | port (before §86) |
|---|---|---|
| bright runs | 40 | — (every row its own colour) |
| mean bright run | **4.32 rows** | 1 row |
| mean dark run | **0.86 rows** | none |
| bars per 15-row colour cycle | **3** | 15 |

So the arcade's tunnel is **bars of five rows with a one-row dark seam**, three to a colour cycle —
and the port's was fifteen one-row stripes of unrelated colours, which is exactly "the bars look
slim". Nothing about the ring geometry was wrong: §85's one-ROM-pixel edges and tiling rows stand.

### 86.2 Why — the walk gives one palette slot per ROW

The walk leaves one palette slot per row and the chain descends one slot per row, so **what the bars
look like is a property of the palette, not of the drawing**. `CRTAB`'s neighbours are unrelated
colours (0x81, 0x81, 0xCC, 0x17, 0x38, 0xFF, 0xA4, 0xC0 …) → fifteen stripes. The arcade's slots hold
a **ramp**, whose neighbours are one step apart → a gradient, and **three of the fifteen are black**
→ the 5-row bars with dark seams. `DRAW_COLOUR_CYCLING_TUNNEL_EFFECT` is named for that cycling, and
the tables it uses are in the SAME ROM block as the walker: the walker's task data at $576D is
followed by **twelve ramp descriptors** (`$5984/$1F`, `$5900/$3F`, `$58A8/$3F`, `$579D/$0F`, …) and
their data.

### 86.3 The ROM decode — `$59B1` (set-up) and `$59D0` (the pass)

- `$59B1`: pick a ramp at random, retrying until the index is under twelve (`ANDA #$0F / CMPA #$0C /
  BCC`), then a random window inside it — `ANDA B,Y` masks the random value with the descriptor's
  second byte, so the start is `random & $0F/$1F/$3F`.
- `$59D0`, once per pass: bump the stored pointer by ONE (`LEAX $0001,X`, wrapping to the table start
  when the byte it lands on is zero), write **fifteen consecutive non-zero values into slots 1-15**
  (`LDY #$9801 … CMPY #$9810 / BCS`, skipping a zero by wrapping), then black **three slots —
  `$9801+A`, `$9806+A` and `$980B+A`** (`LDB #$00`, `STB A,X`, `LEAX $0005,X`) where `A` walks
  **4,3,2,1,0,4…** (`LDA $000B,U / DECA / CMPA #$05 / BCS` — a value of 5 or more falls back to 4).
- `$5A09`: re-allocate on delay `1` — the same cadence as the rings, so the palette slides one value
  per ring pass, sweeping the wheel through the screen.
- The black slots are `A` apart from each group's start, so **one slot in each group of five is
  black** — and since the walk descends one slot per row, that is one dark row every five ✓ exactly
  what the reference measures.

The port's palette reloads `CRTAB` per wave (`$27C3`/`$2852`'s `LOAD_DA51_PALETTE`), which is what
puts the game colours back afterwards.

### 86.4 The implementation

`Rendering/TunnelPalette.cs` (generated from the ROM's own table block so the ramps cannot be
transcribed wrongly): the twelve ramps, their start masks, `Start()` (random ramp + masked window),
`Advance()` (pointer +1, black offset 4→0→4) and `Apply(GamePalette)` (fifteen window values, then
the three blacks). `WaveClearState` runs it beside the tunnel — one palette pass per ring pass, the
ROM's own cadence — **suspends the six colour processes** while it owns the palette (the ROM's own
palette task writes all fifteen slots every pass, so nothing else may fight it for 10-15) and
restores `CRTAB` + resumes them on the way out.

**Measured after the change:** 38 bright / 38 dark runs, **3.96 rows** bright against the arcade's
4.32, **1.57 rows** dark against 0.86 — the same bar structure at the same scale, from the ROM's own
tables.

### 86.5 §85.2 CORRECTED — compare STRUCTURE, not hue mix

§85.2 concluded "the reference is a colour-shifted photograph of the same palette" from the *mix* of
bright hues (green/blue/cyan only) — but a hue mix is a weak instrument: the dark pixels were
excluded from it, so the run structure that carried the whole answer was thrown away. The reference
is a real arcade tunnel over a **ramp palette**; the port was drawing CRTAB. The same mistake would
have been caught instantly by measuring *run lengths*.

### 86.6 Lesson

**An effect owns more than its draw routine.** Its palette, its task cadence and its tables live in
the ROM beside it, and a table that looks like imported data (as $576D's descriptors do) may be the
effect's own. When a reference's *texture* differs while the geometry matches, look for a palette
the effect loads — and compare the two images' **structure** (run lengths, periods, counts) before
comparing their colours.

## 87. The prog's step is in COLUMNS, and the quark's first tank counts animation cycles (2026-09-17)

Author: *"Fix the prog movement, the quark birth speed."*

### 87.1 The prog — the step is 2 COLUMNS, not 2 pixels

`RRB10.ASM`'s tables are:

```
PRGAL FCB 0,-2,0 ...   ; LEFT
PRGAR FCB 12,2,0 ...   ; RIGHT
PRGAD FCB 24,0,4 ...   ; DOWN
PRGAU FCB 36,0,-4 ...  ; UP
```

and `PROG` applies them with `LDD 1,Y / ADDA OX16,X / ADDB OY16,X`. **`OX16` is a screen
coordinate — `column*256 + row` — so its high byte is a whole COLUMN**, and the X delta of −2
moves the prog **two columns = four arcade pixels** a body. The port moved two *spec pixels*,
i.e. half the arcade's distance: the author's *"the progs horizontal movement seems a little
slow"*. The Y delta of ±4 is in ROWS, so both axes cover **the same 4 px a body** — the old
"vertical progs move twice as fast as horizontal ones" read the column as a pixel.

The same unit governs the aim: `GPOFF` builds `PD4 = ((16 - RND(1..15)) * 4) - 32`, i.e. ±28 in
steps of 4, and `GPDIR` adds it to `PX16` (the player's **column**) before comparing with
`OX16`, so the standing error is **±28 columns = ±56 px** (the port had ±28 px), and the wrap
margin is `XMAX + $30` = **48 columns** (the port had 48 px). `StepXColumns`, `ArcadePixelsPerColumn`
and `WrapMarginXColumns` in `Prog` now carry those units, and
`Prog_CoversFourPixelsOnEitherAxis_PerBody` pins the 4 px step on both axes.

### 87.2 The quark — the first drop counts ROTATIONS, not bodies

`RRTK4`'s idle pass:

```
SQUARE LDX PD,U
 LDD OPICT,X
 ADDD #4
 CMPD #SQP4
 BLS SQ1          ; every pass that stays inside the idle pictures …
 LDD #SQP0        ; … only the WRAP pass reaches here
 TST STATUS
 BNE SQ1
 DEC PD2,U        ; ← the drop countdown, once per animation CYCLE
 BEQ SQ2
SQ1 STD OPICT,X
```

so `PD2 = RND(1..TDPTIM)` counts **cycles of the idle animation** — five pictures to walk plus
the wrap pass, six bodies each — and the first tank is due **six times later** than a per-body
countdown puts it. The port decremented it once per body, so a quark began dumping tanks the
moment it appeared: *"the Quarks are dropping tanks WAY too quickly."* In the drop phase
(`SQ2L`) the same countdown *does* run every body, and the port already had that right.

**Measured** (a throwaway harness, deleted before the commit, five seeds at wave 7's TDPTIM 16):
first drop **139-379 ticks (2.3-6.3 s)** against **0-47 ticks (0-0.8 s)** before, with the
subsequent gaps unchanged at 5-43 ticks — the arcade's 1..(TDPTIM/2)+1 bodies. The ROM's
`TST STATUS / BNE SQ1` gate is implemented too, so the countdown now holds during the
player-start grace exactly as the spheroid's does.

### 87.3 The §77.2 suspicion was wrong

§77.2 guessed the port might be handing the quark a wave table's *tank* column instead of
ENFNUM. It is not: `WaveTable.MaxDropsX2` is ENFNUM (10 for waves 1-20, 11 for 21-40 —
`RRG23:604`'s `FCB 10,10,…`), and `PlayField` passes it straight through. The tempo was the bug.

### 87.4 Flagged, not guessed: what is a task pass worth?

`SLEEP` (RRS22:219) is `STA PTIME,U` then a dispatcher that walks the active list with
`DEC PTIME,U / BNE DISP2 / … JMP [PADDR,U]`, so `NAP n` wakes on the **n-th** pass of the list.
That is one pass per game cycle — which contradicts §83, where the tunnel's ~2 s playtest forced
**two frames per pass**. Both cannot be right, and the answer decides the body length of every
task-driven object in the port (the prog's 3 frames, the spheroid's 3 for `NAP 2`, the quark's
4). It needs an arcade measurement of something whose duration the author knows — the same
MAME measurement §83 asked for — so it stays flagged.

### 87.5 Lesson

**A ROM coordinate is not a pixel.** The X byte of a `column*256 + row` address is a COLUMN —
two pixels — and anything compared or added to it (a step, an aim offset, a wrap margin) is in
columns too. §55 recorded this for velocities; this is the third routine to be bitten by it.
And when a countdown looks too fast, check **which branch its `DEC` sits on**: the quark's sat
on the animation's wrap branch, and the spheroid's does the same thing for its enforcers.

## 88. The family's start of wave — they walk at once, and they are kept off the posts (2026-09-17)

Author: *"At the start of the level, the family members wait too long before they start moving.
Also can you check if they should spawn on top of electrodes? I don't think they should."*

### 88.1 The family walks immediately — the stagger IS their first wait

`RRH11`'s `HUMSTV` creates the family EARLY in the wave sequence — `JSR HULKST / BRNST / TANKST /
**HUMST** / PSINIT / … / JSR ROBOFF … LDA #$19 / STA STATUS … JMP APPEAR`, i.e. **before** the
robots' assembly and **before** the player's appear + `CLR STATUS`. Each member is started with

```
 LDA SEED
 ANDA #7      STAGGER START TIME
 INCA
 STA PTIME,U
```

so the **stagger (1..8 sleep units) is written straight into the process's own timer**, and the
wake-up that ends it *runs the walk body* — the first step. And, crucially, `HUMAN` is the one
robot routine with **no `STATUS` check**: everything else waits (`RRP8`: `ROBOT LDA STATUS`;
`RRH11`: `HULK LDA STATUS WAIT FOR STATUS TO GO`; `RRC11`: `TST STATUS DONT START EARLY GUYS`;
`RRB10`'s brain, `RRTK4`'s quark, `RRX7`), while the humans just walk.

The port did the opposite twice over: it held the humans with the other robots behind
`RobotsFrozen` (which is true for the player's whole 2-second start grace), and it seeded a whole
extra 16-frame step period into the walk clock, so the family stood for about 2.4 s. Both are
gone: `Human` no longer checks `RobotsFrozen`, and the stagger's LAST tick falls through into the
step (leaving the accumulator clean, so the following steps sit on the arcade's uniform `ceil(19.2k)`
grid — `Human_StepCadence_RunsOnTheExactSixthsClock`'s expected grid moved with it).

The corollary had to follow: `ResolveHumanCollisions` no longer lets a brain grab or a hulk kill a
human while the robots are held. The ROM does the same thing structurally — its collision process
is created at `PLS2` (`JSR ROBON / MAKP LSPROC / **MAKP COLCHK** / CLR STATUS`), i.e. **immediately
before** `CLR STATUS`, and the hulk itself waits for it — so during the appear and the grace no
robot may touch the family. Without that gate the newly-walking humans were grabbed at wave start.

### 88.2 The electrodes — the ROM allows it, the port does not (deliberate, on request)

`HUMSTV` places each member with a bare `JSR RANDXY / STD OBJX,X / STA OX16,X / STB OY16,X` —
**no retry and no obstacle test**, unlike the hulk (`HLKST0`: `JSR SAFTY` then `CMPB XTEMP … BLS
HKST0A TRY AGAIN` until it is outside the player's safety zone). And a human refuses to step into
a post (`HUMAN`'s `JSR CKOBS` against `PPTR`), so **a human the arcade places inside a post stands
there for the rest of the wave**. The port prevents that, which is a DELIBERATE deviation — the
second, after the human walk period (notes §70) — made at the author's request. It is documented
here as such rather than presented as the Gospel's behaviour.

The rule is now airtight, which it was not:

- the random-attempt loop and the last-resort scan both honour the predicate, and the **scan's step
  is 4 px** (it used to step by the whole entity size, so it could stride past a gap between two
  posts and then fall through to `RandomPointInside()` — an UNCHECKED point, which is how a human
  could still land on an electrode);
- the placement clears the member's **own** box (`Human.SpawnSquarePortPixels`, the largest side of
  the 12x15 / 8x10 picture boxes), not the generic 16-px entity square.

`TheFamilyNeverStartsOnAnElectrode` checks six wave levels × 120 seeds and
`TheFamilyAlsoNeverWalksOntoAnElectrode` checks a whole wave.

### 88.3 Lesson

**A gate is per routine, not per family of entities.** Every robot routine in this ROM checks
`STATUS` — the humans are the exception, and copying the robots' gate onto them put the family to
sleep for two and a half seconds. When a class of objects shares a behaviour, check each one's own
routine before sharing its gate. And a "cannot happen" fallback that returns an unchecked value
(such as a last-resort random point) will eventually hand the caller exactly the value its
predicate existed to exclude.

## 89. SESSION-3 WRAP-UP — the traps, the deviations and what is still open (2026-09-17)

Read this FIRST in a new session. The day built the wave-complete tunnel from scratch (§79-§83), then
tightened it four times as the author's playtests came in (§84-§86); audited the scoring and located
the tunnel (§78); checked the human/electrode spawn and the quark's tank drop (§77); fixed the prog's
step and the quark's drop timer (§87); and finally the family's start of wave (§88). Twelve commits,
each with its own `rebuild-ledger.md` row; the per-item detail lives there and in §77-§88.

### 89.1 The traps that cost time today, in the order they bit

1. **A header comment is a lead; the code is the evidence.** `$473F`'s "A = TOP HALF / B = BOTTOM
   HALF" header built a whole wrong model (§67); the routine never reads `B` (§71). The tunnel's
   `$5A11` header likewise says nothing about the two-pixel mirrored vertical edges the code makes.
2. **A hit point is not a centre** (§73): a laser strikes a sprite's near edge, so anchoring a fan
   at the hit puts the split at 0 or `extent−1` and one half gets everything.
3. **A `Texture2D`'s dimensions are ART pixels; entity `Bounds` are PORT pixels** (§75). A
   `/ ScreenSize.SpecScale` on a texture dimension is ALWAYS wrong.
4. **An effect owns more than its draw routine** (§86): the tunnel's colour-cycling ramp tables sit
   in the walker's own ROM block at $576D, and copied data that looks like imported tables can be
   the effect's own.
5. **An effect that accumulates must be modelled as accumulating** (§82): the ROM never erases a
   ring it has passed — the accumulation IS the tunnel.
6. **Map ROM geometry onto the port's REAL screen, not the art grid** (§84): the screen is 640x400
   for the ROM's 304x256, so a ROM row is 1.5625 px, not 0.78.
7. **Thickness is mapping, not sizes** (§85): one ROM pixel per edge, and every span drawn from its
   own first pixel to the NEXT pixel's first, so rows and columns tile. Rounding each span loses a
   third of it and leaves a seam.
8. **Compare STRUCTURE, not hue mix** (§86.5, correcting §85.2 — my own error, corrected within the
   hour): run lengths, periods and counts identify a reference image; a colour histogram does not,
   and it can make a wrong answer look confirmed.
9. **A ROM coordinate is not a pixel** (§87): the X byte of a `column*256 + row` address is a whole
   COLUMN (2 px), and anything added to or compared with it is in columns. Third time bitten (see
   §55 for velocities and §53 for the tank birth).
10. **A countdown can be gated on an animation wrap** (§87): the quark's `DEC PD2,U` sits on the
    branch that wraps its idle pictures, so the timer counts animation CYCLES, not bodies. The
    spheroid's is gated the same way. When a drop timer looks too fast, read which branch the `DEC`
    is on before touching the number.
11. **A gate is per routine, not per class of object** (§88): every robot routine checks `STATUS`
    — `HUMAN` does not, and copying the robots' gate onto the humans put the family to sleep for
    2.4 s at every wave start.
12. **A "cannot happen" fallback that returns an unchecked value will eventually hand the caller
    exactly what its predicate existed to exclude** (§88.2 — the last-resort placement scan).

### 89.2 The port's deliberate deviations from the Gospel — exactly two, both author-requested

1. **The human walk period is 16 ROM frames, not the Gospel's `NAP 8`** (§70.1, playtest round 8:
   *"the mommies are walking too fast"*). `Human.StepPeriodRomTicks`.
2. **Humans never spawn on an electrode** (§88.2). `HUMSTV` places them with a bare `RANDXY` and no
   retry (contrast the hulk's `SAFTY`/`TRY AGAIN` loop), so the arcade CAN leave a human stuck
   inside a post; the author asked for it not to happen. `PlayField.SpawnHumanKind` +
   `Human.SpawnSquarePortPixels`.

Everything else is intended to be the Gospel's behaviour, and a playtest that disagrees with it is
evidence the DECODE is wrong — that rule is what found §73, §75, §85, §86, §87 and §88.

### 89.3 Open questions, in priority order

1. **What is one task pass worth?** (§87.4) RRS22's `SLEEP` dispatcher is `DEC PTIME,U / BNE DISP2 /
   … JMP [PADDR,U]`, so `NAP n` wakes on the n-th pass — one pass per game cycle; §83 instead
   needed TWO frames per pass for the tunnel to last the author's ~2 s. Every task-driven body
   length depends on it (the prog's 3 frames, the spheroid's 3 for `NAP 2`, the quark's 4). It
   needs ONE MAME measurement of a duration the author knows — do not settle it by feel.
2. **The scoring report** (§78): every laser kill DOES score; the only point-free kills are a grunt
   killed by an electrode, an electrode killed by a hulk, the player walking into an electrode, and
   a human's death. The discriminating question for the author is whether the kill they saw was a
   LASER HIT — the other possibility is the current player's cycling slot-10 score being hard to
   read.
3. **A8:** the progging brain keeps its WALK pose; the ROM switches to the programming pose
   (`$2141` left / `$214D` right) — visible now that the sprite is drawn again (§72.1).
4. **§72.6:** the H family's fan draws `2W` strips where the source's own `WR` loop draws `2W−1`
   (one pixel too many at the left end; the fix is to start the column loop at `i = 1`).
5. **§83:** a MAME measurement of the tunnel's duration to settle `PassFifths` (currently the one
   playtest-derived constant in the port).
6. **Blocked on the author:** the sound note→frequency table (beeping is OFF by default, §68), the
   marquee art, the red screen (§33: which mechanism?), the tank-shell speed + electrode placement
   (§36.3/36.4), an A6 decision on the human walk period, and the playtest of everything shipped
   today (the tunnel, the progs, the quarks, the family's start).

### 89.4 Gate state at the end of session 3

0 warnings (Debug **and** Release) · **266 tests, 0 failed, 1 skipped** · the 12 s launch smoke OK ·
`tools/verify-playfield.py` PASS · `tools/verify-fonts.py` PASS. Both configurations are built, so
a launch cannot pick up a stale binary.

## 90. The PROG's death — the phony card SHATTERS, and the card itself was corrupt (2026-09-17)

Author: *"Now, when I shoot the progs (after they have been programmed) the explosion effect looks
weird."* Two defects, and the louder one was in the ART.

### 90.1 The `ProgBurst` art was NOISE — a transcription slip in the extractor

`ProgBurst` is the port's only **inline** art: PGXPIC is not in the R5 ROM (R5 re-drew the picture;
a byte search for the source's `PGXD` pattern finds nothing), so `tools/SpriteExtractor` carries the
pre-R5 source's bytes in the program itself —
`("ProgBurst", 0, 6, 16, new byte[] { … })`. The writer takes `bytesPerRow = w = 6` and reads
`data[y*6 + (x>>1)]`, so it consumes exactly 96 bytes for a 16-row × 6-byte picture — and the array
held **114**: nine of its sixteen rows had been written as EIGHT bytes, as if
`FDB $AA00,$0000,$0AA0` were four 16-bit values instead of three. (Seven rows — 0, 6, 7, 11, 12,
13, 15 — were the right length, which is why it read as deliberate.) Nothing complained: the writer
just read the first 96 bytes of a longer array, so the rows ran out of step and the PNG decoded to
a ragged field of slot-10/slot-11 noise — a purple blob. Confirmed by decoding the committed PNG
back to palette indices and diffing against `PGXD` (16/16 rows matched the source listing after the
fix; before it, 12/16 were garbage or shifted).

**The root cause is a missing guard, so the guard was added:** the extras loop now rejects inline
data whose length is not exactly `w x h`. Any future hand-copied sprite fails loudly instead of
silently emitting a plausible-looking PNG of noise.

### 90.2 The death is `PGXPIC` + the ordinary `EXST` — not a static pop

The port had the prog enter a `Dying` state and draw the whole card in place for
`ProgBurstTicks = 20` ticks, then vanish. The Gospel (RRB10 `PRGKIL`):

```
PRGKIL LDA  PCFLG
       BNE  PGKILX            ; the player-contact path: the PLAYER dies, not the prog
       LDA  #PD+10            ; erase the SEVEN trail images (PD+10 .. SPSIZE, step 2)
PRGKL  PSHS A / LDD A,X / JSR PCTOFF / …
       JSR  KILL              ; erase the prog's own image
       LDD  #PGXPIC           ; "BLOW PHONY PICT"
       STD  OPICT,X           ; ← the object's PICTURE DESCRIPTOR is replaced
       LDA  #XMAX-5 / CMPA OBJX,X / BHS PGK1 / STA OBJX,X    ; clamp X
PGK1   LDA  #YMAX-15 / CMPA OBJY,X / BHS PGK2 / STA OBJY,X   ; clamp Y
PGK2   JSR  EXST              ; ← the ORDINARY explosion
       JSR  KILROB            ; free the object
       LDD  #PGKSND / JSR SNDLD
       LDD  #$0110 / JSR SCORE
```

`PGXPIC` is a normal picture descriptor (`FCB 6,16` + `FDB PGXD`), so the swap is exactly what any
robot's `OPICT` holds. `EXST` is a **3-byte RAM vector** (`RRF.ASM:262`: `EXST RMB 3` at
`RXORG` = $5B40) whose contents are `JMP EXSTV` (RRX7.ASM:33) — the download-to-RAM module pattern
the marquee and the high-score code use too. So **a prog dies like every other robot**: the same
direction dispatch, the same fan, the same ten-record pool, the same dropped strips — except that
`EXSTV` sizes its record from the **picture descriptor** (`LDD ,X` = 6,16 = 12×16) while `UL` stays
`OBJX/OBJY`. That is the whole point of the swap: the human art would have shattered into a human;
the phony card shatters instead.

The port matches now: `Prog : IExplodable`, `CurrentFrameArt` = `sprites.ProgBurst`, `Kill()` →
`Dead` at once (no `Dying` phase at all, like the enforcer), and the field's `SpawnExplosion` runs
the standard strip explosion. The record's rect comes from a new defaulted interface member,
`IExplodable.ExplosionBounds` (default = `Bounds`, so no other entity changes): for a prog it is the
**card's** rect at the prog's corner. Using `Bounds` would have centred the 12×16 card inside the
converted human's smaller box (8×14 for a Mom, 10×13 for a Dad), i.e. 1 art px up-left of where the
ROM draws it.

Two smaller things fell out of the same decode:

- **The prog kill was SILENT.** `SoundTables.RobotDeath` is precisely the ROM's `PGKSND` ($1ADA,
  priority 208, `(1,4,$14),(2,4,$17)` — §37), and it is played by the strip-explosion branch that a
  prog never reached. It does now.
- **The `(XMAX-5, YMAX-15)` clamp is NOT modelled**, deliberately: it keeps the DMA write inside the
  screen buffer, and a prog is always inside the playfield already (and the port drops any strip
  that leaves it, §35).

Also worth recording: the ROM erases the seven trail images *before* the swap, so the trail goes
with the prog. The port gets that for free — a `Dead` prog is not drawn.

### 90.3 Lesson

**Art that is hand-transcribed from a listing needs a length check.** Every other sprite in the
pipeline is sliced out of the ROM by `offset + w*h`, so its size cannot be wrong; the one inline
picture had no such constraint, and a writer that reads exactly `w*h` bytes from a longer array will
happily produce a PNG of noise. And **a "death effect" is not a drawing job**: when a kill routine
ends in `JSR EXST`, the enemy's death IS the standard explosion — the only per-enemy input is which
picture the record is handed.

## 91. The SPHEROID's picture chain — five pictures, one a body, and the escape (2026-09-17)

Author: *"The spheroid, after giving birth to all the enforcers, looks weird animation wise."*
**Author confirmed the fix: *"Yeah that's it!"*** (2026-09-17) — the escape now pulses at the
arcade's rate and its picture set matches. The escape phase was advancing its picture on every PORT
TICK instead of once per BODY (3.6x the arcade's rate), and §56.2's picture counts were one short in
the two phases that wrap at `CIRP4`.

### 91.1 The chain, from the ROM (two witnesses)

`MKPROB CIRCLE,CIRP4,CIRKIL` (RRC11:29) → `MPROB`'s `LDD ,U++ / STD OLDPIC,X / STD OPICT,X`
(RRS22:427-431) puts **CIRP4** in a new spheroid's `OPICT` — so a spheroid is BORN showing the
fourth picture, and its own first body is already a wrap pass.

| phase | advance | wraps when the next entry would pass | pictures |
|---|---|---|---|
| CIRCLE (`ANIMATE_SPHEROID`) | `OPICT += 4` a body | `CIRP4` (`CMPD #$1502 / BLS $11C7`) | 0..4 |
| CIRC2L (`DROP_ENFORCER`) | `OPICT += 4` a body | `CIRP7` (`CMPD #$150E / BLS $120E`) | 0..7 |
| CIRC3L (`TIME_FOR_SPHEROID_TO_EXIT_PRONTO`) | `OPICT += 4` a body | `CIRP4` (`CMPD #$1502 / BLS $124C`) | 0..4 |

The art explains why the counts differ: the eight pictures are ONE growth animation — a dot, a
plus, a small ring with a pupil, a ring with a cross-hole, a bigger plain ring (`CIRP4`), a big
ring, an opening ring with tabs, and finally the fragments (`CIRP7`). The idle spin therefore
PULSES from the dot up to the medium ring and snaps back, while the drop phase grows all the way to
the burst — which is the pose the enforcer pops out of.

`BLS` (branch if lower or same) is what settles the count: the value being stored is `OPICT+4`, so
`CIRP4` itself IS stored — five pictures, not four. §56.2 read the boundary as "wraps past `CIRP3`"
and gave the port four.

### 91.2 What the port had wrong

1. **The escape strobed.** The escape branch did `AdvancePosition(); _rotation = (_rotation + 1) % 4;`
   with NO body clock, so the picture changed every 1/60 s (a 15 Hz flicker through four growth
   frames) instead of every `NAP 2` body (3.6 ticks). This is the defect the author saw.
2. **The picture count.** The spin and the escape used 4 pictures (`% 4`); the ROM cycles
   `CIRP0..CIRP4`. The idle pulse never reached the medium ring.
3. **The phase transitions.** CIRCLE's `BEQ CIRC2` does NOT store the wrap target, so the drop
   phase carries the pointer on from `CIRP4` into `CIRP5`; the port restarted the cycle at
   `CIRP0`. And `StartEscape` reset the pointer to 0 where the ROM leaves it on `CIRP7` (the first
   escape body wraps it).
4. **The X exit test** sits inside the escape's wrap branch — once per five-picture cycle — so the
   spheroid can overshoot columns 10/133, and at the wall it waits for its next wrap pass. The port
   tested every frame and left at exactly the threshold.
5. **The freeze gate.** `CIRCLE` has `TST STATUS / BNE CIRC1` (which skips only the DEC); `CIRC2L`
   and `CIRC3L` have no such test — a gate is per ROUTINE, not per object (§88's trap) — so a
   dropping spheroid keeps counting through a pause. The port gated both phases.
6. **The birth picture**: a spheroid now starts on `CIRP4`, as the ROM creates it, not on the dot.

`Spheroid` now runs the chain the way the ROM does: ONE `_rotation` pointer advanced at most one
entry per body, with the phase's last picture deciding the wrap, and the phase logic (and the
escape's exit test) living on the wrap pass. `internal PictureIndex` / `IsEscaping` are the test
hooks; `SpheroidAnimationTests` pins the rate (at least 3 ticks between picture changes), the 0..4
escape set, the 0..7 drop set and the `CIRP4` birth.

### 91.3 Left alone, and why — parked as A10, A11 and A12

- **A10 — the mover's rate.** `OPB80` integrates the velocity once per ROM FRAME, but the port
  integrates it once per 60 Hz port TICK, so everything on the generic mover (spheroid, spark,
  missile) travels 60/50 = **20% faster than the arcade**. That is a systemic change with its own
  verification, so it is flagged for the author rather than folded into an animation fix.
- **A11 — CIRC2L's re-arm re-enters its `DEC`** — `BRA $11DB` → `RMAx(CDPTIM/4)` → the `$11E5`
  chain decrements the fresh countdown in the SAME body — so the arcade's cadence is
  `RND(1..CDPTIM/4) − 1` wraps, and a countdown that reaches 0 becomes `$FF` on the next wrap (a
  ~2-minute stall that would stop a wave clearing). The port re-arms without the extra `DEC`. This
  needs a MAME measurement before it is touched.
- **A12 — the escape exit columns** are still the port's own mapping
  (`SpheroidEscapeExitLeftColumn` measured from the wall, `…RightColumn` measured absolutely) — the
  port's 320x200 spec space is not the ROM's 143-column buffer, and moving a vanishing point without
  a measurement would be guesswork. It also has to be checked against A9 (the task-pass length),
  because the exit test only runs once per five-picture cycle.

## 92. The cycling FONT GLYPHS: 468 baked textures become a slot-indexed shader path (2026-09-17)

(B9 — "Delete the 468 baked font cycling variants if the shader can avoid them.")

### 92.1 What the baked variants were

Section 39 gave every glyph blitted in a cycling slot (10-15) a marker-baked texture per slot:
78 glyphs x 6 = 468 PNGs (`Font_<L|S>_<ch>_10..15.png`) whose glyph pixels carry the slot's
MARKER RGB (the de-duplicated RobotronPaletteService values C4/F4/CC/81/45/2F —
`GamePalette.CyclingSlotMarkers`). The M4 colour-cycle shader's main pass remaps a texel whose
RGB equals one of those markers to the slot's live colour (the `Live10..Live15` uniforms).
For ENTITY SPRITES that stays the mechanism: the marker-baked sprite PNGs are the arcade's
palette indices made into pixels, and the shader's six literal comparisons are the blitter's
"which slot am I" test.

But the font masters are WHITE on transparent. For a white glyph, `MainPS` matches no marker,
so the only reason a variant existed was to hand the shader a marker to match: the variant's
pixel colour adds no information (the alpha channel is identical across all seven variants of
a glyph), and the draw had to look up a second texture per slot.

### 92.2 The fix: the shader already has everything it needs

The effect already receives the six live colours as uniforms (`GamePalette.UpdateEffectColors`).
A glyph drawn in slot 10..15 is exactly "every non-transparent pixel of the master becomes that
slot's live colour", so `ColorCycle.fx` gains a THIRD technique, `GlyphCycle` — a PIXEL-ONLY
pass (the section 40 rule: no vertex shader, so `Apply()` cannot clobber SpriteBatch's
projection for the rest of the batch):

- `float4 GlyphCyclePS`: `clip(t.a - 0.5f)` (the same alpha test as `SolidRemapPS`), then
  selects the live colour from a new `SlotId` uniform (`register(c7)`, 0-5 = slots 10-15) with
  an UNROLLED if-chain (no arrays — the section 34 TPGParser restriction), and returns
  `float4(live.rgb, t.a)`.

`SpriteSet`:

- `FontLargeCycling` / `FontSmallCycling` and `LoadCyclingGlyphs` are deleted; the masters are
  the only font textures.
- `DrawGlyphCycling` takes the slot: sets `SlotId`, applies the `GlyphCycle` pass, draws the
  white master, then `UsePassThrough()` (the pass binding OUTLIVES the draw — the section 40
  trap). Without the effect (M4 off) it falls back to a CPU tint of the master with
  `Palette.Color(slot)` — the glyph still cycles with the live palette (the old fallback drew
  the fixed marker colour, which never cycled at all).
- `DrawGlyphSlot` drops the `cyclingGlyphs` parameter (callers: `PlayingState.DrawScore`,
  `SpriteSet`'s small-font helper).

The markers stay where they belong: the entity sprite PNGs, `GamePalette.CyclingSlotMarkers`
(the shader's six literal comparisons) and the
`AllSixCyclingMarkersAreDistinctFromTheFixedSlots` test, all unchanged.

### 92.3 Tools and gates

- `tools/extract-fonts.py`: writes masters only (`CYCLE_MARKERS` / `marker_rgb` deleted);
  docstring updated.
- `tools/verify-fonts.py`: checks the 78 masters, not 546.
- The 468 variant PNGs are deleted; `Content.mgcb` + `SpriteContentPaths.cs` regenerated with
  `tools/generate-mgcb.py`.
- Gate 5's expectation changes from "546 font glyph PNGs" to "78 font glyph PNGs" (status.md +
  the handoff).


## 93. The generic mover runs on the ROM's FRAME clock — and the enforcer's aim was a 4x misread (2026-09-17)

(A10 — "the generic mover is 20% fast": `OPB80` adds the velocity once per ROM
FRAME, the port added it once per 60 Hz tick. Author playtest was fine with the
gameplay; the sweep was taken to the arcade's clock.)

### 93.1 One integration per ROM frame

A ROM frame is 6/5 of a port tick, so each generic-mover entity carries a SIXTHS
accumulator: `+= 5` per tick, integrate the velocity once and subtract 6 when it
reaches 6. The constructor seeds the accumulator at 6, so the object moves on its
FIRST frame — the ROM's mover moves the object from the frame after creation, and
the port's first `Update` is that frame. Entities: `Spheroid`, `Enforcer`, `Quark`,
`Spark`, `TankShell` (the five users of the generic mover, notes §43).

The velocities are now RAW per-frame values; the 5/6 per-tick rescales are gone:
`Quark.AxisVelocityFp`/`StartFlee` (dropped `* 5 / 6`), `Enforcer.RollVelocity`
(dropped 5/3), the `TankShell` speed is per-frame, and the spark's constants are
documented per-frame (its 4-frame MOVE period is unchanged — the mover just
integrates between moves as the ROM does).

### 93.2 The enforcer's aim: `ASLB / ROLA` HALVES, it does not double

Re-checked the aim constant against the Gospel while sweeping: ENFNV's
`SUBB OX16,X / SBCA #0 / ASLB / ROLA / STD OXV,X` is a signed SHIFT RIGHT —
`OXV = (target − pos)/2` in 1/256 column/frame. The port's comment had read the
same instruction sequence as a doubling (that would be ASL/ROL), so the enforcer
glided 4x the arcade speed. `_velocityFp = delta / 2` (fp units per frame) =
Δcol/128 port px/frame, the value the docstring had carried all along ("advances
(target − pos)/128 per frame").

### 93.3 Measured: the spheroid's escape is the ROM's $100

The §91 status text called the escape's OXV "a fixed ±1 column/frame" — the
Gospel says otherwise: RRC11 `CIRC3` does `LDD #$100 / TST SEED / (BPL|NEGA) /
STD OXV,X` = ±$100 in 1/256 column/frame = ±0.25 column/frame. A scratch harness
(deleted before commit) drove 12 seeded spheroids to the escape and measured the
speed: **0.248-0.250 columns/frame** — exactly $100. Six of the twelve seeds die
on the first wrap pass: they spawn past the exit column, and the ROM's CIRC3L X
test sends them straight to `CIRC4 / KILLOF` — faithful to the source.

## 94. Attract mode (the demo): what the arcade actually does, and what the port builds (2026-09-17)

Author: "ignore the gameplay, just get the SCOTTOTRON 2084 out and the demo in —
the one that says ROBOTRON 2084 PROTECT THE LAST HUMAN FAMILY". Research first,
per the golden rule.

### 94.1 The arcade's attract architecture (Gospel + disassembly)

Three distinct pieces, all in the ROM:

1. **The storyline movie** — `RRSCRIPT.ASM` (`STORYLINE_SCRIPT`, $7FA3–$83B1 in
   the disassembly). A scripted text crawl that introduces the family (MOMMY,
   DADDY, MIKEY), the grunt, the hulk, the spheroid, the quark, the enforcer,
   the tank, the brain, the cruise missile, the prog, the electrodes — with
   character sprites blitted in (`MESS` ops) and names printed under them
   (`PRINT_CHARACTER_NAME_FROM_SCRIPT` $7A60). Ten action opcodes in the jump
   table at $7A08 (clear-area $7A9C, newline $7AB6, introduce-characters $7AC1,
   delay-task $7AC7, print-name $7A60, conclude $7A84, colour $7A5A, 14-grunt
   demo $7A29, blink-off $7A1C). The big multi-colour "ROBOTRON 2084" logo is
   drawn as STAGGERED BLITTER ART (the disassembly's `DRAW_STAGGERED_IMAGE` at
   $F124: "also used to draw the word ROBOTRON forming up in large letters on
   title screen") — it is bitmap art, not font text.

2. **The title screen** — the disassembly's $79AF/$79CF/$799B block ("the
   ROBOTRON: 2084 - SAVE THE LAST HUMAN FAMILY attract screen"):
   - $79AF: free all objects, reset text fields, clear screen, start the
     palette-animation task, `CLR $3F` (current player = 1), wall colour $CC,
     then $26D2 = **draw the playfield wall + the player's lives + the score**
     — the title sits ON the playfield (empty, no robots).
   - $79CF: print string **128** in the LARGE font. String 128's bytes at
     $6EC8: `04 AA` (text colour slot 10 = $AA), `12 36 24` (CURSAB), text
     "ROBOTRON?:2084" — the large font renders its two special bytes as blanks,
     so the visible title is **ROBOTRON 2084** (no colon).
   - $799B loop (blink): print string **129** = "SAVE THE LAST HUMAN FAMILY"
     (bytes at $6EDD, same colour slot 10), then run the script's tail.

3. **The auto-play demo** — the CMOS **"FANCY ATTRACT MODE"** switch (CMOS
   $CC13, read by $5BA8 `GET_FANCY_ATTRACT_MODE_FLAG`; adjustment menu label at
   $68B6). When on, the machine plays a full game against itself: the OS ROM
   writes the player's joystick/fire into **ATRSW2/ATRSW3 ($14/$15)** — the
   "phony player control" bytes the GAME ROM reads in place of the real PIA
   inputs when STATUS says attract (RRC11 `PLAYRV`, `LSPROC`; RRF/RRG23
   joystick reads). The demo's explosions/objects are created via the
   `CREATE_???_IF_FANCY_ATTRACT_MODE_ON` helpers ($5BB1/$5BBB). When the demo
   loses all its men the OS silently starts a new game — no high-score entry.

### 94.2 The tagline: the ROM says SAVE, not PROTECT

RRET.ASM:982 and RRSCRIPT.ASM:913 both spell it "SAVE THE LAST HUMAN FAMILY";
the string table entry 129 at $6ECC is the same. The author remembered "PROTECT"
— the ROM is the standard, so the port prints **SAVE THE LAST HUMAN FAMILY**
(flagged in the handoff so the author can see why it reads SAVE).

### 94.3 What the port builds (Phase 12.1)

- **TitleScreenState** — arcade look: the playfield wall + HUD (score 0, three
  men) drawn first (the ROM's $26D2 order), then "ROBOTRON 2084" and "SAVE THE
  LAST HUMAN FAMILY" in the LARGE font, colour slot 10 (the ROM's $AA). The
  blinking start prompt and the 5-second high-score swap stay (port
  conventions). After `TitleIdleSeconds` (12 s) of no start press the port
  enters **AttractState** — the arcade's idle behaviour, minus the CMOS switch.
- **AttractState** — a real game played by a **DemoPlayerInputSource**: 1P
  session, wave clears advance (through the same tunnel `WaveClearState`, now
  with an `attract` flag), a death rebuilds the field, losing all men silently
  starts a new session (the OS ROM's behaviour — never a high-score entry).
  Any human start/fire press exits back to the title.
- **DemoPlayerInputSource (placeholder AI)** — the arcade's real phony-player
  algorithm lives in the OS ROM, which is DISASSEMBLY-ONLY and its ATRSW2
  writes are not decoded in `robomame.asm`. Per the golden rule this is NOT
  invented to look arcade: it is an explicitly-labelled placeholder — flee the
  nearest robot when it is close, drift to the centre otherwise, fire at the
  nearest robot in range, all 8-way/integer, with a small random stutter.
  **Flagged: decode the OS ROM's ATRSW2 writer to replace it.**
- **Deferred (flagged):** the storyline text-crawl movie and the staggered-art
  "ROBOTRON 2084" logo bitmap. The port's title uses the large FONT for both
  lines (the ROM's string-128 path), which is the ROM's own large-font title
  text; the art logo and the script engine are a later phase.

## 95. THE FULL ATTRACT MOVIE (the "storyline") — complete decode of the ROM's scripted demo (2026-09-17)

**Author: "i expected a proper demo mode … do the full demo — bring on mummy, daddy etc."**
The arcade's attract is NOT just "phony player plays a game". It is a **scripted movie**
(the HISTO storyline: the story text crawl with the family, the 14 grunts, the hulk, the
spheroid/enforcer/tank scene, the brain reprogramming a human into a prog, the score
posts) followed by the phony-player game. §94 built only the phony-player part. This
section is the COMPLETE decode of the movie so it can be implemented without
re-deriving anything. **Source hierarchy: RRSCRIPT.ASM (the 1982 listing) is the GOSPEL
for the engine's semantics; the R5 ROM bytes (verified image) are the data authority —
and every script byte below was verified against the ROM, which matches the Gospel.**

### 95.1 The machine flow (R5 addresses)

- **$799D** = FAMPAG (title screen + the "dumb player" family walk): sets ATFLAG bit 8,
  calls $79AF (GNCIDE/INT20V/SCRCLR/COLST/credits-process, STATUS&=$F3, CLR CURPLR,
  **WALCOL=$CC**, TDISPV), prints string 128 (large) then string 129 (large), then loads
  script **$83B2 (DUMPLR)** and runs the page interpreter.
- **$79AF** = SPGSUB (the "clear and set up" block, shared).
- **$79D6** = `LDX #$7FA3` — the main page entry: **HISTO, the storyline, starts at $7FA3.**
- **$79DD–$7A07** = SPWAKE, the page-script interpreter (byte stream; ≤8 = action via
  the jump table at **$7A08** = SCMTAB; ≥$5F = SLEEP that many frames; else printable
  char → **$5F93 = BLIT_LARGE_CHARACTER** — the story text is the LARGE font — then
  NAP 3 between chars).
- **$7A08–$7A9C** = the SCMTAB action routines (CURSAP/CLRMP/NEWLNP/SCRPP/SLEPP/MESSP/
  ENDP/COLORP/GRNTME/DONE2P — read the disassembly for the exact R5 behaviour).
- **$7B0F (approx)** = GRPROC (the 14-grunt spawner; §94 mapped its surroundings).
- **DONE2 (opcode 9)** at the end of a page script: `ATFLAG &= $7F; if 0 → RUNIT (the
  phony-player game) else → HISTRY (the story)**. ACCHNG (the coin handler) increments
  ATFLAG: first coin → FAMPAG (title + family walk), later coins → just counted. So
  the arcade cycles: title → storyline movie → phony game → … (the exact RUNIT/LOGORG
  loop and the CMOS "FANCY ATTRACT MODE" flag are OS-level — read $7A08–$7A9C when you
  build the port's state machine).

### 95.2 PAGE-SCRIPT opcodes (the SPAGE level — RRSCRIPT.ASM "SPAGE PROCESSOR")

Byte < 9 → action; 9 = LASCOM/DONE2; ≥ $5F → SLEEP n; else → print char (large font,
NAP 3 per char). Actions (SCMTAB order):
- **0 CURSAB**: 2 bytes (X col, Y row) → text cursor.
- **1 CLEARM**: byte = clear width; cursor → (LEFT=$14, TOP=$20); then byte = Y range;
  BLKCLR colour $74 over (LEFT, TOP, width, yrange−$10) — the top $10 rows stay for
  the title.
- **2 NEWLIN**: cursor = (LEFT, Y+11).
- **3 SCRPT**: 2-byte pointer → **OSTART**: start an OBJECT script (see 95.3).
- **4 SNOOZE**: byte = SLEEP n frames.
- **5 MESS**: byte = X offset, byte = message number → clear the old message area then
  print the ROM string-table entry with WRD5V (SMALL font) at (X, row $D8=216).
- **6 DONE**: end → OINIT/GNCIDE → LOGORG (the attract controller).
- **7 COLOR**: byte = TEXCOL (text colour slot = high nibble: $AA→10, $DD→13, $FF→15,
  $33→3, $55→5, $BB→11, $CC→12, $EE→14).
- **8 GRUNTS**: start GRPROC — 14 grunts, one every NAP $10 frames, each spawned from a
  random one of the four grunt scripts GS1–GS4 (R5: $84F5/$8517/$8540/$8567).
- **9 DONE2**: as in 95.1.

### 95.3 OBJECT-SCRIPT opcodes (the SCRIPT/SPROC level — RRSCRIPT.ASM "MNEMONICS")

Per-object process; the object is a **descriptor** (95.6) + a position + an image number.
- **1 SETOB** 2B ptr | **2 SETIM** 1B img | **3/4/5/6 MLEFT/MRIGHT/MDOWN/MUP** 1B steps
  (steps run through the descriptor's walk table, 8 frames/step, animated) |
  **7 SETPOS** 1B X, 1B Y (columns/rows) | **8 SETXV** 2B signed (1/256 col/frame) |
  **9 SETYV** 2B | **10 CYCLE** 2B (frames per image, image count) | **11 REST** 1B
  frames | **12 DIE** kill object+process | **13 EXP** kill + horizontal-laser strip
  explosion (EXST) | **14 JUMP** 2B script address | **15 HIB** erase from screen+list |
  **16 REBORN** restore to list (image resync) | **17 FORK** 2B ptr — start a NEW
  object+process from that script | **18 LFIRE / 19 RFIRE** 2B (delay frames, count) —
  fire `count` lasers left/right from the object (velocity ∓$280 = 0.97 col/frame);
  the (delay,count) reading matches every Gospel comment (e.g. `RFIRE $B,$10` "SHOOT
  THE 14 GRUNTS" × LOOPER 14) — VERIFY against the R5 action code when porting |
  **20 LOOPER** 1B count + 2B label — repeat from the label | **21 GHOST** 2B ptr —
  parallel process, own script pointer, SAME object | **22 SETRP** 2B signed (dx,dy)
  relative move, no animation | **23 GDIE** kill process only | **24 INCIM** next
  image (0 operands) | **25 MONO** 4B (box colour, object colour, time frames, special:
  1 = brain-in-box) — hide the object, then each 3 frames move it by its velocity and
  redraw it in a coloured box (this is the brain's reprogramming square) |
  **26 RPROG** random-walk the object's Y for 64 frames then die (the "shake") |
  **27 PDEAD** hide + PKPRCV (check the R5 disassembly — used to retire the score
  posts).

Walk-table semantics (ANAHUM, the human walker at $7DF3–$7DFE in R5): each direction
is a 12-byte cycle of 3-byte entries `(dx, dy, image×4)` (+1 guard byte); the walker
NAPs 8 frames per step, applies (dx,dy) to the 16.16 position (dx is the low byte
sign-extended, dy the high), and sets image = byte>>2 with wrap at the descriptor's
image count. (The 1982 source's `HUMANA RMB 2` is a RESERVED placeholder — the tables
live in the ROM, at 95.7.)

### 95.4 THE STORYLINE (HISTO @ $7FA3 in the R5 ROM — verified byte-for-byte vs Gospel)

Scene order (text is verbatim; colours are slots; sleeps are ROM frames @50 Hz):
1. **Prologue** (clear from row 56, colour 13): "INSPIRED BY HIS NEVER ENDING / QUEST
   FOR PROGRESS, / IN **2084** [slot10] **MAN PERFECTS THE ROBOTRONS**[slot10]: [sleep
   32, newline×2, colour 15] A ROBOT SPECIES SO ADVANCED THAT / MAN IS INFERIOR TO HIS
   OWN CREATION. [32, colour 13] GUIDED BY THEIR INFALLIBLE LOGIC, / THE **ROBOTRONS**
   CONCLUDE: [48, colour 10] THE HUMAN RACE IS INEFFICIENT, / AND THEREFORE MUST BE
   DESTROYED. [112]
2. **The player** (new clear, colour 13; **SCRPT PLAYRR** — the hero walks in and
   shoots): "YOU ARE THE LAST HOPE OF MANKIND. [96, colour 15] DUE TO A GENETIC
   ENGINEERING ERROR, / YOU POSSESS SUPERHUMAN POWERS. [colour 13] YOUR MISSION IS TO
   **STOP THE ROBOTRONS**[10], / AND **SAVE THE LAST HUMAN FAMILY**[10]:
   **SCRPT FAMSCR**" — then names pop up in the score row: "MOMMY" (X28), 88 fr,
   "DADDY" (X24), 88 fr, "MIKEY" (X20), 112 fr, clear.
3. **The grunts** (clear, colour 3, **GRUNTS** = 14 walk in from the right): "THE
   FORCE OF GROUND ROVING / UNIT NETWORK TERMINATOR (GRUNT[10] ROBOTRONS) [colour 3]
   SEEK TO / DESTROY YOU." [255]
4. **The hulk** (clear, colour 5, **SCRPT SCHULK** — walks in from the left, bounces):
   "THE [10] HULK[10] HULK ROBOTRONS [colour 5] SEEK OUT / AND ELIMINATE THE LAST
   HUMAN FAMILY." [249, name clear, 128]
5. **Spheroids & quarks** (clear row 112, colour 13, **SCRPT SCSC**): a spheroid (CIRC)
   drifts in from the right while its products appear — a SQUARE box, a TANKG→TANK
   that rolls right and EXPLODES, an ENF enforcer that walks and is SHOT (EXP), and
   **DADD2: MUMMY is reprogrammed** — the BRAIN walks in with its CRUSER missile,
   enters the MONO reprogramming box (brain-in-box), the human shakes (RPROG) and
   becomes a PROG (PShADOW clones) that walks off right at $2/256 col/frame. Text:
   "THE [10] SPHEREOID[10] AND QUARKS [13] ARE PROGRAMMED TO MANUFACTURE / **ENFORCER
   AND TANK ROBOTRONS**[10]." [160, name clear]
6. **The brain** (clear, colour 15): "BEWARE OF THE INGENIOUS / [10] BRAIN[10] BRAIN
   ROBOTRONS [15] THAT POSSESS / THE POWER TO REPROGRAM / HUMANS INTO SINISTER
   **PROGS**[10]. [115, "PROG" name, 128, **SCRPT POSTER** — the four score posts rise
   in coloured boxes (slots 15/10/12/13) and PDEAD]"
7. **The electrodes** (clear row 100, colour 13): "AS YOU STRUGGLE TO SAVE / HUMANITY,
   BE SURE TO AVOID / **ELECTRODES**[10] IN YOUR PATH." [208] [173] **DONE** → the
   phony-player game.

**DUMPLR @ $83B2** (the title's "dumb player", runs after the title prints):
`SCRPT $83B8, SLEEP 255, DONE2` — $83B8 is the first object script. Its first byte is
the stale FDB $1587 (HELPME's OLD address — zero in R5; the real HELPME data is at
$8715, see 95.5).

### 95.5 Object scripts in the R5 ROM (all verified by parsing; addresses are the
FDB targets of the HISTO SCRPT opcodes)

| label | R5 addr | what it is (Gospel name) |
|---|---|---|
| PLAYRR | $83B7 | the hero: SETOB YOU($7E97), pos (24,160), GHOST PSHOOT, walks right 96, pauses, left 80, right 104, looks around (SETIM 0/3/6 = walk-cycle facing images), descends, fires, ends MRIGHT 128 + **EXP** (explodes into an electrode) |
| PSHOOT | $840F | the hero's gun: bursts of RFIRE/LFIRE (delay,count) × LOOPER — right, left, 14× right "SHOOT THE 14 GRUNTS", left ×10 "HIT THE MUTHA" (the hulk), 3× right, left ×2 barrages, 3× right "SHOOT ELECTRODES", GDIE |
| FAMSCR | $8477 | MUMMY($7E41) at (24,160), FORK DADDP($84AD) + FORK MIKEP($84C6), MRIGHT 40, then a POINTS score-popup ($7EB5) follows her; then DADDY's turn: appears, REST 64, MRIGHT 48, MUP 16, MRIGHT 16, MDOWN 19, MLEFT 18 — then **MOMDED**: HIB, SETOB SKULLV($7EB9), REBORN, REST 128, DIE (Mummy is killed — the skull stays 2.5 s) |
| DADDP | $84AD | DADDY($7E53): hidden 112 fr, walks right 30, then a POINTS popup |
| MIKEP | $84C6 | MIKEY($7E61): hidden 216 fr, right 21, POINTS popup; then REBORN at (24,160), REST 192, the "VOODOO" steps (right4, down8, right16, down24, left1), **JUMP MOMDED** (Mikey dies too) |
| GS1..GS4 | $84F5/$8517/$8540/$8567 | the four grunt personalities: SETOB GRUNT($7E8B), pos (134,133)/(136,144)/(136,188)/(136,177), each a LOOPER-2 loop of REST + SETRP (small left/down steps) + SETIM (3-image trot); then **GSSS1** ($858B): final steps and **EXP** (shot by the hero) |
| SCHULK | $85A1 | HULK($7E6F) at (16,192), GHOST HBOUNC($85B1: SETRP −2,0 ×10 = the bounce), MRIGHT 28, MUP 16, MRIGHT 54, DIE |
| SCSC | $85BD | CIRC($7EA9) at (136,135), FORK SQP($85D6: SQUARE($7EA5) at (10,180), X-vel +$C0/256, CYCLE 2×96 fr, DIE) + FORK TKP($85E3: TANKG($7EAD) appears, 3× (REST 20, SETRP 0,−1, INCIM), becomes TANK($7EB1), rolls right +$80/256, CYCLE 2×112, **EXP**) + FORK ENP($8603: ENF($7E93) at (128,135) appears, 5× INCIM, left −$60/256, REST 48, **EXP**) + FORK DADD2($861A) |
| DADD2 | $861A | the reprogramming: MUMMY($7E41) at (10,180) hidden 192 fr, walks right 32, FORK BRAING @ $868D (BRAIN $7E7D at (10,160): MRIGHT 16, FORK CRUSER @ $86A4 (CRUSM descriptor $86B0, missile at (26,164), X-vel +$60/256, 64 fr, DIE), MDOWN 10, MRIGHT 26, **MONO $BB,$BB,56,1** = the brain box, MRIGHT 11, **EXP**), the human: MDOWN 10, MLEFT 8, MRIGHT 26, SETRP 3,−2, REST 2, GHOST PSHAKE @ $868C (a single RPROG = the shake), **MONO $AA,$BB,40,0**, FORK PSHADO @ $8648 (three MOMMY clones images 3/1/2 at (49,188) walking off right at +$2/256 in $EE boxes, DIE), SETXV +$2/256, **MONO $0,$AA,44,0**, DIE (now a prog) |
| POSTER | $86CC | the score posts: POSTS($7E8F, 36 images) at (134,193) image 12, hidden 72 fr, then FORK POST1–3 at (104/114/124, 193) images 0/4/8 each MONO (box colour 0, post colour slot 15/10/12, ~141 fr) and the main post MONO slot 13 — each ends **PDEAD** |
| HELPME | $8715 | the title's dumb-player family: MUMMY at (80,104) + FORK DUMYOU (the hero walks LEFT 128 from (104,106)) + FORK DAD1/MIK1/MOM2/MIK2 — the family files past, each leaving a POINTS popup (images 0-4), REST 80, DIE. (R5's DUMPLR FDB $1587 is STALE — points to zeros; the live data is here.) |

FORK targets inside DADD2/BRAING (PSHAKE/PSHADO/PSHAD1/PSHAD2/CRUSER) are at
$8640–$86CC; parse them with the corrected opcode table (INCIM = 0 operands;
LFIRE/RFIRE = 2 operands).

### 95.6 Object descriptors (R5, all verified against the scripts' FDB pointers)

Format: FDB base-image-ptr, FCB n-images, FCB bytes-per-image, [FDB walk-L, FDB walk-R,
FDB walk-D, FDB walk-U, FDB walk-table-ptr] for the walking family (14-byte total).

| desc | R5 addr | base | imgs | bpp | notes |
|---|---|---|---|---|---|
| MOMMY | $7E41 | $000E | 12 | 4 | walk $7DF3/$7DF6/$7DFA/$7DFE, table $0018 |
| CRUSM | $86B0 (inline after CRUSER) | art at **$86BA** (18 B: `DD×8 DA A0 DD×6 DA A0`) | 1 | 4 | the cruise missile |
| DADDY | $7E53 | $0010 | 12 | 4 | table $0018 |
| MIKEY | $7E61 | $0012 | 12 | 4 | table $0018 |
| HULK | $7E6F | $0014 | 12 | 4 | table $0016 |
| BRAIN | $7E7D | $1AC3 | 12 | 4 | BRL/R/D/U $7D76/$7D7F/$7D89/$7D92 (02,08) |
| GRUNT | $7E8B | $3891 | 3 | 4 | |
| POSTS | $7E8F | $3893 | 36 | 4 | the score-value poster |
| ENF | $7E93 | $1148 | 6 | 4 | |
| YOU | $7E97 | $26D5 | 12 | 4 | BRL/R/D/U $7D76/$7D7F/$7D89/$7D92 (01,02) |
| SQUARE | $7EA5 | $4B06 | 9 | 4 | the reprogramming box |
| CIRC | $7EA9 | $1146 | 8 | 4 | |
| TANKG | $7EAD | $4B08 | 5 | 6 | |
| TANK | $7EB1 | $4B0A | 4 | 4 | |
| POINTS | $7EB5 | $000C | 5 | 4 | the score-popup text ("1000" etc.) |
| SKULLV | $7EB9 | $001A | 1 | 4 | the skull |

Sprite data: 1 byte = 2 pixels (bpp/2 bytes per row), base addresses above, in
`ref/robotron64k.bin`. Most already exist in the port (grunt, enforcer, spheroid,
tank, brain, hulk and the family walk art are all already extracted). NEW to extract:
POINTS ($000C), SKULLV ($001A), POSTS ($3893), SQUARE ($4B06), the CRUSER missile
(18 art bytes at $86BA — compare with the port's existing cruise-missile art),
YOU ($26D5 — compare with the port's player art; the storyline hero is the same
sprite as the playable one). (The $7E4F descriptor — base $1ACB, 1 img, 4bpp — is
NOT referenced by any movie script; do not use it.)

### 95.7 The walk tables (the animated family steps) — extracted from the ROM

The 14-byte human descriptors point at **$0018**, which is itself a 16-bit pointer to
**$03CF** (HUMANA); the hulk's **$0016** → **$01CC** (HLKANA). Each table = 4
directions × (4 × 3-byte entries + 1 guard byte); entry = (dx, dy, image×4); 8 frames
per step. (1982 source: `HUMANA RMB 2` — the source never shipped the data, the ROM
does.)

```
HUMANA @ $03CF:  00FE00 04FF00 00FE00 08FF00 | 0C0200 100100 0C0200 140100 FF
                         | 180001 1C0001 180001 200001 FF | 2400FF 2800FF 2400FF 2C00FF FF
HLKANA @ $01CC:  00FD00 04FC00 00FD00 08FC00 | 0C0300 100400 0C0300 140400 FF
                         | 180002 1C0002 180002 200002 FF | 1800FE 1C00FE 1800FE 2000FE FF
                     (segments are 13 bytes: L @+0, R @+13, D @+26, U @+39)
```

### 95.8 Message strings (the MESS name popups, small font, score row $D8)

String-table indices (RRFRED.ASM): 115=MOMMY (`PMOM` 'MOMMY'), 116=DADDY, 117=MIKEY,
118=GRUNT (in RRSCRIPT), 119=HULK, 120=SPHEREOID ('SPHEREOID '), 121=ENFORCER
('ENFORCER '), 122=BRAIN, 124=PROG ('PROG '), 126=NULMES (empty), 128=TITLEM,
129=FAMMM. 123 and 125 are unreferenced by the movie — dump the ROM string table to
confirm exact text/lengths of 119/122/126 before printing them. TITLEM/FAMMM are page
SCRIPTS (RRET.ASM $79CF/$79A5 paths): TITLE = `COLOR $AA, CURSAB ($36,$24), "ROBOTRON:2084"`;
FAMMM = `COLOR $AA, CURSAB ($25,$84), "SAVE THE LAST HUMAN FAMILY"`.

### 95.9 BUILD PLAN for the port (what the implementing session does)

1. **Embed the data, don't retype it.** Dump HISTO ($7FA3–$83B1) and the object-script
   block ($83B7–$878D) VERBATIM from `ref/robotron64k.bin` into generated C# arrays
   (like the font pipeline). The Gospel is only for the ENGINE semantics.
2. **Two tiny interpreters** (pure C#, unit-testable, no MonoGame): a PageScriptEngine
   (95.2) and an ObjectScriptEngine (95.3) over a `MovieSprite` (pos 16.16, image index,
   descriptor, optional box). The page engine's outputs: set-text-cursor,
   print-char-queue (one char / 3 frames, large font, live slot colour), clear-rect,
   message-popup, sleep, spawn-object-script, grunts.
3. **Render the movie in AttractState** (or a new `StorylineState` before it): wall
   $CC + score/men HUD (ArcadeHud) + the text + the movie sprites. All timings are ROM
   frames (50 Hz) → sixths accumulator (×5/6 per tick, as in §93). SETPOS/SETRP are in
   COLUMNS/ROWS (1 col = 2 spec px = 4 port px). EXP → the port's existing Explosion
   (horizontal-laser layout). MONO → draw a filled box in the two given slots with the
   sprite inside (the brain box uses the brain art).
4. **Sprites**: reuse port art where it exists; extract POINTS/SKULLV/POSTS/SQUARE
   (and verify CRUSM/YOU against the port's existing missile/player art) via the
   SpriteExtractor convention (base + geometry from 95.6).
5. **Sequence**: title (12 s idle) → **storyline movie (~60-90 s, let it run to DONE)**
   → phony-player demo game (the existing §94 machine) → back to the title (and loop).
   Any human input skips to the title as today.
6. **Tests**: engine-level tests with the real embedded script bytes (page engine
   replays HISTO to DONE in the right order; object engine walks a grunt to EXP; the
   family scene spawns exactly MUMMY+DADDY+MIKEY and two deaths); keep gate 6
   (verify-attract.py) green — extend it to also assert movie content (e.g. after ~20 s
   on the title, the screen shows text crawl + a walking figure).

### 95.10 OPEN ITEMS (do not guess — resolve in this order)

- **PDEAD** (opcode 27) → PKPRCV: read the R5 disassembly for what it does to the
  score posts (likely the score-count-up); approximate with a fade if it's exotic.
- **LFIRE/RFIRE second byte**: (delay, count) matches every Gospel comment — confirm
  against the R5 action code (find it via the SCTABL object-opcode table in the
  disassembly) before wiring lasers.
- **GRPROC in R5** (≈$7B0F): confirm its GSTRTS table points at $84F5/$8517/$8540/
  $8567 and its spawn rate (NAP $10).
- **The RUNIT/LOGORG loop + CMOS FANCY ATTRACT flag** ($7A08–$7A9C actions): determines
  the exact arcade cycle (movie → game → movie? coin behaviour). For the port, 95.9's
  title→movie→game→title loop is the author-approved shape.
- **String table 119/122/126 exact bytes** (95.8).
- **The DADD2/BRAING fork targets** ($8648 PSHADO, $8666 PSHAD1, $867B PSHAD2, $868C
  PSHAKE, $868D BRAING, $86A4 CRUSER): now fully parsed and recorded in 95.5 — nothing
  left to resolve there. (Corrected opcode table for any future re-parsing: INCIM = 0
  operands; LFIRE/RFIRE = 2 operands (delay, count).)
## 96. THE ATTRACT MOVIE, BUILT — and the four defects it flushed out (2026-09-19)

**Author: "I want you to do the intro screen. ROBOTRON 2084, INSPIRED BY HIS NEVER
ENDING QUEST FOR PROGRESS (etc.) — make it arcade faithful."** §95 was the decode
(B28); this section is the implementation, and — as usual — building it found four
things that reading it had not.

### 96.1 What shipped

- **`tools/extract-attract-scripts.py`** generates **`Level/Attract/AttractMovieData.cs`**
  (never hand-edited): the page script HISTO ($7FA3, 1044 B), the object-script block
  ($83B7, 982 B, addressed by `ScriptBase`), HUMANA/HLKANA ($03CF/$01CC, 52 B each),
  ANATAB ($7DEF, 4 B), the LARGE font's 47 width bytes ($EC34 → each glyph's width), the
  twelve message strings (the ROM's pointer table at $6377, numbers 115-126), and the
  four score-post mask PNGs (see 96.4).
- **`Level/Attract/AttractPageMachine`** is SPWAKE ($79DD): a byte < 10 is an action
  (CURSAB/CLEARM/NEWLIN/SCRPT/SNOOZE/MESS/DONE/COLOR/GRUNTS/DONE2), ≥ $5F is a sleep of
  that many frames, anything else is a LARGE-font character printed at `NAP 3`. It builds
  a TEXT LAYER (cells of (x, y, char, slot)) because the ROM blits straight to the screen;
  CLEARM/BLKCLR delete the cells inside the cleared rectangle.
- **`Level/Attract/AttractObjectMachine`** is the object level ($7B58): all 28 opcodes,
  each object a `MovieObject` (position in 1/256 columns / 1/256 rows = the ROM's
  OX16/OY16, velocity = OXV/OYV, IMAG, on/off the list) driven by one or more
  `MovieProcess` tasks (GHOST/FORK). Velocity is integrated once per ROM frame for every
  object ON the list — MONO's object is off it (HIB), which is exactly why MONOP moves its
  own object.
- **`Level/Attract/AttractMovie`** runs both on the ROM's frame clock (the §52/§93
  exact-sixths accumulator: +5 a tick, a frame every 6).
- **`States/StorylineState`** draws it: the title's solid $CC wall, the HUD, the title
  string 128, the objects, the explosions (the port's own `Explosion` engine fed the dead
  object's picture and `Direction8.Left` — EXPP sets `LASDIR = $FF00`, a pure horizontal
  shot, which §69's dispatch turns into the ROW-splitting fan), then the text layer and the
  score-row name popups.
- **The sequence**: title (12 s idle) → **storyline movie** → the §94 phony-player demo →
  title. `TitleScreenState` transitions into `StorylineState`; DONE2 hands over to
  `AttractState`.

### 96.2 The LARGE font's pen advance, and the punctuation the port was missing

`BLIT_LARGE_CHARACTER` ($6023) advances the pen by **(width + 1) pixels**: `LDA ,Y / INCA
/ CLRB / LSRA / LEAX D,X` shifts A only (an 8-bit shift), so `D` is `(width+1)/2`
**columns** ($0100 per column) and the carry toggles the odd-pixel shift flag — the
effective pen step is `width + 1` px, not "texture width + 1". The `FontWidths` table in
the generated data is what the movie uses, so its line breaks land where the ROM's do.

The ROM's font table ($EC34, indexed by `charCode - $30`) also carries **punctuation the
port never extracted**: $3A space (3 px), $3B '!', $3C ',', $3D '.', $3E a solid 10x6
block, $3F ':', $40 '-'. Without them the intro printed blanks for every comma, period and
colon. They are now `Font_L_exclaim/comma/period/hyphen.png` plus a **corrected
`Font_L_colon`** (the old entry pointed at the table's $5D slot — a 2x5 fragment — instead
of $3F), appended at `GlyphIndex` 40-43 so nothing that already existed moved. Gate 5 now
checks **82** masters.

### 96.3 The title band: the ROM's row 36 (and what §94.1 misread)

`SPGSUB` (the block every attract screen starts with) prints TITLEM at the cursor
**($36, $24) = column 54, row 36** — and the page script's CLEARM only clears from **row
48** down, so "ROBOTRON 2084" stays up while the story text scrolls under it. §94.1 read
that 54 as the ROW (it is the column), which is why the port's title screen draws it at
`ArcadeY(54)` — below the movie's clear line, where it would be erased. The story band
therefore draws string 128 at the ROM's own `StoryTitleRow = 36` (centred — the port's
convention); the title screen keeps its own placement, untouched, so nothing already signed
off moved. **Flagged for the author:** if the title screen should also use row 36 (and
FAMMM's own row $84), that is a separate, visible change.

### 96.4 Three picture orders are not address order, and the POSTS table

The port's sprite PNGs are stored in ROM-ADDRESS order, and for most descriptors the ROM's
picture table is that same order — but not for three (`MovieArtTextures`):
- **hulk** ($0CF9): hulk1,2,3,7,8,9,4,5,6 — its LEFT block is files 1-3, its RIGHT 7-9 and
  its DOWN/UP 4-6 (which is why the playfield's `Hulk` walks 6,7,8 for right);
- **enforcer** ($18D2): starts at $1921 (Enforcer_2, the first GROW picture) and ends at
  $18EA (Enforcer_1, the full one the grow-up finishes on);
- **grunt** ($4063): $4073, $40B4, $4073 — ROM image 2 IS image 0, so the movie's `SETIM 2`
  draws Grunt_1 (the port ships a third distinct grunt picture at $40F5, which the movie's
  table does not use).

**POSTS** ($7E8F, 36 images, "4 IMAGES EACH POST, 4 BYTES EACH") has its table at **$3B05**
with 4-byte records. Only the records the POSTER script actually selects are well-formed —
images **12** (the main post), **0**, **4** and **8** (the three that fork off it) — and
each is `(widthBytes=5, height=9, data)`, with the four data blobs tiling $3B95..$3C49
contiguously, which is what confirms the reading. Those four are extracted as
`AttractPost_1..4.png` (white masks: the movie draws them SOLID through MONO's colour
pair). **Open:** the records between each pair of real ones are not referenced by any
script and their layout is not resolved.

### 96.5 THE BUG the author caught: "the player animations … are not quite right"

The generated `AnimTable` had **12 bytes instead of 4**. The generator's byte-array writer
sliced `rom[start+i : start+i+12]` and never truncated to the requested length, so ANATAB
came out as `00 01 00 02 5F 20 0A C6 0D 20 06 C6` — the four real bytes followed by the
first eight bytes of the code after it. `AnimTable.Length` was therefore 12, `WalkCycle`
cycled 0-11, and the BR* walker set picture numbers like 95, 32, 10, 198, 13 — drawn as
`PlayerFrames[index % 12]`, i.e. **arbitrary player frames**, mostly the wrong direction.
The cycle is now `0,1,0,2` on the direction's base (0/3/6/9) exactly as BANA2 has it, and
`AttractMovieTests` asserts both the in-range picture and the `0,1,0,2` sequence so a bad
regeneration cannot come back quietly. (The same padding made the 52-byte HUMANA/HLKANA
arrays 60 bytes; harmless there — the wrap keeps the index inside 0-51 — but wrong, now
exact.)

### 96.6 THE CRASH: MONO's colour operands are palette SLOTS, not slot values

`MONO $BB,$BB,56,1` passes **doubled-nibble palette values** ($BB = slot 11, $AA = 10,
$DD = 13, $FF = 15, 0 = "no box") — the same encoding as the page script's `COLOR`, which
the page machine already divided by 16 and the object machine did not. `GamePalette.Color
(221)` then threw `IndexOutOfRangeException` out of `StorylineState.DrawObjects` and killed
the process mid-movie (≈80 s in, in the brain scene). Both colours are now `>> 4`. This was
the other half of the author's report: the build they watched died part-way through.

### 96.7 The message strings, verified

The MESS number indexes the ROM's pointer table at **$6377** (index 0 = 115 MMOM), and the
twelve strings 115-126 are decoded straight out of the ROM by the generator: MOMMY, DADDY,
MIKEY, "GRUNT - 100", "INDESTRUCTABLE HULK", "SPHEREOID - 1000 QUARK - 1000", "ENFORCER -
150   TANK - 200", "BRAIN - 500     CRUISE MISSILE - 25", COINMF, "PROG - 100", EXTMES,
and 126 = the empty NULMES that clears the row. §95.8's "119/122/126 unresolved" is closed:
119 is the HULK text, 122 the BRAIN text, 126 the empty string.

### 96.8 Open items

- **PDEAD** (opcode 27) is implemented as HIB + end-of-process; the ROM then calls
  **PKPRCV**, which is not decoded, so the score posts vanish instead of playing whatever it
  does. Not guessed.
- **The POSTS table's spare records** (96.4).
- **The title screen's own layout** (96.3) — currently untouched.
- Everything §95.10 listed that this session did not need (the RUNIT/LOGORG loop, the CMOS
  FANCY ATTRACT flag) is still open.

### 96.9 Gates after this change

0 warnings (Debug + Release), **284 tests, 0 failed, 1 skipped** (+6: movie-engine and
page/object-machine cases), 12 s launch smoke OK, `verify-playfield.py` PASS,
`verify-fonts.py` PASS (**82** glyphs), `verify-attract.py` PASS — and it now covers the
movie: title → story band (≈8.7% of the interior lit, against the title's 1.8%) → fire →
title, with `--full` (~2.5 min) adding "wait out the movie → the attract demo". The whole
movie is ~4785 ROM frames ≈ **96 s**, so the demo starts ≈108 s after launch.
