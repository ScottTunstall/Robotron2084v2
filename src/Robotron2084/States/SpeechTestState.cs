using System.Globalization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Robotron2084.Audio;
using Robotron2084.Core;
using Robotron2084.Graphics;
using Robotron2084.Input;
using Robotron2084.Palette;

namespace Robotron2084.States;

/// <summary>
///     The SPEECH TEST page (opened from the DEVELOPER TOOLS page, closed with F10 back to it): every speech line, one at
///     a time, so each can be heard.
/// </summary>
/// <remarks>
///     <list type="bullet">
///         <item>Port only: the arcade has no speech sound chip, so this page is not in the arcade game (notes §149).</item>
///         <item>It works like the SOUND TEST page (<see cref="SoundTestState" />) and looks like it.</item>
///         <item>
///             The page's keys: Enter, Space, Right or Down is ADVANCE, Left or Up goes back one, A is AUTO UP, F10 closes
///             the page.
///         </item>
///         <item>
///             A line is spoken on the speech voice (<see cref="Speech" />), so moving to another line never cuts it off:
///             the new line waits until the one being spoken has finished.
///         </item>
///         <item>
///             Left alone, a line is spoken again a short while after it has finished. In AUTO UP the page moves on to the
///             next line instead.
///         </item>
///     </list>
/// </remarks>
public sealed class SpeechTestState : IGameState
{
    private const string AdvanceLine = "ENTER SPACE RIGHT DOWN - ADVANCE   LEFT UP - BACK";

    private const string AutoLine = "A - AUTO UP";

    private const string Description = "SPEECH SAMPLE";

    private const int DescriptionRow = 170;

    private const string ExitLine = "F10 - DEVELOPER TOOLS";

    private const int ExitRow = 342;

    private const string HeadingWord = "SPEECH ";

    private const int InstructionsRow = 294;

    private const int NameRow = 120;

    private const int NumberRow = 70;

    private const int PositionRow = 200;

    /// <summary>How many port ticks a line is left alone, after it has finished, before it is spoken again.</summary>
    private const int RepeatTicks = 64;

    private const int SecondInstructionRow = 312;

    /// <summary>The palette index the page draws in: the game's white, as the SOUND TEST page does.</summary>
    private const int TextPaletteIndex = 9;

    /// <summary>The names of the lines, in the order of <see cref="SpeechLinePaths" />.</summary>
    private static readonly IReadOnlyList<string> LineNames =
        SpeechLinePaths.All.Select(path => Path.GetFileName(path).ToUpperInvariant()).ToList();

    private readonly GameServices _gameServices;
    private readonly TextureSet _textureSet;
    private int _index;
    private bool _isAutoUp;
    private InputSnapshot _previousSnapshot;
    private int _quietTicks;

    /// <summary>Opens the page and speaks the first line.</summary>
    /// <param name="gameServices">The shared services.</param>
    public SpeechTestState(GameServices gameServices)
    {
        _gameServices = gameServices;
        _textureSet = gameServices.TextureSet;
        _previousSnapshot = InputSnapshot.Read();
        if (_textureSet.BlitterDraw.GamePalette is { } palette)
            palette.SetPaletteIndexValue(TextPaletteIndex, GamePalette.DefaultPaletteValues[TextPaletteIndex]);

        SpeakCurrentLine();
    }

    /// <summary>Gets the name of the line the page is on, or an empty string when there are no lines.</summary>
    internal string GetCurrentLineName()
    {
        if (LineNames.Count == 0) return string.Empty;

        return LineNames[_index];
    }

    /// <inheritdoc />
    public void Draw(SpriteBatch spriteBatch, SpriteFont font)
    {
        var number = (_index + 1).ToString(CultureInfo.InvariantCulture);
        DrawLarge(spriteBatch, HeadingWord + number, NumberRow);
        DrawLarge(spriteBatch, SoundTestState.ToDisplay(GetCurrentLineName()), NameRow);
        DrawSmall(spriteBatch, Description, DescriptionRow);
        var autoUpWord = "OFF";
        if (_isAutoUp) autoUpWord = "ON";

        DrawSmall(spriteBatch, $"{_index + 1} OF {LineNames.Count}   AUTO UP {autoUpWord}", PositionRow);
        DrawSmall(spriteBatch, AdvanceLine, InstructionsRow);
        DrawSmall(spriteBatch, AutoLine, SecondInstructionRow);
        DrawSmall(spriteBatch, ExitLine, ExitRow);
    }

    /// <inheritdoc />
    public void Update(GameTime gameTime, GameStateManager gameStateManager)
    {
        var now = InputSnapshot.Read();

        if (IsPressed(now, Keys.F10))
        {
            gameStateManager.TransitionTo(new DeveloperToolsState(_gameServices));
            return;
        }

        if (IsPressed(now, Keys.A)) _isAutoUp = !_isAutoUp;

        if (IsPressed(now, Keys.Enter) || IsPressed(now, Keys.Space) || IsPressed(now, Keys.Right) ||
            IsPressed(now, Keys.Down))
            MoveTo(_index + 1);
        else if (IsPressed(now, Keys.Left) || IsPressed(now, Keys.Up))
            MoveTo(_index - 1);
        else
            RunTimer();

        _previousSnapshot = now;
    }

    private void DrawLarge(SpriteBatch spriteBatch, string text, int y)
    {
        var width = ScreenSize.ToPortPixelsFromArcadePixels(_textureSet.ArcadeText.MeasureLargeText(text));
        _textureSet.ArcadeText.DrawLargeFontText(spriteBatch, text, (ScreenSize.Width - width) / 2, y, TextPaletteIndex);
    }

    private void DrawSmall(SpriteBatch spriteBatch, string text, int y)
    {
        var width = ScreenSize.ToPortPixelsFromArcadePixels(_textureSet.ArcadeText.MeasureSmallText(text));
        _textureSet.ArcadeText.DrawSmallFontText(spriteBatch, text, (ScreenSize.Width - width) / 2, y, TextPaletteIndex);
    }

    /// <summary>Gets whether a key went down on this tick.</summary>
    private bool IsPressed(InputSnapshot now, Keys key)
    {
        return now.KeyboardState.IsKeyDown(key) && !_previousSnapshot.KeyboardState.IsKeyDown(key);
    }

    /// <summary>Moves to a line, wrapping at either end, and speaks it.</summary>
    /// <param name="index">The place in the list of lines.</param>
    private void MoveTo(int index)
    {
        var count = LineNames.Count;
        if (count == 0) return;

        _index = (index % count + count) % count;
        SpeakCurrentLine();
    }

    /// <summary>Once the speech has been quiet for a while, speaks the line again, or moves on in AUTO UP.</summary>
    private void RunTimer()
    {
        if (Speech.IsBusy())
        {
            _quietTicks = 0;
            return;
        }

        _quietTicks++;
        if (_quietTicks < RepeatTicks) return;

        if (_isAutoUp)
            MoveTo(_index + 1);
        else
            SpeakCurrentLine();
    }

    /// <summary>Asks for the current line. It waits for any line still being spoken, which is never cut off.</summary>
    private void SpeakCurrentLine()
    {
        _quietTicks = 0;
        var lineName = GetCurrentLineName();
        if (lineName.Length > 0) Speech.Say(lineName);
    }
}
