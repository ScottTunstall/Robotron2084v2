using Robotron2084.Entities;
using Robotron2084.Level.Attract;

namespace Robotron2084.AttractMode;

internal sealed record MovieProcess
{
    public required MovieObject MovieObject { get; init; }

    /// <summary>Where the program has got to in <see cref="AttractMovieData.Scripts" />.</summary>
    /// <remarks>The scripts name places by the arcade's memory addresses, which are turned into this index.</remarks>
    public int ScriptIndex { get; set; }

    public int WaitArcadeFrames { get; set; }

    public MovieAction Action { get; set; }

    public int StepsLeft { get; set; }

    public int WalkIndex { get; set; }

    public int WalkCycleIndex { get; set; }

    public int CycleAdvancesLeft { get; set; }

    public int CycleArcadeFrames { get; set; }

    public int MonoStepsLeft { get; set; }

    public int ReprogramShakesLeft { get; set; }

    public bool IsShakingUp { get; set; }

    public int LoopPassesLeft { get; set; }

    public bool IsAlive { get; set; } = true;

    /// <summary>One step of this process.</summary>
    public void Step(AttractObjectMachine attractObjectMachine)
    {
        if (WaitArcadeFrames > 0 && --WaitArcadeFrames > 0) return;

        if (Action != MovieAction.None)
        {
            RunAction(attractObjectMachine);
            if (WaitArcadeFrames > 0 || !IsAlive || Action != MovieAction.None) return;

            // The action finished: the ROM returns into the script loop with
            // `JMP [LEV2,U]`, i.e. the next opcode runs in this same pass.
        }

        while (IsAlive && ReadOp(attractObjectMachine))
        {
        }
    }

    // ---- the arcade's object opcodes (notes §95.3, the R5 $7B58 table) -----

    /// <summary>Runs the opcode the script has reached.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>
    ///             It gets true while the program should keep reading, and false when the opcode has put it to sleep or
    ///             ended it.
    ///         </item>
    ///         <item>
    ///             The arcade's table is a list of 28 jumps. Here the opcodes are split into the groups they already
    ///             belong to, so that no one method is a wall of cases.
    ///         </item>
    ///         <item>Original source: the object opcode table and <c>JMP [LEV2,U]</c> (notes §95.3)</item>
    ///         <item>Disassembly: not separately labelled</item>
    ///     </list>
    /// </remarks>
    private bool ReadOp(AttractObjectMachine attractObjectMachine)
    {
        int opcode = attractObjectMachine.Read(this);
        return opcode switch
        {
            <= 6 => ReadObjectOp(attractObjectMachine, opcode), // 0 NOP · 1 SETOB · 2 SETIM · 3-6 M*
            <= 9 => ReadPlacementOp(attractObjectMachine, opcode), // 7 SETPOS · 8 SETXV · 9 SETYV
            <= 13 => ReadLifetimeOp(attractObjectMachine, opcode), // 10 CYCLE · 11 REST · 12 DIE · 13 EXP
            <= 19 => ReadFlowOp(attractObjectMachine, opcode), // 14-17 JUMP/HIB/REBORN/FORK · 18-19 FIRE
            <= 21 => ReadLoopOp(attractObjectMachine, opcode), // 20 LOOPER · 21 GHOST
            <= 27 => ReadStateOp(attractObjectMachine, opcode), // 22-27 SETRP · GDIE · INCIM · MONO…
            _ => HaltOnUnknownOpcode()
        };
    }

    /// <summary>Family 0-6: what the object IS (descriptor, image) and its four walks.</summary>
    private bool ReadObjectOp(AttractObjectMachine attractObjectMachine, int opcode)
    {
        switch (opcode)
        {
            case 0: // The table's RTS entry — a NOP.
                return true;

            case 1: // SETOB
                MovieObject.Descriptor = MovieDescriptors.Resolve(attractObjectMachine.ReadWord(this));
                SetImage(0);
                return true;

            case 2: // SETIM
                SetImage(attractObjectMachine.Read(this));
                return true;

            case 3: // MLEFT
                return BeginWalk(attractObjectMachine.Read(this), WalkSequence.Left);

            case 4: // MRIGHT
                return BeginWalk(attractObjectMachine.Read(this), WalkSequence.Right);

            case 5: // MDOWN
                return BeginWalk(attractObjectMachine.Read(this), WalkSequence.Down);

            case 6: // MUP
                return BeginWalk(attractObjectMachine.Read(this), WalkSequence.Up);

            default:
                return HaltOnUnknownOpcode();
        }
    }

    /// <summary>Family 7-9: where the object is and how it drifts.</summary>
    private bool ReadPlacementOp(AttractObjectMachine attractObjectMachine, int opcode)
    {
        switch (opcode)
        {
            case 7: // SETPOS — column, row.
                {
                    int column = attractObjectMachine.Read(this);
                    int row = attractObjectMachine.Read(this);
                    MovieObject.XSubpixels = column << 8;
                    MovieObject.YSubpixels = row << 8;
                    return true;
                }

            case 8: // SETXV
                MovieObject.XVelocitySubpixels = attractObjectMachine.ReadSignedWord(this);
                return true;

            case 9: // SETYV
                MovieObject.YVelocitySubpixels = attractObjectMachine.ReadSignedWord(this);
                return true;

            default:
                return HaltOnUnknownOpcode();
        }
    }

    /// <summary>Family 10-13: how long the object lives and how its animation frame advances.</summary>
    private bool ReadLifetimeOp(AttractObjectMachine attractObjectMachine, int opcode)
    {
        switch (opcode)
        {
            case 10: // CYCLE — frames per image, number of advances.
                CycleArcadeFrames = attractObjectMachine.Read(this);
                CycleAdvancesLeft = attractObjectMachine.Read(this);
                Action = MovieAction.Cycle;
                AdvanceImage();
                WaitArcadeFrames = CycleArcadeFrames;
                return false;

            case 11: // REST
                WaitArcadeFrames = attractObjectMachine.Read(this);
                return false;

            case 12: // DIE
                KillObject();
                return false;

            case 13: // EXP — remove the object and explode where it stood.
                attractObjectMachine.AddExplosion(new MovieExplosion(
                    MovieObject.Descriptor?.Animation ?? MovieAnimation.Grunt,
                    MovieObject.AnimationFrameIndex,
                    MovieObject.GetColumn(),
                    AttractObjectMachine.ExplosionRow));
                KillObject();
                return false;

            default:
                return HaltOnUnknownOpcode();
        }
    }

    /// <summary>Family 14-19: the script's own flow — jumps, forks and gunfire.</summary>
    private bool ReadFlowOp(AttractObjectMachine attractObjectMachine, int opcode)
    {
        switch (opcode)
        {
            case 14: // JUMP
                ScriptIndex = attractObjectMachine.ReadWord(this) - AttractMovieData.ScriptBase;
                return true;

            case 15: // HIB — off the object list (not drawn, not moved).
                MovieObject.IsOnList = false;
                return true;

            case 16: // REBORN — back onto it.
                MovieObject.IsOnList = true;
                return true;

            case 17: // FORK — a new object and process from that script.
                attractObjectMachine.StartScript(attractObjectMachine.ReadWord(this));
                return true;

            case 18: // LFIRE — bolt lifetime, then the frames this script waits.
                {
                    int lifetime = attractObjectMachine.Read(this);
                    attractObjectMachine.SpawnLaser(MovieObject, lifetime, false);
                    WaitArcadeFrames = attractObjectMachine.Read(this);
                    return false;
                }

            case 19: // RFIRE
                {
                    int lifetime = attractObjectMachine.Read(this);
                    attractObjectMachine.SpawnLaser(MovieObject, lifetime, true);
                    WaitArcadeFrames = attractObjectMachine.Read(this);
                    return false;
                }

            default:
                return HaltOnUnknownOpcode();
        }
    }

    /// <summary>Family 20-21: the LOOPER block and the GHOST second process.</summary>
    private bool ReadLoopOp(AttractObjectMachine attractObjectMachine, int opcode)
    {
        switch (opcode)
        {
            case 20: // LOOPER — `count` passes over the block from the label.
                {
                    int count = attractObjectMachine.Read(this);
                    var labelIndex = attractObjectMachine.ReadWord(this) - AttractMovieData.ScriptBase;
                    if (LoopPassesLeft == 0) LoopPassesLeft = count;

                    if (--LoopPassesLeft != 0) ScriptIndex = labelIndex;

                    return true;
                }

            case 21: // GHOST — another process on the SAME object.
                {
                    var scriptAddress = attractObjectMachine.ReadWord(this);
                    attractObjectMachine.AddProcess(new MovieProcess
                    {
                        MovieObject = MovieObject,
                        ScriptIndex = scriptAddress - AttractMovieData.ScriptBase
                    });
                    return true;
                }

            default:
                return HaltOnUnknownOpcode();
        }
    }

    /// <summary>Family 22-27: the object's own state — moves, the MONO box, the shakes.</summary>
    private bool ReadStateOp(AttractObjectMachine attractObjectMachine, int opcode)
    {
        switch (opcode)
        {
            case 22: // SETRP — a relative move in whole columns/rows, no animation.
                {
                    int dx = (sbyte)attractObjectMachine.Read(this);
                    int dy = (sbyte)attractObjectMachine.Read(this);
                    MovieObject.XSubpixels += dx << 8;
                    MovieObject.YSubpixels += dy << 8;
                    return true;
                }

            case 23: // GDIE — kill the process, keep the object.
                IsAlive = false;
                return false;

            case 24: // INCIM
                AdvanceImage();
                return true;

            case 25: // MONO — box colour, image colour, frames, special (brain in the box).
                {
                    // The colour operands are doubled-nibble palette values exactly
                    // like the page script's COLOR ($AA = palette index 10), so the palette index is the
                    // HIGH nibble — and 0 means "no box".
                    var boxPaletteIndex = attractObjectMachine.Read(this) >> 4;
                    var silhouettePaletteIndex = attractObjectMachine.Read(this) >> 4;
                    MonoStepsLeft = attractObjectMachine.Read(this);
                    var isBrainVisible = attractObjectMachine.Read(this) != 0;
                    AttractObjectMachine.BeginMono(MovieObject, boxPaletteIndex, silhouettePaletteIndex, isBrainVisible);
                    Action = MovieAction.Mono;
                    WaitArcadeFrames = 3;
                    return false;
                }

            case 26: // RPROG — the 64-step vertical shake.
                // PSHAKE ($868C) is a ONE-BYTE script: RPROG owns the process
                // until it ends, so return false (stop reading).
                ReprogramShakesLeft = 0x40;
                IsShakingUp = false;
                Action = MovieAction.ReprogramShake;
                WaitArcadeFrames = 0;
                return false;

            case 27: // PDEAD — the score electrodes' retirement (notes §95.10).
                MovieObject.IsOnList = false;
                IsAlive = false;
                return false;

            default:
                return HaltOnUnknownOpcode();
        }
    }

    /// <summary>Ends the program, because it has reached something that is not an opcode.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Stopping is better than running off the end of the script block. It is the same end as GDIE and PDEAD.</item>
    ///         <item>The arcade's table has no such opcode.</item>
    ///     </list>
    /// </remarks>
    private bool HaltOnUnknownOpcode()
    {
        IsAlive = false;
        return false;
    }

    private bool BeginWalk(int steps, WalkSequence walkSequence)
    {
        if (MovieObject.Descriptor is not { } descriptor || steps <= 0) return true;

        StepsLeft = steps;
        Action = MovieAction.Walk;
        if (descriptor.Walk == MovieWalk.BrainStep)
        {
            WalkIndex = (int)walkSequence;
            WalkCycleIndex = 0;
            WaitArcadeFrames = descriptor.StepNap;
        }
        else
        {
            WalkIndex = (int)walkSequence * 13;
            WaitArcadeFrames = AttractObjectMachine.WalkStepArcadeFrames;
        }

        return false;
    }

    private void RunAction(AttractObjectMachine attractObjectMachine)
    {
        switch (Action)
        {
            case MovieAction.Walk:
                StepWalk();
                break;

            case MovieAction.Cycle:
                if (--CycleAdvancesLeft <= 0)
                {
                    Action = MovieAction.None;
                    break;
                }

                AdvanceImage();
                WaitArcadeFrames = CycleArcadeFrames;
                break;

            case MovieAction.Mono:
                StepMono();
                break;

            case MovieAction.ReprogramShake:
                StepReprogramShake(attractObjectMachine);
                break;
        }
    }

    /// <summary>ANA2 / BANA2: one walk step, ending the action after the last one.</summary>
    private void StepWalk()
    {
        // Only the WALK actions need the descriptor (its walk table and
        // animation frame count); MONO and RPROG drive any object. A descriptor-less
        // walk must STOP the action: carrying on would read the next
        // script's opcodes as its own (notes §97.4).
        if (MovieObject.Descriptor is not { } descriptor)
        {
            Action = MovieAction.None;
            return;
        }

        // Move, DEC the step count, and only SLEEP again while steps remain — the
        // LAST step falls straight through to the script (`JMP [LEV2,U]`). Sleeping
        // once more after it would put every walk a step period behind and drag the
        // whole script's later phases with it (notes §96.10).
        if (descriptor.Walk == MovieWalk.BrainStep)
        {
            BrainStep(descriptor);
            StepsLeft--;
            WaitArcadeFrames = 0;
            if (StepsLeft > 0) WaitArcadeFrames = descriptor.StepNap;
        }
        else
        {
            TableStep(descriptor.Walk);
            StepsLeft--;
            WaitArcadeFrames = 0;
            if (StepsLeft > 0) WaitArcadeFrames = AttractObjectMachine.WalkStepArcadeFrames;
        }

        if (StepsLeft <= 0) Action = MovieAction.None;
    }

    /// <summary>Moves the boxed object along, and redraws its box.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>The object is already off the object list (HIB), so nothing else moves it.</item>
    ///         <item>
    ///             It moves by whole columns, every third frame: <c>ADDA OXV,X</c> adds the high byte of the speed to the
    ///             packed column and row address.
    ///         </item>
    ///         <item>After the number of passes the opcode asked for, the object is REBORN.</item>
    ///         <item>Original source: <c>MONOP</c></item>
    ///         <item>Disassembly: not separately labelled</item>
    ///     </list>
    /// </remarks>
    private void StepMono()
    {
        MovieObject.XSubpixels += MovieObject.XVelocitySubpixels & ~0xFF;
        MovieObject.YSubpixels += MovieObject.YVelocitySubpixels & ~0xFF;

        if (--MonoStepsLeft <= 0)
        {
            MovieObject.IsMonoActive = false;
            MovieObject.IsOnList = true;
            Action = MovieAction.None;
            return;
        }

        WaitArcadeFrames = 3;
    }

    /// <summary>RPROGP: bounce the row by ±(random 0..7) a frame apart, 64 times, then die.</summary>
    private void StepReprogramShake(AttractObjectMachine attractObjectMachine)
    {
        var magnitude = attractObjectMachine.GetRandomBelow(8);
        if (IsShakingUp)
        {
            MovieObject.ShakeRowOffset = -magnitude;
            if (--ReprogramShakesLeft <= 0)
            {
                MovieObject.ShakeRowOffset = 0;
                IsAlive = false;
                return;
            }
        }
        else
        {
            MovieObject.ShakeRowOffset = magnitude;
        }

        IsShakingUp = !IsShakingUp;
        WaitArcadeFrames = 1;
    }

    /// <summary>Takes one walking step from the family member's or the hulk's walk table.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: the ANA* walker, with <c>HUMANA</c> and <c>HLKANA</c> (notes §95.7)</item>
    ///         <item>Disassembly: not separately labelled</item>
    ///     </list>
    /// </remarks>
    private void TableStep(MovieWalk walk)
    {
        var table = AttractMovieData.WalkFamilyMember;
        if (walk == MovieWalk.Hulk) table = AttractMovieData.WalkHulk;

        SetImage(table[WalkIndex] >> 2);
        ApplyStep((sbyte)table[WalkIndex + 1], (sbyte)table[WalkIndex + 2]);

        WalkIndex += 3;
        if (table[WalkIndex] == 0xFF) WalkIndex -= 12;
    }

    /// <summary>BR* — the descriptor's own step size, cycling the 4-entry ANATAB.</summary>
    private void BrainStep(MovieDescriptor descriptor)
    {
        var walkSequence = (WalkSequence)WalkIndex;
        var dx = walkSequence switch
        {
            WalkSequence.Left => -descriptor.StepSize,
            WalkSequence.Right => descriptor.StepSize,
            _ => 0
        };
        var dy = walkSequence switch
        {
            WalkSequence.Down => descriptor.StepSize,
            WalkSequence.Up => -descriptor.StepSize,
            _ => 0
        };
        ApplyStep(dx, dy);

        SetImage((int)walkSequence * 3 + AttractMovieData.AnimationFrameCycleTable[WalkCycleIndex]);
        WalkCycleIndex = (WalkCycleIndex + 1) % AttractMovieData.AnimationFrameCycleTable.Length;
    }

    /// <summary>Moves the object one step. The sideways distance is in arcade pixels, and a column is two of them.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>DYDX</c></item>
    ///         <item>Disassembly: not separately labelled</item>
    ///     </list>
    /// </remarks>
    private void ApplyStep(int dx, int dy)
    {
        MovieObject.XSubpixels += dx * 128;
        MovieObject.YSubpixels += dy << 8;
    }

    private void AdvanceImage()
    {
        var count = MovieObject.Descriptor?.AnimationFrameCount ?? 1;
        if (count > 1) SetImage((MovieObject.AnimationFrameIndex + 1) % count);
    }

    private void SetImage(int index)
    {
        MovieObject.AnimationFrameIndex = index;
    }

    private void KillObject()
    {
        MovieObject.IsDead = true;
        IsAlive = false;
    }
}
