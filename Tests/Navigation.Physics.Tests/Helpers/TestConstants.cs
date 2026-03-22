namespace Navigation.Physics.Tests.Helpers;

/// <summary>
/// Movement flag constants from WoW 1.12.1 client.
/// Shared across all recording-based tests.
/// </summary>
public static class MoveFlags
{
    public const uint Forward = 0x00000001;
    public const uint Backward = 0x00000002;
    public const uint StrafeLeft = 0x00000004;
    public const uint StrafeRight = 0x00000008;
    public const uint TurnLeft = 0x00000010;
    public const uint TurnRight = 0x00000020;
    public const uint Jumping = 0x00002000;
    public const uint FallingFar = 0x00004000;
    public const uint Swimming = 0x00200000;
    public const uint SplineEnabled = 0x00400000;
    public const uint SplineElevation = 0x04000000;
    public const uint TeleportToPlane = 0x08000000;

    /// <summary>Forward/Backward/StrafeLeft/StrafeRight combined.</summary>
    public const uint DirectionalMask = 0x0000000F;

    /// <summary>Airborne mask: JUMPING | FALLING_FAR.</summary>
    public const uint AirborneMask = 0x00006000;

    /// <summary>Movement mode flags that cause discontinuities when they change between frames.</summary>
    public const uint MovementModeTransitionMask = 0x04606000;
}

/// <summary>
/// Recording filenames for calibrated sessions (Orc Female, 60 FPS).
/// All tests reference these constants instead of duplicating filename strings.
/// Empty strings indicate recordings that need to be captured.
/// </summary>
public static class Recordings
{
    // Orgrimmar - urban terrain with slopes and jumps
    public const string OrgFlatRunForward = "Dralrahgra_Orgrimmar_2026-02-08_11-32-13";
    public const string OrgStandingJump = "Dralrahgra_Orgrimmar_2026-02-08_11-31-46";
    public const string OrgRunningJumps = "Dralrahgra_Orgrimmar_2026-02-08_11-01-15";
    public const string OrgFallFromHeight = "Dralrahgra_Orgrimmar_2026-02-08_11-32-44";
    public const string OrgFallFromHeight2 = "Dralrahgra_Orgrimmar_2026-02-08_11-32-24";
    public const string OrgCityLoop = "Dralrahgra_Orgrimmar_2026-02-12_20-20-05";       // ramps, speed curves, circular path
    public const string OrgCityClimb = "Dralrahgra_Orgrimmar_2026-02-12_20-20-38";      // lower → upper city, Z delta 41.6y

    // Durotar - open terrain
    public const string DurotarLongFlatRun = "Dralrahgra_Durotar_2026-02-08_11-37-56";
    public const string DurotarMixedMovement = "Dralrahgra_Durotar_2026-02-12_20-15-52"; // strafe all directions, backward, jumps on flat
    public const string DurotarDiagonalStrafe = "Dralrahgra_Durotar_2026-02-12_20-15-52"; // same recording covers diagonal strafe

    // Undercity - indoor, falling, complex geometry
    public const string UndercityMixed = "Dralrahgra_Undercity_2026-02-08_11-30-52";

    // Swimming - comprehensive: forward, backward, strafing, turning, 10+ water transitions
    public const string Swimming = "Dralrahgra_Durotar_2026-02-12_20-13-25";
    public const string SwimForward = "Dralrahgra_Durotar_2026-02-12_20-13-25";
    public const string SwimBackward = "Dralrahgra_Durotar_2026-02-12_20-13-25";
    public const string SwimAscend = "Dralrahgra_Durotar_2026-02-12_20-13-25";
    public const string SwimDescend = "Dralrahgra_Durotar_2026-02-12_20-13-25";
    public const string WaterEntry = "Dralrahgra_Durotar_2026-02-12_20-13-25";
    public const string WaterExit = "Dralrahgra_Durotar_2026-02-12_20-13-25";

    // Transport - Undercity elevator (both directions, with NearbyGameObjects captured)
    public const string UndercityElevatorDown = "Dralrahgra_Undercity_2026-02-12_19-29-23";  // top → bottom, 647 transport frames
    public const string UndercityElevatorUp = "Dralrahgra_Undercity_2026-02-12_20-14-05";    // bottom → top, 393 transport frames
    public const string UndercityElevator = "Dralrahgra_Undercity_2026-02-12_19-29-23";      // default: down ride

    // Undercity v2 - re-recorded 2026-02-13 with GoState tracking + door/elevator data
    // Bottom→top, 1754 frames, 29s, Z range -43→+60, 9 GOs (3 elevators + 6 doors)
    // Note: all doors remain goState=1 throughout — MaNGOS doesn't send door transitions
    public const string UndercityElevatorV2 = "Dralrahgra_Undercity_2026-02-13_19-26-54";
}

/// <summary>
/// Common tolerances for recording-based tests (SceneCache path).
///
/// SceneCache uses pre-processed world-space triangles with overlap-only collision.
/// This trades P99 precision for 240x faster startup (6ms vs 30-60s VMAP load).
///
/// Measured precision (SceneCache, overlap-only):
///   - Flat terrain:  avg=0.03-0.05y, SS P99=0.15-0.35y
///   - Jump/complex:  avg=0.07-0.11y, SS P99=1.1-1.6y (outliers at WMO transitions)
///   - Transport:     avg=0.13-0.26y, SS P99=0.9-1.5y (elevator drift)
///
/// The P99 outliers on jump recordings are concentrated at a few frames where
/// the capsule crosses WMO geometry boundaries (ramp edges, building transitions).
/// Average errors are excellent and well within what a bot needs.
/// </summary>
public static class Tolerances
{
    // --- SceneCache precision targets ---
    public const float AvgPosition = 0.13f;       // yards - avg position error (all recordings)
    public const float P99Position = 1.8f;         // yards - SS P99 (worst: RunningJumps 1.58)

    // --- Speed/velocity ---
    public const float Velocity = 0.5f;           // yards/second

    // --- Transport / elevator ---
    // Transport frames simulated via DynamicObjectRegistry with world-coord transform.
    // Elevator model mesh not in map data — position error dominated by vertical drift.
    public const float TransportAvg = 0.30f;       // yards - avg including transport frames
    public const float TransportP99 = 1.6f;        // yards - SS P99 (worst: ElevatorV2 1.51)

    // Recording-level measurement tolerance (client-side variation, not engine precision)
    public const float RelaxedPosition = 0.15f;

    // --- Aggregate drift gates (NPT-MISS-003) ---
    // Cross-recording thresholds for blocking regressions.
    // Clean frames = all frames excluding recording artifacts and SPLINE_ELEVATION transitions.
    public const float AggregateCleanAvg = 0.15f;       // yards - overall clean-frame avg
    public const float AggregateCleanP99 = 2.0f;        // yards - overall clean-frame P99
    public const float WorstCleanFrame = 5.0f;           // yards - single worst clean frame
}
