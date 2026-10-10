using Robotron2084.Entities;
using Robotron2084.Level.Attract;

namespace Robotron2084.AttractMode;

/// <summary>
/// The attract movie's OBJECT-script interpreter (notes §95.3): the ROM's second
/// scripting level — the one that animates every character in the storyline:
/// the family walking on, the hero's stroll and gunfire, the grunts, the hulk's
/// bounce, the spheroid/tank/enforcer scene, the brain's reprogramming box and
/// the score electrodes.
///
/// It runs the ROM's own bytes (<see cref="AttractMovieData.Scripts"/>) with the
/// ROM's own opcodes at the ROM's own frame clock: one <see cref="StepFrame"/>
/// call per fiftieth of a second. The one thing the ROM's listings leave implicit here is
/// the per-frame velocity integrator — the movie's `SETXV`/`SETYV` are read by
/// the object refresh (the same `OPB80` mover the playfield uses, notes §93),
/// and MONO moves its OWN hidden object explicitly because `HIB` has taken that
/// object off the list. So: every object that is ON the list integrates its
/// velocity each frame, and MONO integrates in whole columns on its own.
/// </summary>
public sealed class AttractObjectMachine
{
    /// <summary>The ANA* walker's `NAP 8` (notes §95.7) — fiftieths of a second per walk step. It is the value the machine's <see cref="MovieProcess.WaitRomFrames"/> is set to between walk steps.</summary>
    public const int WalkStepRomFrames = 8;

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

    public AttractObjectMachine(Random random) => _random = random;

    private enum MovieAction
    {
        None,
        Walk,
        Cycle,
        Mono,
        ReprogramShake,
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
        var process = new MovieProcess
        {
            Object = new MovieObject(null, 0, 0, 0),
            ScriptIndex = scriptAddress - AttractMovieData.ScriptBase,
        };
        _objects.Add(process.Object);
        _processes.Add(process);
    }

    /// <summary>Runs one fiftieth of a second: move what moves, then advance every process.</summary>
    public void StepFrame()
    {
        MoveObjects();

        // A script can FORK/GHOST while it runs, which appends to the process
        // list: walk only what existed when the frame started (a new process
        // begins on the next frame — one frame is invisible in the movie).
        int processCount = _processes.Count;
        for (int i = 0; i < processCount; i++)
        {
            if (_processes[i].IsAlive)
            {
                _processes[i].Step(this);
            }
        }

        _objects.RemoveAll(item => item.IsDead || (item.IsLaser && item.LaserRomFramesLeft <= 0));
        _processes.RemoveAll(p => !p.IsAlive);
    }

    /// <summary>
    /// The ROM's object mover (RRS22 <c>OPRC80</c>/<c>OPB80</c>): a step whose new column is left of <c>XMIN</c>, or whose
    /// right edge would pass <c>XMAX</c>, is refused, so a bolt stops at the wall and waits there for its timer.
    /// </summary>
    internal static void MoveLaserWithinTheWalls(MovieObject laser)
    {
        int next = laser.XSubpixels + laser.XVelocitySubpixels;
        int column = next >> 8;
        if (column >= PlayfieldMinColumn && column + LaserWidthColumns <= PlayfieldMaxColumn + 1)
        {
            laser.XSubpixels = next;
        }
    }

    /// <summary>MONOP: hide the object, then every third frame move it in whole columns and box it.</summary>
    private static void BeginMono(MovieObject item, int boxSlot, int imageSlot, bool showsBrain)
    {
        item.IsOnList = false;
        item.IsMonoActive = true;
        item.MonoBoxSlot = boxSlot;
        item.MonoSilhouetteSlot = imageSlot;
        item.ShowsMonoBrain = showsBrain;
    }

    /// <summary>Integrates every live object's velocity for one frame; laser bolts stop at the walls and age.</summary>
    private void MoveObjects()
    {
        foreach (MovieObject item in _objects)
        {
            if (item.IsDead)
            {
                continue;
            }

            if (item.IsLaser)
            {
                MoveLaserWithinTheWalls(item);
                item.LaserRomFramesLeft--;
                continue;
            }

            if (item.IsOnList && !item.IsMonoActive)
            {
                item.XSubpixels += item.XVelocitySubpixels;
                item.YSubpixels += item.YVelocitySubpixels;
            }
        }
    }

    private int RollUpTo(int exclusive) => _random.Next(exclusive);

    private sealed record MovieProcess
    {
        public required MovieObject Object { get; init; }

        /// <summary>Index into <see cref="AttractMovieData.Scripts"/> (scripts address it by ROM address).</summary>
        public int ScriptIndex { get; set; }

        public int WaitRomFrames { get; set; }

        public MovieAction Action { get; set; }

        public int StepsLeft { get; set; }

        public int WalkIndex { get; set; }

        public int WalkCycleStep { get; set; }

        public int CycleAdvancesLeft { get; set; }

        public int CycleRomFrames { get; set; }

        public int MonoStepsLeft { get; set; }

        public int ReprogramShakesLeft { get; set; }

        public bool IsShakingUp { get; set; }

        public int LoopPassesLeft { get; set; }

        public bool IsAlive { get; set; } = true;

        /// <summary>One step of this process.</summary>
        public void Step(AttractObjectMachine machine)
        {
            if (WaitRomFrames > 0 && --WaitRomFrames > 0)
            {
                return;
            }

            if (Action != MovieAction.None)
            {
                RunAction(machine);
                if (WaitRomFrames > 0 || !IsAlive || Action != MovieAction.None)
                {
                    return;
                }

                // The action finished: the ROM returns into the script loop with
                // `JMP [LEV2,U]`, i.e. the next opcode runs in this same pass.
            }

            while (IsAlive && ReadOp(machine))
            {
            }
        }

        // ---- the ROM's object opcodes (notes §95.3, the R5 $7B58 table) -----

        /// <summary>
        /// Runs the opcode at the script cursor (notes §95.3) and returns true while the
        /// process should keep reading — the ROM's <c>JMP [LEV2,U]</c> — or false when the
        /// opcode has slept it or ended it. The ROM's table is a 28-entry vector list; here
        /// it is split into the families it already reads as, so that no one method is a
        /// wall of cases.
        /// </summary>
        private bool ReadOp(AttractObjectMachine machine)
        {
            int opcode = machine.Read(this);
            return opcode switch
            {
                <= 6 => ReadObjectOp(machine, opcode),    // 0 NOP · 1 SETOB · 2 SETIM · 3-6 M*
                <= 9 => ReadPlacementOp(machine, opcode), // 7 SETPOS · 8 SETXV · 9 SETYV
                <= 13 => ReadLifetimeOp(machine, opcode), // 10 CYCLE · 11 REST · 12 DIE · 13 EXP
                <= 19 => ReadFlowOp(machine, opcode),     // 14-17 JUMP/HIB/REBORN/FORK · 18-19 FIRE
                <= 21 => ReadLoopOp(machine, opcode),     // 20 LOOPER · 21 GHOST
                <= 27 => ReadStateOp(machine, opcode),    // 22-27 SETRP · GDIE · INCIM · MONO…
                _ => HaltOnUnknownOpcode(),
            };
        }

        /// <summary>Family 0-6: what the object IS (descriptor, image) and its four walks.</summary>
        private bool ReadObjectOp(AttractObjectMachine machine, int opcode)
        {
            switch (opcode)
            {
                case 0: // The table's RTS entry — a NOP.
                    return true;

                case 1: // SETOB
                    Object.Descriptor = MovieDescriptors.Resolve(machine.ReadWord(this));
                    SetImage(0);
                    return true;

                case 2: // SETIM
                    SetImage(machine.Read(this));
                    return true;

                case 3: // MLEFT
                    return BeginWalk(machine.Read(this), WalkSequence.Left);

                case 4: // MRIGHT
                    return BeginWalk(machine.Read(this), WalkSequence.Right);

                case 5: // MDOWN
                    return BeginWalk(machine.Read(this), WalkSequence.Down);

                case 6: // MUP
                    return BeginWalk(machine.Read(this), WalkSequence.Up);

                default:
                    return HaltOnUnknownOpcode();
            }
        }

        /// <summary>Family 7-9: where the object is and how it drifts.</summary>
        private bool ReadPlacementOp(AttractObjectMachine machine, int opcode)
        {
            switch (opcode)
            {
                case 7: // SETPOS — column, row.
                    {
                        int column = machine.Read(this);
                        int row = machine.Read(this);
                        Object.XSubpixels = column << 8;
                        Object.YSubpixels = row << 8;
                        return true;
                    }

                case 8: // SETXV
                    Object.XVelocitySubpixels = machine.ReadSignedWord(this);
                    return true;

                case 9: // SETYV
                    Object.YVelocitySubpixels = machine.ReadSignedWord(this);
                    return true;

                default:
                    return HaltOnUnknownOpcode();
            }
        }

        /// <summary>Family 10-13: how long the object lives and how its animation frame advances.</summary>
        private bool ReadLifetimeOp(AttractObjectMachine machine, int opcode)
        {
            switch (opcode)
            {
                case 10: // CYCLE — frames per image, number of advances.
                    CycleRomFrames = machine.Read(this);
                    CycleAdvancesLeft = machine.Read(this);
                    Action = MovieAction.Cycle;
                    AdvanceImage();
                    WaitRomFrames = CycleRomFrames;
                    return false;

                case 11: // REST
                    WaitRomFrames = machine.Read(this);
                    return false;

                case 12: // DIE
                    KillObject();
                    return false;

                case 13: // EXP — remove the object and explode where it stood.
                    machine._explosions.Add(new MovieExplosion(
                        Object.Descriptor?.Animation ?? MovieAnimation.Grunt,
                        Object.AnimationFrameIndex,
                        Object.GetColumn(),
                        ExplosionRow));
                    KillObject();
                    return false;

                default:
                    return HaltOnUnknownOpcode();
            }
        }

        /// <summary>Family 14-19: the script's own flow — jumps, forks and gunfire.</summary>
        private bool ReadFlowOp(AttractObjectMachine machine, int opcode)
        {
            switch (opcode)
            {
                case 14: // JUMP
                    ScriptIndex = machine.ReadWord(this) - AttractMovieData.ScriptBase;
                    return true;

                case 15: // HIB — off the object list (not drawn, not moved).
                    Object.IsOnList = false;
                    return true;

                case 16: // REBORN — back onto it.
                    Object.IsOnList = true;
                    return true;

                case 17: // FORK — a new object and process from that script.
                    machine.StartScript(machine.ReadWord(this));
                    return true;

                case 18: // LFIRE — bolt lifetime, then the frames this script waits.
                    {
                        int lifetime = machine.Read(this);
                        machine.SpawnLaser(Object, lifetime, firesRight: false);
                        WaitRomFrames = machine.Read(this);
                        return false;
                    }

                case 19: // RFIRE
                    {
                        int lifetime = machine.Read(this);
                        machine.SpawnLaser(Object, lifetime, firesRight: true);
                        WaitRomFrames = machine.Read(this);
                        return false;
                    }

                default:
                    return HaltOnUnknownOpcode();
            }
        }

        /// <summary>Family 20-21: the LOOPER block and the GHOST second process.</summary>
        private bool ReadLoopOp(AttractObjectMachine machine, int opcode)
        {
            switch (opcode)
            {
                case 20: // LOOPER — `count` passes over the block from the label.
                    {
                        int count = machine.Read(this);
                        int labelIndex = machine.ReadWord(this) - AttractMovieData.ScriptBase;
                        if (LoopPassesLeft == 0)
                        {
                            LoopPassesLeft = count;
                        }

                        if (--LoopPassesLeft != 0)
                        {
                            ScriptIndex = labelIndex;
                        }

                        return true;
                    }

                case 21: // GHOST — another process on the SAME object.
                    {
                        int scriptAddress = machine.ReadWord(this);
                        machine._processes.Add(new MovieProcess
                        {
                            Object = Object,
                            ScriptIndex = scriptAddress - AttractMovieData.ScriptBase,
                        });
                        return true;
                    }

                default:
                    return HaltOnUnknownOpcode();
            }
        }

        /// <summary>Family 22-27: the object's own state — moves, the MONO box, the shakes.</summary>
        private bool ReadStateOp(AttractObjectMachine machine, int opcode)
        {
            switch (opcode)
            {
                case 22: // SETRP — a relative move in whole columns/rows, no animation.
                    {
                        int dx = (sbyte)machine.Read(this);
                        int dy = (sbyte)machine.Read(this);
                        Object.XSubpixels += dx << 8;
                        Object.YSubpixels += dy << 8;
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
                        // like the page script's COLOR ($AA = slot 10), so the slot is the
                        // HIGH nibble — and 0 means "no box".
                        int boxSlot = machine.Read(this) >> 4;
                        int silhouetteSlot = machine.Read(this) >> 4;
                        MonoStepsLeft = machine.Read(this);
                        bool showsBrain = machine.Read(this) != 0;
                        BeginMono(Object, boxSlot, silhouetteSlot, showsBrain);
                        Action = MovieAction.Mono;
                        WaitRomFrames = 3;
                        return false;
                    }

                case 26: // RPROG — the 64-step vertical shake.
                    // PSHAKE ($868C) is a ONE-BYTE script: RPROG owns the process
                    // until it ends, so return false (stop reading).
                    ReprogramShakesLeft = 0x40;
                    IsShakingUp = false;
                    Action = MovieAction.ReprogramShake;
                    WaitRomFrames = 0;
                    return false;

                case 27: // PDEAD — the score electrodes' retirement (notes §95.10).
                    Object.IsOnList = false;
                    IsAlive = false;
                    return false;

                default:
                    return HaltOnUnknownOpcode();
            }
        }

        /// <summary>
        /// Not an opcode the ROM's table has: stop rather than run off the end of the script
        /// block (the same end state as GDIE/PDEAD).
        /// </summary>
        private bool HaltOnUnknownOpcode()
        {
            IsAlive = false;
            return false;
        }

        private bool BeginWalk(int steps, WalkSequence walkSequence)
        {
            if (Object.Descriptor is not { } descriptor || steps <= 0)
            {
                return true;
            }

            StepsLeft = steps;
            Action = MovieAction.Walk;
            if (descriptor.Walk == MovieWalk.BrainStep)
            {
                WalkIndex = (int)walkSequence;
                WalkCycleStep = 0;
                WaitRomFrames = descriptor.StepNap;
            }
            else
            {
                WalkIndex = (int)walkSequence * 13;
                WaitRomFrames = WalkStepRomFrames;
            }

            return false;
        }

        private void RunAction(AttractObjectMachine machine)
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
                    WaitRomFrames = CycleRomFrames;
                    break;

                case MovieAction.Mono:
                    StepMono();
                    break;

                case MovieAction.ReprogramShake:
                    StepReprogramShake(machine);
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
            if (Object.Descriptor is not { } descriptor)
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
                WaitRomFrames = --StepsLeft > 0 ? descriptor.StepNap : 0;
            }
            else
            {
                TableStep(descriptor.Walk);
                WaitRomFrames = --StepsLeft > 0 ? WalkStepRomFrames : 0;
            }

            if (StepsLeft <= 0)
            {
                Action = MovieAction.None;
            }
        }

        /// <summary>
        /// MONOP: the object is already off the list (HIB), so this is what moves
        /// it — `ADDA OXV,X` adds the velocity's HIGH byte to the packed
        /// (column, row) address, i.e. whole columns, every third frame — and the
        /// box is redrawn each pass. After `frames` passes it is REBORN.
        /// </summary>
        private void StepMono()
        {
            Object.XSubpixels += Object.XVelocitySubpixels & ~0xFF;
            Object.YSubpixels += Object.YVelocitySubpixels & ~0xFF;

            if (--MonoStepsLeft <= 0)
            {
                Object.IsMonoActive = false;
                Object.IsOnList = true;
                Action = MovieAction.None;
                return;
            }

            WaitRomFrames = 3;
        }

        /// <summary>RPROGP: bounce the row by ±(random 0..7) a frame apart, 64 times, then die.</summary>
        private void StepReprogramShake(AttractObjectMachine machine)
        {
            int magnitude = machine.RollUpTo(8);
            if (IsShakingUp)
            {
                Object.ShakeRowOffset = -magnitude;
                if (--ReprogramShakesLeft <= 0)
                {
                    Object.ShakeRowOffset = 0;
                    IsAlive = false;
                    return;
                }
            }
            else
            {
                Object.ShakeRowOffset = magnitude;
            }

            IsShakingUp = !IsShakingUp;
            WaitRomFrames = 1;
        }

        /// <summary>ANA* — one walk step out of HUMANA / HLKANA (notes §95.7).</summary>
        private void TableStep(MovieWalk walk)
        {
            byte[] table = walk == MovieWalk.Hulk ? AttractMovieData.WalkHulk : AttractMovieData.WalkHuman;

            SetImage(table[WalkIndex] >> 2);
            ApplyStep((sbyte)table[WalkIndex + 1], (sbyte)table[WalkIndex + 2]);

            WalkIndex += 3;
            if (table[WalkIndex] == 0xFF)
            {
                WalkIndex -= 12;
            }
        }

        /// <summary>BR* — the descriptor's own step size, cycling the 4-entry ANATAB.</summary>
        private void BrainStep(MovieDescriptor descriptor)
        {
            WalkSequence walkSequence = (WalkSequence)WalkIndex;
            int dx = walkSequence switch
            {
                WalkSequence.Left => -descriptor.StepSize,
                WalkSequence.Right => descriptor.StepSize,
                _ => 0,
            };
            int dy = walkSequence switch
            {
                WalkSequence.Down => descriptor.StepSize,
                WalkSequence.Up => -descriptor.StepSize,
                _ => 0,
            };
            ApplyStep(dx, dy);

            SetImage(((int)walkSequence * 3) + AttractMovieData.AnimationFrameCycleTable[WalkCycleStep]);
            WalkCycleStep = (WalkCycleStep + 1) % AttractMovieData.AnimationFrameCycleTable.Length;
        }

        /// <summary>The ROM's `DYDX`: dx counts arcade PIXELS, and a column is two of them.</summary>
        private void ApplyStep(int dx, int dy)
        {
            Object.XSubpixels += dx * 128;
            Object.YSubpixels += dy << 8;
        }

        private void AdvanceImage()
        {
            int count = Object.Descriptor?.AnimationFrameCount ?? 1;
            if (count > 1)
            {
                SetImage((Object.AnimationFrameIndex + 1) % count);
            }
        }

        private void SetImage(int index) => Object.AnimationFrameIndex = index;

        private void KillObject()
        {
            Object.IsDead = true;
            IsAlive = false;
        }
    }

    private byte Read(MovieProcess process) => _scriptBytes[process.ScriptIndex++];

    private int ReadSignedWord(MovieProcess process) => (short)ReadWord(process);

    private int ReadWord(MovieProcess process) => (Read(process) << 8) | Read(process);

    /// <summary>
    /// The ROM's `RIGFIR`/`LEFFIR`: a bolt two columns ahead of the muzzle (right)
    /// or one column behind it (left), six rows down, moving ±$0280 a frame, and
    /// living the LFIRE/RFIRE operand's frame count.
    /// </summary>
    private void SpawnLaser(MovieObject source, int lifetime, bool firesRight)
    {
        int column = (source.XSubpixels >> 8) + (firesRight ? 2 : -1);
        _objects.Add(new MovieObject(null, 0, column << 8, source.YSubpixels + (6 << 8))
        {
            IsLaser = true,
            LaserRomFramesLeft = lifetime,
            XVelocitySubpixels = firesRight ? 0x0280 : -0x0280,
        });
    }
}
