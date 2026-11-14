using Robotron2084.Graphics;
using Robotron2084.Level;
using Robotron2084.States;
using Xunit;

namespace Robotron2084.Tests.States;

/// <summary>The menu page's rows: what each says, and that the small font can print them.</summary>
public sealed class MainMenuStateTests
{
    [Theory]
    [InlineData(MainMenuLine.StartGame, "PLAYER MODE")]
    [InlineData(MainMenuLine.GameStyle, "GAME STYLE")]
    [InlineData(MainMenuLine.Settings, "SETTINGS")]
    [InlineData(MainMenuLine.DeveloperTools, "F10 - DEVELOPER TOOLS")]
    public void EachRowSaysItsShortcutKeysAndWhatItDoes(MainMenuLine line, string text)
    {
        Assert.Equal(text, MainMenuState.GetLabel(line));
    }

    [Theory]
    [InlineData(GameMode.OnePlayer, "START ONE PLAYER GAME (F1)")]
    [InlineData(GameMode.TwoPlayerAlternate, "START TWO PLAYER ALTERNATE (F2)")]
    [InlineData(GameMode.TwoPlayerSimultaneous, "START TWO PLAYER SIMULTANEOUS (F3)")]
    public void TheGameRowShowsTheGameChosen(GameMode gameMode, string text)
    {
        Assert.Equal(text, MainMenuState.GetValue(MainMenuLine.StartGame, gameMode, GameStyle.Classic));
    }

    [Fact]
    public void TheGameStyleRowShowsTheGameStyleChosen()
    {
        Assert.Equal("CLASSIC", MainMenuState.GetValue(MainMenuLine.GameStyle, GameMode.OnePlayer, GameStyle.Classic));
        Assert.Equal("EXTENDED", MainMenuState.GetValue(MainMenuLine.GameStyle, GameMode.OnePlayer, GameStyle.Extended));
    }

    [Theory]
    [InlineData(MainMenuLine.Settings)]
    [InlineData(MainMenuLine.DeveloperTools)]
    public void ARowThatOpensAPageHasNoValue(MainMenuLine line)
    {
        Assert.Equal(string.Empty, MainMenuState.GetValue(line, GameMode.OnePlayer, GameStyle.Classic));
    }

    [Fact]
    public void EveryRowAndValueIsInTheSmallFontsCharacters()
    {
        List<string> texts = [];
        foreach (var line in Enum.GetValues<MainMenuLine>())
        {
            texts.Add(MainMenuState.GetLabel(line));
            foreach (var gameMode in Enum.GetValues<GameMode>())
            foreach (var gameStyle in Enum.GetValues<GameStyle>())
                texts.Add(MainMenuState.GetValue(line, gameMode, gameStyle));
        }

        foreach (var character in texts.SelectMany(text => text))
            Assert.True(character == ' ' || ArcadeText.GetGlyphIndex(character) is >= 0 and < 38 or 43 or 36 or 37, $"'{character}'");
    }
}
