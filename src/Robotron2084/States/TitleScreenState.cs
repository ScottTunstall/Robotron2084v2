using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Graphics;
using Robotron2084.Input;
using Robotron2084.Level;
using Robotron2084.Palette;
using Robotron2084.Persistence;
using Robotron2084.Tuning;

namespace Robotron2084.States;

/// <summary>
///     INTRO2 (notes §123): the arcade's Williams PRESENTATION page (notes §103; built by <c>LOGG2</c> in <c>RRLOG.ASM</c>, <c>$87A6</c> in the disassembly): the
///     "ROBOTRON:" large title letters and the "2084" mark over the operator's attract-mode welcome message
///     (<c>MESSER</c> and <c>LINOUT</c> in <c>RRLOG.ASM</c>, <c>$8822</c>-<c>$8836</c> in the disassembly, print two
///     25-character lines in the LARGE font, an empty row apart),
///     then the Vid Kidz / Williams credit strings in the SMALL font.
///     The big ROBOTRON letters come into view one after another, each pulled together from strips like an explosion run
///     backwards (<see cref="LargeTitleLetterAppear" />, notes §135), and the "2084" mark goes up once the last has settled.
///     COLOUR — the page's OWN decoded set (notes §106): the original writes seven colours into palette
///     entries 1-7 (<c>CSET</c> copying <c>LCTAB</c>, `$8A3A` and `$8A70` in the disassembly) and runs a WHITE flash
///     through them every three frames (<c>COL1</c>/<c>COLL</c>, `$8A4F`). The message and the page's credit strings are drawn in entry 6 (the
///     text operand `$66`), so they are ORANGE with a white sweep, and the
///     traced large title letters cycles through the same seven (notes §104). The ROM's OTHER attract page (the wall
///     + "ROBOTRON 2084" + "SAVE THE LAST HUMAN FAMILY", notes §94.1) is a different screen and is not
///     drawn here.
///     Port conventions kept on top: **1** starts a one-player game and **2** a two-player game (the
///     arcade's START 1 / START 2 buttons, ROM RRG23 START1/START2; fire is the one-player alias),
///     the port's F1/F2/F3 start keys (notes §101; F10 is the DEVELOPER TOOLS page's key now, and is
///     live on every screen), and after
///     <see cref="AttractTuning.TitleIdleSeconds" /> of no start press the arcade's attract movie
///     takes over (notes §95/§96).
///     TEXT (notes §107): the page's two text panes alternate every
///     <see cref="AttractTuning.TitleArcadeTextSeconds" /> and <see cref="AttractTuning.TitlePortTextSeconds" /> — the
///     arcade's own lines (the welcome
///     message, with an empty row between its two lines as the arcade prints it, and the credit strings
///     in the SMALL font with the copyright an empty row below them in a colour of its own), then the
///     port's own (its two LARGE-font credit lines, on the arcade message's own rows). There is no
///     room for both at once, and the
///     port's lines are drawn in the page's text palette index so they flash orange and white like the arcade's.
/// </summary>
public sealed class TitleScreenState : IClosableGameState, IAttractState
{
    private const string CopyrightLine = "COPYRIGHT 1982 WILLIAMS ELECTRONICS INC.";
    private const int CopyrightRow = 308;

    /// <summary>
    ///     The copyright line's OWN colour. Palette index 2 is the page's own BLUE (`$C0`) — the
    ///     colour the ROM's string script writes for a credit line (`04 22` in front of "CREDITS: n" at
    ///     `$6D95`), which is the blue an arcade reference screenshot shows on that very line. It is
    ///     one of the seven entries the page writes, so it cycles with everything else: the white flash
    ///     sweeps through it, exactly as it sweeps through the orange above (notes §106).
    /// </summary>
    private const int CopyrightPaletteIndex = 2;

    /// <summary>
    ///     The port's own credit, which sits among these (notes §102.1). The
    ///     arcade's presentation page has no such line, so this one is a deliberate, labelled
    ///     addition — two LARGE-font lines, on the arcade pane's own message rows.
    /// </summary>
    private const string CreditLineOne = "REMAKE DEVELOPED BY";

    private const string CreditLineTwo = "SCOTT TUNSTALL";

    // Pane 2 — the port's own lines: its two LARGE-font credit lines (notes §102.1).
    // The credit's two lines sit on the arcade pane's message rows (LARGE font, an empty
    // row apart) so the panes swap in the same palette index.
    private const int CreditRowOne = WelcomeRowOne;

    private const int CreditRowTwo = WelcomeRowTwo;

    /// <summary>
    ///     The ROM's own credit strings under the message (notes §103): "DESIGNED BY VID KIDZ" and
    ///     "FOR WILLIAMS ELECTRONICS INC." are at ROM $6D85 (with a copy at $7F50 for the copyright).
    ///     The reference screen prints them in the SMALL font.
    /// </summary>
    private const string DesignedByLine = "ORIGINALLY DESIGNED BY VID KIDZ";

    private const int DesignedByRow = 268;
    private const string ForWilliamsLine = "FOR WILLIAMS ELECTRONICS INC.";
    private const int ForWilliamsRow = 284;
    private const int Logo2084Row = 138;

    /// <summary>
    ///     The palette entry the page's text is drawn in: the ROM's text colour operand (notes §106) —
    ///     <c>LINOUT</c>'s <c>LDA #$66</c> for the two welcome lines (`$884E` in the disassembly), and the same `$66` in front
    ///     of "DESIGNED BY VID KIDZ" in the string table <c>CPYM1</c> in <c>RRET.ASM</c> (`$6D75` in the disassembly). The page's own table makes entry 6 ORANGE, and the
    ///     page's white flash sweeps through it — so the text reads as "cycling
    ///     between orange and white". The port's OWN lines use it too, so the whole
    ///     page's text flashes together.
    /// </summary>
    private const int TextPaletteIndex = PresentationPagePalette.TextPaletteIndex;

    private const string WelcomeLineOne = "PRESENTED BY";
    private const string WelcomeLineTwo = "WILLIAMS ELECTRONICS INC.";

    // Pane 1 — the arcade's own lines: the welcome message (LARGE font), then the credit strings
    // (SMALL font) with the copyright on its own line below them. An empty 18-px row sits between
    // the two message lines, and an empty 16-px row between the credits and the copyright.
    private const int WelcomeRowOne = 214;

    private const int WelcomeRowTwo = 244;

    // Layout of the presentation page (notes §103/§107). It sits inside the border of moving W logos, between
    // the top row's bottom edge (canvas row 66) and the bottom row's top edge (row 323). The large title letters is drawn at the port's 2x sprite
    // scale, which is why the large title letters is 58 canvas px tall. The page's TEXT alternates between two
    // panes, swapping after TitleArcadeTextSeconds and TitlePortTextSeconds: there is no room for both at
    // once — and that is exactly what lets the arcade's two message lines have an EMPTY ROW between
    // them (the ROM prints them from cursors `$86` and `$96`, 16 rows apart on
    // an 8-row line grid, i.e. one blank line).
    private const int LargeTitleLettersRow = 72;

    private readonly TimeSpan _arcadeTextDuration = TimeSpan.FromSeconds(AttractTuning.TitleArcadeTextSeconds);
    private readonly ButtonEdgeDetector _buttonEdgeDetector = new();
    private readonly ControlSettings _controlSettings;
    private readonly GameSettings _gameSettings;
    private readonly HighScoreStore _highScoreStore;
    private readonly TimeSpan _idleDuration = TimeSpan.FromSeconds(AttractTuning.TitleIdleSeconds);
    private readonly IPlayerInputSource _playerInputSource;

    private readonly WilliamsLogoBorder _williamsLogoBorder;
    private readonly PresentationPagePalette _presentationPagePalette = new();

    /// <summary>
    ///     The pane the page HOLDS, or null to alternate the two as the attract cycle does (notes §107).
    /// </summary>
    private readonly TitleTextPane? _pinnedPane;

    private readonly TimeSpan _portTextDuration = TimeSpan.FromSeconds(AttractTuning.TitlePortTextSeconds);
    private readonly GameServices _gameServices;
    private readonly TextureSet _textureSet;
    private readonly LargeTitleLetterAppear _largeTitleLetterAppear;

    private TimeSpan _idleElapsed;

    /// <summary>True while the ARCADE's text pane is up; it alternates with the port's (notes §107).</summary>
    private bool _isShowingArcadeText = true;

    private TimeSpan _textElapsed;

    /// <summary>
    ///     Opens the title page.
    /// </summary>
    /// <param name="gameServices">The attract screens' shared dependencies.</param>
    /// <param name="pinnedPane">
    ///     The text pane to open on and hold (notes §107) — what the DEVELOPER TOOLS page's
    ///     TITLE PAGE 1 / TITLE PAGE 2 options pass. Null, the attract cycle's own, alternates the two.
    /// </param>
    public TitleScreenState(GameServices gameServices, TitleTextPane? pinnedPane = null)
    {
        _gameServices = gameServices;
        _pinnedPane = pinnedPane;
        _isShowingArcadeText = pinnedPane != TitleTextPane.Port;
        _playerInputSource = gameServices.PlayerInputSource;
        _textureSet = gameServices.TextureSet;
        _highScoreStore = gameServices.HighScoreStore;
        _controlSettings = gameServices.ControlSettings;
        _gameSettings = gameServices.GameSettings;
        _williamsLogoBorder = new WilliamsLogoBorder(SpriteMask.CreateFromTexture(_textureSet.WilliamsLogoTexture));
        _largeTitleLetterAppear = LargeTitleLetterAppear.CreateFromMasks(
            _textureSet.LargeTitleLettersRimTexture,
            _textureSet.LargeTitleLettersCoreTexture,
            new Point(
                (ScreenSize.Width - _textureSet.LargeTitleLettersRimTexture.Width * ScreenSize.PortPixelsPerArcadePixel) / 2,
                LargeTitleLettersRow));

        // The presentation page runs its OWN decoded colour set (notes §106): entries 1-7 come
        // from the original's seven-byte table (LCTAB, $8A70 in the disassembly) with a white flash running through them every
        // 3 frames, and the message sits in entry 6. The message reads as "cycling between orange
        // and white", which is what this white flash does (notes §106).
        if (_textureSet.BlitterDraw.GamePalette is { } palette) _presentationPagePalette.Start(palette);
    }

    /// <summary>Gets which of the two text panes is up now.</summary>
    internal TitleTextPane GetShownPane()
    {
        if (_isShowingArcadeText) return TitleTextPane.Arcade;

        return TitleTextPane.Port;
    }

    public void Draw(SpriteBatch spriteBatch, SpriteFont font)
    {
        // The caller clears to black. This is the ROM's Williams PRESENTATION page (notes §103):
        // the border of 28 moving "W" logos, the logos, the operator's welcome message (two
        // 25-character lines) and the credit strings. The arcade's "CREDITS: n" line is not drawn,
        // because the port has no credits, and neither the playfield wall nor the score/lives belong to
        // this page: the cabinet's other attract page carries those.
        _williamsLogoBorder.Draw(spriteBatch, _textureSet.BlitterDraw);

        // The two logos, traced from an arcade screenshot (notes §103.4) — the R5 CPU
        // ROM we hold has no attract large title letters (§103.2). The large title letters' masks are drawn in the two
        // entries the page's large title letters cycle is on this step, so it colour-cycles through the page's own
        // seven COLOURS (§104/§106) — and it starts on the reference screenshot's own pair, a red
        // body on a yellow rim. The "2084" mark keeps its traced colours. Both at the port's 2x
        // sprite scale.
        _largeTitleLetterAppear.Draw(spriteBatch, _textureSet.BlitterDraw, _presentationPagePalette.GetLargeTitleLettersRimPaletteIndex(),
            _presentationPagePalette.GetLargeTitleLettersPaletteIndex());
        if (_largeTitleLetterAppear.IsFinished()) DrawCentredLogo(spriteBatch, _textureSet.Title2084Texture, Logo2084Row);

        // The page's text: ONE of its two panes, alternating (the arcade pane for TitleArcadeTextSeconds, the port's for TitlePortTextSeconds)
        // (notes §107). Both are drawn in the page's own text palette index — the ROM's operand `$66`, entry
        // 6, ORANGE with the white flash sweeping through it (notes §106) — so the port's credit
        // and the shortcut keys flash with the arcade's own lines.
        if (_isShowingArcadeText)
            DrawArcadeText(spriteBatch);
        else
            DrawPortText(spriteBatch);
    }

    public void Update(GameTime gameTime, GameStateManager gameStateManager)
    {
        if (_textureSet.BlitterDraw.GamePalette is { } live) _presentationPagePalette.Update(live);

        _williamsLogoBorder.Tick();
        _largeTitleLetterAppear.Update();

        var playerInputState = _playerInputSource.Poll();

        // The arcade's coin-door buttons: START 1 / START 2 pick the number of
        // players (ROM RRG23 START1/START2 → PLRCNT), and the ROM then runs the
        // alternating 2-player game. Fire is kept as a 1-player alias (a port
        // convention — spec.txt's "press fire", which the arcade does not have).
        // The shell's F1/F2/F3 (notes §101) are the same three modes and work from
        // every attract screen, this one included.
        if (ChooseMode(_buttonEdgeDetector.Advance(playerInputState)) is { } chosen)
        {
            gameStateManager.TransitionTo(PlayingState.CreateNewGame(_controlSettings, _gameSettings, chosen, _textureSet,
                _highScoreStore, _gameServices.Random));
            return;
        }

        // Any button held means a human is at the machine — the arcade's
        // attract only runs while the cabinet sits idle.
        if (playerInputState.FireHeld || playerInputState.StartOnePlayerHeld || playerInputState.StartTwoPlayersHeld) _idleElapsed = TimeSpan.Zero;

        // Blink removed with the port's old prompt: the port's credit is not a prompt, and
        // flickering it every second made it hard to read (notes §101).

        // The page's two TEXT PANES alternate (notes §107): the arcade's own lines, then the port's
        // credit. Nothing else on the page changes with them — the logos and the
        // colour cycling carry on regardless. A page opened ON a pane (the DEVELOPER TOOLS page's
        // TITLE PAGE 1 / TITLE PAGE 2) holds it, so the pane can be looked at.
        if (_pinnedPane is null)
        {
            _textElapsed += gameTime.ElapsedGameTime;
            var textDuration = _portTextDuration;
            if (_isShowingArcadeText) textDuration = _arcadeTextDuration;

            if (_textElapsed >= textDuration)
            {
                _textElapsed = TimeSpan.Zero;
                _isShowingArcadeText = !_isShowingArcadeText;
            }
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
            gameStateManager.TransitionTo(new StorylineState(_gameServices));
        }
    }

    /// <summary>The game mode this tick's presses start, if any: START 1 or fire for one player, START 2 for two.</summary>
    private static GameMode? ChooseMode(ButtonPresses presses)
    {
        if (presses.StartOnePlayerPressed) return GameMode.OnePlayer;

        if (presses.StartTwoPlayersPressed) return GameMode.TwoPlayerAlternate;

        return presses.FirePressed ? GameMode.OnePlayer : null;
    }

    /// <summary>
    ///     Pane 1 — the arcade's own lines: the operator's welcome message and the credit strings, all
    ///     in the page's text colour (the ROM's `$66` — notes §106) except the copyright, which gets an
    ///     entry of its own. The two message lines are ONE EMPTY ROW apart, as the arcade prints them,
    ///     and the copyright sits an empty row below the two credit lines.
    /// </summary>
    private void DrawArcadeText(SpriteBatch spriteBatch)
    {
        DrawCentredLargeText(spriteBatch, WelcomeLineOne, WelcomeRowOne, TextPaletteIndex);
        DrawCentredLargeText(spriteBatch, WelcomeLineTwo, WelcomeRowTwo, TextPaletteIndex);
        DrawCentredSmallText(spriteBatch, DesignedByLine, DesignedByRow, TextPaletteIndex);
        DrawCentredSmallText(spriteBatch, ForWilliamsLine, ForWilliamsRow, TextPaletteIndex);
        DrawCentredSmallText(spriteBatch, CopyrightLine, CopyrightRow, CopyrightPaletteIndex);
    }

    /// <summary>Centres a large-font line horizontally and prints it in one palette index.</summary>
    private void DrawCentredLargeText(SpriteBatch spriteBatch, string text, int y, int paletteIndex)
    {
        _textureSet.ArcadeText.DrawLargeFontText(spriteBatch, text, GetCentredX(text, true), y, paletteIndex);
    }

    private void DrawCentredSmallText(SpriteBatch spriteBatch, string text, int y, int paletteIndex)
    {
        _textureSet.ArcadeText.DrawSmallFontText(spriteBatch, text, GetCentredX(text, false), y, paletteIndex);
    }

    /// <summary>Centres one traced logo horizontally, at the port's 2x sprite scale.</summary>
    private void DrawCentredLogo(SpriteBatch spriteBatch, Texture2D texture, int y)
    {
        var width = texture.Width * ScreenSize.PortPixelsPerArcadePixel;
        var height = texture.Height * ScreenSize.PortPixelsPerArcadePixel;
        var bounds = new Rectangle((ScreenSize.Width - width) / 2, y, width, height);
        _textureSet.BlitterDraw.DrawSprite(spriteBatch, texture, bounds, Color.White);
    }

    /// <summary>
    ///     Pane 2 — the port's own lines: its two LARGE-font credit lines (notes §102.1).
    ///     Port-only and labelled as such; they take the arcade pane's place in its message rows, and
    ///     the arcade's START buttons and the port's F-keys (notes §101) keep working exactly as they
    ///     did either way.
    /// </summary>
    private void DrawPortText(SpriteBatch spriteBatch)
    {
        DrawCentredLargeText(spriteBatch, CreditLineOne, CreditRowOne, TextPaletteIndex);
        DrawCentredLargeText(spriteBatch, CreditLineTwo, CreditRowTwo, TextPaletteIndex);
    }

    /// <summary>
    ///     The X that centres a line on the canvas, measured with the font's own glyph widths
    ///     (<see cref="ArcadeText.MeasureSmallText" />/<see cref="ArcadeText.MeasureLargeText" />) — the two
    ///     fonts advance differently, so a fixed per-character width would mis-centre one of them.
    /// </summary>
    private int GetCentredX(string text, bool isLarge)
    {
        var textWidth = _textureSet.ArcadeText.MeasureSmallText(text);
        if (isLarge) textWidth = _textureSet.ArcadeText.MeasureLargeText(text);

        var width = ScreenSize.ToPortPixelsFromArcadePixels(textWidth);
        return (ScreenSize.Width - width) / 2;
    }

    /// <summary>Draws an arcade-small-font line centred on the canvas.</summary>
    /// <summary>
    ///     The page's colour set dies with the page (notes §103.3/§106): both ways off this screen
    ///     stand the ROM's CRTAB values back up over the seven entries the page had taken.
    /// </summary>
    /// <summary>Puts the palette entries the page's own colours took over back on the game's usual colours.</summary>
    public void Close()
    {
        if (_textureSet.BlitterDraw.GamePalette is { } palette) _presentationPagePalette.Stop(palette);
    }
}
