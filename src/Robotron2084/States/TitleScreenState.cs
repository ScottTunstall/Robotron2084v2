using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Hud;
using Robotron2084.Input;
using Robotron2084.Level;
using Robotron2084.Persistence;
using Robotron2084.Rendering;
using Robotron2084.Tuning;

namespace Robotron2084.States;

/// <summary>
/// The arcade title screen (notes §94.1): the ROM draws the playfield wall +
/// the player's lives + the score FIRST ($26D2, wall colour $CC), then prints
/// string 128 — "ROBOTRON 2084" — and, in its blink loop, string 129 —
/// "SAVE THE LAST HUMAN FAMILY" — both in the LARGE font, colour slot 10
/// (the ROM's $AA). The ROM's tagline is SAVE, not PROTECT (RRET.ASM:982).
///
/// Port conventions kept on top: **1** starts a one-player game and **2** a
/// two-player game (the arcade's START 1 / START 2 buttons, ROM RRG23
/// START1/START2; fire is the one-player alias), a blinking start prompt, and
/// every 5 seconds the prompt swaps for the saved top-10 high-score list
/// (Phase 11.7). After <see cref="GameplayConstants.TitleIdleSeconds"/> of
/// no start press the arcade's attract demo takes over (Phase 12.1, notes §94).
/// </summary>
public sealed class TitleScreenState : IGameState, IAttractState
{
    private static readonly int Margin = ScreenSize.Scaled(GameplayConstants.PlayfieldMarginSpecPixels);
    private static readonly Rectangle InnerBounds = new(Margin, Margin, ScreenSize.Width - 2 * Margin, ScreenSize.Height - 2 * Margin);

    private const string WelcomeLineOne = "PRESENTED BY";
    private const string WelcomeLineTwo = "WILLIAMS ELECTRONICS INC.";

    /// <summary>
    /// The ROM's own credit strings under the message (notes §103): "DESIGNED BY VID KIDZ" and
    /// "FOR WILLIAMS ELECTRONICS INC." are at ROM $6D85 (with a copy at $7F50 for the copyright).
    /// The reference screen prints them in the SMALL font.
    /// </summary>
    private const string DesignedByLine = "DESIGNED BY VID KIDZ";
    private const string ForWilliamsLine = "FOR WILLIAMS ELECTRONICS INC.";
    private const string CopyrightLine = "COPYRIGHT 1982 WILLIAMS ELECTRONICS INC.";

    /// <summary>
    /// The port's own credit, which the author asked to sit among these lines (notes §102.1). The
    /// arcade's presentation page has no such line, so this one is a deliberate, labelled
    /// addition — in the SMALL font, like the cabinet's own credit lines.
    /// </summary>
    private const string CreditLine = "REVERSE ENGINEERING AND DEVELOPMENT BY SCOTT TUNSTALL";

    // Layout of the presentation page (notes §103). The art is drawn at the port's 2x sprite
    // scale, which is why the logo rows are 58 and 68 px tall. The menu steps by
    // Scaled(TitleOptionRowStepPixels) = 28, so its four rows need 84 px — hence MenuRow 292.
    private const int WordmarkRow = 30;
    private const int Logo2084Row = 96;
    private const int WelcomeRowOne = 176;
    private const int WelcomeRowTwo = 194;
    private const int DesignedByRow = 220;
    private const int ForWilliamsRow = 236;
    private const int CopyrightRow = 252;
    private const int CreditRow = 270;
    private const int MenuRow = 292;

    private readonly IPlayerInputSource _input;
    private readonly SpriteSet _sprites;
    private readonly HighScoreStore _highScores;
    private readonly ControlSettings _controls;
    private readonly GameServices _services;
    private readonly HighScorePalette _colour = new();
    private readonly TimeSpan _idleDuration = TimeSpan.FromSeconds(GameplayConstants.TitleIdleSeconds);
    private TimeSpan _idleElapsed;
    private bool _previousFire;
    private bool _previousStartOne;
    private bool _previousStartTwo;

    public TitleScreenState(GameServices services)
    {
        _services = services;
        _input = services.Input;
        _sprites = services.Sprites;
        _highScores = services.HighScores;
        _controls = services.Controls;

        // The presentation page runs the arcade's attract colour processes (notes §103.3):
        // slot 8 walks COLTAB, so the welcome message and the credit lines shimmer through
        // the oranges the reference screenshot shows, instead of the static grey and white
        // their CRTAB defaults are. The tables and the process set are the decoded ones
        // (§98.5) — the attract page's own call set has not been decoded separately.
        if (_sprites.Palette is { } palette)
        {
            _colour.StartProcesses(palette);
            _colour.StartRamps(palette);
        }
    }

    public void Update(GameTime gameTime, GameStateManager manager)
    {
        if (_sprites.Palette is { } live)
        {
            _colour.Update(live);
        }

        PlayerInputState input = _input.Poll();

        // The arcade's coin-door buttons: START 1 / START 2 pick the number of
        // players (ROM RRG23 START1/START2 → PLRCNT), and the ROM then runs the
        // alternating 2-player game. Fire is kept as a 1-player alias (a port
        // convention — spec.txt's "press fire", which the arcade does not have).
        // The shell's F1/F2/F3 (notes §101) are the same three modes and work from
        // every attract screen, this one included.
        GameMode? mode = null;
        if (input.StartOnePlayerPressed && !_previousStartOne)
        {
            mode = GameMode.OnePlayer;
        }
        else if (input.StartTwoPlayersPressed && !_previousStartTwo)
        {
            mode = GameMode.TwoPlayerAlternate;
        }
        else if (input.FirePressed && !_previousFire)
        {
            mode = GameMode.OnePlayer;
        }

        if (mode is { } chosen)
        {
            StopColours();
            manager.TransitionTo(PlayingState.StartNewGame(_controls, chosen, _sprites, _highScores));
            return;
        }

        _previousFire = input.FirePressed;
        _previousStartOne = input.StartOnePlayerPressed;
        _previousStartTwo = input.StartTwoPlayersPressed;

        // Any button held means a human is at the machine — the arcade's
        // attract only runs while the cabinet sits idle.
        if (input.FirePressed || input.StartOnePlayerPressed || input.StartTwoPlayersPressed)
        {
            _idleElapsed = TimeSpan.Zero;
        }

        // Blink removed with the port's old prompt: the F-key menu is a menu, and
        // flickering it every second made it hard to read (notes §101).

        // The port's old "top ten" swap lived here; the arcade's table is its
        // own screen now (`HighScoreTableState`, notes §98) and the ROM's title
        // page (`FAMPAG`/`SPGSUB`) shows the title and nothing else.

        // Idle long enough: the machine starts playing itself (notes §94.3) — and
        // the arcade plays its STORY first: the ROM's FAMPAG/SPGSUB prints the
        // title, then the page script runs HISTO, whose DONE2 hands over to the
        // phony-player game (notes §95.1/§96).
        _idleElapsed += gameTime.ElapsedGameTime;
        if (_idleElapsed >= _idleDuration)
        {
            _idleElapsed = TimeSpan.Zero;
            StopColours();
            manager.TransitionTo(new StorylineState(_services, new Random()));
        }
    }

    public void Draw(SpriteBatch spriteBatch, SpriteFont font)
    {
        // The caller clears to black. This is the ROM's Williams PRESENTATION page (notes §103):
        // the logos, the operator's welcome message (two 25-character lines) and the credit
        // strings. The arcade draws a border of 28 moving "W" logos round it and a "CREDITS: n"
        // line under the message; the author asked for neither (this port has no credits), and
        // neither the playfield wall nor the score/men belong to this page — the cabinet's other
        // attract page carries those.
        int slot = GameplayConstants.HudScoreSlotCurrent; // the ROM's $AA (slot 10)

        // The two logos, traced from the author's arcade screenshot (notes §103.4) — the R5 CPU
        // ROM we hold has no attract wordmark (§103.2). Drawn at the port's 2x sprite scale.
        DrawCentredLogo(spriteBatch, _sprites.TitleWordmark, WordmarkRow);
        DrawCentredLogo(spriteBatch, _sprites.Title2084, Logo2084Row);

        // The welcome message, exactly as $8822-$8836 prints it: LARGE font, slot 8 then slot 9.
        DrawCenteredLargeText(spriteBatch, WelcomeLineOne, WelcomeRowOne, 8);
        DrawCenteredLargeText(spriteBatch, WelcomeLineTwo, WelcomeRowTwo, 9);
        DrawCenteredSmallText(spriteBatch, DesignedByLine, DesignedByRow, 8);
        DrawCenteredSmallText(spriteBatch, ForWilliamsLine, ForWilliamsRow, 8);
        DrawCenteredSmallText(spriteBatch, CopyrightLine, CopyrightRow, 8);
        DrawCenteredSmallText(spriteBatch, CreditLine, CreditRow, 9);

        // Port-only menu (notes §101): the arcade's presentation page has no such list, and its
        // START buttons still work exactly as they did.
        int y = MenuRow;
        foreach (string option in Options)
        {
            DrawCenteredSmallText(spriteBatch, option, y, slot);
            y += ScreenSize.Scaled(GameplayConstants.TitleOptionRowStepPixels);
        }
    }

    /// <summary>The title's port-only menu, in the author's order (notes §101).</summary>
    private static readonly string[] Options =
    [
        "F1 ONE PLAYER GAME",
        "F2 TWO PLAYER GAME (ALTERNATE)",
        "F3 TWO PLAYER (SIMULTANEOUS)",
        "F10 DEFINE INPUTS",
    ];

    /// <summary>Draws an arcade-small-font line centred on the canvas.</summary>
    /// <summary>
    /// The ROM's colour processes die with the page, and the page after it sets its own slots
    /// (<c>FAMPAG</c>) — so both ways off this screen stand the ROM's CRTAB values back up and
    /// hand slots 10-15 back to the in-game animator (notes §103.3).
    /// </summary>
    private void StopColours()
    {
        if (_sprites.Palette is { } palette)
        {
            _colour.Stop(palette);
        }
    }

    /// <summary>Centres one traced logo horizontally, at the port's 2x sprite scale.</summary>
    private void DrawCentredLogo(SpriteBatch spriteBatch, Texture2D texture, int y)
    {
        int width = texture.Width * ScreenSize.SpecScale;
        int height = texture.Height * ScreenSize.SpecScale;
        var bounds = new Rectangle((ScreenSize.Width - width) / 2, y, width, height);
        _sprites.DrawSprite(spriteBatch, texture, bounds, Color.White);
    }

    private void DrawCenteredSmallText(SpriteBatch spriteBatch, string text, int y, int slot)
    {
        int width = 0;
        foreach (char character in text)
        {
            if (character == ' ')
            {
                width += ScreenSize.Scaled(GameplayConstants.HudSmallFontBlankAdvancePixels);
                continue;
            }

            int index = SpriteSet.GlyphIndex(character);
            if (index >= 0 && index < _sprites.FontSmall.Length)
            {
                width += ScreenSize.Scaled(_sprites.FontSmall[index].Width + GameplayConstants.HudSmallFontGlyphGapPixels);
            }
        }

        _sprites.DrawSmallFontText(spriteBatch, text, (ScreenSize.Width - width) / 2, y, slot);
    }

    /// <summary>Centres a large-font line horizontally and prints it in one slot.</summary>
    private void DrawCenteredLargeText(SpriteBatch spriteBatch, string text, int y, int slot)
    {
        int width = 0;
        foreach (char character in text)
        {
            if (character == ' ')
            {
                width += ScreenSize.Scaled(GameplayConstants.HudSmallFontBlankAdvancePixels);
                continue;
            }

            int index = SpriteSet.GlyphIndex(character);
            if (index < 0 || index >= _sprites.FontLarge.Length)
            {
                continue;
            }

            width += ScreenSize.Scaled(_sprites.FontLarge[index].Width + GameplayConstants.HudSmallFontGlyphGapPixels);
        }

        _sprites.DrawLargeFontText(spriteBatch, text, (ScreenSize.Width - width) / 2, y, slot);
    }

    private static Vector2 CenteredHorizontal(Vector2 unscaledSize, float scale, float y) =>
        new((ScreenSize.Width - unscaledSize.X * scale) / 2, y);
}
