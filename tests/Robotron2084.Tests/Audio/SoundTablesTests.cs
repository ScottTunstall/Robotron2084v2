using Robotron2084.Audio;
using Robotron2084.Level;
using Xunit;

namespace Robotron2084.Tests.Audio;

public class SoundTablesTests
{
    [Fact]
    public void EveryTable_MatchesTheMainROMsOwnBytes()
    {
        byte[] rom = RomFiles.ReadOrSkip(RomFiles.MainRom);

        foreach (SoundSequence table in SoundTables.All)
        {
            List<byte> expected = [(byte)table.Priority];
            foreach (SoundEntry entry in table.Entries)
            {
                expected.AddRange([entry.Repetitions, entry.LengthVblanks, entry.SoundNumber]);
            }

            expected.Add(0); // a repeat count of 0 ends the table
            byte[] actual = rom[table.RomTableAddress..(table.RomTableAddress + expected.Count)];
            Assert.True(expected.SequenceEqual(actual), $"The table at ${table.RomTableAddress:X4} does not match the ROM.");
        }
    }

    [Fact]
    public void TheLaserIsLASSND_NotTheTwoPlayerStartSound()
    {
        // RRG23: LASSND FCB $D0,$01,$08,1,0 and ST2SND FCB $F0,$01,$10,$25,0.
        Assert.Equal(0xD0, SoundTables.Laser.Priority);
        Assert.Equal([new SoundEntry(1, 8, 0x01)], SoundTables.Laser.Entries);
        Assert.Equal([new SoundEntry(1, 16, 0x25)], SoundTables.StartTwoPlayers.Entries);
    }

    [Theory]
    [InlineData(RobotKind.Electrode, "PostKill")]
    [InlineData(RobotKind.Grunt, "RobotHit")]
    [InlineData(RobotKind.Hulk, "HulkHit")]
    [InlineData(RobotKind.Spheroid, "CircleKill")]
    [InlineData(RobotKind.Enforcer, "EnforcerKill")]
    [InlineData(RobotKind.Quark, "SquareKill")]
    [InlineData(RobotKind.Tank, "TankKill")]
    [InlineData(RobotKind.Brain, "BrainKill")]
    [InlineData(RobotKind.Prog, "ProgKill")]
    [InlineData(RobotKind.Spark, "SparkKill")]
    [InlineData(RobotKind.TankShell, "ShellKill")]
    [InlineData(RobotKind.CruiseMissile, "CruiseMissileKill")]
    public void EachKind_PlaysItsOwnKillSound(RobotKind kind, string table)
    {
        object? expected = typeof(SoundTables).GetField(table)!.GetValue(null);

        Assert.Same(expected, RobotKinds.Of(kind).LaserHitSound);
    }
}
