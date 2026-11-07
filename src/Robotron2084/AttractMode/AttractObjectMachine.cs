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
    internal const int ExplosionRow = 0xA0 + 6;

    /// <summary>The laser bolt's sprite (<c>LASPIC</c>, the 6-pixel bar) is three columns wide.</summary>
    private const int LaserWidthColumns = 3;

    /// <summary>RRF.ASM <c>XMAX</c>: the rightmost column an object's right edge may reach.</summary>
    private const int PlayfieldMaxColumn = 0x8F;

    /// <summary>RRF.ASM <c>XMIN</c>: the leftmost column an object may occupy (the inside of the left wall).</summary>
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

    /// <summary>Starts a script: creates its object and its process (the ROM's `OSTART`).</summary>
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

    /// <summary>
    ///     The ROM's `RIGFIR`/`LEFFIR`: a bolt two columns ahead of the muzzle (right)
    ///     or one column behind it (left), six rows down, moving ±$0280 a frame, and
    ///     living the LFIRE/RFIRE operand's frame count.
    /// </summary>
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
