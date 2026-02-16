using Microsoft.Extensions.Configuration;
using RecordedTests.Shared.Abstractions;
using System;
using System.Collections.Generic;
using System.IO;

namespace RecordedTests.PathingTests.Configuration;

/// <summary>
/// Parses configuration from multiple sources with priority: CLI args > Environment variables > appsettings.json > Defaults.
/// </summary>
public static class ConfigurationParser
{
    /// <summary>
    /// Parses configuration from all available sources.
    /// </summary>
    /// <param name="args">Command-line arguments</param>
    /// <returns>Parsed configuration</returns>
    public static TestConfiguration Parse(string[] args)
    {
        // Build configuration from all sources
        // Priority (later overrides earlier): env → appsettings → CLI
        var configBuilder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddEnvironmentVariables()
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json", optional: true)
            .AddCommandLine(args, GetCommandLineMappings());

        var config = configBuilder.Build();

        // Map to TestConfiguration
        var testConfig = new TestConfiguration
        {
            // Server
            ServerInfo = ParseServerInfo(config),
            TrueNasApiUrl = config["TRUENAS_API"],
            TrueNasApiKey = config["TRUENAS_API_KEY"],
            ServerTimeout = ParseTimeSpan(config["SERVER_TIMEOUT_MINUTES"], TimeSpan.FromMinutes(10)),

            // Accounts
            GmAccount = config["WWOW_GM_ACCOUNT"] ?? string.Empty,
            GmPassword = config["WWOW_GM_PASSWORD"] ?? string.Empty,
            GmCharacter = config["WWOW_GM_CHARACTER"] ?? string.Empty,
            TestAccount = config["WWOW_TEST_ACCOUNT"] ?? string.Empty,
            TestPassword = config["WWOW_TEST_PASSWORD"] ?? string.Empty,
            TestCharacter = config["WWOW_TEST_CHARACTER"] ?? string.Empty,

            // PathfindingService
            PathfindingServiceIp = config["PATHFINDING_SERVICE_IP"] ?? "127.0.0.1",
            PathfindingServicePort = ParseInt(config["PATHFINDING_SERVICE_PORT"], 5000),
            // --no-pathfinding-inprocess takes precedence
            StartPathfindingServiceInProcess = !ParseBool(config["no-pathfinding-inprocess"], false) && ParseBool(config["START_PATHFINDING_INPROCESS"] ?? config["start-pathfinding-inprocess"], true),

            // OBS
            ObsExecutablePath = config["OBS_EXECUTABLE_PATH"],
            ObsWebSocketUrl = config["OBS_WEBSOCKET_URL"] ?? "ws://localhost:4455",
            ObsWebSocketPassword = config["OBS_WEBSOCKET_PASSWORD"],
            ObsRecordingPath = config["OBS_RECORDING_PATH"],
            ObsAutoLaunch = ParseBool(config["OBS_AUTO_LAUNCH"], false),

            // Recording target
            WowWindowTitle = config["WWOW_WOW_WINDOW_TITLE"],
            WowProcessId = ParseIntNullable(config["WWOW_WOW_PROCESS_ID"]),
            WowWindowHandle = ParseIntPtrNullable(config["WWOW_WOW_WINDOW_HANDLE"]),

            // Test execution
            TestFilter = config["test-filter"] ?? config["TestFilter"],
            CategoryFilter = config["category-filter"] ?? config["CategoryFilter"],
            StopOnFirstFailure = ParseBool(config["stop-on-failure"], false),
            ArtifactsRoot = config["artifacts-root"] ?? config["ARTIFACTS_ROOT"] ?? "./TestLogs",
            // --disable-recording takes precedence over --enable-recording
            EnableRecording = !ParseBool(config["disable-recording"], false) && ParseBool(config["enable-recording"], true)
        };

        return testConfig;
    }

    /// <summary>
    /// Gets command-line argument mappings (short names to full names).
    /// </summary>
    private static Dictionary<string, string> GetCommandLineMappings()
    {
        return new Dictionary<string, string>
        {
            // Server
            { "--server-host", "ServerHost" },
            { "--server-port", "ServerPort" },
            { "--server-realm", "ServerRealm" },
            { "--truenas-api", "TRUENAS_API" },
            { "--truenas-api-key", "TRUENAS_API_KEY" },
            { "--server-timeout", "SERVER_TIMEOUT_MINUTES" },

            // Accounts
            { "--gm-account", "WWOW_GM_ACCOUNT" },
            { "--gm-password", "WWOW_GM_PASSWORD" },
            { "--gm-character", "WWOW_GM_CHARACTER" },
            { "--test-account", "WWOW_TEST_ACCOUNT" },
            { "--test-password", "WWOW_TEST_PASSWORD" },
            { "--test-character", "WWOW_TEST_CHARACTER" },

            // PathfindingService
            { "--pathfinding-ip", "PATHFINDING_SERVICE_IP" },
            { "--pathfinding-port", "PATHFINDING_SERVICE_PORT" },
            { "--start-pathfinding-inprocess", "start-pathfinding-inprocess" },
            { "--no-pathfinding-inprocess", "no-pathfinding-inprocess" },

            // OBS
            { "--obs-executable", "OBS_EXECUTABLE_PATH" },
            { "--obs-websocket-url", "OBS_WEBSOCKET_URL" },
            { "--obs-password", "OBS_WEBSOCKET_PASSWORD" },
            { "--obs-recording-path", "OBS_RECORDING_PATH" },
            { "--obs-auto-launch", "OBS_AUTO_LAUNCH" },

            // Recording target
            { "--wow-window-title", "WWOW_WOW_WINDOW_TITLE" },
            { "--wow-process-id", "WWOW_WOW_PROCESS_ID" },
            { "--wow-window-handle", "WWOW_WOW_WINDOW_HANDLE" },

            // Test execution
            { "--test-filter", "test-filter" },
            { "--category", "category-filter" },
            { "--stop-on-failure", "stop-on-failure" },
            { "--artifacts-root", "artifacts-root" },
            { "--enable-recording", "enable-recording" },
            { "--disable-recording", "disable-recording" }
        };
    }

    /// <summary>
    /// Parses server info from configuration or SERVER_DEFINITIONS environment variable.
    /// </summary>
    private static ServerInfo ParseServerInfo(IConfiguration config)
    {
        var host = config["ServerHost"] ?? config["server-host"];
        var portStr = config["ServerPort"] ?? config["server-port"];
        var realm = config["ServerRealm"] ?? config["server-realm"];

        // Check SERVER_DEFINITIONS format: "releaseName|host|port[|realm]"
        var serverDef = config["SERVER_DEFINITIONS"];
        if (!string.IsNullOrEmpty(serverDef))
        {
            var parts = serverDef.Split('|');
            if (parts.Length >= 3)
            {
                host = parts[1];
                portStr = parts[2];
                if (parts.Length >= 4)
                    realm = parts[3];
            }
        }

        var port = ParseInt(portStr, 3724);
        return new ServerInfo(host ?? "127.0.0.1", port, realm);
    }

    private static int ParseInt(string? value, int defaultValue)
    {
        return int.TryParse(value, out var result) ? result : defaultValue;
    }

    private static int? ParseIntNullable(string? value)
    {
        return int.TryParse(value, out var result) ? result : null;
    }

    private static IntPtr? ParseIntPtrNullable(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return null;

        // Handle hex format (0x12345678)
        if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            value = value.Substring(2);

        return long.TryParse(value, System.Globalization.NumberStyles.HexNumber, null, out var result)
            ? new IntPtr(result)
            : null;
    }

    private static bool ParseBool(string? value, bool defaultValue)
    {
        if (string.IsNullOrEmpty(value))
            return defaultValue;

        return value.Equals("true", StringComparison.OrdinalIgnoreCase)
            || value.Equals("1", StringComparison.OrdinalIgnoreCase)
            || value.Equals("yes", StringComparison.OrdinalIgnoreCase);
    }

    private static TimeSpan ParseTimeSpan(string? value, TimeSpan defaultValue)
    {
        if (string.IsNullOrEmpty(value))
            return defaultValue;

        return int.TryParse(value, out var minutes)
            ? TimeSpan.FromMinutes(minutes)
            : defaultValue;
    }
}
