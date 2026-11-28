using Robotron2084.Core.Models;
using Robotron2084.Input;
using Robotron2084.Input.Models;
using Robotron2084.Level;
using Robotron2084.Level.Models;
using Robotron2084.Persistence;
using Robotron2084.States;
using Robotron2084.Tuning;
using Xunit;

namespace Robotron2084.Tests.States;

public sealed class EnhancedInterludeStateTests
{
    [Fact]
    public void ClearingWaveThreeInExtendedMode_PlaysInterludeThenResumes()
    {
        var gameSession = CreateSession(GameStyle.Extended);
        var stateManager = StartWaveClear(gameSession);

        AdvanceUntil<EnhancedInterludeState>(stateManager);

        var interlude = Assert.IsType<EnhancedInterludeState>(stateManager.CurrentGameState);
        for (var tick = 0; tick < 1000 && !interlude.IsBrainChasePhase; tick++)
            stateManager.Update(new Microsoft.Xna.Framework.GameTime());

        Assert.True(interlude.IsBrainChasePhase);

        AdvanceUntil<PlayingState>(stateManager);

        Assert.IsType<PlayingState>(stateManager.CurrentGameState);
    }

    [Fact]
    public void ClearingWaveThreeInClassicMode_SkipsInterlude()
    {
        var gameSession = CreateSession(GameStyle.Classic);
        var stateManager = StartWaveClear(gameSession);

        AdvanceUntil<PlayingState>(stateManager);

        Assert.IsType<PlayingState>(stateManager.CurrentGameState);
    }

    [Fact]
    public void InterludeShowsTwoPlayerSpritesOnlyForSimultaneousMode()
    {
        var onePlayer = new EnhancedInterludeState(TestTextures.SharedTextureSet, null!,
            CreateSession(GameStyle.Extended, GameMode.OnePlayer), new Random(1));
        var simultaneous = new EnhancedInterludeState(TestTextures.SharedTextureSet, null!,
            CreateSession(GameStyle.Extended, GameMode.TwoPlayerSimultaneous), new Random(1));

        Assert.Equal(1, onePlayer.PlayerSpriteCount);
        Assert.Equal(2, simultaneous.PlayerSpriteCount);
    }

    [Fact]
    public void FamilyAndBrainsRunInOneRow_WithBrainsQueuedBehindTheFamily()
    {
        var interlude = new EnhancedInterludeState(TestTextures.SharedTextureSet, null!,
            CreateSession(GameStyle.Extended), new Random(1));
        var field = PlayfieldLayout.GetInnerBounds();
        var rowY = field.Top + field.Height / 2;
        var family = interlude.FamilySpriteBounds;
        var brains = interlude.BrainSpriteBounds;

        Assert.All(family, sprite => Assert.Equal(rowY, sprite.Bottom));
        Assert.All(brains, sprite => Assert.Equal(rowY, sprite.Bottom));
        Assert.True(family[0].X < family[1].X && family[1].X < family[2].X);
        Assert.True(family[2].Right < brains[0].X);
        Assert.True(brains[0].Right < brains[1].X && brains[1].Right < brains[2].X);
    }

    [Fact]
    public void PlayerChasesBehindTheBrainQueue_WithoutOvertaking()
    {
        var interlude = new EnhancedInterludeState(TestTextures.SharedTextureSet, null!,
            CreateSession(GameStyle.Extended), new Random(1));
        var stateManager = new GameStateManager(interlude);

        for (var tick = 0; tick < 1000 && !interlude.IsBrainChasePhase; tick++)
            stateManager.Update(new Microsoft.Xna.Framework.GameTime());

        Assert.True(interlude.IsBrainChasePhase);
        var playerBefore = interlude.PlayerSpriteBounds[0];
        var trailingBrainBefore = interlude.BrainSpriteBounds[^1];
        var initialGap = trailingBrainBefore.Left - playerBefore.Right;
        Assert.True(initialGap > 0);

        for (var tick = 0; tick < 20; tick++)
            stateManager.Update(new Microsoft.Xna.Framework.GameTime());

        var playerAfter = interlude.PlayerSpriteBounds[0];
        var trailingBrainAfter = interlude.BrainSpriteBounds[^1];
        Assert.Equal(initialGap, trailingBrainAfter.Left - playerAfter.Right);
    }

    private static GameSession CreateSession(GameStyle gameStyle, GameMode gameMode = GameMode.OnePlayer)
    {
        var gameSettings = GameSettings.CreateFactoryDefaults();
        gameSettings.GameStyle = gameStyle;
        var gameSession = GameSession.CreateNewGame(gameMode, new IdleInputSource(), gameSettings: gameSettings);
        gameSession.GetCurrent().Wave = 4;
        return gameSession;
    }

    private static GameStateManager StartWaveClear(GameSession gameSession)
    {
        var waveClear = new WaveClearState(TestTextures.SharedTextureSet, null!, gameSession, new Random(1));
        return new GameStateManager(waveClear);
    }

    private static void AdvanceUntil<TGameState>(GameStateManager stateManager) where TGameState : IGameState
    {
        for (var tick = 0; tick < 1000 && stateManager.CurrentGameState is not TGameState; tick++)
            stateManager.Update(new Microsoft.Xna.Framework.GameTime());
    }

    private sealed class IdleInputSource : IPlayerInputSource
    {
        public PlayerInputState Poll()
        {
            return new PlayerInputState(IntVector2.Zero, IntVector2.Zero, false, false);
        }
    }
}
