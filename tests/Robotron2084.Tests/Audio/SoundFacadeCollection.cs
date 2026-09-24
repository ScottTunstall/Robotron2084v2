using Robotron2084.Audio;
using Xunit;

namespace Robotron2084.Tests.Audio;

/// <summary>
/// The <see cref="Sound"/> facade's master switch (notes §68): the port's only sink
/// is the STUB square-wave beeper, so the beeping is OFF by default and a flag (or
/// the <c>ROBOTRON2084_SOUND=1</c> environment variable) turns it back on.
/// </summary>
/// <remarks>
/// DisableParallelization: these tests flip a PROCESS-WIDE switch, so they must not
/// overlap another collection that plays a sound.
/// </remarks>
[CollectionDefinition("SoundFacade", DisableParallelization = true)]
public sealed class SoundFacadeCollection
{
}
