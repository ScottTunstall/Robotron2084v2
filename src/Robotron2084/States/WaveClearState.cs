using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Audio;
using Robotron2084.Graphics;
using Robotron2084.Level;
using Robotron2084.Palette;
using Robotron2084.Persistence;
using Robotron2084.Tuning;

namespace Robotron2084.States;

/// <summary>
///     Inter-wave screen: after the display time, the same player
///     resumes at their next wave, carrying lives, score and rescue count over. The
///     session rides through unchanged — a wave clear never passes the turn in the
///     arcade (RRG23 GEXEC0 only advances the CURRENT player's PWAV), so a 2-player
///     game stays with whoever cleared the wave.
/// </summary>
public sealed class WaveClearState : IGameState
{
    private readonly HighScoreStore _highScoreStore;

    /// <summary>
    ///     When true the tunnel belongs to the ATTRACT demo
    ///     (notes §94) and the game resumes in <see cref="AttractState" /> (the machine keeps
    ///     playing itself) instead of <see cref="PlayingState" />. The shell reads this through
    ///     <see cref="IsAttractMode" /> for the ATTRACT MODE SOUND switch (notes §131).
    /// </summary>
    private readonly bool _isAttractMode;

    private readonly GameSession _gameSession;
    private readonly TextureSet _textureSet;

    /// <summary>
    ///     The arcade's wave-complete effect (notes §79): a hatched, colour-cycling tunnel that
    ///     expands from the screen's centre line out to its corners and then erases itself in
    ///     black. This replaces the port's old "LEVEL n COMPLETE" text — the ROM shows no text
    ///     here at all (its `&lt;n&gt; WAVE` text belongs to the wave START, §58).
    /// </summary>
    private readonly TunnelEffect _tunnelEffect = new();

    /// <summary>
    ///     The colour-cycling palette the tunnel is drawn in (notes §86) — the ROM fills palette indices
    ///     1-15 from a ramp table that lives beside the walker's own task data, blacks one palette index in each
    ///     group of five, and slides the whole window on every pass.
    /// </summary>
    private readonly TunnelPalette _tunnelPalette;

    private readonly Random _random;

    private bool _hasAskedForWaveEndSound;
    private bool _paletteStarted;
    private int _ringsPaletted = -1;

    private int _ticksRemaining = ScreenTuning.WaveClearDisplayTicks;

    /// <summary>Makes the tunnel for the wave that has just been cleared.</summary>
    /// <param name="textureSet">The shared texture set.</param>
    /// <param name="highScoreStore">The high score store.</param>
    /// <param name="gameSession">The game in progress.</param>
    /// <param name="random">
    ///     The game's shared source of random numbers. The tunnel's palette uses it, and so do the screens that follow.
    /// </param>
    /// <param name="isAttractMode">True when the tunnel belongs to the attract demo.</param>
    public WaveClearState(TextureSet textureSet, HighScoreStore highScoreStore, GameSession gameSession, Random random,
            bool isAttractMode = false)
    {
        _textureSet = textureSet;
        _highScoreStore = highScoreStore;
        _gameSession = gameSession;
        _random = random;
        _tunnelPalette = new TunnelPalette(random);
        _isAttractMode = isAttractMode;
    }

    /// <summary>
    ///     True while this tunnel belongs to the ATTRACT demo. The shell's attract-cycle sound
    ///     switch (notes §131) treats the demo's tunnel as part of the attract sequence, so the
    ///     wave-end music it asks for obeys ATTRACT MODE SOUND like every other attract sound.
    /// </summary>
    public bool IsAttractMode => _isAttractMode;

    public void Draw(SpriteBatch spriteBatch, SpriteFont font)
    {
        // The wave that was just CLEARED is carried in _clearedWave (the session's counter has
        // already advanced); nothing needs to be written for it — the ROM draws the tunnel.
        _tunnelEffect.Draw(spriteBatch, _textureSet);
    }

    public void Update(GameTime gameTime, GameStateManager gameStateManager)
    {
        if (!_hasAskedForWaveEndSound)
        {
            Sound.PlayWaveEnd(); // RRG23 GEXEC0 asks for WVSND as the wave ends
            _hasAskedForWaveEndSound = true;
        }

        // Two rings a frame (the ROM's task delay is 1 and its counter is seeded 2), then the
        // black pass — 53 + 53 rings, about 0.9 s. The state still honours the display time as
        // a floor, so the tunnel is never cut off.
        if (!_tunnelEffect.IsFinished)
        {
            // The palette task runs beside the walker's, on the same delay of 1, so the ramp's
            // window slides once per ring pass (notes §86).
            if (_ringsPaletted != _tunnelEffect.RingsDrawn) AdvancePalette();

            _tunnelEffect.Update();
        }

        if (--_ticksRemaining <= 0 && _tunnelEffect.IsFinished)
        {
            RestorePalette();
            IGameState nextGameState = new PlayingState(_textureSet, _highScoreStore, _gameSession, _random);
            if (_isAttractMode)
                nextGameState = new AttractState(GameServices.CreateFrom(_gameSession, _textureSet, _highScoreStore,
                    _gameSession.GetCurrent().PlayerInputSource, _random));

            gameStateManager.TransitionTo(nextGameState);
        }
    }

    /// <summary>One pass of the ROM's palette task: pick the ramp once, then slide its window.</summary>
    private void AdvancePalette()
    {
        _ringsPaletted = _tunnelEffect.RingsDrawn;

        if (_paletteStarted)
        {
            _tunnelPalette.Advance();
        }
        else
        {
            _paletteStarted = true;
            _tunnelPalette.Start();

            // While the ramp runs it owns every palette index, so the six colour processes must not fight
            // it for palette indices 10-15 (the ROM's own palette task writes all fifteen every pass).
            if (_textureSet.BlitterDraw.GamePalette is { } live)
                for (var paletteIndex = FontPaletteIndices.FirstCyclingPaletteIndex; paletteIndex <= 15; paletteIndex++)
                    live.SuspendPaletteIndex(paletteIndex);
        }

        if (_textureSet.BlitterDraw.GamePalette is { } palette) _tunnelPalette.Apply(palette);
    }

    /// <summary>
    ///     Puts the game palette back the way the next wave wants it — the ROM's per-wave
    ///     `LOAD_DA51_PALETTE` (notes §86).
    /// </summary>
    private void RestorePalette()
    {
        if (_textureSet.BlitterDraw.GamePalette is not { } palette) return;

        for (var paletteIndex = 1; paletteIndex <= 15; paletteIndex++) palette.SetPaletteIndexValue(paletteIndex, GamePalette.DefaultPaletteValues[paletteIndex]);

        for (var paletteIndex = FontPaletteIndices.FirstCyclingPaletteIndex; paletteIndex <= 15; paletteIndex++) palette.ResumePaletteIndex(paletteIndex);
    }
}
