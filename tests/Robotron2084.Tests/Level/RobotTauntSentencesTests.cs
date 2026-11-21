using Robotron2084.Audio.Models;
using Robotron2084.Level;
using Xunit;
using static Robotron2084.Audio.Models.BerzerkWord;

namespace Robotron2084.Tests.Level;

/// <summary>The Berzerk robot's taunt sentences: exactly the author's, in each list.</summary>
public sealed class RobotTauntSentencesTests
{
    private static readonly BerzerkWord[] OrderWords = [Get, Charge, Attack, Destroy, Shoot, Kill];

    /// <summary>Turns each sentence into its words with spaces between, so a list can be compared as text.</summary>
    private static string[] GetSentenceTexts(IEnumerable<IReadOnlyList<BerzerkWord>> sentences)
    {
        return [.. sentences.Select(sentence => string.Join(' ', sentence)).Order()];
    }

    /// <summary>Makes the expected sentences: each order with each target, then each escaper's MUST NOT ESCAPE.</summary>
    private static string[] GetExpectedTexts(BerzerkWord[][] targetWords, BerzerkWord[] escaperWords,
        params BerzerkWord[][] otherSentences)
    {
        var orderSentences = OrderWords.SelectMany(orderWord =>
            targetWords.Select(target => (IReadOnlyList<BerzerkWord>)[orderWord, .. target]));
        var escapeSentences = escaperWords.Select(escaperWord => (IReadOnlyList<BerzerkWord>)
            [The, escaperWord, Must, Not, Escape]);
        return GetSentenceTexts([.. orderSentences, .. escapeSentences, .. otherSentences]);
    }

    [Fact]
    public void OnePlayer_IsTheOrdersAtItTheIntruderOrTheHumanoid_AndTheIntruderOrHumanoidMustNotEscape()
    {
        var expected = GetExpectedTexts([[It], [The, Intruder], [The, Humanoid]], [Intruder, Humanoid]);

        Assert.Equal(expected, GetSentenceTexts(RobotTauntSentences.OnePlayer));
        Assert.Equal(20, RobotTauntSentences.OnePlayer.Count);
        Assert.Contains(RobotTauntSentences.OnePlayer, sentence => sentence.SequenceEqual([Get, The, Humanoid]));
    }

    [Fact]
    public void OnePlayerChicken_AddsTheChicken_ToTheOrdersAndToMustNotEscape()
    {
        var expected = GetExpectedTexts([[It], [The, Intruder], [The, Humanoid], [The, Chicken]],
            [Intruder, Humanoid, Chicken]);

        Assert.Equal(expected, GetSentenceTexts(RobotTauntSentences.OnePlayerChicken));
    }

    [Fact]
    public void TwoPlayers_IsKillIt_TheOrdersAtTheIntrudersOrTheHumanoids_AndTheHumanoidsMustNotEscape()
    {
        var expected = GetExpectedTexts([[The, Intruders], [The, Humanoids]], [Humanoids], [Kill, It]);

        Assert.Equal(expected, GetSentenceTexts(RobotTauntSentences.TwoPlayers));
    }

    [Fact]
    public void TwoPlayersChicken_IsTheOrdersAtItTheIntrudersTheChickensOrTheHumanoids_AndTheHumanoidsMustNotEscape()
    {
        var expected = GetExpectedTexts([[It], [The, Intruders], [The, Chickens], [The, Humanoids]], [Humanoids]);

        Assert.Equal(expected, GetSentenceTexts(RobotTauntSentences.TwoPlayersChicken));
    }

    [Fact]
    public void
        FamilyWithHulksOrBrains_IsTheOrdersAtItTheIntrudersTheFamilyOrTheHumans_AndTheHumansOrFamilyMustNotEscape()
    {
        var expected = GetExpectedTexts([[It], [The, Intruders], [The, Family], [The, Humans]], [Humans, Family]);

        Assert.Equal(expected, GetSentenceTexts(RobotTauntSentences.FamilyWithHulksOrBrains));
    }

    [Fact]
    public void OnlyTheChickenLists_SayChickenOrChickens()
    {
        IReadOnlyList<BerzerkWord>[] notChickenSentences =
        [
            .. RobotTauntSentences.OnePlayer, .. RobotTauntSentences.TwoPlayers,
            .. RobotTauntSentences.FamilyWithHulksOrBrains
        ];

        Assert.DoesNotContain(notChickenSentences,
            sentence => sentence.Contains(Chicken) || sentence.Contains(Chickens));
    }
}
