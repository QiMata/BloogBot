using System;
using GameData.Core.Enums;

namespace ForegroundBotRunner.Tests;

public sealed class ForegroundBotWorkerWorldEntryCinematicTests
{
    [Fact]
    public void ShouldAttemptWorldEntryCinematicDismiss_True_WhenAllGuardsSatisfied()
    {
        var shouldAttempt = ForegroundBotWorker.ShouldAttemptWorldEntryCinematicDismiss(
            screenState: WoWScreenState.LoadingWorld,
            hasEnteredWorld: true,
            worldEntryHydrated: false,
            loadingWorldDuration: TimeSpan.FromSeconds(4),
            sinceLastAttempt: TimeSpan.FromSeconds(3));

        Assert.True(shouldAttempt);
    }

    [Fact]
    public void ShouldAttemptWorldEntryCinematicDismiss_False_WhenWorldAlreadyHydrated()
    {
        var shouldAttempt = ForegroundBotWorker.ShouldAttemptWorldEntryCinematicDismiss(
            screenState: WoWScreenState.LoadingWorld,
            hasEnteredWorld: true,
            worldEntryHydrated: true,
            loadingWorldDuration: TimeSpan.FromSeconds(10),
            sinceLastAttempt: TimeSpan.FromSeconds(10));

        Assert.False(shouldAttempt);
    }

    [Fact]
    public void ShouldAttemptWorldEntryCinematicDismiss_False_WhenStillWithinGraceWindow()
    {
        var shouldAttempt = ForegroundBotWorker.ShouldAttemptWorldEntryCinematicDismiss(
            screenState: WoWScreenState.LoadingWorld,
            hasEnteredWorld: true,
            worldEntryHydrated: false,
            loadingWorldDuration: TimeSpan.FromSeconds(2),
            sinceLastAttempt: TimeSpan.FromSeconds(10));

        Assert.False(shouldAttempt);
    }

    [Fact]
    public void ShouldAttemptWorldEntryCinematicDismiss_False_WhenRetryCooldownNotElapsed()
    {
        var shouldAttempt = ForegroundBotWorker.ShouldAttemptWorldEntryCinematicDismiss(
            screenState: WoWScreenState.LoadingWorld,
            hasEnteredWorld: true,
            worldEntryHydrated: false,
            loadingWorldDuration: TimeSpan.FromSeconds(10),
            sinceLastAttempt: TimeSpan.FromSeconds(1));

        Assert.False(shouldAttempt);
    }
}
