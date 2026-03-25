using ForegroundBotRunner;
using GameData.Core.Enums;

namespace ForegroundBotRunner.Tests;

public sealed class MovementScenarioRunnerTests
{
    [Fact]
    public void ParseScenarioSelection_EmptyInput_ReturnsEmptyArray()
    {
        Assert.Empty(MovementScenarioRunner.ParseScenarioSelection(null));
        Assert.Empty(MovementScenarioRunner.ParseScenarioSelection(""));
        Assert.Empty(MovementScenarioRunner.ParseScenarioSelection(" , ;  "));
    }

    [Fact]
    public void ParseScenarioSelection_SplitsTrimsAndDeduplicatesTokens()
    {
        var tokens = MovementScenarioRunner.ParseScenarioSelection(
            "13_undercity_lower_route, 14_undercity_elevator_west_up ; 13_undercity_lower_route");

        Assert.Equal(
            ["13_undercity_lower_route", "14_undercity_elevator_west_up"],
            tokens);
    }

    [Fact]
    public void ShouldRunScenario_EmptySelection_AllowsEverything()
    {
        bool actual = MovementScenarioRunner.ShouldRunScenario("08_swim_forward", []);

        Assert.True(actual);
    }

    [Theory]
    [InlineData("13_undercity_lower_route", "13_undercity", true)]
    [InlineData("14_undercity_elevator_west_up", "elevator_west", true)]
    [InlineData("08_swim_forward", "undercity", false)]
    public void ShouldRunScenario_UsesCaseInsensitiveContainsMatching(
        string scenarioName,
        string selectionToken,
        bool expected)
    {
        bool actual = MovementScenarioRunner.ShouldRunScenario(scenarioName, [selectionToken]);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void BuildMovementStopLua_EmitsStopCommandsForDirectionalBits()
    {
        string? lua = MovementScenarioRunner.BuildMovementStopLua(
            ControlBits.Front | ControlBits.StrafeRight | ControlBits.Left);

        Assert.Equal(
            "MoveForwardStop(); TurnLeftStop(); StrafeRightStop()",
            lua);
    }

    [Fact]
    public void BuildMovementStopLua_NoSupportedBits_ReturnsNull()
    {
        Assert.Null(MovementScenarioRunner.BuildMovementStopLua(ControlBits.Turning));
    }

    [Fact]
    public void BuildMovementStartLua_EmitsStartCommandsForDirectionalBits()
    {
        string? lua = MovementScenarioRunner.BuildMovementStartLua(
            ControlBits.Front | ControlBits.StrafeRight | ControlBits.Left);

        Assert.Equal(
            "MoveForwardStart(); TurnLeftStart(); StrafeRightStart()",
            lua);
    }

    [Fact]
    public void BuildMovementStartLua_NoSupportedBits_ReturnsNull()
    {
        Assert.Null(MovementScenarioRunner.BuildMovementStartLua(ControlBits.Turning));
    }
}
