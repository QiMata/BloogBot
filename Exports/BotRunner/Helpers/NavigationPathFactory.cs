using BotRunner.Clients;
using BotRunner.Movement;
using GameData.Core.Interfaces;

namespace BotRunner.Helpers;

/// <summary>
/// Centralises NavigationPath construction so every call site uses the same
/// capsule-dimension lookup, optional nearby-object provider and stuck-recovery wiring.
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
        var capabilities = NavigationMovementCapabilities.Resolve(player);
        return new NavigationPath(client,
            capsuleRadius: capabilities.CapsuleRadius,
            capsuleHeight: capabilities.CapsuleHeight,
            nearbyObjectProvider: BotRunner.Movement.NavigationPathFactory.IsDynamicOverlayEnabled()
                ? (start, end) => PathfindingOverlayBuilder.BuildNearbyObjects(objectManager, start, end)
                : null,
            stuckRecoveryGenerationProvider: () => objectManager.MovementStuckRecoveryGeneration,
            race: capabilities.Race,
            gender: capabilities.Gender,
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
        var capabilities = NavigationMovementCapabilities.Resolve(player);
        return new NavigationPath(client,
            enableProbeHeuristics: false,
            enableDynamicProbeSkipping: false,
            strictPathValidation: false,
            capsuleRadius: capabilities.CapsuleRadius,
            capsuleHeight: capabilities.CapsuleHeight,
            nearbyObjectProvider: BotRunner.Movement.NavigationPathFactory.IsDynamicOverlayEnabled()
                ? (start, end) => PathfindingOverlayBuilder.BuildNearbyObjects(objectManager, start, end)
                : null,
            stuckRecoveryGenerationProvider: () => objectManager.MovementStuckRecoveryGeneration,
            race: capabilities.Race,
            gender: capabilities.Gender,
            supportsNativeLocalPhysicsQueries: LocalPhysicsSupport.SupportsReliableQueries(objectManager));
    }
}
