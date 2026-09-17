namespace Robotron2084.Audio;

/// <summary>
/// Sound tables decoded from the ROM (notes §36.2). Each entry is the
/// (priority, (dur, len, note)…) table a ROM call site loads with
/// <c>LDD #addr</c> before <c>JSR $D04B</c> (→ $D3C7). The note→frequency
/// map is sound-board hardware (NOT in the CPU ROM) — the port uses a
/// stub scale in <see cref="MonoGameSoundSink"/> (BLOCKED on the author
/// for the real table; see notes §36.2 question).
/// </summary>
public static class SoundTables
{
    // R5 $273A (the "Player laser sound" comment): (1, 16, $25), priority 240.
    public static readonly SoundSequence PlayerLaser =
        new SoundSequence(240, 0x26E6, [new SoundEntry(1, 16, 0x25)]);

    // R5 $30EF (KILL_PLAYER): (2, 8, $11) then (1, 32, $17), priority 238.
    public static readonly SoundSequence PlayerDeath =
        new SoundSequence(238, 0x26D9, [new SoundEntry(2, 8, 0x11), new SoundEntry(1, 32, 0x17)]);

    // R5 $1F5B (laser hit / explosion area, the $5B43 explosion call
    // neighbourhood): (1, 4, $14) then (2, 4, $17), priority 208.
    public static readonly SoundSequence RobotDeath =
        new SoundSequence(208, 0x1ADA, [new SoundEntry(1, 4, 0x14), new SoundEntry(2, 4, 0x17)]);

    // R5 $4F8C (shell creation): (1, 8, $04), priority 200.
    public static readonly SoundSequence ShellFire =
        new SoundSequence(200, 0x4B11, [new SoundEntry(1, 8, 0x04)]);

    // R5 $4FCD (wall bounce, COM the delta): (1, 4, $14) then (1, 1, $13), priority 200.
    public static readonly SoundSequence ShellBounce =
        new SoundSequence(200, 0x4B16, [new SoundEntry(1, 4, 0x14), new SoundEntry(1, 1, 0x13)]);

    // R5 $4607 (PLAY_BRAIN_WAVE_WARP_IN_SOUNDS): (1, 1, $13), priority 255.
    public static readonly SoundSequence BrainWarpIn =
        new SoundSequence(255, 0x4143, [new SoundEntry(1, 1, 0x13)]);

    // R5 $DBF9 (bonus life — score reached the next free-man threshold):
    // (1, 32, $1E), priority 239.
    public static readonly SoundSequence BonusLife =
        new SoundSequence(239, 0xD0C9, [new SoundEntry(1, 32, 0x1E)]);

    // R5 $2A9C (the long 29-repetition drone): (29, 4, $0E), priority 224.
    public static readonly SoundSequence Drone =
        new SoundSequence(224, 0x26EB, [new SoundEntry(29, 4, 0x0E)]);

    // RRB10.ASM's own sound tables (BRAINS & CO.), at BRNORG $1AC0 + the table
    // block. The offsets are checkable against a known one: PGKSND lands at
    // $1ADA, which is exactly where R5's (1,4,$14),(2,4,$17) priority-208
    // sequence lives — so the stride arithmetic is right.
    //   BKSND  $1ACD  brain kill      $D0 = 208  (1,4,$14) (1,8,$11)
    //   CMKSND $1AD5  cruise kill     $D0 = 208  (2,4,$17)
    //   PGKSND $1ADA  prog kill       $D0 = 208  (1,4,$14) (2,4,$17)
    //   BSHSND $1AE2  brain shoot     $C8 = 200  (1,8,$15) (1,8,$14)
    //   PRGSND $1AEA  programming     $D0 = 208  (2,3,$12)

    /// <summary>RRB10 `PRGSND` — requested once per BMUT reprogram iteration.</summary>
    public static readonly SoundSequence ProgProgramming =
        new SoundSequence(0xD0, 0x1AEA, [new SoundEntry(2, 3, 0x12)]);

    /// <summary>RRB10 `HPSND` — the human-to-prog FINAL CONVERSION at BMUT's end.</summary>
    public static readonly SoundSequence HumanProgConversion =
        new SoundSequence(0xD8, 0x1AEF, [new SoundEntry(1, 8, 0x11)]);

    // --- Further decoded tables (wiring parked; kept for provenance) ---

    // R5 $00FC (boot / credit area): (1, 16, $06), priority 208.
    public static readonly SoundSequence Boot =
        new SoundSequence(208, 0x001C, [new SoundEntry(1, 16, 0x06)]);

    // R5 $038A: (1, 32, $0D), priority 224.
    public static readonly SoundSequence SpawnA =
        new SoundSequence(224, 0x0026, [new SoundEntry(1, 32, 0x0D)]);

    // R5 $039C: (1, 24, $1A), priority 224.
    public static readonly SoundSequence SpawnB =
        new SoundSequence(224, 0x002B, [new SoundEntry(1, 24, 0x1A)]);

    // R5 $12EA: (1, 8, $08), priority 209.
    public static readonly SoundSequence WaveSfxA =
        new SoundSequence(209, 0x1151, [new SoundEntry(1, 8, 0x08)]);

    // R5 $1377: (1, 8, $18), priority 216.
    public static readonly SoundSequence WaveSfxB =
        new SoundSequence(216, 0x114C, [new SoundEntry(1, 8, 0x18)]);

    // R5 $147B: (1, 8, $1D), priority 208.
    public static readonly SoundSequence WaveSfxC =
        new SoundSequence(208, 0x1156, [new SoundEntry(1, 8, 0x1D)]);

    // R5 $14A1: (3, 4, $17), priority 208.
    public static readonly SoundSequence WaveSfxD =
        new SoundSequence(208, 0x115B, [new SoundEntry(3, 4, 0x17)]);

    // R5 $14EB: (1, 4, $15) then (1, 8, $14), priority 208.
    public static readonly SoundSequence WaveSfxE =
        new SoundSequence(208, 0x1160, [new SoundEntry(1, 4, 0x15), new SoundEntry(1, 8, 0x14)]);

    // R5 $1D22: (2, 3, $12), priority 208.
    public static readonly SoundSequence WaveSfxF =
        new SoundSequence(208, 0x1AEA, [new SoundEntry(2, 3, 0x12)]);

    // R5 $1D7E: (1, 8, $11), priority 216.
    public static readonly SoundSequence WaveSfxG =
        new SoundSequence(216, 0x1AEF, [new SoundEntry(1, 8, 0x11)]);

    // R5 $2053: (1, 8, $15) then (1, 8, $14), priority 200.
    public static readonly SoundSequence WaveSfxH =
        new SoundSequence(200, 0x1AE2, [new SoundEntry(1, 8, 0x15), new SoundEntry(1, 8, 0x14)]);

    // R5 $213A: (2, 4, $17), priority 208.
    public static readonly SoundSequence WaveSfxI =
        new SoundSequence(208, 0x1AD5, [new SoundEntry(2, 4, 0x17)]);

    // R5 $3221: (1, 8, $01), priority 208.
    public static readonly SoundSequence WaveSfxJ =
        new SoundSequence(208, 0x26F0, [new SoundEntry(1, 8, 0x01)]);

    // R5 $3A68 (grunt area): (1, 10, $06), priority 192.
    public static readonly SoundSequence GruntSfxA =
        new SoundSequence(192, 0x38A0, [new SoundEntry(1, 10, 0x06)]);

    // R5 $3A8E: (1, 12, $14) then (1, 8, $17), priority 208.
    public static readonly SoundSequence GruntSfxB =
        new SoundSequence(208, 0x3898, [new SoundEntry(1, 12, 0x14), new SoundEntry(1, 8, 0x17)]);

    // R5 $3ACD: (1, 8, $17), priority 208.
    public static readonly SoundSequence GruntSfxC =
        new SoundSequence(208, 0x38A5, [new SoundEntry(1, 8, 0x17)]);

    // R5 $4BEB (tank area): (1, 4, $15) then (1, 8, $11), priority 208.
    public static readonly SoundSequence TankSfxA =
        new SoundSequence(208, 0x4B29, [new SoundEntry(1, 4, 0x15), new SoundEntry(1, 8, 0x11)]);

    // R5 $4CCE (tank area): (1, 8, $19), priority 208.
    public static readonly SoundSequence TankSfxB =
        new SoundSequence(208, 0x4B31, [new SoundEntry(1, 8, 0x19)]);

    // R5 $4E0A (tank area): (1, 8, $11), priority 208.
    public static readonly SoundSequence TankSfxC =
        new SoundSequence(208, 0x4B0C, [new SoundEntry(1, 8, 0x11)]);

    // R5 $4FE7 (tank area): (1, 3, $01) (1, 4, $15) (1, 4, $13), priority 208.
    public static readonly SoundSequence TankSfxD =
        new SoundSequence(208, 0x4B1E, [new SoundEntry(1, 3, 0x01), new SoundEntry(1, 4, 0x15), new SoundEntry(1, 4, 0x13)]);
}
