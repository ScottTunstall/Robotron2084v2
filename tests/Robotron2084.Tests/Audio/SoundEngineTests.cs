using Robotron2084.Audio;
using Xunit;

namespace Robotron2084.Tests.Audio;

public class SoundEngineTests
{
    private static SoundSequence Seq(int priority, params (byte dur, byte len, byte note)[] entries) =>
        new(priority, 0, entries.Select(e => new SoundEntry(e.dur, e.len, e.note)).ToArray());

    [Fact]
    public void SingleEntrySoundsForLenTicksThenFreesTheVoice()
    {
        // R5 $273A laser: (1, 16, $25), priority 240.
        var sink = new RecordingSink();
        var engine = new SoundEngine(sink);

        engine.Play(SoundTables.PlayerLaser);

        for (int i = 0; i < 20; i++)
        {
            engine.Tick();
        }

        // First note sounds on the first tick (the ROM primes $57=$58=1, so
        // the first vblank lands straight on the first entry), held 16 ticks
        // in total. Then the table ends and the voice is free (priority 0).
        Assert.Single(sink.Calls);
        Assert.Equal((0x25, 16), sink.Calls[0]);
        Assert.Equal(0, engine.CurrentPriority);
    }

    [Fact]
    public void MultiEntrySequencesEntriesInOrder()
    {
        // R5 $30EF player death: (2, 8, $11) then (1, 32, $17) — 48 ticks.
        var sink = new RecordingSink();
        var engine = new SoundEngine(sink);

        engine.Play(SoundTables.PlayerDeath);

        for (int i = 0; i < 60; i++)
        {
            engine.Tick();
        }

        List<(int note, int ticks)> expected = [(0x11, 8), (0x11, 8), (0x17, 32)];
        Assert.Equal(expected, sink.Calls);
        Assert.Equal(0, engine.CurrentPriority);
    }

    [Fact]
    public void SamePriorityIsIgnoredWhileSounding()
    {
        var sink = new RecordingSink();
        var engine = new SoundEngine(sink);
        SoundSequence seq = Seq(208, (1, 16, 0x25));

        engine.Play(seq);
        engine.Tick();
        engine.Play(seq); // priority 208 vs current 208: NOT strictly higher

        // Only the original request's note is in flight; a second one would
        // have produced a third PlayNote at tick 17 (it must not).
        for (int i = 0; i < 16; i++)
        {
            engine.Tick();
        }

        Assert.Single(sink.Calls);
    }

    [Fact]
    public void LowerPriorityIsIgnoredHigherPreempts()
    {
        var sink = new RecordingSink();
        var engine = new SoundEngine(sink);

        engine.Play(Seq(200, (1, 8, 0x04))); // shell fire (p200)
        engine.Tick();
        engine.Play(Seq(208, (1, 4, 0x14))); // laser-ish (p208): strictly higher
        engine.Tick();

        List<(int note, int ticks)> expected = [(0x04, 8), (0x14, 4)];
        Assert.Equal(expected, sink.Calls);
    }

    [Fact]
    public void VoiceIsFreeAfterTheTableEnds()
    {
        var sink = new RecordingSink();
        var engine = new SoundEngine(sink);

        engine.Play(Seq(255, (1, 2, 0x13))); // brain warp-in, top priority
        for (int i = 0; i < 10; i++)
        {
            engine.Tick();
        }

        // A low-priority request must now play (the $56 priority is 0).
        engine.Play(Seq(192, (1, 10, 0x06)));
        engine.Tick();

        List<(int note, int ticks)> expected = [(0x13, 2), (0x06, 10)];
        Assert.Equal(expected, sink.Calls);
    }

    [Fact]
    public void TicksWithoutARequestAreNoOps()
    {
        var sink = new RecordingSink();
        var engine = new SoundEngine(sink);

        for (int i = 0; i < 10; i++)
        {
            engine.Tick();
        }

        Assert.Empty(sink.Calls);
    }

    [Fact]
    public void TablesMatchTheDecodedRom()
    {
        // Spot-check the decoded ROM tables (notes §36.2) — the whole point
        // of the provenance is that these bytes came from robotron64k.bin.
        Assert.Equal(240, SoundTables.PlayerLaser.Priority);
        Assert.Equal(0x26E6, SoundTables.PlayerLaser.RomTableAddress);
        Assert.Equal(new[] { new SoundEntry(1, 16, 0x25) }, SoundTables.PlayerLaser.Entries);

        Assert.Equal(238, SoundTables.PlayerDeath.Priority);
        Assert.Equal(new[] { new SoundEntry(2, 8, 0x11), new SoundEntry(1, 32, 0x17) }, SoundTables.PlayerDeath.Entries);

        Assert.Equal(200, SoundTables.ShellFire.Priority);
        Assert.Equal(new[] { new SoundEntry(1, 8, 0x04) }, SoundTables.ShellFire.Entries);

        Assert.Equal(255, SoundTables.BrainWarpIn.Priority);
        Assert.Equal(new[] { new SoundEntry(1, 1, 0x13) }, SoundTables.BrainWarpIn.Entries);

        Assert.Equal(239, SoundTables.BonusLife.Priority);
        Assert.Equal(new[] { new SoundEntry(1, 32, 0x1E) }, SoundTables.BonusLife.Entries);
    }
}
