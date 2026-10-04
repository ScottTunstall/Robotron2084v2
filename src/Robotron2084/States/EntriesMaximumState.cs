using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Core;
using Robotron2084.Tuning;

namespace Robotron2084.States;

/// <summary>
/// The ONLY5P page (notes §116) — "5 ENTRIES MAXIMUM PER PLAYER / LOWEST ENTRY REPLACED", which the
/// arcade shows when the all-time list's per-initials cap has just applied, held for 1.2 s before the
/// ceremony carries on.
/// </summary>
/// <remarks>
/// ROM RRTESTC <c>GTTHM8</c>: <c>SCRCLR</c>, then RRET message 100 (<c>ONLY5P</c>) —
/// <c>COLOR $BB</c> with "5 ENTRIES MAXIMUM" + " PER PLAYER" at (32, 112) and "LOWEST ENTRY REPLACED"
/// at (40, 144) — and <c>NAP $60</c>. Slot 11 is one of the ROM's cycling slots, but <c>ENDGAM</c>
/// kills the colour processes before any of these pages, so on the cabinet this text does not cycle;
/// the port's animator is global, so in the port it does (a parked deviation, notes §116).
/// </remarks>
public sealed class EntriesMaximumState : IGameState
{
    private const int FirstLineColumn = 32;
    private const int FirstLineRow = 112;
    private const int SecondLineColumn = 40;
    private const int SecondLineRow = 144;

    private readonly ScoreEntryCeremony _ceremony;
    private readonly int _holdTicks = ArcadeClock.ToPortTicks(ScreenTuning.EntriesMaximumHoldRomFrames);
    private readonly GameServices _services;
    private int _elapsedTicks;

    /// <summary>Builds the page for the score whose initials hit the cap.</summary>
    /// <param name="services">The attract screens' bundle: sprites, the store, the controls and player 1's input.</param>
    /// <param name="ceremony">The ceremony to carry on with once the page has been held.</param>
    public EntriesMaximumState(GameServices services, ScoreEntryCeremony ceremony)
    {
        _services = services;
        _ceremony = ceremony;
    }

    public void Draw(SpriteBatch spriteBatch, SpriteFont font)
    {
        DrawLine(spriteBatch, "5 ENTRIES MAXIMUM PER PLAYER", FirstLineColumn, FirstLineRow);
        DrawLine(spriteBatch, "LOWEST ENTRY REPLACED", SecondLineColumn, SecondLineRow);
    }

    public void Update(GameTime gameTime, GameStateManager manager)
    {
        if (++_elapsedTicks < _holdTicks)
        {
            return;
        }

        manager.TransitionTo(_ceremony.NextScreen());
    }

    private void DrawLine(SpriteBatch spriteBatch, string text, int column, int row) =>
        _services.Sprites.TextRenderer.DrawLargeFontText(
            spriteBatch,
            text,
            HudLayout.ToPortColumnX(column),
            HudLayout.ToPortY(row),
            ScreenTuning.EntriesMaximumSlot);
}
