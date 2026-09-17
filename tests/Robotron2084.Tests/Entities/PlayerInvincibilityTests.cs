using Robotron2084.Core;
using Robotron2084.Entities;
using Robotron2084.Tuning;
using Xunit;

namespace Robotron2084.Tests;

/// <summary>
/// Round 7 playtest aid: the player is invincible for testing
/// (GameplayConstants.PlayerInvincibleForTesting) so whole waves can be
/// playtested; while the flag is on, Kill() must be a COMPLETE no-op
/// (state, lives, death timer all untouched). The flag is temporary —
/// when it flips to false, delete this file and re-verify contact deaths.
/// </summary>
public sealed class PlayerInvincibilityTests
{
    [Fact]
    public void Kill_IsANoOp_WhileThePlaytestFlag_IsOn()
    {
        Assert.True(GameplayConstants.PlayerInvincibleForTesting); // the flag must be on for this test to mean anything

        var player = new Player(new IntVector2(100, 100), 3);

        player.Kill();
        player.Kill();

        Assert.Equal(EntityLifeState.Alive, player.LifeState);
        Assert.Equal(3, player.Lives);
    }
}
