using BotRunner.Clients;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using WWoW.RecordedTests.PathingTests.Configuration;
using WWoW.RecordedTests.PathingTests.Context;
using WWoW.RecordedTests.PathingTests.Descriptions;
using WWoW.RecordedTests.PathingTests.Models;
using WWoW.RecordedTests.PathingTests.Runners;
using WWoW.RecordedTests.Shared;
using WWoW.RecordedTests.Shared.Abstractions;
using WWoW.RecordedTests.Shared.Abstractions.I;
using WWoW.RecordedTests.Shared.Configuration;
using WWoW.RecordedTests.Shared.Recording;

namespace WWoW.RecordedTests.PathingTests;

/// <summary>
/// Main entry point for pathfinding test execution.
/// </summary>
internal class Program
{
    private static IHost? _pathfindingServiceHost;

    static async Task<int> Main(string[] args)
    {
        Console.WriteLine("==========================================================");
        Console.WriteLine("  BloogBot Pathfinding Tests");
        Console.WriteLine("==========================================================");
        Console.WriteLine();

        try
        {
            var logger = new ConsoleTestLogger();

            // Parse configuration from CLI → env → config
            var configuration = ConfigurationParser.Parse(args);

            // Start PathfindingService in-process if configured
            if (configuration.StartPathfindingServiceInProcess)
            {
                await StartPathfindingServiceAsync(logger);
            }

            // Print configuration summary
            PrintConfigurationSummary(configuration);

            // Validate configuration
            configuration.Validate();

            // Create the appropriate Mangos client based on configuration
            IMangosAppsClient? mangosClient = null;
            try
            {
                mangosClient = ServerConfigurationHelper.ResolveMangosClie(
                    cliTrueNasApi: configuration.TrueNasApiUrl,
                    cliTrueNasApiKey: configuration.TrueNasApiKey,
                    useLocalDockerFallback: true);
            }
            catch (Exception ex)
            {
                logger.Warn($"Failed to create Mangos client: {ex.Message}");
                logger.Warn("Proceeding with direct server connection");
            }

            // Create server availability checker
            var serverChecker = mangosClient != null
                ? ServerConfigurationHelper.CreateServerAvailabilityChecker(
                    mangosClient: mangosClient,
                    cliServerDefinitions: null, // Will use configuration.ServerInfo directly
                    logger: logger)
                : new AlwaysAvailableServerChecker(configuration.ServerInfo);

            // Filter tests based on configuration
            var testDefinitions = FilterTests(configuration, PathingTestDefinitions.All);

            logger.Info($"Found {testDefinitions.Count()} tests to execute");
            Console.WriteLine();

            // Run tests
            var results = await RunTestsAsync(configuration, testDefinitions, serverChecker, logger);

            // Print summary
            PrintSummary(results);

            // Cleanup
            mangosClient?.Dispose();
            await StopPathfindingServiceAsync(logger);

            return results.All(r => r.Success) ? 0 : 1;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine($"[ERROR] Fatal error: {ex.Message}");
            Console.Error.WriteLine(ex.StackTrace);
            Console.ResetColor();

            // Ensure PathfindingService is stopped on error
            await StopPathfindingServiceAsync(new ConsoleTestLogger());

            return 1;
        }
    }

    /// <summary>
    /// Starts the PathfindingService in-process.
    /// </summary>
    private static async Task StartPathfindingServiceAsync(ITestLogger logger)
    {
        try
        {
            logger.Info("Starting PathfindingService in-process...");

            // Build the host using PathfindingService's CreateHostBuilder
            _pathfindingServiceHost = PathfindingService.Program.CreateHostBuilder(Array.Empty<string>()).Build();

            // Start the host asynchronously
            await _pathfindingServiceHost.StartAsync();

            logger.Info("PathfindingService started successfully");

            // Give the service a moment to initialize
            await Task.Delay(1000);
        }
        catch (Exception ex)
        {
            logger.Error($"Failed to start PathfindingService in-process: {ex.Message}", ex);
            throw;
        }
    }

    /// <summary>
    /// Stops the PathfindingService if it was started in-process.
    /// </summary>
    private static async Task StopPathfindingServiceAsync(ITestLogger logger)
    {
        if (_pathfindingServiceHost == null)
            return;

        try
        {
            logger.Info("Stopping PathfindingService...");
            await _pathfindingServiceHost.StopAsync(TimeSpan.FromSeconds(10));
            _pathfindingServiceHost.Dispose();
            _pathfindingServiceHost = null;
            logger.Info("PathfindingService stopped successfully");
        }
        catch (Exception ex)
        {
            logger.Warn($"Error stopping PathfindingService: {ex.Message}");
        }
    }

    private static IReadOnlyList<PathingTestDefinition> FilterTests(
        TestConfiguration config,
        IReadOnlyList<PathingTestDefinition> allTests)
    {
        var filtered = allTests.AsEnumerable();

        // Filter by test name - EXACT MATCH (case-insensitive)
        if (!string.IsNullOrEmpty(config.TestFilter))
        {
            filtered = filtered.Where(t =>
                string.Equals(t.Name, config.TestFilter, StringComparison.OrdinalIgnoreCase));
        }

        // Filter by category (exact match, case-insensitive)
        if (!string.IsNullOrEmpty(config.CategoryFilter))
        {
            filtered = filtered.Where(t =>
                string.Equals(t.Category, config.CategoryFilter, StringComparison.OrdinalIgnoreCase));
        }

        var result = filtered.ToList();

        // FAIL FAST if filters produce zero tests
        if (result.Count == 0)
        {
            var filterDesc = new List<string>();
            if (!string.IsNullOrEmpty(config.TestFilter))
                filterDesc.Add($"name='{config.TestFilter}'");
            if (!string.IsNullOrEmpty(config.CategoryFilter))
                filterDesc.Add($"category='{config.CategoryFilter}'");

            throw new InvalidOperationException(
                $"No tests match the specified filters: {string.Join(", ", filterDesc)}. " +
                $"Available tests: {string.Join(", ", allTests.Select(t => t.Name))}");
        }

        return result;
    }

    private static async Task<List<OrchestrationResult>> RunTestsAsync(
        TestConfiguration config,
        IEnumerable<PathingTestDefinition> testDefinitions,
        IServerAvailabilityChecker serverChecker,
        ITestLogger logger)
    {
        var results = new List<OrchestrationResult>();

        foreach (var testDef in testDefinitions)
        {
            Console.WriteLine("----------------------------------------------------------");
            Console.WriteLine($"Test: {testDef.Name}");
            Console.WriteLine($"Category: {testDef.Category}");
            Console.WriteLine($"Description: {testDef.Description}");
            Console.WriteLine("----------------------------------------------------------");

            try
            {
                // Create test context
                var context = new PathingRecordedTestContext(testDef, config.ServerInfo);

                // Create bot runners
                var foregroundRunner = new ForegroundRecordedTestRunner(
                    config.GmAccount,
                    config.GmPassword,
                    config.GmCharacter,
                    logger,
                    config);

                var pathfindingClient = new PathfindingClient(
                    config.PathfindingServiceIp,
                    config.PathfindingServicePort,
                    NullLogger.Instance);

                var backgroundRunner = new BackgroundRecordedTestRunner(
                    testDef,
                    pathfindingClient,
                    config.TestAccount,
                    config.TestPassword,
                    config.TestCharacter,
                    logger);

                // Create screen recorder (if enabled)
                IScreenRecorder? recorder = null;
                if (config.EnableRecording)
                {
                    var recorderConfig = new ObsWebSocketConfiguration
                    {
                        WebSocketUrl = config.ObsWebSocketUrl,
                        WebSocketPassword = config.ObsWebSocketPassword ?? string.Empty,
                        ObsExecutablePath = config.ObsExecutablePath ?? string.Empty,
                        RecordingOutputPath = config.ObsRecordingPath ?? Path.Combine(config.ArtifactsRoot, "Recordings"),
                        AutoLaunchObs = config.ObsAutoLaunch
                    };

                    recorder = new ObsWebSocketScreenRecorder(recorderConfig, logger);
                }

                // Create test description
                var testDescription = new PathingRecordedTestDescription(
                    context,
                    foregroundRunner,
                    backgroundRunner,
                    recorder,
                    logger);

                // Create orchestrator and run test
                var orchestrator = new RecordedTestOrchestrator(serverChecker, config.OrchestrationOptions, logger);
                var result = await orchestrator.RunAsync(testDescription, CancellationToken.None);

                results.Add(result);

                // Print result
                if (result.Success)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"✓ Test '{testDef.Name}' succeeded");
                    Console.ResetColor();
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"✗ Test '{testDef.Name}' failed");
                    if (!string.IsNullOrEmpty(result.Message))
                        Console.WriteLine($"  Error: {result.Message}");
                    Console.ResetColor();
                }

                // Stop on first failure if configured
                if (!result.Success && config.StopOnFirstFailure)
                {
                    Console.WriteLine();
                    Console.WriteLine("[INFO] Stopping execution due to test failure (StopOnFirstFailure = true)");
                    break;
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[ERROR] Exception running test {testDef.Name}: {ex.Message}");
                Console.ResetColor();

                results.Add(new OrchestrationResult(false, ex.Message));

                if (config.StopOnFirstFailure)
                    break;
            }

            Console.WriteLine();
        }

        return results;
    }

    private static void PrintConfigurationSummary(TestConfiguration config)
    {
        Console.WriteLine("Configuration:");
        Console.WriteLine($"  Server: {config.ServerInfo.Host}:{config.ServerInfo.Port} (Realm: {config.ServerInfo.Realm ?? "default"})");
        Console.WriteLine($"  GM Account: {config.GmAccount}");
        Console.WriteLine($"  Test Account: {config.TestAccount}");
        Console.WriteLine($"  PathfindingService: {config.PathfindingServiceIp}:{config.PathfindingServicePort} (In-Process: {config.StartPathfindingServiceInProcess})");
        Console.WriteLine($"  Recording: {(config.EnableRecording ? "Enabled" : "Disabled")}");
        if (config.EnableRecording)
        {
            Console.WriteLine($"  OBS WebSocket: {config.ObsWebSocketUrl}");
        }
        Console.WriteLine($"  Artifacts: {config.ArtifactsRoot}");
        if (!string.IsNullOrEmpty(config.TestFilter))
            Console.WriteLine($"  Test Filter: {config.TestFilter}");
        if (!string.IsNullOrEmpty(config.CategoryFilter))
            Console.WriteLine($"  Category Filter: {config.CategoryFilter}");
        Console.WriteLine();
    }

    private static void PrintSummary(List<OrchestrationResult> results)
    {
        Console.WriteLine();
        Console.WriteLine("==========================================================");
        Console.WriteLine("  Test Execution Summary");
        Console.WriteLine("==========================================================");
        Console.WriteLine($"Total Tests: {results.Count}");
        Console.WriteLine($"Passed: {results.Count(r => r.Success)}");
        Console.WriteLine($"Failed: {results.Count(r => !r.Success)}");

        if (results.Any(r => !r.Success))
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Failed Tests:");
            foreach (var result in results.Where(r => !r.Success))
            {
                Console.WriteLine($"  - {result.Message ?? "Unknown error"}");
            }
            Console.ResetColor();
        }

        Console.WriteLine("==========================================================");
    }
}

/// <summary>
/// Simple console logger for tests.
/// </summary>
public class ConsoleTestLogger : ITestLogger
{
    public void Info(string message) => Console.WriteLine($"[INFO] {message}");
    public void Warn(string message)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"[WARN] {message}");
        Console.ResetColor();
    }
    public void Error(string message, Exception? ex = null)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Error.WriteLine($"[ERROR] {message}");
        if (ex != null)
        {
            Console.Error.WriteLine(ex);
        }
        Console.ResetColor();
    }
}

/// <summary>
/// Simple server availability checker that always returns the configured server as available.
/// </summary>
internal class AlwaysAvailableServerChecker : IServerAvailabilityChecker
{
    private readonly ServerInfo _serverInfo;

    public AlwaysAvailableServerChecker(ServerInfo serverInfo)
    {
        _serverInfo = serverInfo;
    }

    public Task<ServerInfo> WaitForAvailableAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_serverInfo);
    }
}
