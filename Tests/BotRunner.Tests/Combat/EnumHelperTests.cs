using GameData.Core.Enums;

namespace BotRunner.Tests.Combat;

public class EnumCustomAttributeHelperTests
{
    [Theory]
    [InlineData(Race.Human, "Human")]
    [InlineData(Race.Orc, "Orc")]
    [InlineData(Race.Dwarf, "Dwarf")]
    [InlineData(Race.NightElf, "Night Elf")]
    [InlineData(Race.Undead, "Undead")]
    [InlineData(Race.Tauren, "Tauren")]
    [InlineData(Race.Gnome, "Gnome")]
    [InlineData(Race.Troll, "Troll")]
    [InlineData(Race.None, "None")]
    public void GetDescription_ReturnsDescriptionAttribute(Race race, string expected)
    {
        Assert.Equal(expected, race.GetDescription());
    }

    [Fact]
    public void GetDescription_EnumWithoutAttribute_ReturnsNull()
    {
        // EffectType enum has no [Description] attributes
        var result = EffectType.Magic.GetDescription();
        Assert.Null(result);
    }

    [Theory]
    [InlineData(EffectType.None)]
    [InlineData(EffectType.Poison)]
    [InlineData(EffectType.Curse)]
    [InlineData(EffectType.Disease)]
    public void GetDescription_AllEffectTypes_ReturnNull(EffectType type)
    {
        Assert.Null(type.GetDescription());
    }

    [Fact]
    public void GetDescription_SpaceInDescription()
    {
        // "Night Elf" has a space — verifies multi-word descriptions
        Assert.Equal("Night Elf", Race.NightElf.GetDescription());
    }

    [Fact]
    public void GetDescription_InvalidEnumValue_ReturnsNull()
    {
        // Cast an invalid int to Race — no matching field, should return null
        var invalid = (Race)999;
        Assert.Null(invalid.GetDescription());
    }
}

public class ResolveNextWaypointTests
{
    [Fact]
    public void NullArray_ReturnsNull()
    {
        var result = BotRunnerService.ResolveNextWaypoint(null);
        Assert.Null(result);
    }

    [Fact]
    public void EmptyArray_ReturnsNull()
    {
        var result = BotRunnerService.ResolveNextWaypoint([]);
        Assert.Null(result);
    }

    [Fact]
    public void SingleWaypoint_ReturnsFirst()
    {
        var waypoint = new GameData.Core.Models.Position(100, 200, 300);
        var result = BotRunnerService.ResolveNextWaypoint([waypoint]);

        Assert.NotNull(result);
        Assert.Equal(100f, result!.X);
        Assert.Equal(200f, result.Y);
        Assert.Equal(300f, result.Z);
    }

    [Fact]
    public void TwoWaypoints_ReturnsSecond()
    {
        var wp0 = new GameData.Core.Models.Position(0, 0, 0);
        var wp1 = new GameData.Core.Models.Position(10, 20, 30);

        var result = BotRunnerService.ResolveNextWaypoint([wp0, wp1]);

        Assert.NotNull(result);
        Assert.Equal(10f, result!.X);
        Assert.Equal(20f, result.Y);
    }

    [Fact]
    public void MultipleWaypoints_ReturnsSecond()
    {
        var positions = new[]
        {
            new GameData.Core.Models.Position(0, 0, 0),
            new GameData.Core.Models.Position(10, 10, 10),
            new GameData.Core.Models.Position(20, 20, 20),
            new GameData.Core.Models.Position(30, 30, 30)
        };

        var result = BotRunnerService.ResolveNextWaypoint(positions);

        Assert.NotNull(result);
        Assert.Equal(10f, result!.X);
    }

    [Fact]
    public void LogAction_CalledForNullPath()
    {
        string? logMessage = null;
        BotRunnerService.ResolveNextWaypoint(null, msg => logMessage = msg);

        Assert.NotNull(logMessage);
        Assert.Contains("no waypoints", logMessage!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LogAction_CalledForEmptyPath()
    {
        string? logMessage = null;
        BotRunnerService.ResolveNextWaypoint([], msg => logMessage = msg);

        Assert.NotNull(logMessage);
        Assert.Contains("no waypoints", logMessage!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LogAction_CalledForSingleWaypoint()
    {
        string? logMessage = null;
        var wp = new GameData.Core.Models.Position(1, 2, 3);
        BotRunnerService.ResolveNextWaypoint([wp], msg => logMessage = msg);

        Assert.NotNull(logMessage);
        Assert.Contains("single waypoint", logMessage!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LogAction_Null_DoesNotThrow()
    {
        // Passing null logAction should not throw
        var result = BotRunnerService.ResolveNextWaypoint(null, null);
        Assert.Null(result);
    }
}
