using BotRunner.Clients;
using BotRunner.Movement;
using GameData.Core.Constants;
using GameData.Core.Enums;
using GameData.Core.Interfaces;

namespace BotRunner.Helpers;

/// <summary>
/// Centralises NavigationPath construction so every call site uses the same
/// capsule-dimension lookup, nearby-object provider and stuck-recovery wiring.
/// </summary>
public static class NavigationPathFactory
{
    /// <summary>
    /// Create a standard NavigationPath with probe heuristics enabled.
    /// </summary>
    public static NavigationPath Create(
        PathfindingClient client,
        IWoWLocalPlayer? player,
        IObjectManager objectManager)
    {
        var (radius, height) = player != null
            ? RaceDimensions.GetCapsuleForRace(player.Race, player.Gender)
            : (0.3064f, 2.0313f);
        return new NavigationPath(client,
            capsuleRadius: radius,
            capsuleHeight: height,
            nearbyObjectProvider: (start, end) => PathfindingOverlayBuilder.BuildNearbyObjects(objectManager, start, end),
            stuckRecoveryGenerationProvider: () => objectManager.MovementStuckRecoveryGeneration,
            race: player?.Race ?? 0,
            gender: player?.Gender ?? 0,
            supportsNativeLocalPhysicsQueries: LocalPhysicsSupport.SupportsReliableQueries(objectManager));
    }

    /// <summary>
    /// Create a NavigationPath tuned for ghost / corpse-run movement
    /// (probe heuristics, dynamic probe skipping, and strict validation all disabled).
    /// </summary>
    public static NavigationPath CreateForCorpseRun(
        PathfindingClient client,
        IWoWLocalPlayer? player,
        IObjectManager objectManager)
    {
        var (radius, height) = player != null
            ? RaceDimensions.GetCapsuleForRace(player.Race, player.Gender)
            : (0.3064f, 2.0313f);
        return new NavigationPath(client,
            enableProbeHeuristics: false,
            enableDynamicProbeSkipping: false,
            strictPathValidation: false,
            capsuleRadius: radius,
            capsuleHeight: height,
            nearbyObjectProvider: (start, end) => PathfindingOverlayBuilder.BuildNearbyObjects(objectManager, start, end),
            stuckRecoveryGenerationProvider: () => objectManager.MovementStuckRecoveryGeneration,
            race: player?.Race ?? 0,
            gender: player?.Gender ?? 0,
            supportsNativeLocalPhysicsQueries: LocalPhysicsSupport.SupportsReliableQueries(objectManager));
    }
}
