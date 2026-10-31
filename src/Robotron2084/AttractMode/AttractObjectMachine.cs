using Robotron2084.Entities;
using Robotron2084.Level.Attract;

namespace Robotron2084.AttractMode;

/// <summary>
///     The attract movie's OBJECT-script interpreter (notes §95.3): the ROM's second
///     scripting level — the one that animates every character in the storyline:
///     the family walking on, the hero's stroll and gunfire, the grunts, the hulk's
///     bounce, the spheroid/tank/enforcer scene, the brain's reprogramming box and
///     the score electrodes.
///     It runs the ROM's own bytes (<see cref="AttractMovieData.Scripts" />) with the
///     ROM's own opcodes at the ROM's own frame clock: one <see cref="StepFrame" />
///     call per fiftieth of a second. The one thing the ROM's listings leave implicit here is
///     the per-frame velocity integrator — the movie's `SETXV`/`SETYV` are read by
///     the object refresh (the same `OPB80` mover the playfield uses, notes §93),
///     and MONO moves its OWN hidden object explicitly because `HIB` has taken that
///     object off the list. So: every object that is ON the list integrates its
///     velocity each frame, and MONO integrates in whole columns on its own.
/// </summary>
public sealed class AttractObjectMachine
{
    /// <summary>
    ///     The ANA* walker's `NAP 8` (notes §95.7) — fiftieths of a second per walk step. It is the value the machine's
    ///     <see cref="MovieProcess.WaitArcadeFrames" /> is set to between walk steps.
    /// </summary>
    public const int WalkStepArcadeFrames = 8;

    /// <summary>ROM `EXPP` stores ACTHIT+6 into the explosion's centre row.</summary>
    private const int ExplosionRow = 0xA0 + 6;

    /// <summary>The laser bolt's sprite (<c>LASPIC</c>, the 6-pixel bar) is three columns wide.</summary>
    private const int LaserWidthColumns = 3;

    /// <summary>RRF.ASM <c>XMAX</c>: the rightmost column an object's right edge may reach.</summary>
    private const int PlayfieldMaxColumn = 0x8F;

    /// <summary>RRF.ASM <c>XMIN</c>: the leftmost column an object may occupy (the inside of the left wall).</summary>
    private const int PlayfieldMinColumn = 7;

    private readonly List<MovieExplosion> _explosions = [];
    private readonly List<MovieObject> _objects = [];
    private readonly List<MovieProcess> _processes = [];
    private readonly Random _random;
    private readonly byte[] _scriptBytes = AttractMovieData.Scripts;

    public AttractObjectMachine(Random random)
    {
        _random = random;
    }

    private enum MovieAction
    {
        None,
        Walk,
        Cycle,
        Mono,
        ReprogramShake
    }

    /// <summary>Every live object, in spawn order (laser bolts included).</summary>
    public IReadOnlyList<MovieObject> Objects => _objects;

    /// <summary>Explosions the movie asked for since the last drain (the EXP opcode).</summary>
    public List<MovieExplosion> DrainExplosions()
    {
        var drained = new List<MovieExplosion>(_explosions);
        _explosions.Clear();
        return drained;
    }

    /// <summary>Starts a script: creates its object and its process (the ROM's `OSTART`).</summary>
    public void StartScript(int scriptAddress)
    {
        var movieProcess = new MovieProcess
        {
            MovieObject = new MovieObject(null, 0, 0, 0),
            ScriptIndex = scriptAddress - AttractMovieData.ScriptBase
        };
        _objects.Add(movieProcess.MovieObject);
        _processes.Add(movieProcess);
    }

    /// <summary>Runs one fiftieth of a second: move what moves, then advance every process.</summary>
    public void StepFrame()
    {
        MoveObjects();

        // A script can FORK/GHOST while it runs, which appends to the process
        // list: walk only what existed when the frame started (a new process
        // begins on the next frame — one frame is invisible in the movie).
        var processCount = _processes.Count;
        for (var i = 0; i < processCount; i++)
            if (_processes[i].IsAlive)
                _processes[i].Step(this);

        _objects.RemoveAll(movieObject => movieObject.IsDead || (movieObject.IsLaser && movieObject.LaserArcadeFramesLeft <= 0));
        _processes.RemoveAll(p => !p.IsAlive);
    }

    /// <summary>
    ///     The ROM's object mover (RRS22 <c>OPRC80</c>/<c>OPB80</c>): a step whose new column is left of <c>XMIN</c>, or whose
    ///     right edge would pass <c>XMAX</c>, is refused, so a bolt stops at the wall and waits there for its timer.
    /// </summary>
    internal static void MoveLaserWithinTheWalls(MovieObject laserMovieObject)
    {
        var next = laserMovieObject.XSubpixels + laserMovieObject.XVelocitySubpixels;
        var column = next >> 8;
        if (column >= PlayfieldMinColumn && column + LaserWidthColumns <= PlayfieldMaxColumn + 1)
            laserMovieObject.XSubpixels = next;
    }

    /// <summary>MONOP: hide the object, then every third frame move it in whole columns and box it.</summary>
    private static void BeginMono(MovieObject movieObject, int boxPaletteIndex, int imagePaletteIndex, bool isBrainVisible)
    {
        movieObject.IsOnList = false;
        movieObject.IsMonoActive = true;
        movieObject.MonoBoxPaletteIndex = boxPaletteIndex;
        movieObject.MonoSilhouettePaletteIndex = imagePaletteIndex;
        movieObject.BrainVisible = isBrainVisible;
    }

    /// <summary>Integrates every live object's velocity for one frame; laser bolts stop at the walls and age.</summary>
    private void MoveObjects()
    {
        foreach (var movieObject in _objects)
        {
            if (movieObject.IsDead) continue;

            if (movieObject.IsLaser)
            {
                MoveLaserWithinTheWalls(movieObject);
                movieObject.LaserArcadeFramesLeft--;
                continue;
            }

            if (movieObject.IsOnList && !movieObject.IsMonoActive)
            {
                movieObject.XSubpixels += movieObject.XVelocitySubpixels;
                movieObject.YSubpixels += movieObject.YVelocitySubpixels;
            }
        }
    }

    private byte Read(MovieProcess movieProcess)
    {
        return _scriptBytes[movieProcess.ScriptIndex++];
    }

    private int ReadSignedWord(MovieProcess movieProcess)
    {
        return (short)ReadWord(movieProcess);
    }

    private int ReadWord(MovieProcess movieProcess)
    {
        return (Read(movieProcess) << 8) | Read(movieProcess);
    }

    private int GetRandomBelow(int exclusive)
    {
        return _random.Next(exclusive);
    }

    /// <summary>
    ///     The ROM's `RIGFIR`/`LEFFIR`: a bolt two columns ahead of the muzzle (right)
    ///     or one column behind it (left), six rows down, moving ±$0280 a frame, and
    ///     living the LFIRE/RFIRE operand's frame count.
    /// </summary>
    private void SpawnLaser(MovieObject sourceMovieObject, int lifetime, bool firesRight)
    {
        var columnOffset = -1;
        var xVelocitySubpixels = -0x0280;
        if (firesRight)
        {
            columnOffset = 2;
            xVelocitySubpixels = 0x0280;
        }

        var column = (sourceMovieObject.XSubpixels >> 8) + columnOffset;
        _objects.Add(new MovieObject(null, 0, column << 8, sourceMovieObject.YSubpixels + (6 << 8))
        {
            IsLaser = true,
            LaserArcadeFramesLeft = lifetime,
            XVelocitySubpixels = xVelocitySubpixels
        });
    }

    private sealed record MovieProcess
    {
        public required MovieObject MovieObject { get; init; }

        /// <summary>Index into <see cref="AttractMovieData.Scripts" /> (scripts address it by ROM address).</summary>
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

        // ---- the ROM's object opcodes (notes §95.3, the R5 $7B58 table) -----

        /// <summary>
        ///     Runs the opcode at the script cursor (notes §95.3) and returns true while the
        ///     process should keep reading — the ROM's <c>JMP [LEV2,U]</c> — or false when the
        ///     opcode has slept it or ended it. The ROM's table is a 28-entry vector list; here
        ///     it is split into the families it already reads as, so that no one method is a
        ///     wall of cases.
        /// </summary>
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
                    attractObjectMachine._explosions.Add(new MovieExplosion(
                        MovieObject.Descriptor?.Animation ?? MovieAnimation.Grunt,
                        MovieObject.AnimationFrameIndex,
                        MovieObject.GetColumn(),
                        ExplosionRow));
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
                        attractObjectMachine._processes.Add(new MovieProcess
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
                        BeginMono(MovieObject, boxPaletteIndex, silhouettePaletteIndex, isBrainVisible);
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

        /// <summary>
        ///     Not an opcode the ROM's table has: stop rather than run off the end of the script
        ///     block (the same end state as GDIE/PDEAD).
        /// </summary>
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
                WaitArcadeFrames = WalkStepArcadeFrames;
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
                if (StepsLeft > 0) WaitArcadeFrames = WalkStepArcadeFrames;
            }

            if (StepsLeft <= 0) Action = MovieAction.None;
        }

        /// <summary>
        ///     MONOP: the object is already off the list (HIB), so this is what moves
        ///     it — `ADDA OXV,X` adds the velocity's HIGH byte to the packed
        ///     (column, row) address, i.e. whole columns, every third frame — and the
        ///     box is redrawn each pass. After `frames` passes it is REBORN.
        /// </summary>
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

        /// <summary>ANA* — one walk step out of HUMANA / HLKANA (notes §95.7).</summary>
        private void TableStep(MovieWalk walk)
        {
            var table = AttractMovieData.WalkHuman;
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

        /// <summary>The ROM's `DYDX`: dx counts arcade PIXELS, and a column is two of them.</summary>
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
}
