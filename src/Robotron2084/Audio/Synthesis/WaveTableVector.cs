namespace Robotron2084.Audio.Synthesis;

/// <summary>
///     The settings for one wave table sound, as the sound board's program stores them: seven bytes, two of
///     which pack two settings each.
/// </summary>
/// <remarks>
///     Original source: <c>VSNDRM3.SRC</c>, the comment above <c>SVTAB</c> ("VECTOR FORMAT").
/// </remarks>
/// <param name="EchoesAndPlays">
///     Byte 0: how many echoes in the top four bits (<c>GECHO</c>), how many times the wave plays
///     at each pitch in the bottom four (<c>GCCNT</c>).
/// </param>
/// <param name="EchoDecayAndWave">
///     Byte 1: how much quieter each echo gets in the top four bits (<c>GECDEC</c>), which wave
///     in the bottom four.
/// </param>
/// <param name="PreDecay">Byte 2: how much quieter the wave is made before it first plays (<c>PRDECA</c>).</param>
/// <param name="PitchStep">
///     Byte 3: how far the pitch moves after the echoes, or 0 for not at all (<c>GDFINC</c>); above
///     127 it moves the other way.
/// </param>
/// <param name="PitchSteps">Byte 4: how many times the pitch moves (<c>GDCNT</c>).</param>
/// <param name="PatternLength">Byte 5: how many pitches the pattern has.</param>
/// <param name="PatternStart">Byte 6: where the pattern starts in <see cref="WaveTableData.Patterns" />.</param>
internal readonly record struct WaveTableVector(
    byte EchoesAndPlays,
    byte EchoDecayAndWave,
    byte PreDecay,
    byte PitchStep,
    byte PitchSteps,
    byte PatternLength,
    int PatternStart);
