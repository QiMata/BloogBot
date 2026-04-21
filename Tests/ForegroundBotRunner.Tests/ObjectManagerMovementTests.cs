using ForegroundBotRunner.Statics;
using GameData.Core.Enums;
using GameData.Core.Interfaces;
using Moq;

namespace ForegroundBotRunner.Tests;

public sealed class ObjectManagerMovementTests
{
    [Fact]
    public void ExpandControlBits_SplitsDirectionalCombinationsIntoStableOrder()
    {
        var expanded = ObjectManager.ExpandControlBits(
            ControlBits.Front | ControlBits.StrafeRight | ControlBits.Left);

        Assert.Equal(
            [ControlBits.Front, ControlBits.Left, ControlBits.StrafeRight],
            expanded);
    }

    [Fact]
    public void ExpandControlBits_PreservesUnknownFlagsAfterDiscreteBits()
    {
        var expanded = ObjectManager.ExpandControlBits(
            ControlBits.Front | ControlBits.Turning);

        Assert.Equal(
            [ControlBits.Front, ControlBits.Turning],
            expanded);
    }

    [Fact]
    public void ExpandControlBits_Nothing_ReturnsEmpty()
    {
        Assert.Empty(ObjectManager.ExpandControlBits(ControlBits.Nothing));
    }

    [Fact]
    public void ShouldUseGhostForwardKeyInput_GhostForward_ReturnsTrue()
    {
        var player = new Mock<IWoWLocalPlayer>();
        player.SetupGet(p => p.PlayerFlags).Returns(PlayerFlags.PLAYER_FLAGS_GHOST);

        var useGhostInput = ObjectManager.ShouldUseGhostForwardKeyInput(player.Object, ControlBits.Front);

        Assert.True(useGhostInput);
    }

    [Fact]
    public void ShouldUseGhostForwardKeyInput_GhostWithoutForward_ReturnsFalse()
    {
        var player = new Mock<IWoWLocalPlayer>();
        player.SetupGet(p => p.PlayerFlags).Returns(PlayerFlags.PLAYER_FLAGS_GHOST);

        var useGhostInput = ObjectManager.ShouldUseGhostForwardKeyInput(player.Object, ControlBits.StrafeRight);

        Assert.False(useGhostInput);
    }

    [Fact]
    public void ShouldUseGhostForwardKeyInput_AliveForward_ReturnsFalse()
    {
        var player = new Mock<IWoWLocalPlayer>();
        player.SetupGet(p => p.PlayerFlags).Returns(0);

        var useGhostInput = ObjectManager.ShouldUseGhostForwardKeyInput(player.Object, ControlBits.Front);

        Assert.False(useGhostInput);
    }
}
