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
    private readonly IBotRunnerFactory? _foregroundFactory;
    private readonly IBotRunnerFactory? _backgroundFactory;
    private readonly IScreenRecorderFactory? _recorderFactory;
    private readonly IServerDesiredState? _initialDesiredState;
    private readonly IServerDesiredState? _baseDesiredState;
    private readonly OrchestrationOptions _options;
    private readonly ITestLogger _logger;
    private readonly IRecordedTestContext? _providedContext;
    private readonly IServerDesiredState? _desiredState;

    // Direct runner instances for the second constructor
    private readonly Func<IBotRunner>? _createForegroundRunner;
    private readonly Func<IBotRunner>? _createBackgroundRunner;
    private readonly Func<IScreenRecorder?>? _createRecorder;

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
            // Use provided context or the one passed in
            var effectiveContext = _providedContext ?? context;

            Directory.CreateDirectory(effectiveContext.TestRunDirectory);

            // Create runners either from factories or from stored functions
            IBotRunner fg;
            IBotRunner bg;
            IScreenRecorder? recorder;

            if (_createForegroundRunner != null && _createBackgroundRunner != null)
            {
                fg = _createForegroundRunner();
                bg = _createBackgroundRunner();
                recorder = _createRecorder?.Invoke();
            }
            else
            {
                fg = _foregroundFactory!.Create();
                bg = _backgroundFactory!.Create();
                recorder = _recorderFactory?.Create();
            }

            await using var fgDisposable = fg;
            await using var bgDisposable = bg;
            await using var _ = recorder;

            TestArtifact? artifact = null;
            var baseStateRestored = false;

            try
            {
                // Connect runners
                _logger.Info("[Test] Connecting Foreground (GM) runner...");
                await fg.ConnectAsync(effectiveContext.Server, cancellationToken);
                _logger.Info("[Test] Connecting Background runner...");
                await bg.ConnectAsync(effectiveContext.Server, cancellationToken);

                if (_initialDesiredState is not null)
                {
                    _logger.Info($"[Test] Applying initial desired server state '{_initialDesiredState.Name}'...");
                    await _initialDesiredState.ApplyAsync(fg, effectiveContext, cancellationToken).ConfigureAwait(false);
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
                    await _desiredState.ApplyAsync(fg, effectiveContext, cancellationToken);
                }
                else
                {
                    await fg.PrepareServerStateAsync(effectiveContext, cancellationToken);
                }

                // Start recording before background test
                if (recorder != null)
                {
                    _logger.Info("[Test] Starting recording...");
                    await recorder.StartAsync(effectiveContext, cancellationToken);
                }

                // Execute the test using Background runner
                _logger.Info("[Test] Running test with Background runner...");
                await bg.RunTestAsync(effectiveContext, cancellationToken);

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
                    await _desiredState.RevertAsync(fg, effectiveContext, cancellationToken);
                }
                else
                {
                    await fg.ResetServerStateAsync(effectiveContext, cancellationToken);
                }

                if (_baseDesiredState is not null)
                {
                    _logger.Info($"[Test] Restoring base server state '{_baseDesiredState.Name}'...");
                    try
                    {
                        await _baseDesiredState.ApplyAsync(fg, effectiveContext, cancellationToken).ConfigureAwait(false);
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
                    artifact = await recorder.MoveLastRecordingAsync(effectiveContext.TestRunDirectory, effectiveContext.SanitizedTestName, cancellationToken);
                }

                // Shut down foreground UI
                _logger.Info("[Test] Shutting down Foreground UI...");
                await fg.ShutdownUiAsync(cancellationToken);

                var successMsg = $"Test '{Name}' executed successfully.";
                _logger.Info(successMsg);
                return new OrchestrationResult(true, successMsg, artifact, effectiveContext.TestRunDirectory);
            }
            catch (OperationCanceledException)
            {
                var msg = "Test execution canceled.";
                _logger.Warn(msg);
                return new OrchestrationResult(false, msg, artifact, effectiveContext.TestRunDirectory);
            }
            catch (Exception ex)
            {
                var msg = $"Test execution failed: {ex.Message}";
                _logger.Error(msg, ex);
                return new OrchestrationResult(false, msg, artifact, effectiveContext.TestRunDirectory);
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
                        await _desiredState.RevertAsync(fg, effectiveContext, CancellationToken.None);
                    }
                    else
                    {
                        await fg.ResetServerStateAsync(effectiveContext, CancellationToken.None);
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
