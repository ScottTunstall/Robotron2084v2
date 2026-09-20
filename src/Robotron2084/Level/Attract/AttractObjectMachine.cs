namespace Robotron2084.Level.Attract;

/// <summary>
/// The attract movie's OBJECT-script interpreter (notes §95.3): the ROM's second
/// scripting level — the one that animates every character in the storyline:
/// the family walking on, the hero's stroll and gunfire, the grunts, the hulk's
/// bounce, the spheroid/tank/enforcer scene, the brain's reprogramming box and
/// the score posts.
///
/// It runs the ROM's own bytes (<see cref="AttractMovieData.Scripts"/>) with the
/// ROM's own opcodes at the ROM's own frame clock: one <see cref="StepFrame"/>
/// call per ROM frame. The one thing the ROM's listings leave implicit here is
/// the per-frame velocity integrator — the movie's `SETXV`/`SETYV` are read by
/// the object refresh (the same `OPB80` mover the playfield uses, notes §93),
/// and MONO moves its OWN hidden object explicitly because `HIB` has taken that
/// object off the list. So: every object that is ON the list integrates its
/// velocity each frame, and MONO integrates in whole columns on its own.
/// </summary>
public sealed class AttractObjectMachine
{
    /// <summary>The ANA* walker's `NAP 8` (notes §95.7) — ROM frames per walk step.</summary>
    public const int WalkStepFrames = 8;

    /// <summary>ROM `EXPP` stores ACTHIT+6 into the explosion's centre row.</summary>
    private const int ExplosionRow = 0xA0 + 6;

    private readonly byte[] _scripts = AttractMovieData.Scripts;
    private readonly List<MovieObject> _objects = [];
    private readonly List<MovieProcess> _processes = [];
    private readonly List<MovieExplosion> _explosions = [];
    private readonly Random _random;

    public AttractObjectMachine(Random random) => _random = random;

    /// <summary>Every live object, in spawn order (laser bolts included).</summary>
    public IReadOnlyList<MovieObject> Objects => _objects;

    /// <summary>True while any object or process is still running.</summary>
    public bool IsRunning => _processes.Count > 0 || _objects.Count > 0;

    /// <summary>Starts a script: creates its object and its process (the ROM's `OSTART`).</summary>
    public void StartScript(int scriptAddress)
    {
        var process = new MovieProcess
        {
            Object = new MovieObject(null, 0, 0, 0),
            Pc = scriptAddress - AttractMovieData.ScriptBase,
        };
        _objects.Add(process.Object);
        _processes.Add(process);
    }

    /// <summary>Explosions the movie asked for since the last drain (the EXP opcode).</summary>
    public List<MovieExplosion> DrainExplosions()
    {
        var drained = new List<MovieExplosion>(_explosions);
        _explosions.Clear();
        return drained;
    }

    /// <summary>Runs one ROM frame: move what moves, then advance every process.</summary>
    public void StepFrame()
    {
        foreach (MovieObject item in _objects)
        {
            if (item.Dead)
            {
                continue;
            }

            if (item.IsLaser)
            {
                item.X += item.XVelocity;
                item.LaserFramesLeft--;
                continue;
            }

            if (item.OnList && !item.MonoActive)
            {
                item.X += item.XVelocity;
                item.Y += item.YVelocity;
            }
        }

        // A script can FORK/GHOST while it runs, which appends to the process
        // list: walk only what existed when the frame started (a new process
        // begins on the next frame — one frame is invisible in the movie).
        int processCount = _processes.Count;
        for (int i = 0; i < processCount; i++)
        {
            if (_processes[i].Alive)
            {
                _processes[i].Step(this);
            }
        }

        for (int i = _objects.Count - 1; i >= 0; i--)
        {
            if (_objects[i].Dead || (_objects[i].IsLaser && _objects[i].LaserFramesLeft <= 0))
            {
                _objects.RemoveAt(i);
            }
        }

        _processes.RemoveAll(p => !p.Alive);
    }

    private int RandomUpTo(int exclusive) => _random.Next(exclusive);

    private sealed class MovieProcess
    {
        public required MovieObject Object { get; init; }

        /// <summary>Index into <see cref="AttractMovieData.Scripts"/> (scripts address it by ROM address).</summary>
        public int Pc { get; set; }

        public int Wait { get; set; }

        public MovieAction Action { get; set; }

        public int StepsLeft { get; set; }

        public int WalkIndex { get; set; }

        public int WalkCycle { get; set; }

        public int CycleLeft { get; set; }

        public int CycleFrames { get; set; }

        public int MonoLeft { get; set; }

        public int RprogLeft { get; set; }

        public bool RprogPhase { get; set; }

        public int Loop { get; set; }

        public bool Alive { get; set; } = true;

        /// <summary>One ROM frame of this process — the ROM's task wake-up.</summary>
        public void Step(AttractObjectMachine machine)
        {
            if (Wait > 0 && --Wait > 0)
            {
                return;
            }

            if (Action != MovieAction.None)
            {
                RunAction(machine);
                if (Wait > 0 || !Alive || Action != MovieAction.None)
                {
                    return;
                }

                // The action finished: the ROM returns into the script loop with
                // `JMP [LEV2,U]`, i.e. the next opcode runs in this same pass.
            }

            while (Alive && ReadOp(machine))
            {
            }
        }

        // ---- the ROM's object opcodes (notes §95.3, the R5 $7B58 table) -----

        private bool ReadOp(AttractObjectMachine machine)
        {
            switch (machine.Read(this))
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
                    return BeginWalk(machine.Read(this), 0);

                case 4: // MRIGHT
                    return BeginWalk(machine.Read(this), 1);

                case 5: // MDOWN
                    return BeginWalk(machine.Read(this), 2);

                case 6: // MUP
                    return BeginWalk(machine.Read(this), 3);

                case 7: // SETPOS — column, row.
                {
                    int column = machine.Read(this);
                    int row = machine.Read(this);
                    Object.X = column << 8;
                    Object.Y = row << 8;
                    return true;
                }

                case 8: // SETXV
                    Object.XVelocity = machine.ReadSignedWord(this);
                    return true;

                case 9: // SETYV
                    Object.YVelocity = machine.ReadSignedWord(this);
                    return true;

                case 10: // CYCLE — frames per image, number of advances.
                    CycleFrames = machine.Read(this);
                    CycleLeft = machine.Read(this);
                    Action = MovieAction.Cycle;
                    AdvanceImage();
                    Wait = CycleFrames;
                    return false;

                case 11: // REST
                    Wait = machine.Read(this);
                    return false;

                case 12: // DIE
                    KillObject();
                    return false;

                case 13: // EXP — remove the object and explode where it stood.
                    machine._explosions.Add(new MovieExplosion(
                        Object.Descriptor?.Art ?? MovieArt.Grunt,
                        Object.ImageIndex,
                        Object.Column,
                        ExplosionRow));
                    KillObject();
                    return false;

                case 14: // JUMP
                    Pc = machine.ReadWord(this) - AttractMovieData.ScriptBase;
                    return true;

                case 15: // HIB — off the object list (not drawn, not moved).
                    Object.OnList = false;
                    return true;

                case 16: // REBORN — back onto it.
                    Object.OnList = true;
                    return true;

                case 17: // FORK — a new object and process from that script.
                    machine.StartScript(machine.ReadWord(this));
                    return true;

                case 18: // LFIRE — bolt lifetime, then the frames this script waits.
                {
                    int lifetime = machine.Read(this);
                    machine.SpawnLaser(Object, lifetime, right: false);
                    Wait = machine.Read(this);
                    return false;
                }

                case 19: // RFIRE
                {
                    int lifetime = machine.Read(this);
                    machine.SpawnLaser(Object, lifetime, right: true);
                    Wait = machine.Read(this);
                    return false;
                }

                case 20: // LOOPER — `count` passes over the block from the label.
                {
                    int count = machine.Read(this);
                    int label = machine.ReadWord(this) - AttractMovieData.ScriptBase;
                    if (Loop == 0)
                    {
                        Loop = count;
                    }

                    if (--Loop != 0)
                    {
                        Pc = label;
                    }

                    return true;
                }

                case 21: // GHOST — another process on the SAME object.
                {
                    int script = machine.ReadWord(this);
                    machine._processes.Add(new MovieProcess
                    {
                        Object = Object,
                        Pc = script - AttractMovieData.ScriptBase,
                    });
                    return true;
                }

                case 22: // SETRP — a relative move in whole columns/rows, no animation.
                {
                    int dx = (sbyte)machine.Read(this);
                    int dy = (sbyte)machine.Read(this);
                    Object.X += dx << 8;
                    Object.Y += dy << 8;
                    return true;
                }

                case 23: // GDIE — kill the process, keep the object.
                    Alive = false;
                    return false;

                case 24: // INCIM
                    AdvanceImage();
                    return true;

                case 25: // MONO — box colour, image colour, frames, special (brain in the box).
                {
                    // The colour operands are doubled-nibble palette values exactly
                    // like the page script's COLOR ($AA = slot 10), so the slot is the
                    // HIGH nibble — and 0 means "no box".
                    int box = machine.Read(this) >> 4;
                    int image = machine.Read(this) >> 4;
                    MonoLeft = machine.Read(this);
                    bool brain = machine.Read(this) != 0;
                    BeginMono(Object, box, image, brain);
                    Action = MovieAction.Mono;
                    Wait = 3;
                    return false;
                }

                case 26: // RPROG — the 64-step vertical shake.
                    RprogLeft = 0x40;
                    RprogPhase = false;
                    Action = MovieAction.Rprog;
                    Wait = 0;
                    return true;

                case 27: // PDEAD — the score posts' retirement (notes §95.10).
                    Object.OnList = false;
                    Alive = false;
                    return false;

                default:
                    // Not an opcode the ROM's table has: stop rather than run off
                    // the end of the script block.
                    Alive = false;
                    return false;
            }
        }

        private bool BeginWalk(int steps, int direction)
        {
            if (Object.Descriptor is not { } descriptor || steps <= 0)
            {
                return true;
            }

            StepsLeft = steps;
            Action = MovieAction.Walk;
            if (descriptor.Walk == MovieWalk.BrainStep)
            {
                WalkIndex = direction;
                WalkCycle = 0;
                Wait = descriptor.StepNap;
            }
            else
            {
                WalkIndex = direction * 13;
                Wait = WalkStepFrames;
            }

            return false;
        }

        private void RunAction(AttractObjectMachine machine)
        {
            if (Object.Descriptor is not { } descriptor)
            {
                Action = MovieAction.None;
                return;
            }

            switch (Action)
            {
                case MovieAction.Walk:
                    // ANA2 / BANA2: move, DEC the step count, and only SLEEP again
                    // while steps remain — the LAST step falls straight through to
                    // the script (`JMP [LEV2,U]`). Sleeping once more after it put
                    // every walk a step period behind and dragged the whole script
                    // phase with it (the hulk reaching a human 0.5 s before her
                    // scripted death, notes §96.10).
                    if (descriptor.Walk == MovieWalk.BrainStep)
                    {
                        BrainStep(descriptor);
                        Wait = --StepsLeft > 0 ? descriptor.StepNap : 0;
                    }
                    else
                    {
                        TableStep(descriptor.Walk);
                        Wait = --StepsLeft > 0 ? WalkStepFrames : 0;
                    }

                    if (StepsLeft <= 0)
                    {
                        Action = MovieAction.None;
                    }

                    break;

                case MovieAction.Cycle:
                    if (--CycleLeft <= 0)
                    {
                        Action = MovieAction.None;
                        break;
                    }

                    AdvanceImage();
                    Wait = CycleFrames;
                    break;

                case MovieAction.Mono:
                    StepMono();
                    break;

                case MovieAction.Rprog:
                    StepRprog(machine);
                    break;
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
            Object.X += Object.XVelocity & ~0xFF;
            Object.Y += Object.YVelocity & ~0xFF;

            if (--MonoLeft <= 0)
            {
                Object.MonoActive = false;
                Object.OnList = true;
                Action = MovieAction.None;
                return;
            }

            Wait = 3;
        }

        /// <summary>RPROGP: bounce the row by ±(random 0..7) a frame apart, 64 times, then die.</summary>
        private void StepRprog(AttractObjectMachine machine)
        {
            int magnitude = machine.RandomUpTo(8);
            if (RprogPhase)
            {
                Object.ShakeRowOffset = -magnitude;
                if (--RprogLeft <= 0)
                {
                    Object.ShakeRowOffset = 0;
                    Alive = false;
                    return;
                }
            }
            else
            {
                Object.ShakeRowOffset = magnitude;
            }

            RprogPhase = !RprogPhase;
            Wait = 1;
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

        /// <summary>BR* — the descriptor's own step size, cycling the 4-picture ANATAB.</summary>
        private void BrainStep(MovieDescriptor descriptor)
        {
            int direction = WalkIndex;
            int dx = direction switch
            {
                0 => -descriptor.StepSize,
                1 => descriptor.StepSize,
                _ => 0,
            };
            int dy = direction switch
            {
                2 => descriptor.StepSize,
                3 => -descriptor.StepSize,
                _ => 0,
            };
            ApplyStep(dx, dy);

            SetImage((direction * 3) + AttractMovieData.AnimTable[WalkCycle]);
            WalkCycle = (WalkCycle + 1) % AttractMovieData.AnimTable.Length;
        }

        /// <summary>The ROM's `DYDX`: dx counts arcade PIXELS, and a column is two of them.</summary>
        private void ApplyStep(int dx, int dy)
        {
            Object.X += dx * 128;
            Object.Y += dy << 8;
        }

        private void AdvanceImage()
        {
            int count = Object.Descriptor?.ImageCount ?? 1;
            if (count > 1)
            {
                SetImage((Object.ImageIndex + 1) % count);
            }
        }

        private void SetImage(int index) => Object.ImageIndex = index;

        private void KillObject()
        {
            Object.Dead = true;
            Alive = false;
        }
    }

    private enum MovieAction
    {
        None,
        Walk,
        Cycle,
        Mono,
        Rprog,
    }

    /// <summary>MONOP: hide the object, then every third frame move it in whole columns and box it.</summary>
    private static void BeginMono(MovieObject item, int boxSlot, int imageSlot, bool brain)
    {
        item.OnList = false;
        item.MonoActive = true;
        item.MonoBoxSlot = boxSlot;
        item.MonoImageSlot = imageSlot;
        item.MonoBrain = brain;
    }

    private byte Read(MovieProcess process) => _scripts[process.Pc++];

    private int ReadWord(MovieProcess process) => (Read(process) << 8) | Read(process);

    private int ReadSignedWord(MovieProcess process) => (short)ReadWord(process);

    /// <summary>
    /// The ROM's `RIGFIR`/`LEFFIR`: a bolt two columns ahead of the muzzle (right)
    /// or one column behind it (left), six rows down, moving ±$0280 a frame, and
    /// living the LFIRE/RFIRE operand's frame count.
    /// </summary>
    private void SpawnLaser(MovieObject source, int lifetime, bool right)
    {
        int column = (source.X >> 8) + (right ? 2 : -1);
        _objects.Add(new MovieObject(null, 0, column << 8, source.Y + (6 << 8))
        {
            IsLaser = true,
            LaserRight = right,
            LaserFramesLeft = lifetime,
            XVelocity = right ? 0x0280 : -0x0280,
        });
    }
}
