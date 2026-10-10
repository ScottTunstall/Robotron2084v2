namespace Robotron2084.Level;

/// <summary>The points the player scores for each kind of kill, and for rescuing a human.</summary>
/// <remarks>
///     <list type="bullet">
///         <item>
///             Original source: the <c>SCORE</c> calls in each robot's routines,
///             checked against the title screen's score table in <c>RRET.ASM</c>
///         </item>
///         <item>Disassembly: the calls to the score update routine at <c>$DB9C</c></item>
///     </list>
///     An electrode
///     scores nothing, and a hulk cannot be killed. The player never loses points. A rescue's bonus is
///     given where the rescue happens (<see cref="RescueBonus" />), not here (notes §11.3).</item>
///     </list>
/// </remarks>
public static class ScoreValues
{
    /// <summary>The points for killing a brain.</summary>
    public const int Brain = 500;

    /// <summary>
    ///     The points for killing a BerzerkRobot. It is worth what a grunt is, because it is the author's own robot and
    ///     the arcade has no score for it.
    /// </summary>
    public const int BerzerkRobot = 100;

    /// <summary>The points for shooting a cruise missile.</summary>
    public const int CruiseMissile = 25;

    /// <summary>The points for shooting an electrode, which is nothing.</summary>
    public const int Electrode = 0;

    /// <summary>The points for killing an enforcer.</summary>
    public const int Enforcer = 150;

    /// <summary>The points for killing a grunt.</summary>
    public const int Grunt = 100;

    /// <summary>
    ///     The points for killing a Gorf. It is worth what a grunt is, because it is the author's own robot and the
    ///     arcade has no score for it.
    /// </summary>
    public const int Gorf = 100;

    /// <summary>The points for killing a prog.</summary>
    public const int Prog = 100;

    /// <summary>The points for killing a quark.</summary>
    public const int Quark = 1000;

    /// <summary>The number of rescues after which the rescue bonus stops growing.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRH11.ASM</c> <c>SVITAB</c>.</item>
    ///         <item>Disassembly: not separately labelled.</item>
    ///     </list>
    /// </remarks>
    public const int RescueBonusMaxCount = 5;

    /// <summary>
    ///     The bonus for a life's first rescue, which each later rescue adds another of, up to
    ///     <see cref="RescueBonusMaxCount" />.
    /// </summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRH11.ASM</c> <c>SVITAB</c>.</item>
    ///         <item>Disassembly: not separately labelled.</item>
    ///     </list>
    /// </remarks>
    public const int RescueBonusMin = 1000;

    /// <summary>The points for shooting a spark.</summary>
    public const int Spark = 25;

    /// <summary>The points for killing a spheroid.</summary>
    public const int Spheroid = 1000;

    /// <summary>The points for killing a tank.</summary>
    public const int Tank = 200;

    /// <summary>The points for shooting a tank shell.</summary>
    public const int TankShell = 25;

    /// <summary>Works out the bonus for rescuing a human. It grows with each rescue in a life.</summary>
    /// <param name="rescuesThisLife">
    ///     How many humans the player has rescued this life, counting this one. The bonus stops
    ///     growing at <see cref="RescueBonusMaxCount" />.
    /// </param>
    public static int RescueBonus(int rescuesThisLife)
    {
        var index = Math.Clamp(rescuesThisLife, 1, RescueBonusMaxCount);
        return RescueBonusMin * index;
    }
}
