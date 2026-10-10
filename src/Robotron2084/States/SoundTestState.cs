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
/// The SOUND TEST page (opened with F4 from the attract screens, closed with F10): the arcade's own sound test
/// with every sound the board has, each named as the sound ROM's source names it.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>Original source: <c>RRTEST2.ASM</c>, routines <c>SNDSRT</c> and <c>SNDCYC</c> ("SOUND TEST"), called from
/// <c>RRTEST1.ASM</c> (<c>DIAG6</c>); the sounds' names are from <c>VSNDRM3.SRC</c>.</item>
/// <item>Disassembly: <c>SOUND_TEST</c> (<c>$F5A7</c>) and the routines at <c>$FC90</c> to <c>$FCE5</c>.</item>
/// </list>
/// The arcade clears the screen and prints "SOUND LINE" and a number in the large font, in the game's white,
/// and plays one sound; the service switch ADVANCE moves on to the next (and past the last, back to the first),
/// and the AUTO UP switch makes it move on by itself. Left alone, a sound is started again every 64 vblanks (the port waits until the sound has finished, so a long one is not cut off)
/// (<c>LDA #$40</c>, <c>STA PD+4,U</c>), after the board has been silenced and given a vblank to settle
/// (sound lines high, <c>$2C</c>, high again). The arcade steps through the six sound lines, so it only
/// reaches the numbers 1, 2, 4, 8, 16 and 32; this page steps through all 63, which is the one difference.
/// After the 63 comes one entry that is not a single sound: the transporter, the sound of a brain wave's robots
/// being beamed in (<c>RRT2.ASM</c> <c>TRSPRC</c>), which the main board makes by sending sound <c>$12</c> over and over.
/// The port's keys: Enter, Space, Right or Down is ADVANCE, Left or Up goes back one, A is AUTO UP.
/// The fonts have no <c>$</c>, so it is shown as <c>S</c>; apostrophes and <c>#</c> in the source's words are left out.
/// </remarks>
public sealed class SoundTestState : IGameState
{
    /// <summary>The vblanks a sound is left alone before it is started again (<c>LDA #$40</c>).</summary>
    private const int RepeatTicks = 0x40;

    /// <summary>The vblanks the board is given to settle between stopping it and sending the number (<c>NAP 1</c> three times).</summary>
    private const int SettleTicks = 3;

    /// <summary>The ticks the board must have been finished for before the sound is started again: the audio already queued for the speakers is still playing out.</summary>
    private const int QuietTicksNeeded = 6;

    /// <summary>The sound number that silences the board (<c>BGEND</c>, the arcade's <c>$2C</c> sent through the inverting pins).</summary>
    private const int SilenceNumber = 0x13;

    /// <summary>The palette slot the page draws in: the game's white (the page's own heading colour in the other service pages).</summary>
    private const int TextSlot = 9;

    private const int NumberRow = 70;
    private const int NameRow = 120;
    private const int DescriptionRow = 170;
    private const int PositionRow = 200;
    private const int InstructionsRow = 294;
    private const int SecondInstructionRow = 312;
    private const int ExitRow = 342;

    /// <summary>
    /// The page's list: the board's 63 sounds in sound-number order, then the transporter.
    /// </summary>
    private static readonly IReadOnlyList<SoundTestEntry> Entries =
    [
        .. BoardSounds.All.Select(sound => new SoundTestEntry(
            "SOUND " + sound.Number.ToString("X2", CultureInfo.InvariantCulture), sound.Name, sound.Description, sound.Number)),
        new SoundTestEntry("TRANSPORTER", "TRSPRC", "BRAIN WAVE", null),
    ];

    private const string AdvanceLine = "ENTER SPACE RIGHT DOWN - ADVANCE   LEFT UP - BACK";
    private const string AutoLine = "A - AUTO UP";
    private const string ExitLine = "F10 - TITLE";

    private readonly GameServices _services;
    private readonly SpriteSet _sprites;
    private InputSnapshot _previousSnapshot;
    private int _index;
    private bool _isAutoUp;
    private int _settleTicksLeft;
    private int _repeatTicksLeft;
    private int _quietTicks;

    /// <summary>Opens the page and starts the first sound.</summary>
    /// <param name="services">The attract sequence's shared services.</param>
    public SoundTestState(GameServices services)
    {
        _services = services;
        _sprites = services.Sprites;
        _previousSnapshot = InputSnapshot.Read();
        if (_sprites.Blitter.Palette is { } palette)
        {
            palette.SetSlot(TextSlot, GamePalette.DefaultSlotValues[TextSlot]);
        }

        StartSound();
    }

    /// <summary>The entry the page is on.</summary>
    internal SoundTestEntry Current => Entries[_index];

    /// <inheritdoc />
    public void Draw(SpriteBatch spriteBatch, SpriteFont font)
    {
        SoundTestEntry entry = Current;
        DrawLarge(spriteBatch, entry.Heading, NumberRow);
        DrawLarge(spriteBatch, ToDisplay(entry.Name), NameRow);
        DrawSmall(spriteBatch, ToDisplay(entry.Description), DescriptionRow);
        DrawSmall(spriteBatch, $"{_index + 1} OF {Entries.Count}   AUTO UP {(_isAutoUp ? "ON" : "OFF")}", PositionRow);
        DrawSmall(spriteBatch, AdvanceLine, InstructionsRow);
        DrawSmall(spriteBatch, AutoLine, SecondInstructionRow);
        DrawSmall(spriteBatch, ExitLine, ExitRow);
    }

    /// <inheritdoc />
    public void Update(GameTime gameTime, GameStateManager manager)
    {
        InputSnapshot now = InputSnapshot.Read();

        if (IsPressed(now, Keys.F10))
        {
            Silence();
            manager.TransitionTo(new TitleScreenState(_services));
            return;
        }

        if (IsPressed(now, Keys.A))
        {
            _isAutoUp = !_isAutoUp;
        }

        if (IsPressed(now, Keys.Enter) || IsPressed(now, Keys.Space) || IsPressed(now, Keys.Right) || IsPressed(now, Keys.Down))
        {
            MoveTo(_index + 1);
        }
        else if (IsPressed(now, Keys.Left) || IsPressed(now, Keys.Up))
        {
            MoveTo(_index - 1);
        }
        else
        {
            RunTimers();
        }

        _previousSnapshot = now;
    }

    /// <summary>Moves to a sound, wrapping at either end, and starts it.</summary>
    /// <param name="index">The place in <see cref="Entries"/>.</param>
    private void MoveTo(int index)
    {
        int count = Entries.Count;
        _index = ((index % count) + count) % count;
        StartSound();
    }

    /// <summary>Silences the board; the sound itself is sent <see cref="SettleTicks"/> vblanks later (<c>SNDCYC</c>).</summary>
    private void StartSound()
    {
        Silence();
        _settleTicksLeft = SettleTicks;
        _repeatTicksLeft = 0;
        _quietTicks = 0;
    }

    /// <summary>Sends the sound when the board has settled, and starts it again, or moves on in AUTO UP, when it has been left for a while.</summary>
    private void RunTimers()
    {
        if (_settleTicksLeft > 0)
        {
            _settleTicksLeft--;
            if (_settleTicksLeft == 0)
            {
                Play(Current);
                _repeatTicksLeft = RepeatTicks;
            }

            return;
        }

        _repeatTicksLeft--;
        _quietTicks = Sound.IsPlaying || Sound.IsTransporterRunning() ? 0 : _quietTicks + 1;
        if (_repeatTicksLeft > 0 || _quietTicks < QuietTicksNeeded)
        {
            return;
        }

        if (_isAutoUp)
        {
            MoveTo(_index + 1);
        }
        else
        {
            StartSound();
        }
    }

    /// <summary>Stops whatever is playing: the transporter's sends, and the sound on the board.</summary>
    private static void Silence()
    {
        Sound.StopTransporter();
        Sound.SendDirect(SilenceNumber);
    }

    /// <summary>Starts an entry: sends its sound number, or starts the transporter when it has none.</summary>
    /// <param name="entry">The entry.</param>
    private static void Play(SoundTestEntry entry)
    {
        if (entry.SoundNumber is { } soundNumber)
        {
            Sound.SendDirect(soundNumber);
        }
        else
        {
            Sound.PlayTransporter();
        }
    }

    /// <summary>True on the tick a key goes down.</summary>
    private bool IsPressed(InputSnapshot now, Keys key) => now.Keys.IsKeyDown(key) && !_previousSnapshot.Keys.IsKeyDown(key);

    /// <summary>The source's words as the fonts can print them: <c>$</c> as <c>S</c>, and nothing for the marks the fonts lack.</summary>
    /// <param name="text">The source's words.</param>
    /// <returns>Words in the fonts' characters.</returns>
    internal static string ToDisplay(string text) =>
        new([.. text.Replace('$', 'S').Where(character => ArcadeText.GetGlyphIndex(character) >= 0 || character == ' ')]);

    private void DrawLarge(SpriteBatch spriteBatch, string text, int y)
    {
        int width = ScreenSize.ToPortPixelsFromArcadePixels(_sprites.TextRenderer.MeasureLargeText(text));
        _sprites.TextRenderer.DrawLargeFontText(spriteBatch, text, (ScreenSize.Width - width) / 2, y, TextSlot);
    }

    private void DrawSmall(SpriteBatch spriteBatch, string text, int y)
    {
        int width = ScreenSize.ToPortPixelsFromArcadePixels(_sprites.TextRenderer.MeasureSmallText(text));
        _sprites.TextRenderer.DrawSmallFontText(spriteBatch, text, (ScreenSize.Width - width) / 2, y, TextSlot);
    }
}
