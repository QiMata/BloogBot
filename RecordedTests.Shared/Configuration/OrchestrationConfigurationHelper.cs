using RecordedTests.Shared.Abstractions;
using System;

namespace RecordedTests.Shared.Configuration;

/// <summary>
/// Helper for resolving orchestration options with CLI → env → config → defaults precedence.
/// </summary>
public static class OrchestrationConfigurationHelper
{
    /// <summary>
    /// Resolves orchestration options from CLI, environment, config, and defaults.
    /// </summary>
    /// <param name="cliArtifactsRoot">Artifacts root directory from CLI.</param>
    /// <param name="cliServerTimeoutMinutes">Server availability timeout in minutes from CLI.</param>
    /// <param name="cliDoubleStopRecorder">Double-stop recorder flag from CLI.</param>
    /// <param name="configOptions">Orchestration options from config file.</param>
    /// <returns>Resolved orchestration options.</returns>
    public static OrchestrationOptions ResolveOrchestrationOptions(
        string? cliArtifactsRoot = null,
        int? cliServerTimeoutMinutes = null,
        bool? cliDoubleStopRecorder = null,
        OrchestrationOptions? configOptions = null)
    {
        var defaultOptions = new OrchestrationOptions();

        var artifactsRoot = ConfigurationResolver.ResolveString(
            cliArtifactsRoot,
            "ARTIFACTS_ROOT",
            configOptions?.ArtifactsRootDirectory,
            defaultOptions.ArtifactsRootDirectory);

        var serverTimeoutMinutes = ConfigurationResolver.ResolveInt(
            cliServerTimeoutMinutes,
            "SERVER_TIMEOUT_MINUTES",
            configOptions != null ? (int)configOptions.ServerAvailabilityTimeout.TotalMinutes : null,
            (int)defaultOptions.ServerAvailabilityTimeout.TotalMinutes);

        var doubleStopRecorder = ConfigurationResolver.ResolveBool(
            cliDoubleStopRecorder,
            "DOUBLE_STOP_RECORDER",
            configOptions?.DoubleStopRecorderForSafety,
            defaultOptions.DoubleStopRecorderForSafety);

        return new OrchestrationOptions
        {
            ArtifactsRootDirectory = artifactsRoot,
            ServerAvailabilityTimeout = TimeSpan.FromMinutes(serverTimeoutMinutes),
            DoubleStopRecorderForSafety = doubleStopRecorder
        };
    }

    /// <summary>
    /// Parses orchestration options from command-line arguments.
    /// </summary>
    /// <param name="args">Command-line arguments.</param>
    /// <returns>Parsed orchestration options or null if no relevant args found.</returns>
    public static (string? artifactsRoot, int? timeoutMinutes, bool? doubleStop) ParseFromCommandLine(string[] args)
    {
        string? artifactsRoot = null;
        int? timeoutMinutes = null;
        bool? doubleStop = null;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--artifacts-root" when i + 1 < args.Length:
                    artifactsRoot = args[++i];
                    break;

                case "--server-timeout" when i + 1 < args.Length:
                    if (int.TryParse(args[++i], out var timeout))
                        timeoutMinutes = timeout;
                    break;

                case "--double-stop-recorder":
                    doubleStop = true;
                    break;

                case "--no-double-stop-recorder":
                    doubleStop = false;
                    break;
            }
        }

        return (artifactsRoot, timeoutMinutes, doubleStop);
    }
}
