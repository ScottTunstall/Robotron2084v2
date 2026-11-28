using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Robotron2084.Audio;
using Robotron2084.Core;
using Robotron2084.Core.Models;
using Robotron2084.Entities;
using Robotron2084.Entities.Extensions;
using Robotron2084.Entities.Models;
using Robotron2084.Graphics;
using Robotron2084.Level;
using Robotron2084.Level.Models;
using Robotron2084.Persistence;
using Robotron2084.Tuning;

namespace Robotron2084.States;

/// <summary>Shows the family fleeing the brains after wave 3 in Extended mode.</summary>
internal sealed class EnhancedInterludeState : IClosableGameState
{
    private const int FamilyAndBrainSpeed = PlayerTuning.PlayerSpeedX * 2;
    private const int BrainChaseSpeed = PlayerTuning.PlayerSpeedX;
    private const int FamilyAnimationIntervalTicks = 10;
    private const int BrainAnimationIntervalTicks = 10;
    private const int PlayerAnimationIntervalTicks = 3;
    private const int GroupSpacing = 24;
    private const int ChaseGap = 48;
    private const int BrainQueueGap = 8;

    private readonly GameSession _gameSession;
    private readonly HighScoreStore _highScoreStore;
    private readonly List<InterludeSprite> _brains;
    private readonly List<InterludeSprite> _family;
    private readonly List<InterludeSprite> _players;
    private readonly Random _random;
    private readonly TextureSet _textureSet;
    private readonly int? _musicDurationTicks;
    private int _firstPhaseTicks;
    private int _phaseDistance;
    private int _phaseDurationTicks;
    private int _phaseTicksElapsed;
    private int _phaseTravelled;
    private int _secondPhaseTicks;
    private bool _hasStartedMusic;
    private InterludePhase _phase;

    public EnhancedInterludeState(TextureSet textureSet, HighScoreStore highScoreStore, GameSession gameSession, Random random,
        TimeSpan? musicDuration = null)
    {
        _textureSet = textureSet;
        _highScoreStore = highScoreStore;
        _gameSession = gameSession;
        _random = random;

        var field = PlayfieldLayout.GetInnerBounds();
        var rowY = field.Top + field.Height / 2;
        var familyKinds = new[] { FamilyMemberKind.Mikey, FamilyMemberKind.Mommy, FamilyMemberKind.Daddy };
        _family = familyKinds.Select((kind, index) =>
        {
            var x = ScreenSize.Width + index * GroupSpacing;
            return CreateFamilySprite(kind, x, rowY);
        }).ToList();

        var brainWidth = ScreenSize.ToPortPixelsFromArcadePixels(CollisionSizes.BrainCollisionSize.Width);
        var brainSpacing = brainWidth + BrainQueueGap;
        var firstBrainX = _family.Max(sprite => sprite.Bounds.Right) + ChaseGap;
        _brains = Enumerable.Range(0, 3).Select(index =>
            CreateBrainSprite(firstBrainX + index * brainSpacing, rowY)).ToList();

        _players = CreatePlayerSprites(field);

        var trackDuration = musicDuration ?? Sound.FamilyChaseMusicDuration;
        if (trackDuration is { } duration && duration > TimeSpan.Zero)
        {
            _musicDurationTicks = Math.Max(2,
                (int)Math.Round(duration.TotalSeconds * ArcadeClock.PortTicksPerSecond, MidpointRounding.AwayFromZero));
            _phaseDistance = _family.Concat(_brains).Max(sprite => sprite.Bounds.Right);
            var trailingBrainX = -_brains[0].Bounds.Width - (_brains.Count - 1) * brainSpacing;
            var trailingPlayerX = trailingBrainX - ChaseGap - _players[0].Bounds.Width;
            var secondPhaseDistance = ScreenSize.Width - trailingPlayerX;
            var totalDistance = _phaseDistance + secondPhaseDistance;
            _firstPhaseTicks = Math.Clamp(
                (int)Math.Round(_musicDurationTicks.Value * (double)_phaseDistance / totalDistance), 1,
                _musicDurationTicks.Value - 1);
            _secondPhaseTicks = _musicDurationTicks.Value - _firstPhaseTicks;
            _phaseDurationTicks = _firstPhaseTicks;
        }
    }

    internal bool IsBrainChasePhase => _phase == InterludePhase.BrainsChasingPlayers;
    internal int PlayerSpriteCount => _players.Count;
    internal IReadOnlyList<Rectangle> FamilySpriteBounds => _family.Select(sprite => sprite.Bounds).ToArray();
    internal IReadOnlyList<Rectangle> BrainSpriteBounds => _brains.Select(sprite => sprite.Bounds).ToArray();
    internal IReadOnlyList<Rectangle> PlayerSpriteBounds => _players.Select(sprite => sprite.Bounds).ToArray();

    public void Close()
    {
        Sound.StopFamilyChaseMusic();
    }

    internal void StartBeforeFirstDraw()
    {
        StartAtFamilyEntry();
    }

    public void Draw(SpriteBatch spriteBatch, SpriteFont font)
    {
        // The render target clips sprites that run beyond the canvas; this screen has no wall.
        foreach (var sprite in _family)
            DrawSprite(spriteBatch, sprite);

        foreach (var sprite in _brains)
            DrawSprite(spriteBatch, sprite);

        foreach (var sprite in _players)
            DrawSprite(spriteBatch, sprite);
    }

    public void Update(GameTime gameTime, GameStateManager gameStateManager)
    {
        if (!_hasStartedMusic && _phase == InterludePhase.FamilyFleeing)
        {
            StartAtFamilyEntry();
            return;
        }

        if (_phase == InterludePhase.FamilyFleeing)
        {
            UpdateFamilyFleeing();
            return;
        }

        UpdateBrainChase(gameStateManager);
    }

    private void StartAtFamilyEntry()
    {
        // Move the formation one pixel so Mikey is visible when the music starts.
        foreach (var sprite in _family)
            Advance(sprite, -1, FamilyAnimationIntervalTicks);
        foreach (var sprite in _brains)
            Advance(sprite, -1, BrainAnimationIntervalTicks);

        if (_musicDurationTicks.HasValue) _phaseDistance--;
        StartMusicIfNeeded();
    }

    private void UpdateFamilyFleeing()
    {
        var displacement = GetPhaseDisplacement(-1, FamilyAndBrainSpeed);
        foreach (var sprite in _family)
            Advance(sprite, displacement, FamilyAnimationIntervalTicks);
        foreach (var sprite in _brains)
            Advance(sprite, displacement, BrainAnimationIntervalTicks);

        var hasLeftScreen = _family.All(IsCompletelyOffscreenLeft) && _brains.All(IsCompletelyOffscreenLeft);
        if ((_musicDurationTicks.HasValue && HasTimedPhaseEnded()) || (!_musicDurationTicks.HasValue && hasLeftScreen))
            StartBrainChase();
    }

    private void UpdateBrainChase(GameStateManager gameStateManager)
    {
        var displacement = GetPhaseDisplacement(1, BrainChaseSpeed);
        foreach (var sprite in _brains)
            Advance(sprite, displacement, BrainAnimationIntervalTicks);
        foreach (var sprite in _players)
            Advance(sprite, displacement, PlayerAnimationIntervalTicks);

        var hasLeftScreen = _players.All(IsCompletelyOffscreenRight);
        if ((_musicDurationTicks.HasValue && HasTimedPhaseEnded()) || (!_musicDurationTicks.HasValue && hasLeftScreen))
        {
            Sound.StopFamilyChaseMusic();
            gameStateManager.TransitionTo(new PlayingState(_textureSet, _highScoreStore, _gameSession, _random));
        }
    }

    private int GetPhaseDisplacement(int direction, int fallbackSpeed)
    {
        if (!_musicDurationTicks.HasValue) return direction * fallbackSpeed;

        _phaseTicksElapsed++;
        var travelled = _phaseTicksElapsed == _phaseDurationTicks
            ? _phaseDistance
            : (int)((long)_phaseDistance * _phaseTicksElapsed / _phaseDurationTicks);
        var displacement = travelled - _phaseTravelled;
        _phaseTravelled = travelled;
        return direction * displacement;
    }

    private bool HasTimedPhaseEnded()
    {
        return _phaseTicksElapsed >= _phaseDurationTicks;
    }

    private void StartMusicIfNeeded()
    {
        if (_hasStartedMusic) return;

        Sound.StopRobotronSounds();
        Speech.Stop();
        Sound.PlayFamilyChaseMusic();
        _hasStartedMusic = true;
    }

    private static void Advance(InterludeSprite sprite, int dx, int animationIntervalTicks)
    {
        sprite.Bounds = sprite.Bounds with { X = sprite.Bounds.X + dx };
        sprite.AnimationTicks++;
        if (sprite.AnimationTicks < animationIntervalTicks) return;

        sprite.AnimationTicks = 0;
        sprite.AnimationStep = (sprite.AnimationStep + 1) % sprite.WalkCycleLength;
    }

    private InterludeSprite CreateFamilySprite(FamilyMemberKind kind, int x, int rowY)
    {
        var frames = kind.GetAnimationFrames(_textureSet);
        var directionBlock = FamilyMember.GetDirectionBlock(Direction8.Left);
        var animationFrames = Enumerable.Range(0, FamilyMember.SubStepsPerDirection)
            .Select(step => frames[FamilyMember.GetWalkAnimationFrameIndex(directionBlock, step)])
            .ToArray();
        var (width, height) = kind.GetArcadeCollisionSize();
        var portHeight = ScreenSize.ToPortPixelsFromArcadePixels(height);
        return CreateSprite(animationFrames, x, rowY - portHeight, width, height, FamilyMember.SubStepsPerDirection);
    }

    private InterludeSprite CreateBrainSprite(int x, int rowY)
    {
        var (width, height) = CollisionSizes.BrainCollisionSize;
        var portHeight = ScreenSize.ToPortPixelsFromArcadePixels(height);
        return CreateSprite(GetBrainAnimationFrames(WalkSequence.Left), x, rowY - portHeight, width, height,
            Brain.WalkCycleLength);
    }

    private Texture2D[] GetBrainAnimationFrames(WalkSequence walkSequence)
    {
        return Enumerable.Range(0, Brain.WalkCycleLength)
            .Select(step => _textureSet.BrainAnimationFrameTextures[
                Brain.GetWalkAnimationFrameIndex(walkSequence, step)])
            .ToArray();
    }

    private List<InterludeSprite> CreatePlayerSprites(Rectangle field)
    {
        var playerCount = 1;
        if (_gameSession.GameMode == GameMode.TwoPlayerSimultaneous)
        {
            // TODO: The simultaneous game itself is not implemented; this only depicts both players in the interlude.
            playerCount = 2;
        }

        var width = ScreenSize.ToPortPixelsFromArcadePixels(CollisionSizes.PlayerCollisionSize.Width);
        var height = ScreenSize.ToPortPixelsFromArcadePixels(CollisionSizes.PlayerCollisionSize.Height);
        var x = -width - ChaseGap;
        var rowY = field.Top + field.Height / 2;
        return Enumerable.Range(0, playerCount).Select(index =>
        {
            var y = rowY - height + (index * 2 - (playerCount - 1)) * GroupSpacing;
            var frames = Enumerable.Range(0, Player.WalkCycleLength)
                .Select(step => _textureSet.PlayerAnimationFrameTextures[
                    Player.GetWalkAnimationFrameIndex(WalkSequence.Right, step)])
                .ToArray();
            return CreateSprite(frames, x, y - height / 2, CollisionSizes.PlayerCollisionSize.Width,
                CollisionSizes.PlayerCollisionSize.Height, Player.WalkCycleLength);
        }).ToList();
    }

    private static InterludeSprite CreateSprite(Texture2D[] frames, int x, int y, int arcadeWidth, int arcadeHeight,
        int walkCycleLength)
    {
        var bounds = new Rectangle(x, y, ScreenSize.ToPortPixelsFromArcadePixels(arcadeWidth),
            ScreenSize.ToPortPixelsFromArcadePixels(arcadeHeight));
        return new InterludeSprite(frames, bounds, walkCycleLength);
    }

    private void StartBrainChase()
    {
        _phase = InterludePhase.BrainsChasingPlayers;
        var field = PlayfieldLayout.GetInnerBounds();
        var rowY = field.Top + field.Height / 2;
        var brainSpacing = _brains[0].Bounds.Width + BrainQueueGap;
        foreach (var (brain, index) in _brains.Select((brain, index) => (brain, index)))
        {
            brain.Bounds = brain.Bounds with
            {
                X = -brain.Bounds.Width - index * brainSpacing,
                Y = rowY - brain.Bounds.Height
            };
            brain.Frames = GetBrainAnimationFrames(WalkSequence.Right);
            brain.AnimationStep = 0;
            brain.AnimationTicks = 0;
        }

        var playerRowY = rowY;
        foreach (var (player, index) in _players.Select((player, index) => (player, index)))
        {
            player.Bounds = player.Bounds with
            {
                X = _brains[^1].Bounds.X - ChaseGap - player.Bounds.Width,
                Y = playerRowY - player.Bounds.Height + (index * 2 - (_players.Count - 1)) * GroupSpacing
            };
            player.AnimationStep = 0;
            player.AnimationTicks = 0;
        }

        if (_musicDurationTicks.HasValue)
        {
            _phaseDistance = ScreenSize.Width - _brains.Concat(_players).Min(sprite => sprite.Bounds.Left);
            _phaseDurationTicks = _secondPhaseTicks;
            _phaseTicksElapsed = 0;
            _phaseTravelled = 0;
        }
    }

    private void DrawSprite(SpriteBatch spriteBatch, InterludeSprite sprite)
    {
        _textureSet.BlitterDraw.DrawSprite(spriteBatch, sprite.Frames[sprite.AnimationStep], sprite.Bounds, Color.White);
    }

    private static bool IsCompletelyOffscreenLeft(InterludeSprite sprite)
    {
        return sprite.Bounds.Right <= 0;
    }

    private static bool IsCompletelyOffscreenRight(InterludeSprite sprite)
    {
        return sprite.Bounds.Left >= ScreenSize.Width;
    }

    private enum InterludePhase
    {
        FamilyFleeing,
        BrainsChasingPlayers
    }

    private sealed class InterludeSprite(Texture2D[] frames, Rectangle bounds, int walkCycleLength)
    {
        public int AnimationStep { get; set; }
        public int AnimationTicks { get; set; }
        public Rectangle Bounds { get; set; } = bounds;
        public Texture2D[] Frames { get; set; } = frames;
        public int WalkCycleLength { get; } = walkCycleLength;
    }
}
