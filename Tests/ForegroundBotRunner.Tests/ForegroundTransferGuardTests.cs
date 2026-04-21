using ForegroundBotRunner.Objects;
using GameData.Core.Enums;
using GameData.Core.Models;

namespace ForegroundBotRunner.Tests;

public sealed class ForegroundBotWorkerTransitionGuardTests
{
    [Theory]
    [InlineData(true, 1u, true)]
    [InlineData(false, 0xFFFFFFFFu, true)]
    [InlineData(false, 0xFFu, true)]
    [InlineData(false, 0u, false)]
    public void IsWorkerInTransition_FollowsMapTransitionAndSentinelMapIds(
        bool isInMapTransition,
        uint continentId,
        bool expected)
    {
        bool result = ForegroundBotWorker.IsWorkerInTransition(isInMapTransition, continentId);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(false, WoWScreenState.LoginScreen, true)]
    [InlineData(false, WoWScreenState.InWorld, true)]
    [InlineData(false, WoWScreenState.LoadingWorld, false)]
    [InlineData(true, WoWScreenState.InWorld, false)]
    public void ShouldPollWorkerLuaDiagnostics_SkipsLoadingAndTransitions(
        bool isInMapTransition,
        WoWScreenState screenState,
        bool expected)
    {
        bool result = ForegroundBotWorker.ShouldPollWorkerLuaDiagnostics(isInMapTransition, screenState);

        Assert.Equal(expected, result);
    }
}

public sealed class MovementRecorderTransitionGuardTests
{
    [Theory]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(false, false, false)]
    public void ShouldReinstallChatHookAfterTransition_OnlyWhenTransitionClears(
        bool wasInTransition,
        bool isInMapTransition,
        bool expected)
    {
        bool result = MovementRecorder.ShouldReinstallChatHookAfterTransition(wasInTransition, isInMapTransition);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(false, false, true)]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    public void ShouldPauseForCurrentState_PausesWithoutPlayerOrDuringTransfer(
        bool hasPlayer,
        bool isInMapTransition,
        bool expected)
    {
        bool result = MovementRecorder.ShouldPauseForCurrentState(hasPlayer, isInMapTransition);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(false, false, false)]
    public void ShouldCaptureFrame_RequiresWorldAndStableMapState(
        bool isInWorld,
        bool isInMapTransition,
        bool expected)
    {
        bool result = MovementRecorder.ShouldCaptureFrame(isInWorld, isInMapTransition);

        Assert.Equal(expected, result);
    }
}

public sealed class LocalPlayerGhostStateTests
{
    [Fact]
    public void EvaluateGhostFormState_UsesDescriptorGhostFlagFirst()
    {
        bool result = LocalPlayer.EvaluateGhostFormState(
            health: 100,
            playerFlags: 0x10,
            bytes1: [0u],
            memoryGhostFlag: 0,
            luaGhostValue: null,
            corpsePos: new Position(0, 0, 0));

        Assert.True(result);
    }

    [Fact]
    public void EvaluateGhostFormState_DoesNotTreatCorpseStateAsGhost()
    {
        bool result = LocalPlayer.EvaluateGhostFormState(
            health: 0,
            playerFlags: 0,
            bytes1: [0u],
            memoryGhostFlag: 1,
            luaGhostValue: "1",
            corpsePos: new Position(10, 20, 30));

        Assert.False(result);
    }

    [Fact]
    public void EvaluateGhostFormState_UsesMemoryGhostFlagWhenAlive()
    {
        bool result = LocalPlayer.EvaluateGhostFormState(
            health: 50,
            playerFlags: 0,
            bytes1: [0u],
            memoryGhostFlag: 1,
            luaGhostValue: null,
            corpsePos: new Position(0, 0, 0));

        Assert.True(result);
    }

    [Fact]
    public void EvaluateGhostFormState_UsesLuaWhenDescriptorAndMemoryAreSilent()
    {
        bool result = LocalPlayer.EvaluateGhostFormState(
            health: 50,
            playerFlags: 0,
            bytes1: [0u],
            memoryGhostFlag: 0,
            luaGhostValue: "1",
            corpsePos: new Position(0, 0, 0));

        Assert.True(result);
    }

    [Fact]
    public void EvaluateGhostFormState_FallsBackToCorpsePositionHeuristic()
    {
        bool result = LocalPlayer.EvaluateGhostFormState(
            health: 1,
            playerFlags: 0,
            bytes1: [0u],
            memoryGhostFlag: 0,
            luaGhostValue: null,
            corpsePos: new Position(5, 0, 0));

        Assert.True(result);
    }
}
