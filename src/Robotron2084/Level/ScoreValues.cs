namespace Robotron2084.Level;

/// <summary>
/// Point values per kill — the arcade's exact values (arcade-fidelity-notes §11.3).
/// Decoded from the original source's SCORE
/// calls (A = trailing-zero count, B = BCD digits) and confirmed against the
/// title-screen table (RRET.ASM) and the R5 disassembly:
///
/// Grunt 100 · Spheroid 1000 · Quark 1000 · Enforcer 150 · Tank 200 ·
/// Brain 500 · Cruise missile (brain's) 25 · Prog 100 · Spark 25 ·
/// Tank shell 25 · Electrode 0 (no score call — destroying a post scores
/// nothing) · Hulk 0 (indestructible).
///
/// The player never loses points. Rescue bonuses (human saved: 1000-5000
/// by the running save count, SVITAB) are applied where rescue happens,
/// not here.
/// </summary>
public static class ScoreValues
{
    public const int Electrode = 0;
    public const int Grunt = 100;
    public const int Spheroid = 1000;
    public const int Enforcer = 150;
    public const int Quark = 1000;
    public const int Tank = 200;
    public const int Brain = 500;
    public const int CruiseMissile = 25;
    public const int Prog = 100;
    public const int Spark = 25;
    public const int TankShell = 25;

    /// <summary>Rescue bonus by running save count (1-based; capped at 5). ROM SVITAB.</summary>
    public const int RescueBonusMin = 1000;

    public static int RescueBonus(int savesThisGame)
    {
        int index = Math.Clamp(savesThisGame, 1, 5);
        return RescueBonusMin * index;
    }
}
