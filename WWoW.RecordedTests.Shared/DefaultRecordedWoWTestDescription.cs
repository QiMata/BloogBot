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
    private readonly IBotRunnerFactory _foregroundFactory;
    private readonly IBotRunnerFactory _backgroundFactory;
    private readonly IScreenRecorderFactory? _recorderFactory;
    private readonly IServerDesiredState? _initialDesiredState;
    private readonly IServerDesiredState? _baseDesiredState;
    private readonly OrchestrationOptions _options;
    private readonly ITestLogger _logger;
    private readonly IRecordedTestContext? _providedContext;
    private readonly IServerDesiredState? _desiredState;

    /// <summary>
    /// Creates a test description with factory functions for runners and recorder.
    /// </summary>
    public DefaultRecordedWoWTestDescription(
        string name,
        IBotRunnerFactory foregroundFactory,
        IBotRunnerFactory backgroundFactory,
        IScreenRecorderFactory? recorderFactory = null,
        IServerDesiredState? initialDesiredState = null,
        IServerDesiredState? baseDesiredState = null,
        OrchestrationOptions? options = null,
        ITestLogger? logger = null)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        _foregroundFactory = foregroundFactory ?? throw new ArgumentNullException(nameof(foregroundFactory));
        _backgroundFactory = backgroundFactory ?? throw new ArgumentNullException(nameof(backgroundFactory));
        _recorderFactory = recorderFactory;
        _initialDesiredState = initialDesiredState;
        _baseDesiredState = baseDesiredState;
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

    public async Task<OrchestrationResult> ExecuteAsync(IRecordedTestContext context, CancellationToken cancellationToken)
    {
        // Use provided context or create a new one
        var context = _providedContext ?? new RecordedTestContext(Name, server);

        Directory.CreateDirectory(context.TestRunDirectory);

        await using var fg = _foregroundFactory.Create();
        await using var bg = _backgroundFactory.Create();
        var recorder = _recorderFactory?.Create();
        await using var _ = recorder;

        TestArtifact? artifact = null;
        var baseStateRestored = false;

        try
        {
            // Connect runners
            _logger.Info("[Test] Connecting Foreground (GM) runner...");
            await fg.ConnectAsync(context.Server, cancellationToken);
            _logger.Info("[Test] Connecting Background runner...");
            await bg.ConnectAsync(context.Server, cancellationToken);

            if (_initialDesiredState is not null)
            {
                _logger.Info($"[Test] Applying initial desired server state '{_initialDesiredState.Name}'...");
                await _initialDesiredState.ApplyAsync(fg, context, cancellationToken).ConfigureAwait(false);
            }

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

            if (_baseDesiredState is not null)
            {
                _logger.Info($"[Test] Restoring base server state '{_baseDesiredState.Name}'...");
                try
                {
                    await _baseDesiredState.ApplyAsync(fg, context, cancellationToken).ConfigureAwait(false);
                    baseStateRestored = true;
                }
                catch
                {
                    baseStateRestored = false;
                    throw;
                }
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
                artifact = await recorder.MoveLastRecordingAsync(context.TestRunDirectory, context.SanitizedTestName, cancellationToken);
            }

            // Shut down foreground UI
            _logger.Info("[Test] Shutting down Foreground UI...");
            await fg.ShutdownUiAsync(cancellationToken);

            var successMsg = $"Test '{Name}' executed successfully.";
            _logger.Info(successMsg);
            return new OrchestrationResult(true, successMsg, artifact, context.TestRunDirectory);
        }
        catch (OperationCanceledException)
        {
            var msg = "Test execution canceled.";
            _logger.Warn(msg);
            return new OrchestrationResult(false, msg, artifact, context.TestRunDirectory);
        }
        catch (Exception ex)
        {
            var msg = $"Test execution failed: {ex.Message}";
            _logger.Error(msg, ex);
            return new OrchestrationResult(false, msg, artifact, context.TestRunDirectory);
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
}
