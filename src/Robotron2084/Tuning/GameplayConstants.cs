using Microsoft.Xna.Framework;
using Robotron2084.Core;

namespace Robotron2084.Tuning;

/// <summary>
/// The gameplay tuning constants: every tunable the entities, the playfield and the HUD
/// read, together with the values decoded from the arcade ROM.
/// </summary>
/// <remarks>Each constant's own summary gives its source — a ROM routine, a measurement
/// or the author's own tuning.</remarks>
public static class GameplayConstants
{
    /// <summary>
    /// Converts a delay in ROM frames to the equivalent number of port ticks, so every timer in
    /// this port waits for the same real-world length of time the arcade did.
    /// </summary>
    /// <remarks>Truncates, so it is one tick early for a period that does not divide evenly — see
    /// <see cref="ArcadeClock"/> for the clock itself and <see cref="PortTicksCeil"/> for the
    /// first tick a period actually fires on.</remarks>
    public static int PortTicks(int romTicks) => romTicks * ArcadeClock.UnitsPerRomFrame / ArcadeClock.UnitsPerPortTick;

    /// <summary>
    /// The FIRST port tick on which a <paramref name="romFrames"/>-period clock running on the
    /// clock units described on <see cref="ArcadeClock"/> fires:
    /// <c>ceil(romFrames * 6 / 5)</c>. Use this in tests that tick to a boundary —
    /// <see cref="PortTicks"/> TRUNCATES, so it is always one tick too early for a short
    /// period (<c>PortTicks(3)</c> = 3, but the step actually lands on tick 4).
    /// </summary>
    public static int PortTicksCeil(int romFrames) => (romFrames * ArcadeClock.UnitsPerRomFrame + 4) / ArcadeClock.UnitsPerPortTick;

    // Level generation
    public const int EnemySpeedBonusCapPerLevel = 5;
    public const int SpawnPlacementMaxAttempts = 100;

    // Wall
    public const int WallStepDurationMilliseconds = 150;

    /// <summary>Default 4-colour cyan cycle, dim to bright — never fully dark (spec-stated rule).</summary>
    public static readonly Color[] DefaultWallPalette =
    {
        new(0, 80, 80),
        new(0, 140, 140),
        new(0, 200, 200),
        new(0, 255, 255),
    };

    // Player
    // R5 MOVE_PLAYER ($2FD0) deltas: vertical = 1 arcade px/tick; horizontal
    // = 0.5 arcade px/tick (X is 15.1 fixed point: FF/01 -> ASRA/RORB half
    // step). Diagonals apply BOTH, unnormalised. Scaled to the port's
    // 640x400 screen (3.33x / 2.5x) and rounded: 2 / 3 screen px per port
    // tick ~= arcade field-crossing times (6.4s X / 2.67s Y).
    public const int PlayerSpeedX = 2;
    public const int PlayerSpeedY = 3;

    // PLAYER DEATH (notes §66) — RRX7.ASM `PDTHV` ("GENIES BITCHEN 330AM PDEATH").
    // The player is drawn as a SOLID silhouette ($99 = slot 9) for 2 frames and
    // then in a RANDOM colour from PDCTAB ($00,$11,$33,$77 -> slots 0,1,3,7) for
    // 6 frames, repeated `LDA #10` = 10 times; then the colour processes are
    // restarted, the DECAY process (slot 12) is STOPPED, and the fade table is
    // written into slot 12 one byte per `NAP 4` while the player keeps being drawn
    // solid in slot 12 — the trailing $00 ends it and erases the player.
    // Total: 10 x (2 + 6) = 80 frames of flash, then 7 gaps of 4 frames across the
    // 8-byte fade table = 108 ROM frames (129.6 port ticks).
    public const int PlayerDeathFlashIterations = 10;
    public const int PlayerDeathWhiteRomFrames = 2;
    public const int PlayerDeathColourRomFrames = 6;
    public const int PlayerDeathFadeRomFrames = 4;
    public const int PlayerDeathWhiteSlot = 0x09; // $99
    public const int PlayerDeathFadeSlot = 0x0C;  // $CC = slot 12 (the DECAY slot)

    /// <summary>ROM `PDCTAB`: FCB $00,$11,$33,$77 — doubled-nibble slots 0, 1, 3, 7.</summary>
    public static readonly int[] PlayerDeathFlashSlots = [0x00, 0x01, 0x03, 0x07];

    /// <summary>ROM `PD2TAB`: the slot-12 fade, FF F6 AD A4 5B 52 09 00 — the last byte ends it.</summary>
    public static readonly byte[] PlayerDeathFadeValues = [0xFF, 0xF6, 0xAD, 0xA4, 0x5B, 0x52, 0x09, 0x00];

    public const int PlayerStartGraceSeconds = 2;
    public const int PlayerInvincibilityTicks = 120;
    public const int InvincibilityFlickerVisibleTicks = 4;
    public const int InvincibilityFlickerHiddenTicks = 2;
    public const int HitStopTicks = 10;

    /// <summary>
    /// TEMPORARY playtest aid: the player cannot be
    /// killed at all, so new robot types (brains/progs/missiles) can be
    /// playtested across whole waves. TURN THIS OFF (and re-verify the
    /// gates) once the gameplay is confirmed working.
    /// </summary>
    public static bool PlayerInvincibleForTesting => true; // flip to false when playtesting is done (a property, not const, so the guard below doesn't fold to unreachable)

    /// <summary>
    /// A press fires immediately (the arcade's rising edge) and HOLDING the fire
    /// button re-fires every this many ticks; the 3-laser SLOT cap (LaserSlots)
    /// is the real binding limit.
    /// </summary>
    public const int PlayerAutoFireTicks = 12;

    // Player laser
    public const int LaserSpeed = 12;

    // Electrode
    // Post/electrode IMAGE per wave — RRG23.ASM `GTWCOL` ("GET WALL COLOR",
    // which sets all four per-wave values): `LDU #WCTAB / LDA PWAV,X / DECA`,
    // wrap the wave into 1..10 (`GTWL CMPA #9 / BLS / SUBA #10`), then
    // `LDB 20,U / LDX PSTP1 / ABX / STX PSTANI` — the POST IMAGE offset table
    // (the third of the wave colour tables) with $10 bytes per family of
    // 3 pictures. So the post's picture family is WAVE-DEPENDENT and repeats
    // every 10 waves: offsets $00,$10,$20,$30,$40,$50,$70,$80,$00,$60 →
    // families 0,1,2,3,4,5,7,8,0,6.
    // The 27 electrode pictures in sprite-list order are 9 families x 3 frames
    // — frame 0 = alive, frames 1-2 = the shrivel steps (RRP8 `PKPROC`).
    public static readonly int[] PostFamilyByWaveMod10 = [0, 1, 2, 3, 4, 5, 7, 8, 0, 6];

    /// <summary>How many pictures make up one post picture family (alive + 2 shrivel frames).</summary>
    public const int PostPicturesPerFamily = 3;

    /// <summary>
    /// The post/electrode picture family for a wave (RRG23 `GTWCOL`; the ROM
    /// wraps the wave into 1..10, so the pattern repeats every 10 waves).
    /// </summary>
    public static int PostFamilyForWave(int wave) => PostFamilyByWaveMod10[(wave - 1) % PostFamilyByWaveMod10.Length];

    /// <summary>
    /// The POST COLOUR per wave — the SECOND of RRG23.ASM's four per-wave tables
    /// (`GTWCOL`: `LDA 10,U / STA PSTCOL`), indexed by (wave-1) mod 10 like the
    /// rest. These are PALETTE SLOTS written in the arcade's doubled-nibble form
    /// ($XX = two pixels of slot X, because the video is 4bpp with 2 pixels per
    /// byte and a SOLID fill needs both nibbles equal) — so the value's LOW NIBBLE
    /// is the slot. Use <see cref="PostSlotForWave"/>.
    ///
    /// Decoded: wave 1 = $FF = slot 15, 2 = $EE = 14, 3 = $BB = 11, 4 = $DD = 13,
    /// 7 = $11 = slot 1 = RED (the ROM's own CRTAB: slot 1 = $07), 10 = $AA = 10.
    /// The ROM makes the post a SOLID silhouette in this slot's live colour
    /// (`PSTKON` → `OPON1` → `MPCTON`, blitter op $1A), so slots 10-15 — the
    /// colour-cycling ones — make that wave's posts cycle (notes §47).
    /// </summary>
    public static readonly byte[] PostSlotByWaveMod10 =
    [
        0xFF, 0xEE, 0xBB, 0xDD, 0xEE, 0xFF, 0x11, 0xBB, 0xDD, 0xAA,
    ];

    /// <summary>The post/electrode palette slot for a wave (RRG23 `GTWCOL`; wraps every 10 waves).</summary>
    public static int PostSlotForWave(int wave) => PostSlotByWaveMod10[(wave - 1) % PostSlotByWaveMod10.Length] & 0x0F;

    /// <summary>
    /// The WALL/BORDER colour per wave — the FIRST of RRG23.ASM's four per-wave
    /// tables (`GTWCOL`: `LDA ,U / STA WALCOL`), indexed by (wave-1) mod 10. Same
    /// doubled-nibble form as <see cref="PostSlotByWaveMod10"/> ($XX = two pixels
    /// of slot X, because the video is 4bpp and the ROM's `BORDER` fills the
    /// border by storing ONE byte per step, which must be solid) — so the value's
    /// LOW NIBBLE is the slot. Use <see cref="WallSlotForWave"/>.
    ///
    /// Decoded against the ROM's CRTAB defaults (see <c>GamePalette.DefaultSlots</c>):
    /// wave 1 = $22 = slot 2 = $17 = ORANGE, 2 = $55 = slot 5 = $3F = YELLOW,
    /// 3 = $11 = slot 1 = $07 = RED, 4 = $EE = slot 14 = a CYCLING slot,
    /// 5 = $77 = slot 7 = $C0 = BLUE, 6 = $33 = slot 3 = $C7 = MAGENTA,
    /// 7 = $44 = slot 4 = $1F = YELLOW (dimmer), 8 = $88 = slot 8 = $A4 = PURPLE,
    /// 9 = $00 = slot 0 = $00 = **BLACK** (the wave-9 border is genuinely
    /// invisible — the ROM means it), 10 = $CC = slot 12 = a CYCLING slot.
    ///
    /// VERIFIED 2026-09-17 against `ref/original-source/RRG23.ASM` `BORDER`
    /// (`LDA WALCOL` → `STA ,X+` … `STA (XMAX-XMIN)*$100+$200,X`, one byte per
    /// step, so the border is a SOLID fill of that slot's live colour).
    /// CAVEAT: the ROM's colour processes also re-write slots during play, so the
    /// border may shift shade within a wave — that machinery is NOT modelled yet
    /// (see notes §63).
    /// </summary>
    public static readonly byte[] WallSlotByWaveMod10 =
    [
        0x22, 0x55, 0x11, 0xEE, 0x77, 0x33, 0x44, 0x88, 0x00, 0xCC,
    ];

    /// <summary>The wall/border palette slot for a wave (RRG23 `WALCOL`; wraps every 10 waves).</summary>
    public static int WallSlotForWave(int wave) => WallSlotByWaveMod10[(wave - 1) % WallSlotByWaveMod10.Length] & 0x0F;

    /// <summary>
    /// The LASER-WALL COLLIDE colour per wave — the FOURTH of RRG23.ASM's per-wave
    /// tables (`GTWCOL`: `LDA 30,U / STA LASCOL`), indexed by (wave-1) mod 10.
    /// RRF.ASM names the variable: `LASCOL RMB 1 LASER WALL COLLIDE COLOR` — it is
    /// NOT the laser's own colour; it is the FLARE painted where a laser runs off
    /// the playfield. `LASDIH`/`LASDIV` paint the end pixel(s) in LASCOL for 2
    /// frames, then in WALCOL for 1 frame, then leave the wall colour.
    /// Use <see cref="LaserWallSlotForWave"/>.
    ///
    /// Decoded: wave 1/3/5/6/7/10 = $99 = slot 9 = $FF = WHITE, 2 = $00 = slot 0 =
    /// BLACK (that wave's flare is invisible — faithful), 4 = $66 = slot 6 = $38 =
    /// GREEN, 8 = $11 = slot 1 = $07 = RED, 9 = $AA = slot 10 = a CYCLING slot.
    /// </summary>
    public static readonly byte[] LaserWallSlotByWaveMod10 =
    [
        0x99, 0x00, 0x99, 0x66, 0x99, 0x99, 0x99, 0x11, 0xAA, 0x99,
    ];

    /// <summary>The laser-vs-wall flare palette slot for a wave (RRG23 `LASCOL`; wraps every 10 waves).</summary>
    public static int LaserWallSlotForWave(int wave) => LaserWallSlotByWaveMod10[(wave - 1) % LaserWallSlotByWaveMod10.Length] & 0x0F;
    public const int ElectrodeMinDistanceFromPlayer = 40; // spec-px (apply ScreenSize.Scaled at the use site)

    // Grunt
    public const int GruntMinDistanceFromPlayer = 20; // spec-px, spec-stated

    // Hulk — ROM RRH11: step period comes from the
    // wave table (HLKSPD, in ROM ticks); step sizes are fixed by the ROM
    // animation table (horizontal 3/4 arcade px alternating, vertical 2).
    // Laser knockback is the ROM RRH11 HULKIL per-axis random
    // push — X ±1/±2 arcade px (50/50), Y ±1/±4 (25/75); see
    // Hulk.ApplyKnockback (the magnitudes are ROM-intrinsic, no constant).
    public const int HulkMinDistanceFromPlayer = 35; // spec-px (spec gives a 30–40 range; 35 = midpoint)

    // Spheroid (re-decoded from RRC11 CIRCLE/CIRNAC/CIRGO in
    // notes §56). There is NO constant speed: the spheroid accumulates a random
    // acceleration and damps it by a 64th every beat, so its speed is emergent.
    // The clamps below are the ROM's own velocity limits ($0100 / $0200 = 1
    // column/frame and 2 rows/frame, the same 2 arcade px/frame).
    public const int SpheroidBeatRomFrames = 3; // `NAP 2` + 1
    public const int SpheroidMaxVelocityXSubpixels = 0x0100; // 1/256-column units per frame
    public const int SpheroidMaxVelocityYSubpixels = 0x0200; // 1/256-row units per frame
    // CIRC3L's exit test: `CMPA #XMIN+3` / `CMPA #XMAX-10` with XMIN=7 and
    // XMAX=$8F (RRF.ASM:69-70), i.e. column 10 / column 133 of the video buffer.
    public const int SpheroidEscapeExitLeftColumn = 10;
    public const int SpheroidEscapeExitRightColumn = 133;
    public const int SpheroidMinDistanceFromPlayer = 100; // spec-px, spec-stated
    public const int SpheroidNearWallBiasPercent = 70;
    public const int SpheroidNearWallBiasDistance = 30; // spec-px

    // Enforcer (R5 retune, notes §17)
    public const int GlobalActiveSparkCap = 20; // R5 $1412: $14 (20) sparks max

    // Quark — from the GOSPEL (RRTK4 `SQUARE` + `SQVEL`; notes §43,
    // §51). The quark DRIFTS: it is not waypoint-seeking and its speed is not
    // proportional to any distance.
    //
    //   SQVEL:  OXV = ±RND(1..SQSPD) × 4   and   OYV = ±RND(1..SQSPD) × 8
    //           in 1/256-px-per-FRAME units, so Y is twice X per unit.
    //           SQSPD is a wave-table value (50/56/60).
    //   sign:   flipped away from the walls FIRST — X <= XMIN+5 → +,
    //           X >= XMAX-12 → -, Y <= YMIN+5 → +, Y >= YMAX-20 → - — and only
    //           otherwise taken from the seed bit (X: set = negative, Y: set =
    //           positive; the opposite polarity decorrelates the axes).
    //   beat:   NAP 3, and PD7 counts down in BEATS to the next SQVEL.
    public const int QuarkBeatRomTicks = 4;      // NAP 3 + the beat vblank
    public const int QuarkVelocityXScale = 4;    // ROM: two ASLB/ROLA pairs
    public const int QuarkVelocityYScale = 8;    // ROM: three
    public const int QuarkSubpixelsPerPixel = 256; // 16-bit world coordinates
    public const int QuarkReaimMaxBeats = 32;   // ROM PD7 = (SEED & $1F) + 1
    public const int QuarkWallMarginLowArcadePixels = 5;     // XMIN+5 / YMIN+5
    public const int QuarkWallMarginRightArcadePixels = 12;  // XMAX-12
    public const int QuarkWallMarginBottomArcadePixels = 20; // YMAX-20
    public const int QuarkFleeVelocityRom = 0x0200; // SQ3: OXV = 0, OYV = ±$200 per frame
    public const int QuarkFleeExitLowArcadePixels = 2;   // SQ3L: Y <= YMIN+2 ...
    public const int QuarkFleeExitHighArcadePixels = 16; // ... or Y >= YMAX-16 → gone
    public const int QuarkTravelFrames = 5;  // SQP0..SQP4 while wandering
    public const int QuarkTotalFrames = 9;   // SQP0..SQP8 once it starts dropping tanks

    // $4CAC (`TNKDRP`): the tank's blitter address = the quark's + `ADDD #$0206`,
    // i.e. **+2 COLUMNS and +6 ROWS — not +2 px**. A column is 2 px (notes §53),
    // so the X offset is 4 arcade px = 8 screen units. The ROW is decremented
    // first UNLESS the quark sits exactly on the
    // top wall (`CMPB #YMIN / BEQ TNKDP1 / DECB`), so a tank normally lands 5 rows
    // below its quark and 6 on the top wall.
    public const int TankBirthOffsetX = 8;   // +2 columns = 4 arcade px
    public const int TankBirthOffsetY = 12;  // +6 rows (quark on the TOP wall)

    /// <summary>ROM `TNKDRP`: +5 rows on the `DECB` path (quark not on the top wall).</summary>
    public const int TankBirthOffsetYOffTopWall = 10;

    /// <summary>
    /// ROM `MTANK`: each grow picture's own (dx,dy) — descriptor bytes 4 and 5 of
    /// `MTNKP1..4`, in COLUMNS and ROWS. `MTANK` adds the CURRENT picture's delta
    /// to the object's address and only then advances the pointer, so the mini tank
    /// walks up-left as it grows and the full 14x16 tank lands centred on the drop
    /// point (a total of -2 columns, -6 rows).
    /// </summary>
    public static readonly (int Columns, int Rows)[] TankGrowDeltas =
    [
        (-1, -1),   // MTNKP1  $FF,$FF
        (0, -1),    // MTNKP2  $00,$FF
        (-1, -2),   // MTNKP3  $FF,$FE
        (0, -2),    // MTNKP4  $00,$FE
    ];

    /// <summary>ROM `MTANK`: grow pictures `MTNKP1..4` — 4x4, 8x7, 8x8, 12x12 arcade px (notes §53).</summary>
    public const int TankGrowSteps = 4;

    /// <summary>
    /// The birth pictures' sizes in arcade px, from the `MTNKP1..4` descriptors
    /// (`FCB 2,4` / `4,7` / `4,8` / `6,12` — bytes wide x rows, and a byte is 2 px).
    /// The ROM bounds and collides against the CURRENT picture, so a growing tank
    /// is a smaller target than a full one.
    /// </summary>
    public static readonly (int Width, int Height)[] TankGrowSizes =
    [
        (4, 4), (8, 7), (8, 8), (12, 12),
    ];

    /// <summary>ROM `MTANK`: `NAP 12` per grow step.</summary>
    public const int TankGrowRomFrames = 12;

    /// <summary>
    /// ROM `ENFR0`: the enforcer's grow-up is FIVE spawn pictures at `NAP 8`
    /// each = 5 x 9 ROM frames = **45** (a `NAP n` beat is n+1 frames, as with the
    /// quark). The port used 40, treating each step as 8.
    /// </summary>
    public const int EnforcerGrowUpRomFrames = 45;

    /// <summary>ROM `ENFRCE`: `NAP 8` per spawn picture.</summary>
    public const int EnforcerGrowStepRomFrames = 9;

    /// <summary>
    /// ROM `ENFR1B`: `NAP 3` — the enforcer's logic beat is 4 ROM frames (3 vblanks
    /// plus the frame it runs in), and its re-aim and shot timers count BEATS.
    /// </summary>
    public const int EnforcerBeatRomFrames = 4;

    /// <summary>
    /// ROM `TNKSPD` = **2**, and it is a CONSTANT, not a wave value: `LDA #2 / STA
    /// TNKSPD` in the per-level reset (RRG23:677). `TANK6` does `LDA TNKSPD / LDX
    /// #TANKL / JMP SLEEP`, so the tank's process re-runs every TNKSPD vblanks — a
    /// beat of 2 vblanks plus the frame it runs in = **3 ROM frames**.
    /// </summary>
    public const int TankBeatRomFrames = 3;

    /// <summary>
    /// ROM `TANK1`: `LDA PD4,U / CLRB / ASRA / RORB / ADDD OX16,X` — the X step is
    /// the direction byte HALVED, exactly like the player's table, so ±1 means 0.5
    /// COLUMNS = **1 arcade px**; `ADDB PD5,U` steps Y by 1 ROW = 1 px. Both axes
    /// therefore move one pixel per BEAT (the port moved one unit per tick, ~1.8x
    /// too fast).
    /// </summary>
    public const int TankStepArcadePixels = 1;

    // Spark — arcade-faithful per the GOSPEL (ref/original-source/
    // RRC11.ASM: ENFSHT + SPARK + the SPKP0..3 pics; notes 41). A spark is a
    // BALLISTIC particle, NOT a random walk:
    //   ENFSHT: OXV = 4 x (player coord + jitter - spark coord) [subpixels];
    //           jitter = (SEED & $1F) - 16, i.e. -16..+15 COLUMNS per axis, and
    //           the X jitter is forced to 0 when the player is within 16 columns
    //           of the left wall (`CMPA #XMIN+$10 / BHS ENFS2 / CLRB`);
    //           PD2/PD4 = (LSEED/HSEED & $1F) - 16 = a CONSTANT per-axis
    //           acceleration in those same subpixel units;
    //           PD7 = (HSEED & $F) + $14 = 20..35 MOVES of life.
    //   SPARK:  once per move (NAP 4 = 4 vblanks): OXV += PD2, OYV += PD4. The
    //           acceleration integrates into the velocity, so the path is a
    //           PARABOLA — that is the arcade spark's "habit of going off
    //           course", and it is why a spark can start out moving AWAY from
    //           the player.
    //   Mover (RRS22.ASM OPB80: `LDD OX16,X / LDU OPICT,X / ADDD OXV,X / ... /
    //   STD OX16,X`, looping the object list): the FULL 16-bit velocity is added
    //   to the 16-bit world position ONCE PER FRAME. So OXV = 4 x delta means the
    //   spark travels delta/64 COLUMNS per FRAME, and PD2 = a bends that by
    //   a/256 columns per frame. (An axis update that would leave the playfield
    //   is REJECTED, not clamped — the object keeps its last valid coordinate on
    //   that axis while the other axis still moves.)
    // Port units: 1 ROM column = 2 arcade px = Scaled(2) port px = 4 port px,
    // so one frame advances deltaPort/64 port px and the acceleration is
    // a/64 port px per frame of velocity. Both live in 1/256-px fixed point.
    public const int SparkMoveIntervalRomTicks = 4;   // NAP 4
    public const int SparkVelocityScale = 256;        // fixed point: 1 px/frame = 256
    public const int SparkAimDivisor = 64;            // ROM: the delta is covered in 64 frames
    public const int SparkJitterColumns = 16;         // (seed & $1F) - 16 → -16..+15
    public const int SparkLeftWallJitterColumns = 16; // XMIN+$10: no X jitter this near the wall
    public const int SparkAccelRomRange = 16;         // PD2/PD4 = (seed & $1F) - 16
    public const int SparkMaxSpeed = 8;               // port safety cap on the px/frame step
    public const int SparkLifeMinRomTicks = 80;
    public const int SparkLifeMaxRomTicks = 140;
    // Spark flicker: the ROM SPARK process advances OPICT by one 4-byte
    // picture entry (SPKP0..3) on every beat pass and re-runs every 4
    // vblanks (NAP 4) — a 4-frame flash, one frame per 4 vblanks (notes 32).
    public const int SparkFramePeriodRomTicks = 4;

    // Explosion / appear — the RRDX2 "DIAGONAL EXPLOSIONS" engine (notes §35.5,
    // §61). The records come from ONE pool of 10 (`EX` at DXTAB, `RMB
    // ((10-1)*EXSIZE)`) shared by explosions and appears, so that is the cap on
    // the pair TOGETHER (notes §35.5).
    //
    // The sizes are the ROM's 16-bit fixed-point accumulators: the HIGH byte is
    // the step (rows for a row fan, columns for a column fan), so:
    //   explosion  YSIZER $0100 ("1 UNIT IS MIN"), +$0100 a frame, FRAMES $10
    //              -> the steps are 2,3,...,16 over 15 draws
    //   appear     YSIZER $1000 ("START LARGE FOR APPEAR"), -$0100 a frame,
    //              ending when the step would reach 1
    public const int StripExplosionStartSizer = 0x0100;
    public const int StripAppearStartSizer = 0x1000;
    public const int StripSizerStep = 0x0100;
    public const int StripExplosionFrames = 0x10;
    public const int StripMaxConcurrent = 10;

    // Spheroid / quark DEATH BURST (notes §64) — the spheroid's `CIRKP`
    // ("DRAW_SPHEROID_IN_DEATH_THROES", RRG23.ASM $12F1) and the quark's
    // `CIRKV`, which is `$1143: JMP $12FA` — the SAME routine entered after its
    // parameter setup. The enemy's own pictures play as a SOLID silhouette, then
    // its "1000" picture is displayed. `LDD #$FFAA` (spheroid) / `LDD #$DDDD`
    // (quark) sets one colour per phase, named by the disassembly's own comment
    // as "the same colour as the player score"; each is a PALETTE SLOT in the
    // doubled-nibble form, so both effects shimmer, because all the slots used
    // here (10, 13, 15) are colour-CYCLING ones.
    public const int ScoreBurstSpheroidCount = 7; // `LDA #7` — = the LAST picture's index
    public const int ScoreBurstQuarkCount = 8;    // `LDA #8`
    public const int ScoreBurstRomFramesPerStep = 2; // `NAP 2`
    public const int ScoreBurstPointsSteps = 30;  // `LDA #$1E`
    public const int ScoreBurstSpheroidBurstSlot = 0x0A;  // $AA = slot 10
    public const int ScoreBurstSpheroidPointsSlot = 0x0F; // $FF = slot 15
    public const int ScoreBurstQuarkBurstSlot = 0x0D;     // $DD = slot 13
    public const int ScoreBurstQuarkPointsSlot = 0x0D;    // $DD = slot 13

    // `ADDD #$0105` on the blitter's column:row destination: +1 column (2 px) and
    // +5 rows, so the "1000" sits down-right of where the enemy died.
    public const int ScoreBurstPointsOffsetXSpecPixels = 2;
    public const int ScoreBurstPointsOffsetYSpecPixels = 5;

    // Tank shell — ROM R5 4F82-4F8A: lifespan = (RND & $1F) + $30
    // ROM ticks (48..79); the shell flies straight, aimed once at spawn, and
    // bounces off all four walls (4F94-4FCD).
    public const int TankShellSpeed = 5;
    public const int TankShellLifeBaseRomTicks = 48;

    /// <summary>
    /// ROM `SHLP1 FCB 4,7` — the shell's picture is 4 BYTES wide (a byte is 2 px)
    /// by 7 rows = **8x7 px**, and the ROM bounds and collides a projectile against
    /// the picture it is showing (notes §53). The port used the spec's square 4x4
    /// missile box while drawing a wrongly-sized 14x16 extraction into it.
    /// </summary>
    public static readonly (int Width, int Height) TankShellCollisionSize = (8, 7);

    // Attract mode (notes §94) — the arcade's attract cycle: idle
    // title, then the machine plays itself (CMOS "FANCY ATTRACT MODE" when on).
    public const int TitleWallSlot = 12; // ROM title screen: $79C8 LDA #$CC / STA $8F — the wall is solid slot 12
    public const int TitleIdleSeconds = 12; // port choice: how long the title sits before the demo takes over
    /// <summary>
    /// ROM `SPGSUB` ($79AF) prints string 128 at the cursor (54, 36) — column 54,
    /// row 36 — and every attract-movie screen keeps it: the page script's CLEARM
    /// only clears from row 48 down, so the story text scrolls UNDER the title.
    /// The movie's story band therefore uses the ROM's own row (notes §96.3); the
    /// title screen keeps the port's own placement of the pair.
    /// </summary>
    public const int StoryTitleRow = 36;
    public const int DemoThreatDistanceSpecPixels = 60; // AI: flee a robot closer than this (arcade AI is OS-ROM-only, §94.3)
    public const int DemoFireRangeSpecPixels = 120; // AI: fire at the nearest robot within this
    public const int DemoWallClearanceSpecPixels = 24; // AI: steer away from a wall within this
    public const int DemoStutterChanceDenominator = 16; // AI: 1-in-N ticks of deliberate pause (feels alive, not robotic)
    /// <summary>
    /// AI stick hysteresis: a freshly computed flee/drift direction must win this
    /// many ticks IN A ROW before the stick follows it. The flee direction is
    /// `sign(player − robot)` recomputed against a moving field, so without hysteresis it flipped on
    /// ~75% of ticks; the arcade's walk animation RESETS on every facing change
    /// (R5 $3003-3009), which makes the demo's man twitch in place instead of walking
    /// (notes §97.5).
    /// </summary>
    public const int DemoDirectionSwitchTicks = 3;

    /// <summary>
    /// AI stick minimum hold: once the stick follows a new direction it keeps it
    /// this many ticks. The arcade's walk animation is a four-frame cycle at three
    /// ticks a frame (twelve ticks), so a direction that lasts fewer ticks than that
    /// can never show a complete walk (notes §97.5).
    /// </summary>
    public const int DemoDirectionHoldTicks = 9;

    // Playfield layout
    public const int PlayfieldMarginSpecPixels = 20;

    // Scoring — per-kill point values live in Level/ScoreValues.cs
    public const int ExtraLifeThresholdStep = 10000;

    // Wave clear
    public const int WaveClearDisplayTicks = 90; // 1.5s at 60Hz fixed timestep

    // HIGH SCORE TABLE (notes §98) — RRTABLE's TABLE, RRTESTC's CMOS lists and
    // RRET's texts; every value is a ROM constant.
    public const int HighScoreHoldRomFrames = 200 * 3; // LDA #200 with NAP 3 = 600 frames = 12 s
    public const int HighScoreLeaveCheckRomFrames = 4; // TAB777's NAP 4 between switch reads
    public const int HighScoreLeaveChecks = 255; // LDA #$FF: one DEC per check THAT FINDS A SWITCH DOWN
    public const int HighScoreHeaderSlot = 7; // SCRMEP: COLOR $77 = slot 7
    // TABLE sets TWO colour pairs: TCOL1/TCOL2 = $99/$CC ($D8/$D9 at ROM $DF4F) for
    // TODAY'S list, then $AA/$DD ($DF75) for the top entry AND the all-time list —
    // NOINTS prints the second list without changing them again.
    public const int HighScoreTodaySlot = 9;
    public const int HighScoreTodayHighlightSlot = 12;
    public const int HighScoreAllTimeSlot = 10;
    public const int HighScoreAllTimeHighlightSlot = 13;
    // The wall is NOT one slot: FRAMER's flavour starts at $88 and GETA walks it down by
    // $11 a stroke, so each of the eight visible strokes takes its own slot — see
    // HighScoreTableLayout.FrameStrokeSlot (LOOPP then cycles slots 1-8, which is why the
    // band reads as eight colours chasing at once).

    // The game-over message: RRG23 PLEND prints string 40 (GOMP = "GAME OVER") in
    // the LARGE font, colour $AA, at CURSAB $3E,$80, and waits NAP 120.
    public const int GameOverMessageRomFrames = 120;
    public const int GameOverTextColumn = 62;
    public const int GameOverTextRow = 128;
    public const int GameOverTextSlot = 10;

    // INITIALS ENTRY (notes §116) — RRTESTC's ENDGAM/EGSUB, RRET's messages 95 (CONG) and 100
    // (ONLY5P), and RRTESTB's GETLET. The page's geometry is InitialsEntryLayout's and the input
    // model's own clocks are InitialsEntryModel's; these two are what the states need.

    /// <summary>ONLY5P's <c>NAP $60</c>: the "5 ENTRIES MAXIMUM" page is held for 60 ROM frames (1.2 s).</summary>
    public const int EntriesMaximumHoldRomFrames = 0x60;

    /// <summary>ONLY5P's <c>COLOR $BB</c> — the page's ink, slot 11.</summary>
    public const int EntriesMaximumSlot = 11;

    // Session / geometry values taken from spec.txt
    public const int StartingLives = 3; // spec-stated ("the PLAYER is awarded 3 lives")
    public const int StartingLevelNumber = 1; // spec-stated ("assigned level 1")
    // The spec's 16x16 entity box is not a collision box — see the
    // per-entity *CollisionSize table below. Still used for the spawn-candidate grid
    // (the largest entity box is 16 spec-px wide) and the pixel-art factory
    // pattern size.
    public const int EntitySizeSpecPixels = 16; // spec-stated (16x16 entities)
    public const int MissileSizeSpecPixels = 4; // spec-stated (4x4 lasers/missiles)
    public const int WallThicknessSpecPixels = 4; // spec-stated ("4px width" wall)

    // ---- Collision boxes ----
    // The ROM (COL0V, RRS22 ~1038) intersects each object's PICTURE
    // dimensions — ADDD OBJW,U / ADDD [OPICT,X] — not a fixed square: the
    // collision box IS the art. Sizes are the live-frame dimensions from
    // docs/sprite-map.md, in arcade px; apply ScreenSize.Scaled at the use
    // site. Lasers/sparks keep the spec's 4x4 box (spec-stated).
    public static readonly (int Width, int Height) PlayerCollisionSize = (8, 12);
    public static readonly (int Width, int Height) GruntCollisionSize = (10, 13);
    public static readonly (int Width, int Height) HulkCollisionSize = (14, 16);
    public static readonly (int Width, int Height) SpheroidCollisionSize = (16, 15);
    public static readonly (int Width, int Height) EnforcerCollisionSize = (10, 11);
    public static readonly (int Width, int Height) QuarkCollisionSize = (16, 15);
    public static readonly (int Width, int Height) TankCollisionSize = (14, 16);
    public static readonly (int Width, int Height) ElectrodeCollisionSize = (10, 9);
    public static readonly (int Width, int Height) MomCollisionSize = (8, 14);
    public static readonly (int Width, int Height) DadCollisionSize = (10, 13);
    public static readonly (int Width, int Height) MikeyCollisionSize = (6, 11);
    public static readonly (int Width, int Height) SkullCollisionSize = (12, 11);
    // Brain art (notes (18) RRB10 decode): 7 bytes x 16 rows =
    // 14x16 px; a prog re-draws its converted human's art and keeps that
    // human's per-kind box (see Prog.ArcadeCollisionSize); cruise missile =
    // the ROM's "FAT PHONY GUY" box (the 6x6 head inset 1 px = 4x4).
    public static readonly (int Width, int Height) BrainCollisionSize = (14, 16);
    /// <summary>
    /// ROM CMMOV: `SUBD #$0101 / STD OBJX,X` — the FAT collision box, `CMPIC
    /// FCB 3,4` = 3 BYTES x 4 rows = **6x4 px**, whose top-left sits ONE COLUMN
    /// and ONE ROW up-left of the missile's true coordinate. The port used a 4x4
    /// box with no offset. (The box is collision-only: `CMPIC`/`CMP1` are never
    /// blitted — notes §49.)
    /// </summary>
    public static readonly (int Width, int Height) CruiseMissileCollisionSize = (6, 4);

    /// <summary>ROM `SUBD #$0101`: the fat box sits -1 column / -1 row from the true coordinate.</summary>
    public const int CruiseMissileBoxOffsetColumns = -1;

    /// <summary>ROM `SUBD #$0101`: the row component of the fat box offset.</summary>
    public const int CruiseMissileBoxOffsetRows = -1;

    /// <summary>ROM BCMCNT cap: a brain fires only while fewer than 8 missiles fly.</summary>
    public const int CruiseMissileMax = 8;

    /// <summary>
    /// ROM PGXPIC: `FCB 6,16` — the prog's PHONY burst card, 6 BYTES x 16 rows =
    /// 12x16 px. PRGKIL swaps the object's picture descriptor to this card and then
    /// calls the ordinary `EXST`, so the card is what the strip explosion shatters,
    /// and `EXSTV` sizes its record from the picture — see
    /// <see cref="Entities.Prog.ExplosionBounds"/> (notes §90).
    /// </summary>
    public static readonly (int Width, int Height) ProgBurstSize = (12, 16);

    // ---- BMUT: a brain reprogramming a human (notes §46, §47) ----
    // The ROM's 20-iteration loop, each iteration doing TWO redraws of the
    // human (one with its Y lifted by SEED & 7, one with it dropped by the
    // same) separated by NAP 2. Both the brain and the human are drawn in a
    // single blitter colour for the duration: the brain as a solid block
    // (BRNON/BLKON, op $12) and the human as a solid rectangle plus a solid
    // silhouette (HUMON with D = $AABB, op $12 then op $1A).
    public const int ReprogramIterations = 20;   // ROM: `LDA #20 / STA PD4,U`
    public const int ReprogramRedrawsPerIteration = 2;
    public const int ReprogramStepRomTicks = 3;  // NAP 2 + the beat vblank
    public const int ReprogramJitterPixels = 8;  // ROM: SEED & 7, i.e. 0..7

    /// <summary>Blitter colour 1 for the reprogramming human ($AA = palette slot 10).</summary>
    public const int ReprogramBackgroundSlot = 0x0A;

    /// <summary>
    /// Blitter colour 2 for the reprogramming human AND the BACKDROP the brain is drawn
    /// on ($BB = slot 11). For the brain this is a backdrop, not a replacement: ROM
    /// $1DAF fills the brain's rectangle with it ($DA61 / BLKON, op $12) and then blits
    /// the brain's own picture over the top (JMP $D018) — notes §72.
    /// </summary>
    public const int ReprogramShapeSlot = 0x0B;

    // ---- The PROG's own colours (RRB10 PROG3/PROG4) ----
    // A prog is drawn as TWO blitter colour pairs, not as a sprite. Each beat:
    //   PROG3: `LDD #$EE00 / JSR HUMON` at the position it is LEAVING
    //   PROG4: `LDD #$00AA / JSR HUMON` at the position it is entering
    // i.e. a ghost (slot 14 block, black shape) behind it and a black block
    // with a slot-10 shape where it is. HUMON maps A (the high byte) to the
    // BLOCK through BLKON and B (the low byte) to the FIGURE through MPCTON, so
    // $EE00 really is a slot-14 card carrying a black figure, and $00AA is its
    // exact inverse (RRB10:356).
    // The shadow ring: PD+8 is the index and the entries run PD+10, PD+12, ...
    // wrapping at SPSIZE = 31. PD = 7 (RRF.ASM:551) and SPSIZE = PSIZE+16 = 31
    // (RRF.ASM:565), so those offsets are bytes 17, 19 ... 29 — SEVEN entries,
    // and a ghost is erased by PCTOFF as its entry is reused 7 beats later.
    public const int ProgGhostCount = 7;
    public const int ProgBackgroundSlot = 0x00;   // $00 — black
    public const int ProgShapeSlot = 0x0A;        // $AA
    public const int ProgGhostBackgroundSlot = 0x0E; // $EE
    public const int ProgGhostShapeSlot = 0x00;      // $00 — black

    // ---- The CRUISE MISSILE's colours and trail (RRB10 CMMOV) ----
    // CMMOV never blits the CMPIC/CMP1 pictures; it writes VIDEO MEMORY
    // directly, one 16-bit word per step: `$AAAA` (two pixels of slot 10) at
    // the new coordinate and `$DDDD` (two of slot 13) at the coordinate it just
    // left. The video address is COLUMN-MAJOR — `column*256 + row`, proved by
    // RRG23's BORDER loop, which draws its vertical border with `STA ,X+` (one
    // address per ROW) — so a 16-bit word is TWO VERTICALLY ADJACENT PIXELS:
    // the mark is 1px wide and 2px tall.
    public const int MissileHeadSlot = 0x0A;   // $AA
    public const int MissileTrailSlot = 0x0D;  // $DD

    /// <summary>
    /// ROM CMMOV's mark: `LDD #$AAAA / LDY OX16,X / STD ,Y` — a 16-BIT write at
    /// the video address. The video is column-major (`column*256 + row`) with 2 px
    /// per byte (notes §52), so that paints TWO vertically adjacent addresses,
    /// each holding 2 px: a **2x2 arcade px** block. The port drew 1x2.
    /// </summary>
    public const int MissileMarkArcadeWidth = 2;
    public const int MissileMarkArcadeHeight = 2;

    /// <summary>
    /// The trail's length in marks — and the thing that stops it being a snake.
    /// CMMOV keeps a ring of coordinates at `PD+6`..`SPSIZE` stepping by 2
    /// (`SPSIZE` 31, initialised to `PD+6` = 13 → 13,15,..,29 = NINE entries)
    /// and, EVERY STEP, erases the screen pixel at the entry it is about to
    /// overwrite (`LDY #0 / LDA PD5,U / STY [A,U]`): the pixel nine steps back.
    /// So the missile drags a rolling NINE-MARK tail. `CMKIL` then wipes the
    /// remaining nine, so the tail vanishes with the missile.
    /// </summary>
    public const int MissileTrailMarks = 9;

    // ---- Arcade HUD mapping (notes §58 — the ROM's own HUD, decoded) ----
    // The arcade screen is 304x256 (mame-notes: pair-major 4bpp, 152 two-px
    // columns). Arcade coordinates map to the 640x400 port screen by screen
    // proportion (integer math).
    public const int ArcadeScreenWidth = 304;
    public const int ArcadeScreenHeight = 256;

    /// <summary>Arcade pixels in one ROM column — the video buffer is addressed as <c>column*256 + row</c>.</summary>
    /// <remarks>Conversions that the ROM expresses in COLUMNS multiply by this, never by
    /// <see cref="ScreenSize.SpecScale"/>: a column is two arcade pixels whatever the render scale is.</remarks>
    public const int ArcadePixelsPerColumn = 2;

    // The HUD is the arcade's, from DRAW_PLAYER_SCORES ($DC13, called by
    // $34AF), DRAW_LIVES_REMAINING ($34E0) and the $6291 string table:
    //
    //   score   P1 cursor = col 21 (x 42), row 14; P2 = col 85 (x 170), row 14
    //           (`LEAX -$300,X` from the $180E/$580E blit destination)
    //   digits  the 4 BCD score bytes as EIGHT positions, left to right:
    //           10M (always masked off), 1M, 100k, 10k, 1k, 100, 10, 1
    //           a drawn glyph advances width+1 = 7 px; a suppressed leading
    //           zero advances 6 px ($6128: 4 px, then +2 for the large font)
    //   men     P1 col 46 (x 92), P2 col 110 (x 220), row 14, 8 px apart,
    //           the 6x8 mini man picture ($3592/$3596), capped at SEVEN
    //   colour  the CURRENT player's score blits in slot 10 ($AA — a CYCLING
    //           slot: the LF process); everyone else's in slot 1 ($11)
    //   wave    "<n>  WAVE" at col 62 / row 238 (the BOTTOM), string 104,
    //           small font, number in $AA and " WAVE" in $BB
    //
    // The ROM's HUD row is 14, which keeps the score clear of the port's 40-px
    // wall band.
    public const int HudScoreOriginColumnP1 = 21;     // arcade byte column
    public const int HudScoreOriginColumnP2 = 85;     // = P1 + 64 (P2ORG - P1ORG)
    public const int HudMenOriginColumnP1 = 46;
    public const int HudMenOriginColumnP2 = 110;

    /// <summary>
    /// The ROM draws the whole HUD on row 14 (<c>col*256 + 14</c>) while the top wall
    /// sits on row 22, i.e. EIGHT rows above the wall — and the 8-row-tall mini men
    /// therefore finish on row 21, exactly adjacent to the wall. The port derives its
    /// HUD row the same way (wall top minus this many arcade pixels) instead of using
    /// <see cref="ArcadeY"/>'s screen proportion, because the port's wall comes from
    /// spec.txt's margin and is not at the arcade's 22/256 height: at ArcadeY(14) = 21
    /// the 12-px score glyphs and 16-px men ran through the 32..40 wall band, which is
    /// exactly the round-8 complaint ("the score also overlaps the border wall").
    /// </summary>
    public const int HudRowAboveWallPixels = 8;
    public const int HudMaxMen = 7;                   // ROM MANDSV: "MAX OF 7"
    public const int HudMenPitchPixels = 8;           // ROM $3506: ADDA #$04 (byte cols)
    public const int HudMiniManWidthPixels = 6;       // ROM $3592 metadata (3 bytes)
    public const int HudMiniManHeightPixels = 8;      // ROM $3592 metadata (8 rows)
    public const int HudScoreDigitAdvancePixels = 7;  // ROM $6009: width + 1
    public const int HudScoreBlankAdvancePixels = 6;  // ROM $6128 + $6136 (4 + 2)
    public const int HudScoreSlotCurrent = 10;        // ROM $DC19: $AA, the LF process
    public const int HudScoreSlotIdle = 1;            // ROM $DC13: $11
    public const int HudWaveTextColumn = 62;          // ROM string 104: cursor $3EEE
    public const int HudWaveTextRow = 238;

    // Small-font metrics (the font every arcade MESSAGE uses — notes §58.3):
    // 4x5 glyphs that advance width+1 = 5 px ($6009); a suppressed leading zero
    // advances 4 px ($6128, the $6136 +2 is large-font only); a space is the
    // ROM's 1-px ':' glyph, so it advances 2 px.
    public const int HudSmallFontGlyphGapPixels = 1;
    public const int HudSmallFontBlankAdvancePixels = 4;
    public const int HudSmallFontSpaceAdvancePixels = 2;

    // Messages (ROM strings 103/75/40/104 — control codes decoded, notes §58.3).
    public const int HudWaveNumberGapPixels = 6;      // string 104: MOVE_CURSOR_REL +$03
    public const int HudWaveTextSlot = 0x0B;          // string 104: colour $BB
    public const int PlayerTurnMessageColumn = 63;    // string 103: cursor $3F7A
    public const int PlayerTurnMessageRow = 122;
    public const int PlayerGameOverMessageRow = 134;  // string 75: cursor $3F79 (the name)
    public const int GameOverMessageColumn = 62;      // string 40 / 75: cursor $3E80/$3E86
    public const int GameOverMessageRow = 128;
    // Port-only (notes §101): the PAUSE banner, on the same centre line the ROM's own
    // messages use, so it looks like one of them.
    public const int PausedMessageColumn = 62;
    public const int PausedMessageRow = 120;
    // Port-only: the title's F-key menu, and how far apart its lines sit.
    public const int TitleOptionRowStepPixels = 14;

    /// <summary>
    /// How long each of the presentation page's two TEXT PANES is shown before they swap.
    /// The page has no room for the arcade's message and credits and the port's credit
    /// and F-key menu at once — and the arcade's two message lines want an empty row between them
    /// (the ROM's own cursors, `$86`/`$96`, are 16 rows apart on an 8-row line grid) — so each pane
    /// gets the whole band to itself. Notes §107.
    /// </summary>
    public const int TitleTextSwapSeconds = 3;

    /// <summary>
    /// ROM RRG23 PLS0D: the "PLAYER n" message is drawn and the game waits
    /// <c>NAP 115</c> before erasing it — 115 ROM frames at a turn start in a
    /// 2-player game (1-player games skip it: <c>LDA PLRCNT / DECA / BEQ</c>).
    /// </summary>
    public const int PlayerTurnMessageRomFrames = 115;

    /// <summary>ROM RRG23 PLEND3: "PLAYER n GAME OVER" is shown for <c>NAP $60</c>.</summary>
    public const int PlayerGameOverMessageRomFrames = 0x60;

    /// <summary>Arcade screen x (of 304) mapped to the port screen (proportional, integer math).</summary>
    public static int ArcadeX(int arcadePx) => arcadePx * ScreenSize.Width / ArcadeScreenWidth;

    /// <summary>An arcade COLUMN to the port screen's x — a column is <see cref="ArcadePixelsPerColumn"/> arcade pixels.</summary>
    public static int ArcadeColumnX(int column) => ArcadeX(column * ArcadePixelsPerColumn);

    /// <summary>Arcade screen y (of 256) mapped to the port screen (proportional, integer math).</summary>
    public static int ArcadeY(int arcadePx) => arcadePx * ScreenSize.Height / ArcadeScreenHeight;
}
