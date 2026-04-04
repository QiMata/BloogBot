using BotRunner.Combat;
using GameData.Core.Enums;
using Xunit;

namespace BotRunner.Tests.Combat;

public class HostilePlayerDetectorTests
{
    [Fact]
    public void IsPvPFlagged_TrueWhenFlagSet()
    {
        var mock = new Moq.Mock<GameData.Core.Interfaces.IWoWUnit>();
        mock.Setup(u => u.UnitFlags).Returns(UnitFlags.UNIT_FLAG_PVP);
        Assert.True(HostilePlayerDetector.IsPvPFlagged(mock.Object));
    }

    [Fact]
    public void IsPvPFlagged_FalseWhenNoFlag()
    {
        var mock = new Moq.Mock<GameData.Core.Interfaces.IWoWUnit>();
        mock.Setup(u => u.UnitFlags).Returns(UnitFlags.UNIT_FLAG_NONE);
        Assert.False(HostilePlayerDetector.IsPvPFlagged(mock.Object));
    }

    [Fact]
    public void IsCivilian_TrueWhenPassiveFlag()
    {
        var mock = new Moq.Mock<GameData.Core.Interfaces.IWoWUnit>();
        mock.Setup(u => u.UnitFlags).Returns(UnitFlags.UNIT_FLAG_PASSIVE);
        Assert.True(HostilePlayerDetector.IsCivilian(mock.Object));
    }

    [Fact]
    public void IsCivilian_FalseForCombatUnit()
    {
        var mock = new Moq.Mock<GameData.Core.Interfaces.IWoWUnit>();
        mock.Setup(u => u.UnitFlags).Returns(UnitFlags.UNIT_FLAG_IN_COMBAT);
        Assert.False(HostilePlayerDetector.IsCivilian(mock.Object));
    }
}
