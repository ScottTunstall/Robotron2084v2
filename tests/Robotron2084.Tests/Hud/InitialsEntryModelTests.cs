using Robotron2084.Core;
using Robotron2084.Hud;
using Robotron2084.Input;
using Xunit;

namespace Robotron2084.Tests.Hud;

/// <summary>
/// The initials entry (notes §116) — RRTESTB's GETLET: three alpha-only letters, the up/down ring
/// with the rub marker, the fire that commits and then types on by itself, and the per-letter
/// deadline that ends the entry whether or not the player is typing.
/// </summary>
public sealed class InitialsEntryModelTests
{
    private static readonly PlayerInputState Idle = new(IntVector2.Zero, IntVector2.Zero, false);
    private static readonly PlayerInputState Up = new(new IntVector2(0, -1), IntVector2.Zero, false);
    private static readonly PlayerInputState Down = new(new IntVector2(0, 1), IntVector2.Zero, false);
    private static readonly PlayerInputState Fire = new(IntVector2.Zero, IntVector2.Zero, true);

    [Fact]
    public void TheEntryWaitsForFireToBeReleasedBeforeItReadsAnythingElse()
    {
        var model = new InitialsEntryModel();

        // GETLZZ: the screen comes up under a held fire, and until it is up the entry looks at
        // nothing else at all.
        Tick(model, Fire, 30);

        Assert.Equal(' ', model.Preview);
        Assert.Equal(0, model.Position);
        Assert.False(model.IsComplete);

        Tick(model, Idle, 5);
        Press(model, Up);
        Assert.Equal('A', model.Preview);
    }

    [Fact]
    public void UpCyclesForwardThroughTheRingAndDownCyclesBack()
    {
        InitialsEntryModel model = Started();

        Press(model, Up);
        Assert.Equal('A', model.Preview);

        Press(model, Up);
        Assert.Equal('B', model.Preview);

        Press(model, Down);
        Assert.Equal('A', model.Preview);

        // Down from a blank is Z, because the rub marker needs a committed letter first (GETLST).
        Press(model, Down);
        Assert.Equal(' ', model.Preview);

        Press(model, Down);
        Assert.Equal('Z', model.Preview);
    }

    [Fact]
    public void TheRubMarkerIsOnlyInTheRingOnceALetterHasBeenCommitted()
    {
        InitialsEntryModel model = Started();

        // Z wraps to a blank before anything is committed...
        Press(model, Up);
        Assert.Equal('A', model.Preview);

        // ...but after a commit the ring goes Z, then the rub marker (LUPP1/LDN1's GETLST test).
        PressFire(model);
        Assert.Equal(1, model.Position);

        Press(model, Down);
        Assert.Equal(InitialsEntryModel.RubLetter, model.Preview);
        Assert.True(model.PreviewIsRub);
    }

    [Fact]
    public void FireCommitsTheLetterAndMovesTheCursorOn()
    {
        InitialsEntryModel model = Started();

        Press(model, Up);
        PressFire(model);

        Assert.Equal("A  ", model.Initials);
        Assert.Equal(1, model.Position);
        Assert.Equal(' ', model.Preview);
        Assert.False(model.IsComplete);
    }

    [Fact]
    public void ThreeCommittedLettersFinishTheEntry()
    {
        InitialsEntryModel model = Started();

        CommitLetter(model, Up);          // A
        CommitLetter(model, 2, Up);       // B
        CommitLetter(model, 3, Up);       // C

        Assert.True(model.IsComplete);
        Assert.Equal("ABC", model.Initials);
        Assert.Equal(InitialsEntryModel.LetterCount, model.Position);
    }

    [Fact]
    public void TheRubMarkerDeletesTheLetterBeforeTheCursor()
    {
        InitialsEntryModel model = Started();

        CommitLetter(model, 2, Up);        // B
        CommitLetter(model, 3, Up);        // C
        Assert.Equal("BC ", model.Initials);

        // Down from the blank cell reaches the rub marker, and fire there takes the C away.
        Press(model, Down);
        Assert.Equal(InitialsEntryModel.RubLetter, model.Preview);
        PressFire(model);

        Assert.False(model.IsComplete);
        Assert.Equal(1, model.Position);
        Assert.Equal("B  ", model.Initials);

        // And the letter the player rejected can be typed again.
        CommitLetter(model, 2, Up);
        Assert.Equal("BB ", model.Initials);
    }

    [Fact]
    public void HoldingFireTypesTheNextLetterItself_ButTheLastOneStillHasToBePressed()
    {
        InitialsEntryModel model = Started();
        Press(model, Up);                 // A

        int ticks = 0;
        while (model.Position < 2 && ticks < 400)
        {
            model.Tick(Fire);
            ticks++;
        }

        // GETLT3/GETLT4: the first auto-commit waits 32 counts of two frames (64 ROM frames,
        // under two seconds) and types the second letter with a blank of its own.
        Assert.Equal("A  ", model.Initials);
        Assert.Equal(2, model.Position);
        Assert.False(model.IsComplete);
        Assert.InRange(ticks, 60, 110);

        // The LAST letter is never typed for the player: it sleeps until the fire comes up.
        Tick(model, Fire, 100);
        Assert.False(model.IsComplete);

        // Releasing and pressing fire again commits it (G1LET from the main loop).
        Tick(model, Idle, 5);
        PressFire(model);

        Assert.True(model.IsComplete);
        Assert.Equal("A  ", model.Initials);
    }

    [Fact]
    public void AHeldDirectionRepeatsTheRomWay_ALateFirstRepeatThenEverySeventyMilliseconds()
    {
        InitialsEntryModel model = Started();

        // The cycle happens the moment the switch is seen (LUP's first LUPP1)...
        Tick(model, Up, 3);
        Assert.Equal('A', model.Preview);

        // ...and then LUP/LDOWN hold their ten DELAY1 turns before the repeat: half a second in
        // which the letter does not move at all.
        Tick(model, Up, 25);
        Assert.Equal('A', model.Preview);

        // The first repeat lands about 0.5 s in, and after that a repeat costs DELAY1 plus LUP's
        // NAP 1 — about 70 ms, so nine more are typed inside the next second.
        Tick(model, Up, 5);
        Assert.Equal('B', model.Preview);

        Tick(model, Up, 40);
        Assert.True(model.Preview >= 'F', $"expected several fast repeats, the preview was {model.Preview}");
    }

    [Fact]
    public void ALetterNoOneTypesTimesOutAfterSixHundredAndFortyFrames()
    {
        InitialsEntryModel model = Started();

        // TIMPRC gives each remaining letter 640 ROM frames (768 port ticks), and the entry ends
        // when the last of them runs out — three deadlines for three letters.
        Tick(model, Idle, TicksForRomFrames(2 * 640));
        Assert.False(model.IsComplete);

        Tick(model, Idle, TicksForRomFrames(640) + 10);
        Assert.True(model.IsComplete);
        Assert.Equal("   ", model.Initials);
    }

    [Fact]
    public void ALetterLeftOnTheRubMarkerTimesOutAsABlank()
    {
        InitialsEntryModel model = Started();

        CommitLetter(model, Up);          // A, and the rub marker joins the ring
        Press(model, Down);               // the preview is the rub marker
        Assert.True(model.PreviewIsRub);

        // TIMPR2 stores a space rather than let a timed-out name end on the marker.
        Tick(model, Idle, TicksForRomFrames(2 * 640) + 10);

        Assert.True(model.IsComplete);
        Assert.Equal("A  ", model.Initials);
    }

    /// <summary>A fresh entry, past GETLZZ's wait for the fire switch to come up.</summary>
    private static InitialsEntryModel Started()
    {
        var model = new InitialsEntryModel();
        Tick(model, Idle, 5);
        return model;
    }

    /// <summary>One press of a direction: the cycle happens the moment the main loop sees it.</summary>
    private static void Press(InitialsEntryModel model, PlayerInputState direction)
    {
        Tick(model, direction, 3);
        Tick(model, Idle, 3);
    }

    /// <summary>Types a letter: <paramref name="steps"/> presses of up, then fire.</summary>
    private static void CommitLetter(InitialsEntryModel model, int steps, PlayerInputState direction)
    {
        for (int step = 0; step < steps; step++)
        {
            Press(model, direction);
        }

        PressFire(model);
    }

    private static void CommitLetter(InitialsEntryModel model, PlayerInputState direction) => CommitLetter(model, 1, direction);

    /// <summary>One press of fire, released again before the typematic could take over.</summary>
    private static void PressFire(InitialsEntryModel model)
    {
        Tick(model, Fire, 3);
        Tick(model, Idle, 3);
    }

    private static void Tick(InitialsEntryModel model, PlayerInputState input, int ticks)
    {
        for (int tick = 0; tick < ticks; tick++)
        {
            model.Tick(input);
        }
    }

    /// <summary>A port tick is five sixths of a ROM frame, so N frames are (N x 6 + 4) / 5 ticks.</summary>
    private static int TicksForRomFrames(int romFrames) => ((romFrames * 6) + 4) / 5;
}
