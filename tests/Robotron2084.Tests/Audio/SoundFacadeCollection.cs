using Robotron2084.Audio;
using Xunit;

namespace Robotron2084.Tests.Audio;

/// <summary>
///     The tests that set up the process-wide <see cref="Sound" /> service and flip its master switch.
/// </summary>
/// <remarks>
///     DisableParallelization: these tests change a PROCESS-WIDE service, so they must not overlap
///     another collection that plays a sound.
/// </remarks>
[CollectionDefinition("SoundFacade", DisableParallelization = true)]
public sealed class SoundFacadeCollection
{
}
