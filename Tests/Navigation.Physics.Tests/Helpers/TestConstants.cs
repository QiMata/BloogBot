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

    /// <summary>Forward/Backward/StrafeLeft/StrafeRight combined.</summary>
    public const uint DirectionalMask = 0x0000000F;

    /// <summary>Airborne mask: JUMPING | FALLING_FAR.</summary>
    public const uint AirborneMask = 0x00006000;

    /// <summary>Movement mode flags that cause discontinuities when they change between frames.</summary>
    public const uint MovementModeTransitionMask = 0x04606000;
}

/// <summary>
/// Recording filenames for the 2026-02-08 session (Orc Female, 60 FPS).
/// All tests reference these constants instead of duplicating filename strings.
/// </summary>
public static class Recordings
{
    // Orgrimmar � urban terrain with slopes and jumps
    public const string OrgFlatRunForward = "Dralrahgra_Orgrimmar_2026-02-08_11-32-13";
    public const string OrgStandingJump = "Dralrahgra_Orgrimmar_2026-02-08_11-31-46";
    public const string OrgRunningJumps = "Dralrahgra_Orgrimmar_2026-02-08_11-01-15";
    public const string OrgFallFromHeight = "Dralrahgra_Orgrimmar_2026-02-08_11-32-44";
    public const string OrgFallFromHeight2 = "Dralrahgra_Orgrimmar_2026-02-08_11-32-24";

    // Durotar � open terrain, mixed movement
    public const string DurotarMixedMovement = "Dralrahgra_Durotar_2026-02-08_11-06-59";
    public const string DurotarDiagonalStrafe = "Dralrahgra_Durotar_2026-02-08_11-24-45";
    public const string DurotarLongFlatRun = "Dralrahgra_Durotar_2026-02-08_11-37-56";

    // Undercity � indoor, falling, complex geometry
    public const string UndercityMixed = "Dralrahgra_Undercity_2026-02-08_11-30-52";

    // Swimming � populate after recording session
    // Record via: /say rec swim_forward ? swim ? /say rec
    // Or use BLOOGBOT_AUTOMATED_RECORDING=1 (scenario 08_swim_forward)
    public const string Swimming = "Dralrahgra_Durotar_2026-02-09_19-16-08";

    // Swimming sub-categories — all captured in the main Swimming recording
    public const string SwimForward = "Dralrahgra_Durotar_2026-02-09_19-16-08";
    public const string SwimBackward = "";   // no dedicated backward recording yet
    public const string SwimAscend = "Dralrahgra_Durotar_2026-02-09_19-16-08";
    public const string SwimDescend = "Dralrahgra_Durotar_2026-02-09_19-16-08";
    public const string WaterEntry = "Dralrahgra_Durotar_2026-02-09_19-16-08";
    public const string WaterExit = "Dralrahgra_Durotar_2026-02-09_19-16-08";
}

/// <summary>
/// Common tolerances for recording-based tests.
/// </summary>
public static class Tolerances
{
    // --- Average position error (per-frame, 3D) ---
    // Calibrated 2026-02-09 after air-snap-margin fix (0.5y instead of 4.0y STEP_DOWN_HEIGHT).
    public const float Position = 0.05f;         // yards - strict steady-state frame-by-frame
    public const float GroundMovement = 0.08f;   // yards - flat run, strafe, swim (worst avg: 0.054y FlatRunForward)
    public const float Airborne = 0.07f;         // yards - falls (worst avg: 0.044y FallFromHeight)
    public const float MixedMovement = 0.08f;    // yards - turns, mode transitions (worst avg: 0.057y RunningJumps)

    // --- P99 position error (99th percentile, single-frame) ---
    public const float P99Ground = 0.25f;        // yards - 99% of ground frames (worst P99: 0.167y SwimForward)
    public const float P99Airborne = 0.55f;      // yards - 99% of airborne frames (worst P99: 0.375y FallFromHeight)
    public const float P99Mixed = 0.30f;         // yards - 99% of mixed frames (worst P99: 0.184y ComplexMixed)

    // --- Speed/velocity ---
    public const float Velocity = 0.5f;         // yards/second

    // Recording-level measurement tolerance (client-side variation, not engine precision)
    public const float RelaxedPosition = 0.15f;
}
