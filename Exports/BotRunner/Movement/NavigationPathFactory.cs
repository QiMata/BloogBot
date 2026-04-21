using BotRunner.Clients;
using GameData.Core.Constants;
using GameData.Core.Enums;
using GameData.Core.Interfaces;
using System;

namespace BotRunner.Movement;

public enum NavigationRoutePolicy
{
    Standard = 0,
    CorpseRun = 1,
}

public readonly record struct NavigationMovementCapabilities(
    Race Race,
    Gender Gender,
    float CapsuleRadius,
    float CapsuleHeight)
{
    public static NavigationMovementCapabilities Resolve(IWoWLocalPlayer? player)
    {
        if (player == null)
            return new(0, 0, 0.306f, 2.0313f);

        var (capsuleRadius, capsuleHeight) = RaceDimensions.GetCapsuleForRace(player.Race, player.Gender);
        return new(player.Race, player.Gender, capsuleRadius, capsuleHeight);
    }
}

public readonly record struct NavigationRoutePolicySettings(
    bool EnableProbeHeuristics,
    bool EnableDynamicProbeSkipping,
    bool StrictPathValidation)
{
    public static NavigationRoutePolicySettings Resolve(NavigationRoutePolicy routePolicy)
    {
        return routePolicy switch
        {
            NavigationRoutePolicy.CorpseRun => new(false, false, false),
            _ => new(true, true, false),
        };
    }
}

public static class NavigationPathFactory
{
    public static NavigationPath Create(
        PathfindingClient? pathfindingClient,
        IObjectManager objectManager,
        NavigationRoutePolicy routePolicy = NavigationRoutePolicy.Standard,
        Func<int>? stuckRecoveryGenerationProvider = null)
    {
        ArgumentNullException.ThrowIfNull(objectManager);

        var capabilities = NavigationMovementCapabilities.Resolve(objectManager.Player);
        var policy = NavigationRoutePolicySettings.Resolve(routePolicy);

        return new NavigationPath(
            pathfindingClient,
            enableProbeHeuristics: policy.EnableProbeHeuristics,
            enableDynamicProbeSkipping: policy.EnableDynamicProbeSkipping,
            strictPathValidation: policy.StrictPathValidation,
            capsuleRadius: capabilities.CapsuleRadius,
            capsuleHeight: capabilities.CapsuleHeight,
            nearbyObjectProvider: (start, end) => PathfindingOverlayBuilder.BuildNearbyObjects(objectManager, start, end),
            stuckRecoveryGenerationProvider: stuckRecoveryGenerationProvider ?? (() => objectManager.MovementStuckRecoveryGeneration),
            race: capabilities.Race,
            gender: capabilities.Gender);
    }
}
