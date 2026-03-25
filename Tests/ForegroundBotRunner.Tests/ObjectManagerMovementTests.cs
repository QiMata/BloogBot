using ForegroundBotRunner.Statics;
using GameData.Core.Enums;

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
}
