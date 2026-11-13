using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Robotron2084.Core;
using Robotron2084.Graphics;
using Robotron2084.Input;
using Robotron2084.Palette;
using Robotron2084.States.SpriteTest;

namespace Robotron2084.States;

/// <summary>
///     The SPRITE TEST page: every sprite in the game, one at a time, animating as it does in the game. The way held on
///     the arrow keys or a controller is the way the sprite walks.
/// </summary>
/// <remarks>
///     <list type="bullet">
///         <item>Port only: the arcade has no such page.</item>
///         <item>
///             The list is on the left, with the arcade's "->" cursor at the sprite showing. The sprite is drawn on the
///             right, at the size it is in the game.
///         </item>
///         <item>
///             When the keyboard and the controllers have been left alone for <see cref="IdleTicksBeforeWalkOn" />, a sprite
///             that walks in the game goes, then walks on from the right-hand edge of the screen to its place, stepping and
///             animating as it does when it walks left in the game. Gorf hops on. Touching any control stops the walk.
///         </item>
///         <item>
///             Keys: the arrow keys, a controller's pad or its left stick hold a way. Enter or a controller's right
///             shoulder button moves to the next sprite, and Backspace or the left shoulder button to the one before.
///             Page Down and Page Up, or the controller's right and left triggers, move <see cref="PageStep" /> at a
///             time. The list wraps at either end.
///         </item>
///         <item>
///             The page draws in the same colours as the DEVELOPER TOOLS page: white for the heading and the
///             instructions, green for the list, and the sprite the cursor is on strobing.
///         </item>
///     </list>
/// </remarks>
public sealed class SpriteTestState : IGameState
{
    /// <summary>The page's heading.</summary>
    private const string Title = "SPRITE TEST";

    /// <summary>Where the heading is, in port pixels from the top.</summary>
    private const int TitleRow = 12;

    /// <summary>Where the cursor is drawn, in port pixels from the left: just left of the names.</summary>
    private const int CursorColumn = 24;

    /// <summary>Where the names in the list start, in port pixels from the left.</summary>
    private const int NameColumn = 42;

    /// <summary>Where the first name shown in the list is, in port pixels from the top.</summary>
    private const int FirstListRow = 58;

    /// <summary>The gap from one name in the list to the next, in port pixels.</summary>
    private const int ListRowStep = 18;

    /// <summary>How many names the list shows at once. The list scrolls to keep the cursor in it.</summary>
    private const int VisibleListRows = 11;

    /// <summary>How many sprites Page Up and Page Down move.</summary>
    private const int PageStep = 5;

    /// <summary>
    ///     How long the keyboard and the controllers must be left alone before the sprite walks on, in
    ///     <see cref="ArcadeClock.PortTicksPerSecond">ticks</see>: ten seconds.
    /// </summary>
    private const int IdleTicksBeforeWalkOn = 10 * ArcadeClock.PortTicksPerSecond;

    /// <summary>How far a controller's trigger must be pulled before it counts as touched, from 0 to 1.</summary>
    private const float TriggerTouchedAt = 0.25f;

    /// <summary>
    ///     Where the middle of the sprite is when it is in its place, and of its name and details, in port pixels from the
    ///     left.
    /// </summary>
    private const int SpriteCentreColumn = 415;

    /// <summary>Where the middle of the sprite is, in port pixels from the top, when it is on the ground.</summary>
    private const int SpriteCentreRow = 140;

    /// <summary>Where the sprite's name is, in port pixels from the top.</summary>
    private const int NameRow = 58;

    /// <summary>Where the way held and the frame showing are, in port pixels from the top.</summary>
    private const int DetailsRow = 230;

    private const string WayLine = "ARROWS OR STICK - WALK THAT WAY";
    private const int WayLineRow = 258;
    private const string StepLine = "ENTER - NEXT   BACKSPACE - BACK";
    private const int StepLineRow = 276;
    private const string PageLine = "PAGE DOWN - 5 ON   PAGE UP - 5 BACK";
    private const int PageLineRow = 294;
    private const string IdleLine = "LEAVE ALONE 10 SECONDS - WATCH IT WALK ON";
    private const int IdleLineRow = 312;
    private const string ExitLine = "F10 - DEVELOPER TOOLS";
    private const int ExitRow = 342;

    /// <summary>
    ///     The palette index of the heading, the instructions, the cursor and the details: white. It is one of
    ///     <see cref="OwnedPaletteIndices" />.
    /// </summary>
    private const int TextPaletteIndex = 9;

    /// <summary>The palette index of the names in the list: green. It is one of <see cref="OwnedPaletteIndices" />.</summary>
    private const int ListPaletteIndex = 6;

    /// <summary>
    ///     The palette indices the page draws its words in, put back on their usual values when the page opens, since the
    ///     screen it came from may have left them holding anything.
    /// </summary>
    private static readonly int[] OwnedPaletteIndices = [ListPaletteIndex, TextPaletteIndex];

    private readonly DefineInputsHighlight _defineInputsHighlight = new();
    private readonly IReadOnlyList<SpriteTestEntry> _entries;
    private readonly Random _random = new();
    private readonly TextureSet _textureSet;
    private SpriteTestAnimator _animator;
    private Direction8? _heldDirection;
    private int _idleTicks;
    private InputSnapshot _previousSnapshot;
    private SpriteTestWalkOnRun? _walkOnRun;

    /// <summary>Opens the page on the first sprite.</summary>
    /// <param name="gameServices">The attract screens' shared services.</param>
    public SpriteTestState(GameServices gameServices)
    {
        _textureSet = gameServices.TextureSet;
        _entries = SpriteTestEntries.Create(_textureSet);
        _animator = new SpriteTestAnimator(_entries[0], _random);
        _previousSnapshot = InputSnapshot.Read();
        RestorePalette();

        if (_textureSet.BlitterDraw.GamePalette is { } palette) _defineInputsHighlight.Start(palette);
    }

    /// <summary>The place in the list of the sprite showing, counting from 0.</summary>
    internal int Index { get; private set; }

    /// <inheritdoc />
    public void Draw(SpriteBatch spriteBatch, SpriteFont font)
    {
        DrawLargeCentred(spriteBatch, Title, ScreenSize.Width / 2, TitleRow);
        DrawList(spriteBatch);

        var entry = _entries[Index];
        DrawSmallCentred(spriteBatch, entry.Name, SpriteCentreColumn, NameRow, TextPaletteIndex);
        DrawSprite(spriteBatch, entry);
        DrawSmallCentred(spriteBatch, GetDetails(entry), SpriteCentreColumn, DetailsRow, TextPaletteIndex);

        DrawSmallCentred(spriteBatch, WayLine, ScreenSize.Width / 2, WayLineRow, TextPaletteIndex);
        DrawSmallCentred(spriteBatch, StepLine, ScreenSize.Width / 2, StepLineRow, TextPaletteIndex);
        DrawSmallCentred(spriteBatch, PageLine, ScreenSize.Width / 2, PageLineRow, TextPaletteIndex);
        DrawSmallCentred(spriteBatch, IdleLine, ScreenSize.Width / 2, IdleLineRow, TextPaletteIndex);
        DrawSmallCentred(spriteBatch, ExitLine, ScreenSize.Width / 2, ExitRow, TextPaletteIndex);
    }

    /// <inheritdoc />
    public void Update(GameTime gameTime, GameStateManager gameStateManager)
    {
        if (_textureSet.BlitterDraw.GamePalette is { } palette) _defineInputsHighlight.Update(palette);

        var now = InputSnapshot.Read();
        if (IsAnyControlTouched(now))
        {
            _idleTicks = 0;
            _walkOnRun = null;
        }
        else
        {
            _idleTicks++;
        }

        if (WasPressed(now, Keys.Enter, Buttons.RightShoulder)) MoveTo(Index + 1);
        else if (WasPressed(now, Keys.Back, Buttons.LeftShoulder)) MoveTo(Index - 1);
        else if (WasPressed(now, Keys.PageDown, Buttons.RightTrigger)) MoveTo(Index + PageStep);
        else if (WasPressed(now, Keys.PageUp, Buttons.LeftTrigger)) MoveTo(Index - PageStep);

        if (_walkOnRun is null && _idleTicks >= IdleTicksBeforeWalkOn) StartWalkOn();

        if (_walkOnRun is { } walkOnRun)
            TickWalkOn(walkOnRun);
        else
        {
            _heldDirection = ReadHeldDirection(now);
            _animator.Tick(_heldDirection);
        }

        _previousSnapshot = now;
    }

    /// <summary>Moves to a sprite, wrapping at either end of the list, and starts its animation from the beginning.</summary>
    /// <param name="index">The place in the list.</param>
    internal void MoveTo(int index)
    {
        var count = _entries.Count;
        Index = (index % count + count) % count;
        _animator = new SpriteTestAnimator(_entries[Index], _random);
        _walkOnRun = null;
    }

    /// <summary>
    ///     Takes the sprite off the page, to walk on again from beyond the right-hand edge of the screen. A sprite that
    ///     does not walk in the game stays where it is, and is tried again after another wait.
    /// </summary>
    private void StartWalkOn()
    {
        _idleTicks = 0;
        if (_entries[Index].WalkOn is not { } walkOn) return;

        _animator = new SpriteTestAnimator(_entries[Index], _random);
        _walkOnRun = new SpriteTestWalkOnRun(walkOn, ScreenSize.Width, _animator);
    }

    /// <summary>Moves the walk on by one tick: the sprite walks left, and stops when it reaches its place.</summary>
    /// <param name="walkOnRun">The walk.</param>
    private void TickWalkOn(SpriteTestWalkOnRun walkOnRun)
    {
        _heldDirection = Direction8.Left;
        _animator.Tick(_heldDirection);
        walkOnRun.Tick(_animator);
        if (walkOnRun.X > GetHomeBounds().X) return;

        // It has arrived. It stands in its place, and the wait for the next walk starts again.
        _walkOnRun = null;
        _idleTicks = 0;
    }

    /// <summary>Says whether any key is down, or any button, pad, stick or trigger on either controller is in use.</summary>
    /// <param name="now">This tick's input.</param>
    private static bool IsAnyControlTouched(InputSnapshot now)
    {
        if (now.KeyboardState.GetPressedKeyCount() > 0) return true;

        foreach (var pad in new[] { now.PlayerOneGamePadState, now.PlayerTwoGamePadState })
        {
            if (!pad.IsConnected) continue;

            var isButtonDown = pad.Buttons != new GamePadButtons() || pad.DPad != new GamePadDPad();
            var isStickPushed = GamePadSticks.Read(pad, false) != IntVector2.Zero || GamePadSticks.Read(pad, true) != IntVector2.Zero;
            var isTriggerPulled = pad.Triggers.Left > TriggerTouchedAt || pad.Triggers.Right > TriggerTouchedAt;
            if (isButtonDown || isStickPushed || isTriggerPulled) return true;
        }

        return false;
    }

    /// <summary>Gets the box the sprite showing takes up when it is in its place, at its size in the game.</summary>
    private Rectangle GetHomeBounds()
    {
        var texture = _animator.GetCurrentAnimationFrameTexture();
        var width = ScreenSize.ToPortPixelsFromArcadePixels(texture.Width);
        var height = ScreenSize.ToPortPixelsFromArcadePixels(texture.Height);
        return new Rectangle(SpriteCentreColumn - width / 2, SpriteCentreRow - height / 2, width, height);
    }

    /// <summary>Reads the way held on the arrow keys, either controller's pad, or either controller's left stick.</summary>
    /// <param name="now">This tick's input.</param>
    /// <returns>The way held, or null when none is, or when opposite ways cancel out.</returns>
    private static Direction8? ReadHeldDirection(InputSnapshot now)
    {
        var x = 0;
        var y = 0;
        AddHeld(now.KeyboardState.IsKeyDown(Keys.Left), now.KeyboardState.IsKeyDown(Keys.Right),
            now.KeyboardState.IsKeyDown(Keys.Up), now.KeyboardState.IsKeyDown(Keys.Down), ref x, ref y);
        foreach (var pad in new[] { now.PlayerOneGamePadState, now.PlayerTwoGamePadState })
        {
            AddHeld(pad.IsButtonDown(Buttons.DPadLeft), pad.IsButtonDown(Buttons.DPadRight), pad.IsButtonDown(Buttons.DPadUp),
                pad.IsButtonDown(Buttons.DPadDown), ref x, ref y);
            var stick = GamePadSticks.Read(pad, false);
            AddHeld(stick.X < 0, stick.X > 0, stick.Y < 0, stick.Y > 0, ref x, ref y);
        }

        return Direction8Extensions.CreateFromDelta(new IntVector2(Math.Sign(x), Math.Sign(y)));
    }

    /// <summary>Adds what one control is holding to the sideways and up-and-down totals.</summary>
    private static void AddHeld(bool isLeft, bool isRight, bool isUp, bool isDown, ref int x, ref int y)
    {
        if (isLeft) x--;
        if (isRight) x++;
        if (isUp) y--;
        if (isDown) y++;
    }

    /// <summary>Gets the line under the sprite: the way held, if the sprite can be steered, and the frame showing.</summary>
    /// <param name="entry">The sprite showing.</param>
    private string GetDetails(SpriteTestEntry entry)
    {
        var frame = $"FRAME {_animator.FrameIndex + 1} OF {_animator.FrameCount}";
        if (!entry.IsSteerable) return frame;

        var way = "NONE";
        if (_heldDirection is { } held) way = GetWayName(held);

        return $"{way}   {frame}";
    }

    /// <summary>Gets the name of a way, in characters the small font has.</summary>
    private static string GetWayName(Direction8 direction)
    {
        return direction switch
        {
            Direction8.Up => "UP",
            Direction8.UpRight => "UP RIGHT",
            Direction8.Right => "RIGHT",
            Direction8.DownRight => "DOWN RIGHT",
            Direction8.Down => "DOWN",
            Direction8.DownLeft => "DOWN LEFT",
            Direction8.Left => "LEFT",
            _ => "UP LEFT"
        };
    }

    /// <summary>
    ///     Draws the part of the list around the cursor: the names in green, the one the cursor is on strobing, with the
    ///     arcade's "->" at its left.
    /// </summary>
    private void DrawList(SpriteBatch spriteBatch)
    {
        var firstShown = Math.Clamp(Index - VisibleListRows / 2, 0, Math.Max(0, _entries.Count - VisibleListRows));
        var lastShown = Math.Min(_entries.Count, firstShown + VisibleListRows);
        for (var index = firstShown; index < lastShown; index++)
        {
            var y = FirstListRow + (index - firstShown) * ListRowStep;
            var paletteIndex = ListPaletteIndex;
            if (index == Index)
            {
                _textureSet.BlitterDraw.DrawGlyphStatic(spriteBatch, _textureSet.CursorArrowTexture, CursorColumn, y, TextPaletteIndex);
                paletteIndex = DefineInputsHighlight.PaletteIndex;
            }

            _textureSet.ArcadeText.DrawSmallFontText(spriteBatch, _entries[index].Name, NameColumn, y, paletteIndex);
        }
    }

    /// <summary>
    ///     Draws the sprite showing at its size in the game, in its own draw style: in its place, or where it has got to
    ///     on its walk.
    /// </summary>
    /// <param name="spriteBatch">The batch to draw into.</param>
    /// <param name="entry">The sprite showing.</param>
    private void DrawSprite(SpriteBatch spriteBatch, SpriteTestEntry entry)
    {
        var texture = _animator.GetCurrentAnimationFrameTexture();
        var bounds = GetHomeBounds();
        if (_walkOnRun is { } walkOnRun) bounds = bounds with { X = walkOnRun.X, Y = bounds.Y - walkOnRun.Rise };

        var blitterDraw = _textureSet.BlitterDraw;
        switch (entry.DrawStyle)
        {
            case SpriteTestDrawStyle.Solid:
                blitterDraw.DrawSpriteSolid(spriteBatch, texture, bounds, blitterDraw.GetPaletteIndexColour(entry.ShapePaletteIndex));
                break;

            case SpriteTestDrawStyle.SolidWithBackground:
                blitterDraw.DrawSpriteSolidWithBackground(spriteBatch, texture, bounds,
                    blitterDraw.GetPaletteIndexColour(entry.BackgroundPaletteIndex),
                    blitterDraw.GetPaletteIndexColour(entry.ShapePaletteIndex));
                break;

            case SpriteTestDrawStyle.Normal:
            default:
                blitterDraw.DrawSprite(spriteBatch, texture, bounds, Color.White);
                break;
        }
    }

    /// <summary>Draws a line in the large font, centred on a column.</summary>
    private void DrawLargeCentred(SpriteBatch spriteBatch, string text, int centreColumn, int y)
    {
        var width = ScreenSize.ToPortPixelsFromArcadePixels(_textureSet.ArcadeText.MeasureLargeText(text));
        _textureSet.ArcadeText.DrawLargeFontText(spriteBatch, text, centreColumn - width / 2, y, TextPaletteIndex);
    }

    /// <summary>Draws a line in the small font, centred on a column.</summary>
    private void DrawSmallCentred(SpriteBatch spriteBatch, string text, int centreColumn, int y, int paletteIndex)
    {
        var width = ScreenSize.ToPortPixelsFromArcadePixels(_textureSet.ArcadeText.MeasureSmallText(text));
        _textureSet.ArcadeText.DrawSmallFontText(spriteBatch, text, centreColumn - width / 2, y, paletteIndex);
    }

    /// <summary>Puts the page's own palette indices back on their usual values.</summary>
    private void RestorePalette()
    {
        if (_textureSet.BlitterDraw.GamePalette is not { } palette) return;

        foreach (var paletteIndex in OwnedPaletteIndices)
            palette.SetPaletteIndexValue(paletteIndex, GamePalette.DefaultPaletteValues[paletteIndex]);
    }

    /// <summary>True on the tick a key goes down.</summary>
    private bool WasKeyPressed(InputSnapshot now, Keys key)
    {
        return now.KeyboardState.IsKeyDown(key) && !_previousSnapshot.KeyboardState.IsKeyDown(key);
    }

    /// <summary>True on the tick a key, or a button on either controller, goes down.</summary>
    private bool WasPressed(InputSnapshot now, Keys key, Buttons button)
    {
        var isPadOnePressed = now.PlayerOneGamePadState.IsButtonDown(button) &&
                              !_previousSnapshot.PlayerOneGamePadState.IsButtonDown(button);
        var isPadTwoPressed = now.PlayerTwoGamePadState.IsButtonDown(button) &&
                              !_previousSnapshot.PlayerTwoGamePadState.IsButtonDown(button);
        return WasKeyPressed(now, key) || isPadOnePressed || isPadTwoPressed;
    }
}
