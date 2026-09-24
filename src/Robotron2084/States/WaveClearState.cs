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
/// Inter-level screen: after the display time, the same player
/// resumes at their next wave, carrying lives, score and rescue count over. The
/// session rides through unchanged — a wave clear never passes the turn in the
/// arcade (RRG23 GEXEC0 only advances the CURRENT player's PWAV), so a 2-player
/// game stays with whoever cleared the wave.
/// </summary>
public sealed class WaveClearState : IGameState
{
    private readonly SpriteSet _sprites;
    private readonly HighScoreStore _highScores;
    private readonly GameSession _session;
    private readonly int _clearedWave;

    /// <summary>
    /// When true the tunnel belongs to the ATTRACT demo
    /// (notes §94) and the game resumes in <see cref="AttractState"/> (the machine keeps
    /// playing itself) instead of <see cref="PlayingState"/>.
    /// </summary>
    private readonly bool _attract;

    /// <summary>
    /// The arcade's wave-complete effect (notes §79): a hatched, colour-cycling tunnel that
    /// expands from the screen's centre line out to its corners and then erases itself in
    /// black. This replaces the port's old "LEVEL n COMPLETE" text — the ROM shows no text
    /// here at all (its `&lt;n&gt; WAVE` text belongs to the wave START, §58).
    /// </summary>
    private readonly TunnelEffect _tunnel = new();

    /// <summary>
    /// The colour-cycling palette the tunnel is drawn in (notes §86) — the ROM fills palette slots
    /// 1-15 from a ramp table that lives beside the walker's own task data, blacks one slot in each
    /// group of five, and slides the whole window on every pass.
    /// </summary>
    private readonly TunnelPalette _tunnelPalette = new();

    private bool _paletteStarted;
    private int _ringsPaletted = -1;

    private int _ticksRemaining = GameplayConstants.WaveClearDisplayTicks;

    public WaveClearState(SpriteSet sprites, HighScoreStore highScores, GameSession session, int clearedWave, bool attract = false)
    {
        _sprites = sprites;
        _highScores = highScores;
        _session = session;
        _clearedWave = clearedWave;
        _attract = attract;
    }

    public void Update(GameTime gameTime, GameStateManager manager)
    {
        // Two rings a frame (the ROM's task delay is 1 and its counter is seeded 2), then the
        // black pass — 53 + 53 rings, about 0.9 s. The state still honours the display time as
        // a floor, so the tunnel is never cut off.
        if (!_tunnel.Finished)
        {
            // The palette task runs beside the walker's, on the same delay of 1, so the ramp's
            // window slides once per ring pass (notes §86).
            if (_ringsPaletted != _tunnel.RingsDrawn)
            {
                AdvancePalette();
            }

            _tunnel.Update();
        }

        if (--_ticksRemaining <= 0 && _tunnel.Finished)
        {
            RestorePalette();
            manager.TransitionTo(_attract
                ? new AttractState(GameServices.From(_session, _sprites, _highScores, _session.Current.Input))
                : new PlayingState(_sprites, _highScores, _session));
        }
    }

    /// <summary>One pass of the ROM's palette task: pick the ramp once, then slide its window.</summary>
    private void AdvancePalette()
    {
        _ringsPaletted = _tunnel.RingsDrawn;

        if (_paletteStarted)
        {
            _tunnelPalette.Advance();
        }
        else
        {
            _paletteStarted = true;
            _tunnelPalette.Start();

            // While the ramp runs it owns every slot, so the six colour processes must not fight
            // it for slots 10-15 (the ROM's own palette task writes all fifteen every pass).
            if (_sprites.Palette is { } live)
            {
                for (int slot = FontSlots.FirstCyclingSlot; slot <= 15; slot++)
                {
                    live.SuspendSlot(slot);
                }
            }
        }

        if (_sprites.Palette is { } palette)
        {
            _tunnelPalette.Apply(palette);
        }
    }

    /// <summary>
    /// Puts the game palette back the way the next wave wants it — the ROM's per-wave
    /// `LOAD_DA51_PALETTE` (notes §86).
    /// </summary>
    private void RestorePalette()
    {
        if (_sprites.Palette is not { } palette)
        {
            return;
        }

        for (int slot = 1; slot <= 15; slot++)
        {
            palette.SetSlot(slot, GamePalette.DefaultSlots[slot]);
        }

        for (int slot = FontSlots.FirstCyclingSlot; slot <= 15; slot++)
        {
            palette.ResumeSlot(slot);
        }
    }

    public void Draw(SpriteBatch spriteBatch, SpriteFont font)
    {
        // The wave that was just CLEARED is carried in _clearedWave (the session's counter has
        // already advanced); nothing needs to be written for it — the ROM draws the tunnel.
        _tunnel.Draw(spriteBatch, _sprites);
    }
}
