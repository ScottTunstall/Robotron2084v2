using Robotron2084.Core;
using Robotron2084.Hud;
using Robotron2084.Input;
using Xunit;

namespace Robotron2084.Tests.Hud;

/// <summary>
///     The initials entry (notes §116) — RRTESTB's GETLET: three alpha-only letters, the up/down ring
///     with the rub marker, the fire that commits and then types on by itself, and the per-letter
///     deadline that ends the entry whether or not the player is typing.
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
        var initialsEntryModel = new InitialsEntryModel();

        // GETLZZ: the screen comes up under a held fire, and until it is up the entry looks at
        // nothing else at all.
        Tick(initialsEntryModel, Fire, 30);

        Assert.Equal(' ', initialsEntryModel.GetPreview());
        Assert.Equal(0, initialsEntryModel.LetterIndex);
        Assert.False(initialsEntryModel.IsComplete);

        Tick(initialsEntryModel, Idle, 5);
        Press(initialsEntryModel, Up);
        Assert.Equal('A', initialsEntryModel.GetPreview());
    }

    [Fact]
    public void UpCyclesForwardThroughTheRingAndDownCyclesBack()
    {
        var initialsEntryModel = Started();

        Press(initialsEntryModel, Up);
        Assert.Equal('A', initialsEntryModel.GetPreview());

        Press(initialsEntryModel, Up);
        Assert.Equal('B', initialsEntryModel.GetPreview());

        Press(initialsEntryModel, Down);
        Assert.Equal('A', initialsEntryModel.GetPreview());

        // Down from a blank is Z, because the rub marker needs a committed letter first (GETLST).
        Press(initialsEntryModel, Down);
        Assert.Equal(' ', initialsEntryModel.GetPreview());

        Press(initialsEntryModel, Down);
        Assert.Equal('Z', initialsEntryModel.GetPreview());
    }

    [Fact]
    public void TheRubMarkerIsOnlyInTheRingOnceALetterHasBeenCommitted()
    {
        var initialsEntryModel = Started();

        // Z wraps to a blank before anything is committed...
        Press(initialsEntryModel, Up);
        Assert.Equal('A', initialsEntryModel.GetPreview());

        // ...but after a commit the ring goes Z, then the rub marker (LUPP1/LDN1's GETLST test).
        PressFire(initialsEntryModel);
        Assert.Equal(1, initialsEntryModel.LetterIndex);

        Press(initialsEntryModel, Down);
        Assert.Equal(InitialsEntryModel.RubLetter, initialsEntryModel.GetPreview());
        Assert.True(initialsEntryModel.IsPreviewRub());
    }

    [Fact]
    public void FireCommitsTheLetterAndMovesTheCursorOn()
    {
        var initialsEntryModel = Started();

        Press(initialsEntryModel, Up);
        PressFire(initialsEntryModel);

        Assert.Equal("A  ", initialsEntryModel.GetInitials());
        Assert.Equal(1, initialsEntryModel.LetterIndex);
        Assert.Equal(' ', initialsEntryModel.GetPreview());
        Assert.False(initialsEntryModel.IsComplete);
    }

    [Fact]
    public void ThreeCommittedLettersFinishTheEntry()
    {
        var initialsEntryModel = Started();

        CommitLetter(initialsEntryModel, Up); // A
        CommitLetter(initialsEntryModel, 2, Up); // B
        CommitLetter(initialsEntryModel, 3, Up); // C

        Assert.True(initialsEntryModel.IsComplete);
        Assert.Equal("ABC", initialsEntryModel.GetInitials());
        Assert.Equal(InitialsEntryModel.LetterCount, initialsEntryModel.LetterIndex);
    }

    [Fact]
    public void TheRubMarkerDeletesTheLetterBeforeTheCursor()
    {
        var initialsEntryModel = Started();

        CommitLetter(initialsEntryModel, 2, Up); // B
        CommitLetter(initialsEntryModel, 3, Up); // C
        Assert.Equal("BC ", initialsEntryModel.GetInitials());

        // Down from the blank cell reaches the rub marker, and fire there takes the C away.
        Press(initialsEntryModel, Down);
        Assert.Equal(InitialsEntryModel.RubLetter, initialsEntryModel.GetPreview());
        PressFire(initialsEntryModel);

        Assert.False(initialsEntryModel.IsComplete);
        Assert.Equal(1, initialsEntryModel.LetterIndex);
        Assert.Equal("B  ", initialsEntryModel.GetInitials());

        // And the letter the player rejected can be typed again.
        CommitLetter(initialsEntryModel, 2, Up);
        Assert.Equal("BB ", initialsEntryModel.GetInitials());
    }

    [Fact]
    public void HoldingFireTypesTheNextLetterItself_ButTheLastOneStillHasToBePressed()
    {
        var initialsEntryModel = Started();
        Press(initialsEntryModel, Up); // A

        var ticks = 0;
        while (initialsEntryModel.LetterIndex < 2 && ticks < 400)
        {
            initialsEntryModel.Tick(Fire);
            ticks++;
        }

        // GETLT3/GETLT4: the first auto-commit waits 32 counts of two frames (64 fiftieths of a second,
        // under two seconds) and types the second letter with a blank of its own.
        Assert.Equal("A  ", initialsEntryModel.GetInitials());
        Assert.Equal(2, initialsEntryModel.LetterIndex);
        Assert.False(initialsEntryModel.IsComplete);
        Assert.InRange(ticks, 60, 110);

        // The LAST letter is never typed for the player: it sleeps until the fire comes up.
        Tick(initialsEntryModel, Fire, 100);
        Assert.False(initialsEntryModel.IsComplete);

        // Releasing and pressing fire again commits it (G1LET from the main loop).
        Tick(initialsEntryModel, Idle, 5);
        PressFire(initialsEntryModel);

        Assert.True(initialsEntryModel.IsComplete);
        Assert.Equal("A  ", initialsEntryModel.GetInitials());
    }

    [Fact]
    public void AHeldDirectionRepeatsTheArcadeWay_ALateFirstRepeatThenEverySeventyMilliseconds()
    {
        var initialsEntryModel = Started();

        // The cycle happens the moment the switch is seen (LUP's first LUPP1)...
        Tick(initialsEntryModel, Up, 3);
        Assert.Equal('A', initialsEntryModel.GetPreview());

        // ...and then LUP/LDOWN hold their ten DELAY1 turns before the repeat: half a second in
        // which the letter does not move at all.
        Tick(initialsEntryModel, Up, 25);
        Assert.Equal('A', initialsEntryModel.GetPreview());

        // The first repeat lands about 0.5 s in, and after that a repeat costs DELAY1 plus LUP's
        // NAP 1 — about 70 ms, so nine more are typed inside the next second.
        Tick(initialsEntryModel, Up, 5);
        Assert.Equal('B', initialsEntryModel.GetPreview());

        Tick(initialsEntryModel, Up, 40);
        Assert.True(initialsEntryModel.GetPreview() >= 'F', $"expected several fast repeats, the preview was {initialsEntryModel.GetPreview()}");
    }

    [Fact]
    public void ALetterNoOneTypesTimesOutAfterSixHundredAndFortyFrames()
    {
        var initialsEntryModel = Started();

        // TIMPRC gives each remaining letter 640 fiftieths of a second (768 port ticks), and the entry ends
        // when the last of them runs out — three deadlines for three letters.
        Tick(initialsEntryModel, Idle, TicksForArcadeFrames(2 * 640));
        Assert.False(initialsEntryModel.IsComplete);

        Tick(initialsEntryModel, Idle, TicksForArcadeFrames(640) + 10);
        Assert.True(initialsEntryModel.IsComplete);
        Assert.Equal("   ", initialsEntryModel.GetInitials());
    }

    [Fact]
    public void ALetterLeftOnTheRubMarkerTimesOutAsABlank()
    {
        var initialsEntryModel = Started();

        CommitLetter(initialsEntryModel, Up); // A, and the rub marker joins the ring
        Press(initialsEntryModel, Down); // the preview is the rub marker
        Assert.True(initialsEntryModel.IsPreviewRub());

        // TIMPR2 stores a space rather than let a timed-out name end on the marker.
        Tick(initialsEntryModel, Idle, TicksForArcadeFrames(2 * 640) + 10);

        Assert.True(initialsEntryModel.IsComplete);
        Assert.Equal("A  ", initialsEntryModel.GetInitials());
    }

    /// <summary>A fresh entry, past GETLZZ's wait for the fire switch to come up.</summary>
    private static InitialsEntryModel Started()
    {
        var initialsEntryModel = new InitialsEntryModel();
        Tick(initialsEntryModel, Idle, 5);
        return initialsEntryModel;
    }

    /// <summary>One press of a direction: the cycle happens the moment the main loop sees it.</summary>
    private static void Press(InitialsEntryModel initialsEntryModel, PlayerInputState direction)
    {
        Tick(initialsEntryModel, direction, 3);
        Tick(initialsEntryModel, Idle, 3);
    }

    /// <summary>Types a letter: <paramref name="steps" /> presses of up, then fire.</summary>
    private static void CommitLetter(InitialsEntryModel initialsEntryModel, int steps, PlayerInputState direction)
    {
        for (var step = 0; step < steps; step++) Press(initialsEntryModel, direction);

        PressFire(initialsEntryModel);
    }

    private static void CommitLetter(InitialsEntryModel initialsEntryModel, PlayerInputState direction)
    {
        CommitLetter(initialsEntryModel, 1, direction);
    }

    /// <summary>One press of fire, released again before the typematic could take over.</summary>
    private static void PressFire(InitialsEntryModel initialsEntryModel)
    {
        Tick(initialsEntryModel, Fire, 3);
        Tick(initialsEntryModel, Idle, 3);
    }

    private static void Tick(InitialsEntryModel initialsEntryModel, PlayerInputState input, int ticks)
    {
        for (var tick = 0; tick < ticks; tick++) initialsEntryModel.Tick(input);
    }

    /// <summary>A port tick is five clock units, and a fiftieth of a second is six, so N frames are (N x 6 + 4) / 5 ticks.</summary>
    private static int TicksForArcadeFrames(int arcadeFrames)
    {
        return (arcadeFrames * 6 + 4) / 5;
    }
}
