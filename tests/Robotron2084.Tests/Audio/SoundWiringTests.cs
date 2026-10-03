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
    private readonly bool _wasAttractMuted = Sound.AttractMuted;
    private readonly bool _wasEnabled = Sound.Enabled;
    private readonly RecordingSink _sink = new();

    public SoundWiringTests()
    {
        Sound.AttractMuted = false;
        Sound.Enabled = true;
        Sound.Initialize(_sink);
    }

    public void Dispose()
    {
        Sound.Enabled = _wasEnabled;
        Sound.AttractMuted = _wasAttractMuted;
    }

    [Fact]
    public void FiringTheLaser_SendsLASSND_SoundNumber01_NotTheTwoPlayerStartSound()
    {
        var firing = new FakeInputSource(new PlayerInputState(IntVector2.Zero, new IntVector2(1, 0), true));
        PlayField field = new PlayFieldBuilder().WithParameters(new LevelParameters(LevelNumber: 1)).WithInput(firing).WithSeed(1).Build();

        for (int tick = 0; tick < 200 && _sink.Sends.Count == 0; tick++)
        {
            field.Update(new GameTime());
            Sound.Tick();
        }

        Assert.Equal(0x01, _sink.Sends[0].SoundNumber);
    }

    [Fact]
    public void ShootingACruiseMissile_SendsCMKSND()
    {
        PlayField field = new PlayFieldBuilder().WithParameters(new LevelParameters(LevelNumber: 1)).WithSeed(99).Build();
        Rectangle inner = field.Wall.PlayfieldBounds;
        IntVector2 spot = new(inner.X + 250, inner.Y + 120);
        field.Entities.CruiseMissiles.Add(new CruiseMissile(TestSprites.Shared, spot, field.Player.Position, new Random(8)));

        Assert.True(field.PlayerLasers.TryFire(new IntVector2(spot.X, spot.Y - 12), Direction8.Down, out PlayerLaser? _));
        field.Update(new GameTime());
        Sound.Tick();

        Assert.Equal(SoundTables.CruiseMissileKill.Entries[0].SoundNumber, _sink.Sends[0].SoundNumber);
    }

    [Fact]
    public void ASoundFromTheLeftOfThePlayfield_IsHeardOnTheLeft()
    {
        PlayField field = new PlayFieldBuilder().WithParameters(new LevelParameters(LevelNumber: 1)).WithSeed(99).Build();
        Rectangle inner = field.Wall.PlayfieldBounds;

        field.PlaySoundFrom(SoundTables.TankFire, new Rectangle(inner.X, inner.Center.Y, 8, 8));
        Sound.Tick();

        Assert.True(_sink.Sends[0].Pan < 0f);
    }

    [Fact]
    public void AGameStart_SendsTheStartSoundForItsNumberOfPlayers()
    {
        Sound.Play(SoundTables.StartOnePlayer);
        Sound.Tick();

        Assert.Equal(0x28, _sink.Sends[0].SoundNumber);
    }
}
