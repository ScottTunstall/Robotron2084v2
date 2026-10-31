using Robotron2084.Audio;
using Robotron2084.Entities;
using Robotron2084.Tuning;

namespace Robotron2084.Level.Spawning;

/// <summary>
///     Puts the wave's brains on the field, not too near where the player starts, and plays the transporter sound if
///     there are any.
/// </summary>
/// <remarks>
///     <list type="bullet">
///         <item>Original source: <c>RRB10.ASM</c> <c>BRNSTV</c>; the sound is <c>RRG23.ASM</c>'s "BRAIN WAVE???" test</item>
///         <item>Disassembly: <c>INITIALISE_ALL_BRAINS</c> (<c>$1AF6</c>), the target picked at <c>$1B43</c></item>
///     </list>
///     A brain picks its target as it is made, and the brains are made before the family. So the search finds every place
///     empty and hands back the first one, which Mikey then fills: the arcade's own "all the brains chase Mikey" bug, kept
///     on
///     purpose (notes §18.8).
/// </remarks>
public sealed class BrainWaveSpawner : IWaveSpawner
{
    /// <inheritdoc />
    public void Spawn(WaveSpawnContext waveSpawnContext)
    {
        var playField = waveSpawnContext.PlayField;
        var fieldEntities = waveSpawnContext.FieldEntities;
        var placement = waveSpawnContext.SpawnPlacement;
        for (var i = 0; i < playField.WaveParameters.BrainCount; i++)
        {
            var position = placement.FindSpawnPointAwayFrom(waveSpawnContext.PlayerStart, SpawnTuning.HulkMinDistanceFromPlayer);
            var brain = new Brain(
                playField.TextureSet,
                position,
                waveSpawnContext.Random,
                playField.WaveParameters.BrainBeatWaitArcadeFrames,
                playField.WaveParameters.BrainFireDelay,
                fieldEntities.GetNearestFamilySlot(position));
            fieldEntities.Add(brain);
        }

        if (playField.WaveParameters.BrainCount > 0) Sound.PlayTransporter();
    }
}
