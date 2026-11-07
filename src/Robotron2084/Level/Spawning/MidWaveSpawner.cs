using Robotron2084.Audio;
using Robotron2084.Core;
using Robotron2084.Entities;
using Robotron2084.Tuning;

namespace Robotron2084.Level.Spawning;

/// <summary>
///     Makes the things that robots make during a wave: a brain's missile, a spheroid's enforcer, a tank's shell and
///     so on.
/// </summary>
/// <remarks>
///     The wave-start spawners (<see cref="IWaveSpawner" />) fill the field once. These are asked for by the robots
///     themselves,
///     one at a time, and each plays the sound the ROM asks for when it is made.
/// </remarks>
internal sealed class MidWaveSpawner
{
    private readonly FieldEntities _fieldEntities;
    private readonly PlayField _playField;
    private readonly Random _random;

    /// <summary>Makes a spawner for one field.</summary>
    /// <param name="playField">The field the things are made on, which also plays their sounds.</param>
    /// <param name="fieldEntities">What is on the field, which the things are added to.</param>
    /// <param name="random">The field's random source.</param>
    public MidWaveSpawner(PlayField playField, FieldEntities fieldEntities, Random random)
    {
        _playField = playField;
        _fieldEntities = fieldEntities;
        _random = random;
    }

    /// <summary>A brain fires a cruise missile at the player.</summary>
    /// <param name="origin">Where the missile starts.</param>
    /// <remarks>Original source: <c>RRB10.ASM</c> <c>BRNSHT</c>, which asks for <c>BSHSND</c>.</remarks>
    public void SpawnCruiseMissile(IntVector2 origin)
    {
        var cruiseMissile = new CruiseMissile(_playField.TextureSet, origin, _playField.GetPlayerPosition(), _random);
        _fieldEntities.Add(cruiseMissile);
        _playField.PlaySoundFrom(SoundTables.BrainShoot, cruiseMissile.GetBounds());
    }

    /// <summary>A spheroid drops an enforcer.</summary>
    /// <param name="position">Where the enforcer grows.</param>
    /// <remarks>Original source: <c>RRC11.ASM</c>, which asks for <c>ENDSND</c>.</remarks>
    public void SpawnEnforcer(IntVector2 position)
    {
        var enforcer = new Enforcer(_playField.TextureSet, position, _random, _playField.WaveParameters.EnforcerFireDelay);
        _fieldEntities.Add(enforcer);
        _playField.PlaySoundFrom(SoundTables.EnforcerDropOff, enforcer.GetBounds());
    }

    /// <summary>
    ///     Gorf drops a grunt: it starts where Gorf is and falls to the ground, with no appear effect. A wave holds only
    ///     so many.
    /// </summary>
    /// <param name="from">Where the grunt starts, which is inside Gorf.</param>
    /// <param name="landing">Where the grunt ends up standing.</param>
    /// <remarks>
    ///     A new robot's drop (notes §138.2): there is no arcade routine for it. The grunt is kept inside the playfield, and
    ///     is not dropped at all when the wave
    ///     already holds as many grunts as it is allowed: the wave's own number (which the difficulty moves), or
    ///     <see cref="GorfTuning.MinGruntCount" /> if that is fewer, or the number Gorf is to drop in all when that is more.
    /// </remarks>
    public void SpawnGrunt(IntVector2 from, IntVector2 landing)
    {
        var waveParameters = _playField.WaveParameters;
        var gruntLimit = Math.Max(waveParameters.GruntCount, GorfTuning.MinGruntCount);
        gruntLimit = Math.Max(gruntLimit, waveParameters.GorfGruntDropCount);
        if (_fieldEntities.Grunts.Count(entity => !entity.IsDead()) >= gruntLimit) return;

        var bounds = _playField.GetPlayfieldBounds();
        var width = ScreenSize.ToPortPixelsFromArcadePixels(CollisionSizes.GruntCollisionSize.Width);
        var height = ScreenSize.ToPortPixelsFromArcadePixels(CollisionSizes.GruntCollisionSize.Height);
        var x = Math.Clamp(from.X, bounds.X, bounds.Right - width);
        var startY = Math.Clamp(from.Y, bounds.Y, bounds.Bottom - height);
        var landingY = Math.Clamp(landing.Y, startY, bounds.Bottom - height);
        var grunt = new Grunt(_playField.TextureSet, new IntVector2(x, startY), _random, _playField.WaveParameters.GruntMoveDelay);
        grunt.BeginFall(landingY);
        _fieldEntities.Add(grunt);
    }

    /// <summary>A brain's touch turns a family member into a prog where they stand.</summary>
    /// <param name="position">Where the family member stood.</param>
    /// <param name="kind">Which family member it was, since the prog keeps their look.</param>
    /// <remarks>Original source: <c>RRB10.ASM</c> <c>BMUT</c> and <c>PROGST</c>.</remarks>
    public void SpawnProg(IntVector2 position, FamilyMemberKind kind)
    {
        _fieldEntities.Add(new Prog(_playField.TextureSet, position, kind, _random));
    }

    /// <summary>An enforcer fires a spark at the player.</summary>
    /// <param name="origin">Where the spark starts.</param>
    /// <param name="playerPosition">Where the player is, which the spark is aimed at.</param>
    /// <remarks>
    ///     Original source: <c>RRC11.ASM</c> <c>ENFSHT</c>, which asks for <c>ENFSND</c>. The wall's edges are passed on for
    ///     its
    ///     rule that there is no sideways wobble when the player is near the left wall.
    /// </remarks>
    public void SpawnSpark(IntVector2 origin, IntVector2 playerPosition)
    {
        var spark = new Spark(_playField.TextureSet, origin, playerPosition, _random, _playField.GetPlayfieldBounds());
        _fieldEntities.Add(spark);
        _playField.PlaySoundFrom(SoundTables.EnforcerShoot, spark.GetBounds());
    }

    /// <summary>A quark drops a tank. The tank is kept inside the playfield, because the quark can be against a wall.</summary>
    /// <param name="position">Where the quark is.</param>
    /// <returns>The new tank.</returns>
    /// <remarks>Original source: <c>RRTK4.ASM</c>, which asks for <c>TKDSND</c>.</remarks>
    public Tank SpawnTank(IntVector2 position)
    {
        Tank tank = new(_playField.TextureSet, Tank.GetPositionInside(_playField.GetPlayfieldBounds(), position), _random,
            _playField.WaveParameters.TankFireDelay);
        _fieldEntities.Add(tank);
        _playField.PlaySoundFrom(SoundTables.TankDrop, tank.GetBounds());
        return tank;
    }

    /// <summary>A tank fires a shell at, or at a wall near, the player.</summary>
    /// <param name="origin">The tank's top-left corner.</param>
    /// <remarks>
    ///     Original source: <c>RRTK4.ASM</c> <c>TNKFIR</c>, which asks for <c>TKFSND</c>. The wave's count of shells goes up
    ///     here, and only a laser hit brings it down (the fizzle bug, notes §53).
    /// </remarks>
    public void SpawnTankShell(IntVector2 origin)
    {
        _playField.CountShellFired();
        var tankShell = new TankShell(_playField.TextureSet, origin, _playField.GetPlayerPosition(), _playField.WaveParameters.ShellSpeed,
            _playField.GetPlayfieldBounds(), _random);
        _fieldEntities.Add(tankShell);
        _playField.PlaySoundFrom(SoundTables.TankFire, tankShell.GetBounds());
    }
}
