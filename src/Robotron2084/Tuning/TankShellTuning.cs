namespace Robotron2084.Tuning;

/// <summary>How a tank shell is aimed, how fast it flies and how long it lasts.</summary>
public static class TankShellTuning
{
    /// <summary>The fewest turns a shell gets before it fizzles out. A turn is <see cref="BodyRomFrames"/> ROM frames.</summary>
    /// <remarks>Original source: <c>RRTK4.ASM</c> <c>TNKFX1</c>, <c>ADDA #$30</c>. Disassembly: <c>CREATE_TANK_SHELL</c> (<c>$4E46</c>) at <c>$4F88</c>.</remarks>
    public const int LifeBaseTurns = 48;

    /// <summary>Extra random turns on top of <see cref="LifeBaseTurns"/>: from none up to one less than this.</summary>
    /// <remarks>Original source: <c>RRTK4.ASM</c> <c>TNKFX1</c>, <c>ANDA #$1F</c>. Disassembly: <c>CREATE_TANK_SHELL</c> (<c>$4E46</c>) at <c>$4F86</c>.</remarks>
    public const int LifeExtraTurnsMaxExclusive = 32;

    /// <summary>How many ROM frames pass between one turn of a shell and the next.</summary>
    /// <remarks>Original source: <c>RRTK4.ASM</c> <c>SHELLP</c>, <c>NAP 2</c>. Disassembly: <c>MAKE_TANK_SHELL_BOUNCE_IF_HITS_BORDER_WALL</c> (<c>$4F94</c>) at <c>$4FB3</c>.</remarks>
    public const int BodyRomFrames = 2;

    /// <summary>How far to the right of the tank's top-left corner a shell starts, in columns.</summary>
    /// <remarks>Original source: <c>RRTK4.ASM</c> <c>TNKFIR</c>, <c>ADDD #$0100</c>. Disassembly: <c>CREATE_TANK_SHELL</c> (<c>$4E46</c>) at <c>$4E70</c>.</remarks>
    public const int StartOffsetColumns = 1;

    /// <summary>A shot is a rebound shot, aimed at a wall, when a roll of 0 to 255 comes up at or above this.</summary>
    /// <remarks>Original source: <c>RRTK4.ASM</c> <c>TNKFIR</c>, <c>LDB #$80 / CMPB SEED / BLS TNKFRB</c>. Disassembly: <c>CREATE_TANK_SHELL</c> (<c>$4E46</c>) at <c>$4E79</c>.</remarks>
    public const int ReboundRollAtOrAbove = 0x80;

    /// <summary>How many different rolls there are for the shot type, 0 to 255.</summary>
    public const int RollSides = 256;

    /// <summary>How far an aimed shot can miss its target on either axis: from minus this to one less than this, in columns sideways and rows up and down.</summary>
    /// <remarks>Original source: <c>RRTK4.ASM</c> <c>TNKF1</c> and <c>TNKF5</c>, <c>ANDB #$1F / ADDB #-$10</c>. Disassembly: <c>CREATE_TANK_SHELL</c> (<c>$4E46</c>) at <c>$4E81</c> and <c>$4EAD</c>.</remarks>
    public const int AimMissRange = 16;

    /// <summary>The player is too near the left wall for a sideways miss when they are fewer than this many columns from it.</summary>
    /// <remarks>Original source: <c>RRTK4.ASM</c> <c>TNKFIR</c>, <c>CMPA #$11</c> with the wall at column 7, so 10 columns. Disassembly: <c>CREATE_TANK_SHELL</c> (<c>$4E46</c>) at <c>$4E87</c>.</remarks>
    public const int NoSidewaysMissColumnsFromLeftWall = 10;

    /// <summary>How the speed of an aimed shot is scaled: the gap to the target times the wave's shell setting, divided by this, then times <see cref="AimedSpeedMultiplier"/>.</summary>
    /// <remarks>Original source: <c>RRTK4.ASM</c> <c>TNKF3</c> and <c>TNKF5</c>, <c>MUL</c> keeping the top byte. Disassembly: <c>CREATE_TANK_SHELL</c> (<c>$4E46</c>) at <c>$4E9B</c> and <c>$4EC0</c>.</remarks>
    public const int AimedSpeedDivisor = 256;

    /// <summary>The gap-times-setting result is multiplied by this to get an aimed shot's speed.</summary>
    /// <remarks>Original source: <c>RRTK4.ASM</c> <c>TNKF4</c> and <c>TNKF6</c>, three <c>ASLB / ROLA</c> pairs. Disassembly: <c>CREATE_TANK_SHELL</c> (<c>$4E46</c>) at <c>$4EA3</c> and <c>$4EC8</c>.</remarks>
    public const int AimedSpeedMultiplier = 8;

    /// <summary>The widest up-and-down miss of a rebound shot aimed at a side wall, from minus this to one less than this, in rows.</summary>
    /// <remarks>Original source: <c>RRTK4.ASM</c> <c>TNKFRB</c>, <c>ANDB #$1F / ADDB #-$10</c>. Disassembly: <c>CREATE_TANK_SHELL</c> (<c>$4E46</c>) at <c>$4EDD</c>.</remarks>
    public const int ReboundHeightMissRange = 16;

    /// <summary>The widest sideways miss of a rebound shot aimed at the top or bottom wall, from minus this to one less than this, in columns.</summary>
    /// <remarks>Original source: <c>RRTK4.ASM</c> <c>TRBY</c>, <c>ANDB #$F / ADDB #-8</c>. Disassembly: <c>CREATE_TANK_SHELL</c> (<c>$4E46</c>) at <c>$4F06</c>.</remarks>
    public const int ReboundWidthMissRange = 8;

    /// <summary>How many different rolls decide whether a rebound shot goes for the player's own side, 0 to 7.</summary>
    /// <remarks>Original source: <c>RRTK4.ASM</c> <c>TNKFRB</c> and <c>TRBY</c>, <c>ANDA #7</c>. Disassembly: <c>CREATE_TANK_SHELL</c> (<c>$4E46</c>) at <c>$4EEB</c> and <c>$4F14</c>.</remarks>
    public const int ReboundSideRollSides = 8;

    /// <summary>The roll that sends a rebound shot to the wall on the far side from the player instead of the near side.</summary>
    public const int ReboundFarSideRoll = 0;

    /// <summary>The setting is turned into a speed floor by multiplying by this and keeping the top byte, then by <see cref="ReboundFloorMultiplier"/>.</summary>
    /// <remarks>Original source: <c>RRTK4.ASM</c> <c>TRBXY</c>, <c>LDA #$40 / MUL</c>. Disassembly: <c>CREATE_TANK_SHELL</c> (<c>$4E46</c>) at <c>$4F42</c>.</remarks>
    public const int ReboundFloorFactor = 0x40;

    /// <summary>The top byte of the setting times <see cref="ReboundFloorFactor"/> is multiplied by this to give the sideways speed floor of a rebound shot.</summary>
    /// <remarks>Original source: <c>RRTK4.ASM</c> <c>TRBXY</c>, two <c>ASLB / ROLA</c> pairs. Disassembly: <c>CREATE_TANK_SHELL</c> (<c>$4E46</c>) at <c>$4F48</c>.</remarks>
    public const int ReboundFloorMultiplier = 4;

    /// <summary>A rebound shot's up-and-down speed floor is this many times its sideways one.</summary>
    /// <remarks>Original source: <c>RRTK4.ASM</c> <c>TRBXY</c>, the extra <c>ASLB / ROLA</c> before the second pair of limits. Disassembly: <c>CREATE_TANK_SHELL</c> (<c>$4E46</c>) at <c>$4F52</c>.</remarks>
    public const int ReboundUpAndDownFloorFactor = 2;

    /// <summary>The most times a rebound shot's speed is doubled while it is brought up to the speed floor.</summary>
    /// <remarks>The ROM loops until the shot is fast enough, and never ends if the shot starts with no speed at all. This stops that.</remarks>
    public const int ReboundMaxDoublings = 16;
}
