using BotRunner.Clients;
using BotRunner.Helpers;
using GameData.Core.Constants;
using GameData.Core.Enums;
using GameData.Core.Interfaces;
using System;

namespace BotRunner.Movement;

public enum NavigationRoutePolicy
{
    Standard = 0,
    CorpseRun = 1,
    LongTravel = 2,
}

public readonly record struct NavigationMovementCapabilities(
    Race Race,
    Gender Gender,
    float CapsuleRadius,
    float CapsuleHeight)
{
    private const float DefaultCapsuleRadius = 0.306f;
    private const float DefaultCapsuleHeight = 2.0313f;

    public static NavigationMovementCapabilities Resolve(IWoWLocalPlayer? player)
    {
        var race = player?.Race ?? Race.None;
        var gender = player?.Gender ?? Gender.Male;

        if (race == Race.None)
            race = ResolveConfiguredRace() ?? Race.None;

        if (player == null || player.Race == Race.None || gender == Gender.None)
            gender = ResolveConfiguredGender() ?? (gender == Gender.None ? Gender.Male : gender);

        if (race == Race.None)
            return new(Race.None, gender, DefaultCapsuleRadius, DefaultCapsuleHeight);

        var (capsuleRadius, capsuleHeight) = RaceDimensions.GetCapsuleForRace(race, gender);
        return new(race, gender, capsuleRadius, capsuleHeight);
    }

    private static Race? ResolveConfiguredRace()
    {
        var configured = Environment.GetEnvironmentVariable("WWOW_CHARACTER_RACE");
        if (string.IsNullOrWhiteSpace(configured))
            return null;

        configured = configured.Trim();
        if (Enum.TryParse<Race>(configured, ignoreCase: true, out var race) && race != Race.None)
            return race;

        return configured.ToUpperInvariant() switch
        {
            "HU" => Race.Human,
            "OR" => Race.Orc,
            "DW" => Race.Dwarf,
            "NE" => Race.NightElf,
            "UD" => Race.Undead,
            "TA" => Race.Tauren,
            "GN" => Race.Gnome,
            "TR" => Race.Troll,
            _ => null,
        };
    }

    private static Gender? ResolveConfiguredGender()
    {
        var configured = Environment.GetEnvironmentVariable("WWOW_CHARACTER_GENDER");
        return !string.IsNullOrWhiteSpace(configured)
            && Enum.TryParse<Gender>(configured.Trim(), ignoreCase: true, out var gender)
            && gender != Gender.None
            ? gender
            : null;
    }
}

public readonly record struct NavigationRoutePolicySettings(
    bool EnableProbeHeuristics,
    bool EnableDynamicProbeSkipping,
    bool StrictPathValidation,
    bool RequireVerticalWaypointArrival,
    bool PreferSmoothPath,
    bool AllowAlternatePathMode,
    bool ValidateLocalPhysicsSegments,
    bool TightenDenseWaypointAcceptance)
{
    public static NavigationRoutePolicySettings Resolve(NavigationRoutePolicy routePolicy)
    {
        return routePolicy switch
        {
            NavigationRoutePolicy.CorpseRun => new(false, false, false, false, false, true, false, false),
            NavigationRoutePolicy.LongTravel => new(false, false, false, true, true, false, true, true),
            _ => new(true, true, false, false, true, true, true, false),
        };
    }
}

public static class NavigationPathFactory
{
    public const string DynamicOverlayEnvironmentVariable = "WWOW_ENABLE_PATHFINDING_DYNAMIC_OVERLAY";
    public const string WaypointScreenshotCadenceEnvironmentVariable = "WWOW_NAV_SCREENSHOT_EVERY_N_WAYPOINTS";

    public static NavigationPath Create(
        PathfindingClient? pathfindingClient,
        IObjectManager objectManager,
        NavigationRoutePolicy routePolicy = NavigationRoutePolicy.Standard,
        Func<int>? stuckRecoveryGenerationProvider = null,
        Action<string>? diagnosticSink = null)
    {
        ArgumentNullException.ThrowIfNull(objectManager);

        var capabilities = NavigationMovementCapabilities.Resolve(objectManager.Player);
        var policy = NavigationRoutePolicySettings.Resolve(routePolicy);
        var cadence = ResolveWaypointDiagnosticCadence();

        return new NavigationPath(
            pathfindingClient,
            enableProbeHeuristics: policy.EnableProbeHeuristics,
            enableDynamicProbeSkipping: policy.EnableDynamicProbeSkipping,
            strictPathValidation: policy.StrictPathValidation,
            requireVerticalWaypointArrival: policy.RequireVerticalWaypointArrival,
            preferSmoothPath: policy.PreferSmoothPath,
            allowAlternatePathMode: policy.AllowAlternatePathMode,
            validateLocalPhysicsSegments: policy.ValidateLocalPhysicsSegments,
            capsuleRadius: capabilities.CapsuleRadius,
            capsuleHeight: capabilities.CapsuleHeight,
            nearbyObjectProvider: IsDynamicOverlayEnabled()
                ? (start, end) => PathfindingOverlayBuilder.BuildNearbyObjects(objectManager, start, end)
                : null,
            stuckRecoveryGenerationProvider: stuckRecoveryGenerationProvider ?? (() => objectManager.MovementStuckRecoveryGeneration),
            beforePathCalculation: routePolicy == NavigationRoutePolicy.LongTravel ? objectManager.StopAllMovement : null,
            race: capabilities.Race,
            gender: capabilities.Gender,
            supportsNativeLocalPhysicsQueries: LocalPhysicsSupport.SupportsReliableQueries(objectManager),
            tightenDenseWaypointAcceptance: policy.TightenDenseWaypointAcceptance,
            diagnosticSink: cadence > 0 ? diagnosticSink : null,
            waypointDiagnosticCadence: cadence);
    }

    public static bool IsDynamicOverlayEnabled()
    {
        var configured = Environment.GetEnvironmentVariable(DynamicOverlayEnvironmentVariable);
        return IsTruthy(configured);
    }

    /// <summary>
    /// Phase 5.3.6 cadence diagnostic (PFS-OVERHAUL-006). Reads
    /// `WWOW_NAV_SCREENSHOT_EVERY_N_WAYPOINTS`. Returns 0 (off) if unset, blank,
    /// or unparseable. Returns the parsed positive int otherwise. Default off
    /// preserves zero behavior change for production / legacy test runs.
    /// </summary>
    public static int ResolveWaypointDiagnosticCadence()
    {
        var configured = Environment.GetEnvironmentVariable(WaypointScreenshotCadenceEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(configured))
            return 0;
        if (int.TryParse(configured.Trim(), out var n) && n > 0)
            return n;
        return 0;
    }

    private static bool IsTruthy(string? configured)
        => !string.IsNullOrWhiteSpace(configured)
            && (string.Equals(configured.Trim(), "1", StringComparison.OrdinalIgnoreCase)
                || string.Equals(configured.Trim(), "true", StringComparison.OrdinalIgnoreCase)
                || string.Equals(configured.Trim(), "yes", StringComparison.OrdinalIgnoreCase)
                || string.Equals(configured.Trim(), "on", StringComparison.OrdinalIgnoreCase));
}
