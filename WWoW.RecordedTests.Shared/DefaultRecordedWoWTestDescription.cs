namespace WWoW.RecordedTests.Shared;

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using WWoW.RecordedTests.Shared.Abstractions;
using WWoW.RecordedTests.Shared.Abstractions.I;

// A concrete ITestDescription that orchestrates two IBotRunner instances and an optional screen recorder.
public class DefaultRecordedWoWTestDescription : ITestDescription
{
    private readonly Func<IBotRunner> _createForegroundRunner;
    private readonly Func<IBotRunner> _createBackgroundRunner;
    private readonly Func<IScreenRecorder>? _createRecorder;
    private readonly OrchestrationOptions _options;
    private readonly ITestLogger _logger;
    private readonly IRecordedTestContext? _providedContext;
    private readonly IServerDesiredState? _desiredState;

    /// <summary>
    /// Creates a test description with factory functions for runners and recorder.
    /// </summary>
    public DefaultRecordedWoWTestDescription(
        string name,
        Func<IBotRunner> createForegroundRunner,
        Func<IBotRunner> createBackgroundRunner,
        Func<IScreenRecorder>? createRecorder = null,
        OrchestrationOptions? options = null,
        ITestLogger? logger = null)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        _createForegroundRunner = createForegroundRunner ?? throw new ArgumentNullException(nameof(createForegroundRunner));
        _createBackgroundRunner = createBackgroundRunner ?? throw new ArgumentNullException(nameof(createBackgroundRunner));
        _createRecorder = createRecorder;
        _options = options ?? new OrchestrationOptions();
        _logger = logger ?? new NullTestLogger();
        _providedContext = null;
        _desiredState = null;
    }

    /// <summary>
    /// Creates a test description with a pre-created context, direct runner instances, and optional server desired state.
    /// This overload is useful when the context needs to carry additional test-specific data (e.g., PathingRecordedTestContext).
    /// </summary>
    public DefaultRecordedWoWTestDescription(
        IRecordedTestContext context,
        IBotRunner foregroundRunner,
        IBotRunner backgroundRunner,
        IServerDesiredState? desiredState,
        IScreenRecorder? recorder = null,
        ITestLogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(foregroundRunner);
        ArgumentNullException.ThrowIfNull(backgroundRunner);

        Name = context.TestName;
        _providedContext = context;
        _desiredState = desiredState;
        _createForegroundRunner = () => foregroundRunner;
        _createBackgroundRunner = () => backgroundRunner;
        _createRecorder = recorder != null ? () => recorder : null;
        _options = new OrchestrationOptions();
        _logger = logger ?? new NullTestLogger();
    }

    public string Name { get; }

    public async Task<OrchestrationResult> ExecuteAsync(ServerInfo server, CancellationToken cancellationToken)
    {
        // Use provided context or create a new one
        var context = _providedContext ?? new RecordedTestContext(Name, server);

        Directory.CreateDirectory(_options.ArtifactsRootDirectory);
        var testDir = Path.Combine(_options.ArtifactsRootDirectory, SanitizeFileName(Name), context.StartedAt.ToString("yyyyMMdd_HHmmss"));
        Directory.CreateDirectory(testDir);

        await using var fg = _createForegroundRunner();
        await using var bg = _createBackgroundRunner();
        await using var recorder = _createRecorder != null ? _createRecorder() : null;

        TestArtifact? artifact = null;

        try
        {
            // Connect runners
            _logger.Info("[Test] Connecting Foreground (GM) runner...");
            await fg.ConnectAsync(server, cancellationToken);
            _logger.Info("[Test] Connecting Background runner...");
            await bg.ConnectAsync(server, cancellationToken);

            // Recorder optional
            if (recorder != null)
            {
                _logger.Info("[Test] Launching recorder...");
                await recorder.LaunchAsync(cancellationToken);

                _logger.Info("[Test] Obtaining recording target from Foreground runner...");
                var target = await fg.GetRecordingTargetAsync(cancellationToken);
                _logger.Info("[Test] Configuring recorder target...");
                await recorder.ConfigureTargetAsync(target, cancellationToken);
            }

            // Prepare server state - use desired state if provided, otherwise delegate to runner
            _logger.Info("[Test] Preparing server state (GM)...");
            if (_desiredState != null)
            {
                await _desiredState.ApplyAsync(fg, context, cancellationToken);
            }
            else
            {
                await fg.PrepareServerStateAsync(context, cancellationToken);
            }

            // Start recording before background test
            if (recorder != null)
            {
                _logger.Info("[Test] Starting recording...");
                await recorder.StartAsync(context, cancellationToken);
            }

            // Execute the test using Background runner
            _logger.Info("[Test] Running test with Background runner...");
            await bg.RunTestAsync(context, cancellationToken);

            // Stop recording immediately after test
            if (recorder != null)
            {
                _logger.Info("[Test] Stopping recording...");
                await recorder.StopAsync(cancellationToken);
            }

            // Reset server state - use desired state if provided, otherwise delegate to runner
            _logger.Info("[Test] Resetting server state (GM)...");
            if (_desiredState != null)
            {
                await _desiredState.RevertAsync(fg, context, cancellationToken);
            }
            else
            {
                await fg.ResetServerStateAsync(context, cancellationToken);
            }

            // Optional double stop to ensure OBS is stopped
            if (recorder != null && _options.DoubleStopRecorderForSafety)
            {
                _logger.Info("[Test] Ensuring recorder stopped (double-stop)...");
                await recorder.StopAsync(cancellationToken);
            }

            // Move artifact to test log folder
            if (recorder != null)
            {
                _logger.Info("[Test] Moving recorded artifact to test log folder...");
                artifact = await recorder.MoveLastRecordingAsync(testDir, SanitizeFileName(Name), cancellationToken);
            }

            // Shut down foreground UI
            _logger.Info("[Test] Shutting down Foreground UI...");
            await fg.ShutdownUiAsync(cancellationToken);

            var successMsg = $"Test '{Name}' executed successfully.";
            _logger.Info(successMsg);
            return new OrchestrationResult(true, successMsg, artifact);
        }
        catch (OperationCanceledException)
        {
            var msg = "Test execution canceled.";
            _logger.Warn(msg);
            return new OrchestrationResult(false, msg, artifact);
        }
        catch (Exception ex)
        {
            var msg = $"Test execution failed: {ex.Message}";
            _logger.Error(msg, ex);
            return new OrchestrationResult(false, msg, artifact);
        }
        finally
        {
            // Best-effort cleanup
            try
            {
                if (recorder != null)
                {
                    _logger.Info("[Test] Cleanup: stopping recorder...");
                    await recorder.StopAsync(CancellationToken.None);
                }
            }
            catch { }
            try
            {
                _logger.Info("[Test] Cleanup: resetting server state (GM)...");
                if (_desiredState != null)
                {
                    await _desiredState.RevertAsync(fg, context, CancellationToken.None);
                }
                else
                {
                    await fg.ResetServerStateAsync(context, CancellationToken.None);
                }
            }
            catch { }
            try
            {
                _logger.Info("[Test] Cleanup: shutting down Foreground UI...");
                await fg.ShutdownUiAsync(CancellationToken.None);
            }
            catch { }
            try
            {
                _logger.Info("[Test] Cleanup: disconnecting runners...");
                await bg.DisconnectAsync(CancellationToken.None);
                await fg.DisconnectAsync(CancellationToken.None);
            }
            catch { }
        }
    }

    private static string SanitizeFileName(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name;
    }
}
