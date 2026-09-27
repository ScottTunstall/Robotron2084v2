namespace Robotron2084.Audio;

/// <summary>
/// Every sound the game asks for, copied from the main board's ROM. Each one says how important the
/// sound is and which sound numbers to send the sound board, how often and for how long.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>Original source: the <c>...SND FCB</c> tables beside their callers (<c>RRB10</c>, <c>RRC11</c>,
/// <c>RRG23</c>, <c>RRH11</c>, <c>RRP8</c>, <c>RRS22</c>, <c>RRT2</c>, <c>RRTK4</c>); the format is the
/// comment above routine <c>SNDLDV</c> in <c>RRS22.ASM</c>.</item>
/// <item>Disassembly: <c>asm/robomame.asm</c>, each table at the address on its field; the call site
/// that asks for it is named in its summary.</item>
/// </list>
/// Each field is named after the original label's own comment ("ROBOT HIT" is <see cref="RobotHit"/>).
/// The original's circle and square are the port's spheroid and quark (<c>docs/terminology-ledger.md</c>).
/// Two tables are not here because nothing in the port can ask for them: <c>HLKSND</c> ("HULK KILL",
/// R5 <c>$0021</c>), which no routine in the original calls, and <c>CNSND</c> ("COIN", R5 <c>$D0CE</c>),
/// since the port has no coin slot.
/// </remarks>
public static class SoundTables
{
    /// <summary><c>BKSND</c> "BRAIN KILL" (<c>RRB10.ASM</c>); R5 <c>$1ACD</c>, asked for at <c>$1E09</c>.</summary>
    public static readonly SoundSequence BrainKill = new(0xD0, 0x1ACD, [new(1, 4, 0x14), new(1, 8, 0x11)]);

    /// <summary><c>BSHSND</c> "BRAIN SHOOT": a brain fires a cruise missile (<c>RRB10.ASM</c>); R5 <c>$1AE2</c>, asked for at <c>$2053</c>.</summary>
    public static readonly SoundSequence BrainShoot = new(0xC8, 0x1AE2, [new(1, 8, 0x15), new(1, 8, 0x14)]);

    /// <summary><c>CRKSND</c> "CIRCLE KILL": a spheroid is shot (<c>RRC11.ASM</c>); R5 <c>$1151</c>, asked for at <c>$12EA</c>.</summary>
    public static readonly SoundSequence CircleKill = new(0xD1, 0x1151, [new(1, 8, 0x08)]);

    /// <summary><c>TR1SND</c> "CLEAR THE SYSTEM": sound <c>$13</c>, which silences the board (<c>RRT2.ASM</c>); R5 <c>$4143</c>, asked for at <c>$4607</c>.</summary>
    public static readonly SoundSequence ClearTheSystem = new(0xFF, 0x4143, [new(1, 1, 0x13)]);

    /// <summary><c>CMKSND</c> "CRUISE MISSILE KILL" (<c>RRB10.ASM</c>); R5 <c>$1AD5</c>, asked for at <c>$213A</c>.</summary>
    public static readonly SoundSequence CruiseMissileKill = new(0xD0, 0x1AD5, [new(2, 4, 0x17)]);

    /// <summary><c>ENDSND</c> "ENFORCER DROP OFF": a spheroid drops an enforcer (<c>RRC11.ASM</c>); R5 <c>$114C</c>, asked for at <c>$1377</c>.</summary>
    public static readonly SoundSequence EnforcerDropOff = new(0xD8, 0x114C, [new(1, 8, 0x18)]);

    /// <summary><c>ENKSND</c> "ENFORCER KILL" (<c>RRC11.ASM</c>); R5 <c>$115B</c>, asked for at <c>$14A1</c>.</summary>
    public static readonly SoundSequence EnforcerKill = new(0xD0, 0x115B, [new(3, 4, 0x17)]);

    /// <summary><c>ENFSND</c> "ENFORCER SHOOT": an enforcer fires a spark (<c>RRC11.ASM</c>); R5 <c>$1156</c>, asked for at <c>$147B</c>.</summary>
    public static readonly SoundSequence EnforcerShoot = new(0xD0, 0x1156, [new(1, 8, 0x1D)]);

    /// <summary><c>HKHSND</c> "HULK HIT": a laser knocks a hulk back (<c>RRH11.ASM</c>); R5 <c>$001C</c>, asked for at <c>$00FC</c>.</summary>
    public static readonly SoundSequence HulkHit = new(0xD0, 0x001C, [new(1, 16, 0x06)]);

    /// <summary><c>HPSND</c> "HUMAN-PROG FINAL CONVERSION" (<c>RRB10.ASM</c>); R5 <c>$1AEF</c>, asked for at <c>$1D7E</c>.</summary>
    public static readonly SoundSequence HumanProgFinalConversion = new(0xD8, 0x1AEF, [new(1, 8, 0x11)]);

    /// <summary><c>HKSND</c> "KILL A HUMAN": a member of the family dies and leaves a skull (<c>RRH11.ASM</c>); R5 <c>$002B</c>, asked for at <c>$039C</c>.</summary>
    public static readonly SoundSequence KillAHuman = new(0xE0, 0x002B, [new(1, 24, 0x1A)]);

    /// <summary><c>LASSND</c> "LASER SOUND": the player fires (<c>RRG23.ASM</c>); R5 <c>$26F0</c>, asked for at <c>$3221</c>.</summary>
    public static readonly SoundSequence Laser = new(0xD0, 0x26F0, [new(1, 8, 0x01)]);

    /// <summary><c>PDSND</c> "PLAYER DEATH" (<c>RRG23.ASM</c>); R5 <c>$26D9</c>, asked for at <c>$30EF</c>.</summary>
    public static readonly SoundSequence PlayerDeath = new(0xEE, 0x26D9, [new(2, 8, 0x11), new(1, 32, 0x17)]);

    /// <summary><c>PSKSND</c> "POST KILL": an electrode is destroyed (<c>RRP8.ASM</c>); R5 <c>$38A5</c>, asked for at <c>$3ACD</c>.</summary>
    public static readonly SoundSequence PostKill = new(0xD0, 0x38A5, [new(1, 8, 0x17)]);

    /// <summary><c>PGKSND</c> "PROG KILL" (<c>RRB10.ASM</c>); R5 <c>$1ADA</c>, asked for at <c>$1F5B</c>.</summary>
    public static readonly SoundSequence ProgKill = new(0xD0, 0x1ADA, [new(1, 4, 0x14), new(2, 4, 0x17)]);

    /// <summary><c>PRGSND</c> "PROGRAMMING SOUND": each pass of a brain's reprogramming (<c>RRB10.ASM</c>); R5 <c>$1AEA</c>, asked for at <c>$1D22</c>.</summary>
    public static readonly SoundSequence Programming = new(0xD0, 0x1AEA, [new(2, 3, 0x12)]);

    /// <summary><c>RPSND</c> "REPLAY": the score earns a spare man (<c>RRS22.ASM</c>); R5 <c>$D0C9</c>, asked for at <c>$DBF9</c>.</summary>
    public static readonly SoundSequence Replay = new(0xEF, 0xD0C9, [new(1, 32, 0x1E)]);

    /// <summary><c>RBSND</c> "ROBOT HIT": a grunt dies (<c>RRP8.ASM</c>); R5 <c>$3898</c>, asked for at <c>$3A8E</c>.</summary>
    public static readonly SoundSequence RobotHit = new(0xD0, 0x3898, [new(1, 12, 0x14), new(1, 8, 0x17)]);

    /// <summary><c>RMVSND</c> "ROBOT MOVE": the grunts take a step (<c>RRP8.ASM</c>); R5 <c>$38A0</c>, asked for at <c>$3A68</c>.</summary>
    public static readonly SoundSequence RobotMove = new(0xC0, 0x38A0, [new(1, 10, 0x06)]);

    /// <summary><c>SAVSND</c> "SAVE A HUMAN": the player rescues a member of the family (<c>RRH11.ASM</c>); R5 <c>$0026</c>, asked for at <c>$038A</c>.</summary>
    public static readonly SoundSequence SaveAHuman = new(0xE0, 0x0026, [new(1, 32, 0x0D)]);

    /// <summary><c>SHKSND</c> "SHELL KILL": a laser destroys a tank shell (<c>RRTK4.ASM</c>); R5 <c>$4B1E</c>, asked for at <c>$4FE7</c>.</summary>
    public static readonly SoundSequence ShellKill = new(0xD0, 0x4B1E, [new(1, 3, 0x01), new(1, 4, 0x15), new(1, 4, 0x13)]);

    /// <summary><c>SRBSND</c> "SHELL REBOUND": a tank shell bounces off a wall (<c>RRTK4.ASM</c>); R5 <c>$4B16</c>, asked for at <c>$4FCD</c>.</summary>
    public static readonly SoundSequence ShellRebound = new(0xC8, 0x4B16, [new(1, 4, 0x14), new(1, 1, 0x13)]);

    /// <summary><c>SPKSND</c> "SPARK KILL": a laser destroys an enforcer's spark (<c>RRC11.ASM</c>); R5 <c>$1160</c>, asked for at <c>$14EB</c>.</summary>
    public static readonly SoundSequence SparkKill = new(0xD0, 0x1160, [new(1, 4, 0x15), new(1, 8, 0x14)]);

    /// <summary><c>SQKSND</c> "SQUARE KILL": a quark is shot (<c>RRTK4.ASM</c>); R5 <c>$4B29</c>, asked for at <c>$4BEB</c>.</summary>
    public static readonly SoundSequence SquareKill = new(0xD0, 0x4B29, [new(1, 4, 0x15), new(1, 8, 0x11)]);

    /// <summary><c>ST1SND</c> "START 1": a one-player game begins (<c>RRG23.ASM</c>); R5 <c>$26E1</c>, asked for at <c>$2735</c>.</summary>
    public static readonly SoundSequence StartOnePlayer = new(0xF0, 0x26E1, [new(1, 16, 0x28)]);

    /// <summary><c>ST2SND</c> "START 2": a two-player game begins (<c>RRG23.ASM</c>); R5 <c>$26E6</c>, asked for at <c>$273A</c>.</summary>
    public static readonly SoundSequence StartTwoPlayers = new(0xF0, 0x26E6, [new(1, 16, 0x25)]);

    /// <summary><c>TKDSND</c> "TANK DROP": a quark drops a tank (<c>RRTK4.ASM</c>); R5 <c>$4B31</c>, asked for at <c>$4CCE</c>.</summary>
    public static readonly SoundSequence TankDrop = new(0xD0, 0x4B31, [new(1, 8, 0x19)]);

    /// <summary><c>TKFSND</c> "TANK FIRE" (<c>RRTK4.ASM</c>); R5 <c>$4B11</c>, asked for at <c>$4F8C</c>.</summary>
    public static readonly SoundSequence TankFire = new(0xC8, 0x4B11, [new(1, 8, 0x04)]);

    /// <summary><c>TNKSND</c> "TANK KILL" (<c>RRTK4.ASM</c>); R5 <c>$4B0C</c>, asked for at <c>$4E0A</c>.</summary>
    public static readonly SoundSequence TankKill = new(0xD0, 0x4B0C, [new(1, 8, 0x11)]);

    /// <summary><c>WVSND</c> "WAVE END": the last robot of a wave dies (<c>RRG23.ASM</c>); R5 <c>$26EB</c>, asked for at <c>$2A9C</c>.</summary>
    public static readonly SoundSequence WaveEnd = new(0xE0, 0x26EB, [new(29, 4, 0x0E)]);

    /// <summary>Every table, for the check against the ROM's own bytes.</summary>
    public static IReadOnlyList<SoundSequence> All { get; } =
    [
        BrainKill, BrainShoot, CircleKill, ClearTheSystem, CruiseMissileKill, EnforcerDropOff, EnforcerKill,
        EnforcerShoot, HulkHit, HumanProgFinalConversion, KillAHuman, Laser, PlayerDeath, PostKill, ProgKill,
        Programming, Replay, RobotHit, RobotMove, SaveAHuman, ShellKill, ShellRebound, SparkKill, SquareKill,
        StartOnePlayer, StartTwoPlayers, TankDrop, TankFire, TankKill, WaveEnd,
    ];
}
