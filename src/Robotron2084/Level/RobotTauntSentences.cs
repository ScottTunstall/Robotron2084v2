using Robotron2084.Audio.Models;
using static Robotron2084.Audio.Models.BerzerkWord;

namespace Robotron2084.Level;

/// <summary>
///     Every sentence the Berzerk robot can taunt with during a wave, in one list for each kind of game and player. Port
///     only.
/// </summary>
/// <remarks>
///     <list type="bullet">
///         <item>
///             An order is one of GET, CHARGE, ATTACK, DESTROY, SHOOT and KILL: Berzerk's own first words
///             (<c>berzerk.asm</c> <c>ROBOT_FIRST_WORD_SPEECH_TABLE</c>, <c>$2C2C</c>).
///         </item>
///         <item>
///             Each list holds every whole sentence once, so a sentence picked from a list at random is as likely as any
///             other in it.
///         </item>
///         <item>The sentences are the author's. The Berzerk game had some of them, but not these lists.</item>
///     </list>
/// </remarks>
public static class RobotTauntSentences
{
    /// <summary>The words that start an order: GET, CHARGE, ATTACK, DESTROY, SHOOT and KILL.</summary>
    private static readonly BerzerkWord[] OrderWords = [Get, Charge, Attack, Destroy, Shoot, Kill];

    /// <summary>
    ///     Family members on the field with a hulk or a brain, in any game: an order then IT, THE INTRUDERS, THE FAMILY or
    ///     THE HUMANS; or THE HUMANS or THE FAMILY MUST NOT ESCAPE.
    /// </summary>
    public static IReadOnlyList<IReadOnlyList<BerzerkWord>> FamilyWithHulksOrBrains { get; } =
        CreateSentences([[It], [The, Intruders], [The, Family], [The, Humans]], [Humans, Family]);

    /// <summary>
    ///     One player on the field, in a one-player game or a two-player game where the players take turns, who is not a
    ///     chicken: an order then IT, THE INTRUDER or THE HUMANOID; or THE INTRUDER or THE HUMANOID MUST NOT ESCAPE.
    /// </summary>
    public static IReadOnlyList<IReadOnlyList<BerzerkWord>> OnePlayer { get; } =
        CreateSentences([[It], [The, Intruder], [The, Humanoid]], [Intruder, Humanoid]);

    /// <summary>
    ///     One player on the field, as in <see cref="OnePlayer" />, who is a chicken: an order then IT, THE INTRUDER,
    ///     THE HUMANOID or THE CHICKEN; or THE INTRUDER, THE HUMANOID or THE CHICKEN MUST NOT ESCAPE.
    /// </summary>
    public static IReadOnlyList<IReadOnlyList<BerzerkWord>> OnePlayerChicken { get; } =
        CreateSentences([[It], [The, Intruder], [The, Humanoid], [The, Chicken]], [Intruder, Humanoid, Chicken]);

    /// <summary>
    ///     Two players on the field at once, neither of them a chicken: KILL IT; an order then THE INTRUDERS or
    ///     THE HUMANOIDS; or THE HUMANOIDS MUST NOT ESCAPE.
    /// </summary>
    public static IReadOnlyList<IReadOnlyList<BerzerkWord>> TwoPlayers { get; } =
        [[Kill, It], .. CreateSentences([[The, Intruders], [The, Humanoids]], [Humanoids])];

    /// <summary>
    ///     Two players on the field at once, either of them a chicken: an order then IT, THE INTRUDERS, THE CHICKENS or
    ///     THE HUMANOIDS; or THE HUMANOIDS MUST NOT ESCAPE.
    /// </summary>
    public static IReadOnlyList<IReadOnlyList<BerzerkWord>> TwoPlayersChicken { get; } =
        CreateSentences([[It], [The, Intruders], [The, Chickens], [The, Humanoids]], [Humanoids]);

    /// <summary>
    ///     Makes every order sentence for some targets, followed by a MUST NOT ESCAPE sentence for each of some escapers.
    /// </summary>
    /// <param name="targetWords">What can follow an order, such as THE INTRUDER.</param>
    /// <param name="escaperWords">Who must not escape, each after THE, such as INTRUDER.</param>
    /// <returns>Each order with each target, then THE, each escaper, MUST NOT ESCAPE.</returns>
    private static IReadOnlyList<IReadOnlyList<BerzerkWord>> CreateSentences(BerzerkWord[][] targetWords,
        BerzerkWord[] escaperWords)
    {
        var orderSentences =
            OrderWords.SelectMany(orderWord =>
                targetWords.Select(target => (IReadOnlyList<BerzerkWord>)[orderWord, .. target]));
        var escapeSentences =
            escaperWords.Select(escaperWord => (IReadOnlyList<BerzerkWord>)[The, escaperWord, Must, Not, Escape]);
        return [.. orderSentences, .. escapeSentences];
    }
}
