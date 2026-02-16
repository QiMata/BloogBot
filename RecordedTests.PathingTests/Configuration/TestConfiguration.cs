using RecordedTests.Shared.Abstractions;
using System;
using System.Collections.Generic;

namespace RecordedTests.PathingTests.Configuration;

/// <summary>
/// Configuration for pathfinding test execution, supporting multiple configuration sources
/// (CLI arguments, environment variables, appsettings.json, defaults).
/// </summary>
public class TestConfiguration
{
    // ============================================================================
    // Server Configuration
    // ============================================================================

    /// <summary>
    /// Server connection information.
    /// </summary>
    public ServerInfo ServerInfo { get; set; } = new ServerInfo("127.0.0.1", 3724, null);

    /// <summary>
    /// TrueNAS API URL for server management.
    /// </summary>
    public string? TrueNasApiUrl { get; set; }

    /// <summary>
    /// TrueNAS API key for authentication.
    /// </summary>
    public string? TrueNasApiKey { get; set; }

    /// <summary>
    /// Timeout for waiting for server availability.
    /// </summary>
    public TimeSpan ServerTimeout { get; set; } = TimeSpan.FromMinutes(10);

    // ============================================================================
    // Account Configuration
    // ============================================================================

    /// <summary>
    /// GM account username.
    /// </summary>
    public string GmAccount { get; set; } = string.Empty;

    /// <summary>
    /// GM account password.
    /// </summary>
    public string GmPassword { get; set; } = string.Empty;

    /// <summary>
    /// GM character name.
    /// </summary>
    public string GmCharacter { get; set; } = string.Empty;

    /// <summary>
    /// Test account username.
    /// </summary>
    public string TestAccount { get; set; } = string.Empty;

    /// <summary>
    /// Test account password.
    /// </summary>
    public string TestPassword { get; set; } = string.Empty;

    /// <summary>
    /// Test character name.
    /// </summary>
    public string TestCharacter { get; set; } = string.Empty;

    // ============================================================================
    // PathfindingService Configuration
    // ============================================================================

    /// <summary>
    /// PathfindingService IP address.
    /// </summary>
    public string PathfindingServiceIp { get; set; } = "127.0.0.1";

    /// <summary>
    /// PathfindingService port.
    /// </summary>
    public int PathfindingServicePort { get; set; } = 5000;

    /// <summary>
    /// Whether to start PathfindingService in-process.
    /// When true, the test harness will host PathfindingService internally
    /// instead of requiring an external process.
    /// </summary>
    public bool StartPathfindingServiceInProcess { get; set; } = true;

    // ============================================================================
    // OBS Configuration
    // ============================================================================

    /// <summary>
    /// Path to OBS executable for auto-launch.
    /// </summary>
    public string? ObsExecutablePath { get; set; }

    /// <summary>
    /// OBS WebSocket URL.
    /// </summary>
    public string ObsWebSocketUrl { get; set; } = "ws://localhost:4455";

    /// <summary>
    /// OBS WebSocket password.
    /// </summary>
    public string? ObsWebSocketPassword { get; set; }

    /// <summary>
    /// OBS recording output path.
    /// </summary>
    public string? ObsRecordingPath { get; set; }

    /// <summary>
    /// Whether to auto-launch OBS if not running.
    /// </summary>
    public bool ObsAutoLaunch { get; set; } = false;

    // ============================================================================
    // Recording Target Configuration
    // ============================================================================

    /// <summary>
    /// WoW window title for recording target.
    /// </summary>
    public string? WowWindowTitle { get; set; }

    /// <summary>
    /// WoW process ID for recording target.
    /// </summary>
    public int? WowProcessId { get; set; }

    /// <summary>
    /// WoW window handle for recording target.
    /// </summary>
    public IntPtr? WowWindowHandle { get; set; }

    // ============================================================================
    // Test Execution Configuration
    // ============================================================================

    /// <summary>
    /// Filter tests by name (substring match, case-insensitive).
    /// </summary>
    public string? TestFilter { get; set; }

    /// <summary>
    /// Filter tests by category (exact match, case-insensitive).
    /// </summary>
    public string? CategoryFilter { get; set; }

    /// <summary>
    /// Whether to stop execution after the first test failure.
    /// </summary>
    public bool StopOnFirstFailure { get; set; } = false;

    /// <summary>
    /// Root directory for test artifacts (recordings, logs).
    /// </summary>
    public string ArtifactsRoot { get; set; } = "./TestLogs";

    /// <summary>
    /// Whether to enable OBS recording for tests.
    /// </summary>
    public bool EnableRecording { get; set; } = true;

    // ============================================================================
    // Computed Properties
    // ============================================================================

    /// <summary>
    /// Gets the orchestration options based on this configuration.
    /// </summary>
    public OrchestrationOptions OrchestrationOptions => new()
    {
        ArtifactsRootDirectory = ArtifactsRoot,
        ServerAvailabilityTimeout = ServerTimeout,
        DoubleStopRecorderForSafety = true
    };

    /// <summary>
    /// Validates the configuration and throws if required values are missing.
    /// </summary>
    public void Validate()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(GmAccount))
            errors.Add("GM account is required (set WWOW_GM_ACCOUNT or --gm-account)");

        if (string.IsNullOrWhiteSpace(GmPassword))
            errors.Add("GM password is required (set WWOW_GM_PASSWORD or --gm-password)");

        if (string.IsNullOrWhiteSpace(GmCharacter))
            errors.Add("GM character is required (set WWOW_GM_CHARACTER or --gm-character)");

        if (string.IsNullOrWhiteSpace(TestAccount))
            errors.Add("Test account is required (set WWOW_TEST_ACCOUNT or --test-account)");

        if (string.IsNullOrWhiteSpace(TestPassword))
            errors.Add("Test password is required (set WWOW_TEST_PASSWORD or --test-password)");

        if (string.IsNullOrWhiteSpace(TestCharacter))
            errors.Add("Test character is required (set WWOW_TEST_CHARACTER or --test-character)");

        if (EnableRecording && string.IsNullOrWhiteSpace(WowWindowTitle) && WowProcessId == null && WowWindowHandle == null)
            errors.Add("Recording target is required when recording is enabled (set WWOW_WOW_WINDOW_TITLE, WWOW_WOW_PROCESS_ID, or WWOW_WOW_WINDOW_HANDLE)");

        // OBS configuration validation when recording is enabled
        if (EnableRecording)
        {
            if (string.IsNullOrWhiteSpace(ObsExecutablePath))
                errors.Add("OBS_EXECUTABLE_PATH is required when recording is enabled");
            if (string.IsNullOrWhiteSpace(ObsWebSocketUrl))
                errors.Add("OBS_WEBSOCKET_URL is required when recording is enabled");
            if (string.IsNullOrWhiteSpace(ObsRecordingPath))
                errors.Add("OBS_RECORDING_PATH is required when recording is enabled");
            // Note: ObsWebSocketPassword can be null/empty - OBS may have no password configured
        }

        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                "Configuration validation failed:\n  - " + string.Join("\n  - ", errors));
        }
    }
}
