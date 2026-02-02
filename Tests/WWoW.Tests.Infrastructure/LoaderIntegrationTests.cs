using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace WWoW.Tests.Infrastructure;

/// <summary>
/// Integration tests for the Loader.dll injection system.
/// These tests require:
/// - WoW 1.12.1 client installed (set WWOW_TEST_WOW_PATH)
/// - .NET 8 x86 runtime installed
/// - Loader.dll and ForegroundBotRunner.dll in test output
/// </summary>
[Trait("Category", TestCategories.Integration)]
[Trait("Category", "Loader")]
public class LoaderIntegrationTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly ILogger<WoWProcessManager> _logger;
    private WoWProcessManager? _processManager;

    public LoaderIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
        _logger = new XunitLogger<WoWProcessManager>(output);
    }

    /// <summary>
    /// Tests that Loader.dll can be injected into WoW and initialize .NET 8 runtime.
    /// </summary>
    [Fact(Skip = "Requires WoW client - run manually with WWOW_TEST_WOW_PATH set")]
    [RequiresServices(RequiredServices.None)] // No server needed, just WoW client
    public async Task Loader_InjectsSuccessfully_AndInitializesNet8Runtime()
    {
        // Arrange
        var config = WoWProcessConfig.FromEnvironment();
        
        _output.WriteLine($"WoW Path: {config.WoWExecutablePath}");
        _output.WriteLine($"Loader Path: {config.LoaderDllPath}");

        // Verify prerequisites
        Assert.True(File.Exists(config.WoWExecutablePath), 
            $"WoW.exe not found. Set WWOW_TEST_WOW_PATH environment variable. Current: {config.WoWExecutablePath}");
        Assert.True(File.Exists(config.LoaderDllPath), 
            $"Loader.dll not found at {config.LoaderDllPath}");

        _processManager = new WoWProcessManager(config, _logger);

        // Act
        var result = await _processManager.LaunchAndInjectAsync();

        // Assert
        _output.WriteLine($"Injection Result: {result.Success}");
        _output.WriteLine($"Final State: {result.FinalState}");
        if (!result.Success)
        {
            _output.WriteLine($"Error: {result.ErrorMessage}");
        }

        Assert.True(result.Success, $"Injection failed: {result.ErrorMessage}");
        Assert.Equal(InjectionState.ManagedCodeRunning, result.FinalState);
        Assert.NotNull(result.ProcessId);

        // Keep process alive for a few seconds to observe behavior
        _output.WriteLine($"WoW process running with PID: {result.ProcessId}");
        await Task.Delay(5000);

        // Verify process is still running (didn't crash after injection)
        Assert.True(_processManager.IsProcessRunning, "WoW process crashed after injection");
    }

    /// <summary>
    /// Tests that injection fails gracefully with missing WoW executable.
    /// </summary>
    [Fact]
    public async Task Loader_FailsGracefully_WhenWoWNotFound()
    {
        // Arrange
        var config = new WoWProcessConfig
        {
            WoWExecutablePath = @"C:\NonExistent\WoW.exe",
            LoaderDllPath = "Loader.dll"
        };

        _processManager = new WoWProcessManager(config, _logger);

        // Act
        var result = await _processManager.LaunchAndInjectAsync();

        // Assert
        Assert.False(result.Success);
        Assert.Equal(InjectionState.Failed, result.FinalState);
        Assert.Contains("not found", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Tests that injection fails gracefully with missing Loader.dll.
    /// </summary>
    [Fact]
    public async Task Loader_FailsGracefully_WhenLoaderDllNotFound()
    {
        // Arrange
        // Use an existing executable to pass the WoW check
        var config = new WoWProcessConfig
        {
            WoWExecutablePath = Environment.ProcessPath!, // Current test process
            LoaderDllPath = @"C:\NonExistent\Loader.dll"
        };

        _processManager = new WoWProcessManager(config, _logger);

        // Act
        var result = await _processManager.LaunchAndInjectAsync();

        // Assert
        Assert.False(result.Success);
        Assert.Equal(InjectionState.Failed, result.FinalState);
        Assert.Contains("Loader.dll not found", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        _processManager?.Dispose();
    }
}

/// <summary>
/// Simple xUnit ILogger adapter for test output.
/// </summary>
public class XunitLogger<T>(ITestOutputHelper output) : ILogger<T>
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        var message = formatter(state, exception);
        output.WriteLine($"[{logLevel}] {message}");
        if (exception != null)
        {
            output.WriteLine($"  Exception: {exception.Message}");
        }
    }
}
