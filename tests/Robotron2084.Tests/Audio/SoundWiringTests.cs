using Microsoft.Xna.Framework;
using Robotron2084.Audio;
using Robotron2084.Core;
using Robotron2084.Entities;
using Robotron2084.Input;
using Robotron2084.Level;
using Xunit;

namespace Robotron2084.Tests.Audio;

/// <summary>The playfield asks for the sound the original's own routine asks for.</summary>
[Collection("SoundFacade")]
public sealed class SoundWiringTests : IDisposable
{
    private readonly RecordingSink _recordingSink = new();
    private readonly bool _wasAttractMuted = Sound.AttractMuted;
    private readonly bool _wasEnabled = Sound.Enabled;

    public SoundWiringTests()
    {
        Sound.AttractMuted = false;
        Sound.Enabled = true;
        Sound.Initialize(_recordingSink);
    }

    public void Dispose()
    {
        Sound.Enabled = _wasEnabled;
        Sound.AttractMuted = _wasAttractMuted;
    }

    [Fact]
    public void FiringTheLaser_SendsLASSND_SoundNumber01_NotTheTwoPlayerStartSound()
    {
        var firingFakeInputSource = new FakeInputSource(new PlayerInputState(IntVector2.Zero, new IntVector2(1, 0), true));
        var playField = new PlayFieldBuilder().WithParameters(new WaveParameters(1)).WithInput(firingFakeInputSource).WithSeed(1).Build();

        for (var tick = 0; tick < 200 && _recordingSink.Sends.Count == 0; tick++)
        {
            playField.Update(new GameTime());
            Sound.Tick();
        }

        Assert.Equal(0x01, _recordingSink.Sends[0].SoundNumber);
    }

    [Fact]
    public void ShootingACruiseMissile_SendsCMKSND()
    {
        var playField = new PlayFieldBuilder().WithParameters(new WaveParameters(1)).WithSeed(99).Build();
        playField.SkipWaveStart();
        var inner = playField.PlayfieldWall.PlayfieldBounds;
        IntVector2 spot = new(inner.X + 250, inner.Y + 120);
        playField.FieldEntities.CruiseMissiles.Add(new CruiseMissile(TestSprites.SharedSpriteSet, spot, playField.Player.Position,
            new Random(8)));

        playField.PlayerLaserSlots.Fire(new IntVector2(spot.X, spot.Y - 12), Direction8.Down);
        playField.Update(new GameTime());
        Sound.Tick();

        Assert.Equal(SoundTables.CruiseMissileKill.Entries[0].SoundNumber, _recordingSink.Sends[0].SoundNumber);
    }

    [Fact]
    public void ASoundFromTheLeftOfThePlayfield_IsHeardOnTheLeft()
    {
        var playField = new PlayFieldBuilder().WithParameters(new WaveParameters(1)).WithSeed(99).Build();
        var inner = playField.PlayfieldWall.PlayfieldBounds;

        playField.PlaySoundFrom(SoundTables.TankFire, new Rectangle(inner.X, inner.Center.Y, 8, 8));
        Sound.Tick();

        Assert.True(_recordingSink.Sends[0].Pan < 0f);
    }

    [Fact]
    public void AGameStart_SendsTheStartSoundForItsNumberOfPlayers()
    {
        Sound.Play(SoundTables.StartOnePlayer);
        Sound.Tick();

        Assert.Equal(0x28, _recordingSink.Sends[0].SoundNumber);
    }
}
