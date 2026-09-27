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
/// Inter-level screen: after the display time, the same player
/// resumes at their next wave, carrying lives, score and rescue count over. The
/// session rides through unchanged — a wave clear never passes the turn in the
/// arcade (RRG23 GEXEC0 only advances the CURRENT player's PWAV), so a 2-player
/// game stays with whoever cleared the wave.
/// </summary>
public sealed class WaveClearState : IGameState
{
    /// <summary>
    /// When true the tunnel belongs to the ATTRACT demo
    /// (notes §94) and the game resumes in <see cref="AttractState"/> (the machine keeps
    /// playing itself) instead of <see cref="PlayingState"/>.
    /// </summary>
    private readonly bool _attract;

    private readonly HighScoreStore _highScores;
    private readonly GameSession _session;
    private readonly SpriteSet _sprites;

    /// <summary>
    /// The arcade's wave-complete marquee (notes §79, §128): a hatched, colour-cycling tunnel that
    /// expands from the screen's centre line out to its corners and then erases itself in black. This
    /// state only STARTS it — the ROM's <c>GEXEC0</c> starts the marquee as a task and jumps straight
    /// into the next level, so the tunnel finishes over the level it introduces.
    /// </summary>
    private readonly WaveCompleteEffect _effect = new();

    private bool _started;

    public WaveClearState(SpriteSet sprites, HighScoreStore highScores, GameSession session, bool attract = false)
    {
        _sprites = sprites;
        _highScores = highScores;
        _session = session;
        _attract = attract;
    }

    public void Draw(SpriteBatch spriteBatch, SpriteFont font) => _effect.Draw(spriteBatch, _sprites);

    public void Update(GameTime gameTime, GameStateManager manager)
    {
        if (!_started)
        {
            _started = true;
            Sound.Play(SoundTables.WaveEnd); // RRG23 GEXEC0 asks for WVSND as the wave ends
            _effect.Start(_sprites.Blitter.Palette);
        }

        _effect.Update();

        // GEXEC0 clears the screen, starts the marquee and jumps STRAIGHT into the next level
        // (`JSR SCRCLR / JSR RMST / JMP PLSTRT`), so the level is handed the still-running effect and
        // the tunnel erases itself over the new field while the wave-end music plays out (notes §128).
        manager.TransitionTo(_attract
            ? new AttractState(GameServices.From(_session, _sprites, _highScores, _session.Current.Input), _effect)
            : new PlayingState(_sprites, _highScores, _session, _effect));
    }
}
