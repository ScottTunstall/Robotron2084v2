using Robotron2084.Entities;
using Robotron2084.Level.Attract;

namespace Robotron2084.AttractMode;

/// <summary>
///     Runs the little programs that make each character in the attract movie move: the family walking on, the hero
///     walking and shooting, the grunts, the hulk, the robots that make other robots, the brain and the score posts.
/// </summary>
/// <remarks>
///     <list type="bullet">
///         <item>
///             It runs the arcade's own program bytes (<see cref="AttractMovieData.Scripts" />) and opcodes, one
///             <see cref="StepFrame" /> for each arcade frame.
///         </item>
///         <item>
///             The arcade's listings leave one thing unwritten here: moving each object by its speed on every frame.
///             The movie's <c>SETXV</c> and <c>SETYV</c> only set a speed. An object that is on the object list is
///             moved by it each frame (the same <c>OPB80</c> mover the playfield uses, notes §93). MONO moves its own
///             object by whole columns, because <c>HIB</c> has taken that object off the list.
///         </item>
///         <item>Original source: <c>RRSCRIPT.ASM</c> <c>SCTABL</c>, the object-script opcodes and their routines; see notes §95.3</item>
///         <item>Disassembly: <c>INVOKE_ACTION_FROM_CHARACTER_INTRO_SCRIPT</c> ($7B39), with the opcode routines listed from $7B58</item>
///     </list>
/// </remarks>
public sealed class AttractObjectMachine
{
    /// <summary>How many 50ths of a second a walking character waits between one step and the next.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRSCRIPT.ASM</c> <c>ANA1</c>, <c>NAP 8</c> (notes §95.7)</item>
    ///         <item>Disassembly: the unlabelled family walker at $7E00, whose wait of 8 is loaded at $7E0F</item>
    ///     </list>
    /// </remarks>
    public const int WalkStepArcadeFrames = 8;

    /// <summary>The row an explosion in the movie is centred on.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRSCRIPT.ASM</c> <c>EXPP</c>, which stores <c>ACTHIT</c> plus 6 as the row</item>
    ///         <item>Disassembly: the <c>EXP</c> opcode routine at $7C6F, which loads $A6 at $7C72</item>
    ///     </list>
    /// </remarks>
    internal const int ExplosionRow = 0xA0 + 6;

    /// <summary>The laser bolt's sprite (<c>LASPIC</c>, the 6-pixel bar) is three columns wide.</summary>
    private const int LaserWidthColumns = 3;

    /// <summary>The rightmost column an object's right edge may reach.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRF.ASM</c> <c>XMAX</c></item>
    ///         <item>Disassembly: no address in the disassembly (a name of the original source only)</item>
    ///     </list>
    /// </remarks>
    private const int PlayfieldMaxColumn = 0x8F;

    /// <summary>The leftmost column an object may occupy: the inside of the left wall.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRF.ASM</c> <c>XMIN</c></item>
    ///         <item>Disassembly: no address in the disassembly (a name of the original source only)</item>
    ///     </list>
    /// </remarks>
    private const int PlayfieldMinColumn = 7;

    private readonly List<MovieExplosion> _movieExplosions = [];
    private readonly List<MovieObject> _objects = [];
    private readonly List<MovieProcess> _movieProcesses = [];
    private readonly Random _random;
    private readonly byte[] _scriptBytes = AttractMovieData.Scripts;

    public AttractObjectMachine(Random random)
    {
        _random = random;
    }

    /// <summary>Every live object, in spawn order (laser bolts included).</summary>
    public IReadOnlyList<MovieObject> Objects => _objects;

    /// <summary>Explosions the movie asked for since the last drain (the EXP opcode).</summary>
    public List<MovieExplosion> DrainExplosions()
    {
        var drained = new List<MovieExplosion>(_movieExplosions);
        _movieExplosions.Clear();
        return drained;
    }

    /// <summary>Gets the first object that is on the screen and drawing the given animation.</summary>
    /// <param name="animation">The animation to look for.</param>
    /// <returns>The object, or null when no object on the screen is drawing that animation.</returns>
    public MovieObject? GetFirstObjectShowing(MovieAnimation animation)
    {
        return _objects.FirstOrDefault(movieObject =>
            !movieObject.IsDead && movieObject.IsOnList && movieObject.Descriptor?.Animation == animation);
    }

    /// <summary>Gets whether any object that is still alive draws the given animation, shown or hidden.</summary>
    /// <param name="animation">The animation to look for.</param>
    public bool HasLiveObjectWith(MovieAnimation animation)
    {
        return _objects.Any(movieObject => !movieObject.IsDead && movieObject.Descriptor?.Animation == animation);
    }

    /// <summary>Starts a script, which makes its object and the program that moves it.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>Original source: <c>RRSCRIPT.ASM</c> <c>OSTART</c></item>
    ///         <item>Disassembly: <c>INTRODUCE_GAME_CHARACTER_FROM_SCRIPT</c> ($7B0F)</item>
    ///     </list>
    /// </remarks>
    public void StartScript(int scriptAddress)
    {
        var movieProcess = new MovieProcess
        {
            MovieObject = new MovieObject(null, 0, 0, 0),
            ScriptIndex = scriptAddress - AttractMovieData.ScriptBase
        };
        _objects.Add(movieProcess.MovieObject);
        _movieProcesses.Add(movieProcess);
    }

    /// <summary>Adds an explosion to those waiting to be taken by the caller.</summary>
    /// <param name="movieExplosion">The explosion to add.</param>
    internal void AddExplosion(MovieExplosion movieExplosion)
    {
        _movieExplosions.Add(movieExplosion);
    }

    /// <summary>Adds a process to those that run on each step.</summary>
    /// <param name="movieProcess">The process to add.</param>
    internal void AddProcess(MovieProcess movieProcess)
    {
        _movieProcesses.Add(movieProcess);
    }

    /// <summary>Runs one fiftieth of a second: move what moves, then advance every process.</summary>
    public void StepFrame()
    {
        MoveObjects();

        // A script can FORK/GHOST while it runs, which appends to the process
        // list: walk only what existed when the frame started (a new process
        // begins on the next frame — one frame is invisible in the movie).
        var processCount = _movieProcesses.Count;
        for (var i = 0; i < processCount; i++)
            if (_movieProcesses[i].IsAlive)
                _movieProcesses[i].Step(this);

        _objects.RemoveAll(movieObject => movieObject.IsDead || (movieObject.IsLaser && movieObject.LaserArcadeFramesLeft <= 0));
        _movieProcesses.RemoveAll(p => !p.IsAlive);
    }

    /// <summary>Moves a laser bolt along, but never through a wall.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>
    ///             A step is refused when it would put the bolt left of the left wall or past the right wall. So a bolt
    ///             stops at the wall and waits there until its time is up.
    ///         </item>
    ///         <item>Original source: <c>RRS22.ASM</c> <c>OPRC80</c> and <c>OPB80</c>, with <c>RRF.ASM</c> <c>XMIN</c> and <c>XMAX</c></item>
    ///         <item>Disassembly: no address in the disassembly (a name of the original source only)</item>
    ///     </list>
    /// </remarks>
    internal static void MoveLaserWithinTheWalls(MovieObject laserMovieObject)
    {
        var next = laserMovieObject.XSubpixels + laserMovieObject.XVelocitySubpixels;
        var column = next >> 8;
        if (column >= PlayfieldMinColumn && column + LaserWidthColumns <= PlayfieldMaxColumn + 1)
            laserMovieObject.XSubpixels = next;
    }

    /// <summary>MONOP: hide the object, then every third frame move it in whole columns and box it.</summary>
    internal static void BeginMono(MovieObject movieObject, int boxPaletteIndex, int imagePaletteIndex, bool isBrainVisible)
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

    internal byte Read(MovieProcess movieProcess)
    {
        return _scriptBytes[movieProcess.ScriptIndex++];
    }

    internal int ReadSignedWord(MovieProcess movieProcess)
    {
        return (short)ReadWord(movieProcess);
    }

    internal int ReadWord(MovieProcess movieProcess)
    {
        return (Read(movieProcess) << 8) | Read(movieProcess);
    }

    internal int GetRandomBelow(int exclusive)
    {
        return _random.Next(exclusive);
    }

    /// <summary>Fires a laser bolt from a character, to the left or to the right.</summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>
    ///             A bolt fired right starts two columns ahead of the gun. A bolt fired left starts one column behind it.
    ///             Both start six rows down.
    ///         </item>
    ///         <item>The bolt moves at the speed the movie's shots always have, and lasts as long as the LFIRE or RFIRE operand says.</item>
    ///         <item>Original source: <c>RRSCRIPT.ASM</c> <c>RIGFIR</c> and <c>LEFFIR</c></item>
    ///         <item>Disassembly: the <c>LFIRE</c> and <c>RFIRE</c> opcode routines at $7C1F and $7C26</item>
    ///     </list>
    /// </remarks>
    internal void SpawnLaser(MovieObject sourceMovieObject, int lifetime, bool firesRight)
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
}
