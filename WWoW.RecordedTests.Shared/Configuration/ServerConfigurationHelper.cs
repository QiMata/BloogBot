using WWoW.RecordedTests.Shared.Abstractions.I;

namespace WWoW.RecordedTests.Shared.Configuration;

/// <summary>
/// Helper for resolving server discovery configuration with CLI → env → config → defaults precedence.
/// </summary>
public static class ServerConfigurationHelper
{
    /// <summary>
    /// Resolves server definitions from CLI args, environment, or config.
    /// </summary>
    /// <param name="cliServerDefinitions">Server definitions from CLI (semicolon-separated).</param>
    /// <param name="configServerDefinitions">Server definitions from config file.</param>
    /// <returns>Array of server definition strings in format: releaseName|host|port[|realm]</returns>
    public static string[] ResolveServerDefinitions(
        string? cliServerDefinitions = null,
        string[]? configServerDefinitions = null)
    {
        // Try CLI first
        if (!string.IsNullOrWhiteSpace(cliServerDefinitions))
        {
            return cliServerDefinitions
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToArray();
        }

        // Try environment variable
        var envServerDefs = Environment.GetEnvironmentVariable("SERVER_DEFINITIONS");
        if (!string.IsNullOrWhiteSpace(envServerDefs))
        {
            return envServerDefs
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToArray();
        }

        // Try config file
        if (configServerDefinitions != null && configServerDefinitions.Length > 0)
        {
            return configServerDefinitions;
        }

        // Default: local server
        return new[] { "mangos-local|127.0.0.1|3724" };
    }

    /// <summary>
    /// Creates an appropriate IMangosAppsClient based on configuration.
    /// </summary>
    /// <param name="cliTrueNasApi">TrueNAS API base address from CLI.</param>
    /// <param name="cliTrueNasApiKey">TrueNAS API key from CLI.</param>
    /// <param name="configTrueNasApi">TrueNAS API base address from config.</param>
    /// <param name="configTrueNasApiKey">TrueNAS API key from config.</param>
    /// <param name="useLocalDockerFallback">Whether to fall back to local Docker if TrueNAS not configured.</param>
    /// <returns>Configured IMangosAppsClient instance.</returns>
    public static IMangosAppsClient ResolveMangosClie(
        string? cliTrueNasApi = null,
        string? cliTrueNasApiKey = null,
        string? configTrueNasApi = null,
        string? configTrueNasApiKey = null,
        bool useLocalDockerFallback = true)
    {
        var apiBaseAddress = ConfigurationResolver.ResolveString(
            cliTrueNasApi,
            "TRUENAS_API",
            configTrueNasApi);

        var apiKey = ConfigurationResolver.ResolveString(
            cliTrueNasApiKey,
            "TRUENAS_API_KEY",
            configTrueNasApiKey);

        // If both are configured, use TrueNAS client
        if (!string.IsNullOrWhiteSpace(apiBaseAddress) && !string.IsNullOrWhiteSpace(apiKey))
        {
            return new TrueNasAppsClient(apiBaseAddress, apiKey);
        }

        // Otherwise, fall back to local Docker-based client
        if (useLocalDockerFallback)
        {
            return new LocalMangosDockerTrueNasAppsClient(new[]
            {
                new LocalMangosDockerConfiguration(
                    releaseName: "mangos-local",
                    image: "azerothcore/azerothcore-wotlk:latest",
                    hostPort: 3724,
                    containerPort: 3724,
                    environment: new Dictionary<string, string>
                    {
                        ["AC_WORLD__REALMLIST"] = "127.0.0.1"
                    })
            });
        }

        throw new InvalidOperationException(
            "No Mangos client configuration found. Either configure TrueNAS API or enable local Docker fallback.");
    }

    /// <summary>
    /// Creates a server availability checker with resolved configuration.
    /// </summary>
    public static IServerAvailabilityChecker CreateServerAvailabilityChecker(
        IMangosAppsClient mangosClient,
        string? cliServerDefinitions = null,
        string[]? configServerDefinitions = null,
        ITestLogger? logger = null)
    {
        var serverDefinitions = ResolveServerDefinitions(cliServerDefinitions, configServerDefinitions);

        return new TrueNasAppServerAvailabilityChecker(
            client: mangosClient,
            serverDefinitions: serverDefinitions,
            logger: logger);
    }
}
