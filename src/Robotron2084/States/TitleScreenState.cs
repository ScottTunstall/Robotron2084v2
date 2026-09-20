using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Input;
using Robotron2084.Level;
using Robotron2084.Persistence;
using Robotron2084.Rendering;
using Robotron2084.Tuning;

namespace Robotron2084.States;

/// <summary>
/// The arcade's Williams PRESENTATION page (notes §103; builder at ROM $87A6 onward): the
/// "ROBOTRON:" wordmark and the "2084" mark over the operator's attract-mode welcome message
/// (<c>$8822</c>-<c>$8836</c> prints two 25-character lines in the LARGE font, an empty row apart),
/// then the Vid Kidz / Williams credit strings in the SMALL font.
///
/// COLOUR — the page's OWN decoded set (notes §106): the ROM writes seven colours into palette
/// entries 1-7 (`$8A3A` copying the table at `$8A70`) and chases a WHITE flash through them every
/// three frames (`$8A4F`). The message and the page's credit strings are drawn in entry 6 (the
/// text operand `$66`), so they are ORANGE with a white sweep — the author's own report — and the
/// traced wordmark cycles through the same seven (notes §104). §103.3 ran the high score page's
/// process set here as a stand-in while this was undecoded. The ROM's OTHER attract page (the wall
/// + "ROBOTRON 2084" + "SAVE THE LAST HUMAN FAMILY", notes §94.1) is a different screen and is not
/// drawn here.
///
/// Port conventions kept on top: **1** starts a one-player game and **2** a two-player game (the
/// arcade's START 1 / START 2 buttons, ROM RRG23 START1/START2; fire is the one-player alias),
/// the port's F1/F2/F3/F10 menu (notes §101), and after
/// <see cref="GameplayConstants.TitleIdleSeconds"/> of no start press the arcade's attract movie
/// takes over (notes §95/§96).
///
/// TEXT (author, 2026-09-20; notes §107): the page's two text panes alternate every
/// <see cref="GameplayConstants.TitleTextSwapSeconds"/> — the arcade's own lines (the welcome
/// message, with an empty row between its two lines as the arcade prints it, and the credit strings
/// in the SMALL font with the copyright an empty row below them in a colour of its own), then the
/// port's own (the author's credit and the F-key menu). There is no room for both at once, and the
/// port's lines are drawn in the page's text slot so they flash orange and white like the arcade's.
/// </summary>
public sealed class TitleScreenState : IGameState, IAttractState
{
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

    // Layout of the presentation page (notes §103/§107). The art is drawn at the port's 2x sprite
    // scale, which is why the wordmark is 58 canvas px tall. The page's TEXT alternates between two
    // panes, swapping every TitleTextSwapSeconds (author, 2026-09-20): there is no room for both at
    // once — and that is exactly what lets the arcade's two message lines have an EMPTY ROW between
    // them, as the author asked (the ROM prints them from cursors `$86` and `$96`, 16 rows apart on
    // an 8-row line grid, i.e. one blank line).
    private const int WordmarkRow = 30;
    private const int Logo2084Row = 96;

    // Pane 1 — the arcade's own lines: the welcome message (LARGE font), then the credit strings
    // (SMALL font) with the copyright on its own line below them. An empty 18-px row sits between
    // the two message lines, and an empty 16-px row between the credits and the copyright.
    private const int WelcomeRowOne = 176;
    private const int WelcomeRowTwo = 212;
    private const int DesignedByRow = 244;
    private const int ForWilliamsRow = 260;
    private const int CopyrightRow = 292;

    // Pane 2 — the port's own lines: the author's credit and the F-key menu (notes §101/§102.1).
    private const int CreditRow = 176;
    private const int MenuRow = 220;

    /// <summary>
    /// The palette entry the page's text is drawn in: the ROM's text colour operand (notes §106) —
    /// `$884E`'s `LDA #$66` for the two welcome lines, and the same `$66` in front of "DESIGNED BY
    /// VID KIDZ" in the string table at `$6D75`. The page's own table makes entry 6 ORANGE, and the
    /// page's white chase sweeps through it — which is what the author saw as the text "cycling
    /// between orange and white". The port's OWN lines use it too (author, 2026-09-20: "render the
    /// shortcut key text in orange and white too"), so the whole page's text flashes together.
    /// </summary>
    private const int TextSlot = PresentationPagePalette.TextSlot;

    /// <summary>
    /// The copyright line's OWN colour (author, 2026-09-20: *"a copyright 1982 williams electronics
    /// inc. message (in a different cycling colour)"*). Slot 2 is the page's own BLUE (`$C0`) — the
    /// colour the ROM's string script writes for a credit line (`04 22` in front of "CREDITS: n" at
    /// `$6D95`), which is the blue the author's reference screenshot shows on that very line. It is
    /// one of the seven entries the page writes, so it cycles with everything else: the white chase
    /// sweeps through it, exactly as it sweeps through the orange above (notes §106).
    /// </summary>
    private const int CopyrightSlot = 2;

    private readonly IPlayerInputSource _input;
    private readonly SpriteSet _sprites;
    private readonly HighScoreStore _highScores;
    private readonly ControlSettings _controls;
    private readonly GameServices _services;
    private readonly PresentationPagePalette _colour = new();
    private readonly TimeSpan _idleDuration = TimeSpan.FromSeconds(GameplayConstants.TitleIdleSeconds);
    private readonly TimeSpan _textSwap = TimeSpan.FromSeconds(GameplayConstants.TitleTextSwapSeconds);
    private TimeSpan _idleElapsed;
    private TimeSpan _textElapsed;

    /// <summary>True while the ARCADE's text pane is up; it alternates with the port's (notes §107).</summary>
    private bool _arcadeText = true;

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

        // The presentation page runs its OWN decoded colour set (notes §106): entries 1-7 come
        // from the ROM's seven-byte table ($8A70) with a white flash chasing through them every
        // 3 frames, and the message sits in entry 6. §103.3 ran the HIGH SCORE page's process set
        // here as a stand-in while this was undecoded — the two look nothing alike (that one walks
        // slot 8 through the whole COLTAB hue wheel), and the author's report that the message
        // "cycles between orange and white" is what the real page's chase does.
        if (_sprites.Palette is { } palette)
        {
            _colour.Start(palette);
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

        // The page's two TEXT PANES alternate (notes §107): the arcade's own lines, then the port's
        // credit and F-key menu. Nothing else on the page changes with them — the logos and the
        // colour cycling carry on regardless.
        _textElapsed += gameTime.ElapsedGameTime;
        if (_textElapsed >= _textSwap)
        {
            _textElapsed = TimeSpan.Zero;
            _arcadeText = !_arcadeText;
        }

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

        // The two logos, traced from the author's arcade screenshot (notes §103.4) — the R5 CPU
        // ROM we hold has no attract wordmark (§103.2). The wordmark's masks are drawn in the two
        // entries the page's art cycle is on this step, so it colour-cycles through the page's own
        // seven COLOURS (§104/§106) — and it starts on the reference screenshot's own pair, a red
        // body on a yellow rim. The "2084" mark keeps its traced colours. Both at the port's 2x
        // sprite scale.
        DrawCentredMask(spriteBatch, _sprites.TitleWordmarkRim, WordmarkRow, _colour.ArtRimSlot);
        DrawCentredMask(spriteBatch, _sprites.TitleWordmarkCore, WordmarkRow, _colour.ArtColorSlot);
        DrawCentredLogo(spriteBatch, _sprites.Title2084, Logo2084Row);

        // The page's text: ONE of its two panes, alternating every TitleTextSwapSeconds
        // (notes §107). Both are drawn in the page's own text slot — the ROM's operand `$66`, entry
        // 6, ORANGE with the white flash sweeping through it (notes §106) — so the author's credit
        // and the shortcut keys flash with the arcade's own lines.
        if (_arcadeText)
        {
            DrawArcadeText(spriteBatch);
        }
        else
        {
            DrawPortText(spriteBatch);
        }
    }

    /// <summary>
    /// Pane 1 — the arcade's own lines: the operator's welcome message and the credit strings, all
    /// in the page's text colour (the ROM's `$66` — notes §106) except the copyright, which gets an
    /// entry of its own. The two message lines are ONE EMPTY ROW apart, as the arcade prints them,
    /// and the copyright sits an empty row below the two credit lines (author, 2026-09-20).
    /// </summary>
    private void DrawArcadeText(SpriteBatch spriteBatch)
    {
        DrawCenteredLargeText(spriteBatch, WelcomeLineOne, WelcomeRowOne, TextSlot);
        DrawCenteredLargeText(spriteBatch, WelcomeLineTwo, WelcomeRowTwo, TextSlot);
        DrawCenteredSmallText(spriteBatch, DesignedByLine, DesignedByRow, TextSlot);
        DrawCenteredSmallText(spriteBatch, ForWilliamsLine, ForWilliamsRow, TextSlot);
        DrawCenteredSmallText(spriteBatch, CopyrightLine, CopyrightRow, CopyrightSlot);
    }

    /// <summary>
    /// Pane 2 — the port's own lines: the author's credit (notes §102.1) and the F-key menu
    /// (notes §101). Port-only and labelled as such; they take the arcade pane's place, and the
    /// arcade's START buttons keep working exactly as they did either way.
    /// </summary>
    private void DrawPortText(SpriteBatch spriteBatch)
    {
        DrawCenteredSmallText(spriteBatch, CreditLine, CreditRow, TextSlot);

        int y = MenuRow;
        foreach (string option in Options)
        {
            DrawCenteredSmallText(spriteBatch, option, y, TextSlot);
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
    /// The page's colour set dies with the page (notes §103.3/§106): both ways off this screen
    /// stand the ROM's CRTAB values back up over the seven entries the page had taken.
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

    /// <summary>
    /// Centres a WHITE MASK and draws it in one palette slot's live colour — the arcade's blitter
    /// REMAP COLOUR op (<c>$1A</c>), which is how the ROM draws anything in a colour from the
    /// palette. A slot the page's colour processes own therefore takes the art round its cycle
    /// with it (notes §104).
    /// </summary>
    private void DrawCentredMask(SpriteBatch spriteBatch, Texture2D texture, int y, int slot)
    {
        int width = texture.Width * ScreenSize.SpecScale;
        int height = texture.Height * ScreenSize.SpecScale;
        var bounds = new Rectangle((ScreenSize.Width - width) / 2, y, width, height);
        _sprites.DrawSpriteSolid(spriteBatch, texture, bounds, _sprites.SlotColor(slot));
    }

    private void DrawCenteredSmallText(SpriteBatch spriteBatch, string text, int y, int slot) =>
        _sprites.DrawSmallFontText(spriteBatch, text, CenteredX(text, large: false), y, slot);

    /// <summary>Centres a large-font line horizontally and prints it in one slot.</summary>
    private void DrawCenteredLargeText(SpriteBatch spriteBatch, string text, int y, int slot) =>
        _sprites.DrawLargeFontText(spriteBatch, text, CenteredX(text, large: true), y, slot);

    /// <summary>
    /// The X that centres a line on the canvas, measured with the font's own glyph widths
    /// (<see cref="SpriteSet.MeasureSmallText"/>/<see cref="SpriteSet.MeasureLargeText"/>) — the two
    /// fonts advance differently, so a fixed per-character width would mis-centre one of them.
    /// </summary>
    private int CenteredX(string text, bool large)
    {
        int width = ScreenSize.Scaled(large ? _sprites.MeasureLargeText(text) : _sprites.MeasureSmallText(text));
        return (ScreenSize.Width - width) / 2;
    }
}
