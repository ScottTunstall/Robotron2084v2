using Robotron2084.Audio;
using Robotron2084.Audio.Models;
using Xunit;

namespace Robotron2084.Tests.Audio;

/// <summary>Reading the Berzerk speech board's control byte, and how loud each volume plays the word samples.</summary>
public sealed class BerzerkVoiceSettingTests
{
    [Theory]
    [InlineData(0x7B, 3, 7)] // INTRUDER ALERT
    [InlineData(0x73, 3, 6)] // CHICKEN, FIGHT LIKE A ROBOT
    [InlineData(0x60, 0, 4)] // the quietest, deepest taunt
    [InlineData(0x7F, 7, 7)] // the loudest, highest taunt
    [InlineData(0x47, 7, 0)] // the byte Berzerk sends after a sentence
    public void AControlByte_HasThePitchInBits0To2_AndTheVolumeInBits3To5(int controlByte, int pitch, int volume)
    {
        Assert.Equal(new BerzerkVoiceSetting(pitch, volume), BerzerkVoiceSetting.CreateFromControlByte(controlByte));
    }

    [Fact]
    public void TheSampleSetting_IsIntruderAlerts()
    {
        Assert.Equal(BerzerkVoiceSetting.CreateFromControlByte(0x7B), BerzerkVoiceSetting.Sample);
    }

    [Theory]
    [InlineData(0, 0f)]
    [InlineData(4, 4f / 7)]
    [InlineData(7, 1f)]
    public void EachVolume_IsItsSevenths_OfTheSamplesLoudness(int volume, float volumeShare)
    {
        Assert.Equal(volumeShare, BerzerkVoice.GetVolumeShare(volume), 5);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(8)]
    public void AVolumeOutside0To7_IsRefused(int volume)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => BerzerkVoice.GetVolumeShare(volume));
    }
}
