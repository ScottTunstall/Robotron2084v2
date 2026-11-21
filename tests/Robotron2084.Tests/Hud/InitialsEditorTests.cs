using Robotron2084.Core.Models;
using Robotron2084.Hud;
using Robotron2084.Input.Models;
using Xunit;

namespace Robotron2084.Tests.Hud;

/// <summary>
///     The initials entry (notes §116) — RRTESTB's GETLET: three alpha-only letters, the up/down ring
///     with the rub marker, the fire that commits and then types on by itself, and the per-letter
///     deadline that ends the entry whether or not the player is typing.
/// </summary>
public sealed class InitialsEditorTests
{
    private static readonly PlayerInputState Down = new(new IntVector2(0, 1), IntVector2.Zero, false);
    private static readonly PlayerInputState Fire = new(IntVector2.Zero, IntVector2.Zero, true);
    private static readonly PlayerInputState Idle = new(IntVector2.Zero, IntVector2.Zero, false);
    private static readonly PlayerInputState Up = new(new IntVector2(0, -1), IntVector2.Zero, false);

    [Fact]
    public void AHeldDirectionRepeatsTheArcadeWay_ALateFirstRepeatThenEverySeventyMilliseconds()
    {
        var initialsEditor = Started();

        // The cycle happens the moment the switch is seen (LUP's first LUPP1)...
        Tick(initialsEditor, Up, 3);
        Assert.Equal('A', initialsEditor.GetPreview());

        // ...and then LUP/LDOWN hold their ten DELAY1 turns before the repeat: half a second in
        // which the letter does not move at all.
        Tick(initialsEditor, Up, 25);
        Assert.Equal('A', initialsEditor.GetPreview());

        // The first repeat lands about 0.5 s in, and after that a repeat costs DELAY1 plus LUP's
        // NAP 1 — about 70 ms, so nine more are typed inside the next second.
        Tick(initialsEditor, Up, 5);
        Assert.Equal('B', initialsEditor.GetPreview());

        Tick(initialsEditor, Up, 40);
        Assert.True(initialsEditor.GetPreview() >= 'F', $"expected several fast repeats, the preview was {initialsEditor.GetPreview()}");
    }

    [Fact]
    public void ALetterLeftOnTheRubMarkerTimesOutAsABlank()
    {
        var initialsEditor = Started();

        CommitLetter(initialsEditor, Up); // A, and the rub marker joins the ring
        Press(initialsEditor, Down); // the preview is the rub marker
        Assert.True(initialsEditor.IsPreviewRub());

        // TIMPR2 stores a space rather than let a timed-out name end on the marker.
        Tick(initialsEditor, Idle, TicksForArcadeFrames(2 * 640) + 10);

        Assert.True(initialsEditor.IsComplete);
        Assert.Equal("A  ", initialsEditor.GetInitials());
    }

    [Fact]
    public void ALetterNoOneTypesTimesOutAfterSixHundredAndFortyFrames()
    {
        var initialsEditor = Started();

        // TIMPRC gives each remaining letter 640 fiftieths of a second (768 port ticks), and the entry ends
        // when the last of them runs out — three deadlines for three letters.
        Tick(initialsEditor, Idle, TicksForArcadeFrames(2 * 640));
        Assert.False(initialsEditor.IsComplete);

        Tick(initialsEditor, Idle, TicksForArcadeFrames(640) + 10);
        Assert.True(initialsEditor.IsComplete);
        Assert.Equal("   ", initialsEditor.GetInitials());
    }

    [Fact]
    public void FireCommitsTheLetterAndMovesTheCursorOn()
    {
        var initialsEditor = Started();

        Press(initialsEditor, Up);
        PressFire(initialsEditor);

        Assert.Equal("A  ", initialsEditor.GetInitials());
        Assert.Equal(1, initialsEditor.LetterIndex);
        Assert.Equal(' ', initialsEditor.GetPreview());
        Assert.False(initialsEditor.IsComplete);
    }

    [Fact]
    public void HoldingFireTypesTheNextLetterItself_ButTheLastOneStillHasToBePressed()
    {
        var initialsEditor = Started();
        Press(initialsEditor, Up); // A

        var ticks = 0;
        while (initialsEditor.LetterIndex < 2 && ticks < 400)
        {
            initialsEditor.Tick(Fire);
            ticks++;
        }

        // GETLT3/GETLT4: the first auto-commit waits 32 counts of two frames (64 fiftieths of a second,
        // under two seconds) and types the second letter with a blank of its own.
        Assert.Equal("A  ", initialsEditor.GetInitials());
        Assert.Equal(2, initialsEditor.LetterIndex);
        Assert.False(initialsEditor.IsComplete);
        Assert.InRange(ticks, 60, 110);

        // The LAST letter is never typed for the player: it sleeps until the fire comes up.
        Tick(initialsEditor, Fire, 100);
        Assert.False(initialsEditor.IsComplete);

        // Releasing and pressing fire again commits it (G1LET from the main loop).
        Tick(initialsEditor, Idle, 5);
        PressFire(initialsEditor);

        Assert.True(initialsEditor.IsComplete);
        Assert.Equal("A  ", initialsEditor.GetInitials());
    }

    [Fact]
    public void TheEntryWaitsForFireToBeReleasedBeforeItReadsAnythingElse()
    {
        var initialsEditor = new InitialsEditor();

        // GETLZZ: the screen comes up under a held fire, and until it is up the entry looks at
        // nothing else at all.
        Tick(initialsEditor, Fire, 30);

        Assert.Equal(' ', initialsEditor.GetPreview());
        Assert.Equal(0, initialsEditor.LetterIndex);
        Assert.False(initialsEditor.IsComplete);

        Tick(initialsEditor, Idle, 5);
        Press(initialsEditor, Up);
        Assert.Equal('A', initialsEditor.GetPreview());
    }

    [Fact]
    public void TheRubMarkerDeletesTheLetterBeforeTheCursor()
    {
        var initialsEditor = Started();

        CommitLetter(initialsEditor, 2, Up); // B
        CommitLetter(initialsEditor, 3, Up); // C
        Assert.Equal("BC ", initialsEditor.GetInitials());

        // Down from the blank cell reaches the rub marker, and fire there takes the C away.
        Press(initialsEditor, Down);
        Assert.Equal(InitialsEditor.RubLetter, initialsEditor.GetPreview());
        PressFire(initialsEditor);

        Assert.False(initialsEditor.IsComplete);
        Assert.Equal(1, initialsEditor.LetterIndex);
        Assert.Equal("B  ", initialsEditor.GetInitials());

        // And the letter the player rejected can be typed again.
        CommitLetter(initialsEditor, 2, Up);
        Assert.Equal("BB ", initialsEditor.GetInitials());
    }

    [Fact]
    public void TheRubMarkerIsOnlyInTheRingOnceALetterHasBeenCommitted()
    {
        var initialsEditor = Started();

        // Z wraps to a blank before anything is committed...
        Press(initialsEditor, Up);
        Assert.Equal('A', initialsEditor.GetPreview());

        // ...but after a commit the ring goes Z, then the rub marker (LUPP1/LDN1's GETLST test).
        PressFire(initialsEditor);
        Assert.Equal(1, initialsEditor.LetterIndex);

        Press(initialsEditor, Down);
        Assert.Equal(InitialsEditor.RubLetter, initialsEditor.GetPreview());
        Assert.True(initialsEditor.IsPreviewRub());
    }

    [Fact]
    public void ThreeCommittedLettersFinishTheEntry()
    {
        var initialsEditor = Started();

        CommitLetter(initialsEditor, Up); // A
        CommitLetter(initialsEditor, 2, Up); // B
        CommitLetter(initialsEditor, 3, Up); // C

        Assert.True(initialsEditor.IsComplete);
        Assert.Equal("ABC", initialsEditor.GetInitials());
        Assert.Equal(InitialsEditor.LetterCount, initialsEditor.LetterIndex);
    }

    [Fact]
    public void UpCyclesForwardThroughTheRingAndDownCyclesBack()
    {
        var initialsEditor = Started();

        Press(initialsEditor, Up);
        Assert.Equal('A', initialsEditor.GetPreview());

        Press(initialsEditor, Up);
        Assert.Equal('B', initialsEditor.GetPreview());

        Press(initialsEditor, Down);
        Assert.Equal('A', initialsEditor.GetPreview());

        // Down from a blank is Z, because the rub marker needs a committed letter first (GETLST).
        Press(initialsEditor, Down);
        Assert.Equal(' ', initialsEditor.GetPreview());

        Press(initialsEditor, Down);
        Assert.Equal('Z', initialsEditor.GetPreview());
    }

    /// <summary>Types a letter: <paramref name="steps" /> presses of up, then fire.</summary>
    private static void CommitLetter(InitialsEditor initialsEditor, int steps, PlayerInputState direction)
    {
        for (var step = 0; step < steps; step++) Press(initialsEditor, direction);

        PressFire(initialsEditor);
    }

    private static void CommitLetter(InitialsEditor initialsEditor, PlayerInputState direction)
    {
        CommitLetter(initialsEditor, 1, direction);
    }

    /// <summary>One press of a direction: the cycle happens the moment the main loop sees it.</summary>
    private static void Press(InitialsEditor initialsEditor, PlayerInputState direction)
    {
        Tick(initialsEditor, direction, 3);
        Tick(initialsEditor, Idle, 3);
    }

    /// <summary>One press of fire, released again before the typematic could take over.</summary>
    private static void PressFire(InitialsEditor initialsEditor)
    {
        Tick(initialsEditor, Fire, 3);
        Tick(initialsEditor, Idle, 3);
    }

    /// <summary>A fresh entry, past GETLZZ's wait for the fire switch to come up.</summary>
    private static InitialsEditor Started()
    {
        var initialsEditor = new InitialsEditor();
        Tick(initialsEditor, Idle, 5);
        return initialsEditor;
    }

    private static void Tick(InitialsEditor initialsEditor, PlayerInputState input, int ticks)
    {
        for (var tick = 0; tick < ticks; tick++) initialsEditor.Tick(input);
    }

    /// <summary>A port tick is five clock units, and a fiftieth of a second is six, so N frames are (N x 6 + 4) / 5 ticks.</summary>
    private static int TicksForArcadeFrames(int arcadeFrames)
    {
        return (arcadeFrames * 6 + 4) / 5;
    }
}
