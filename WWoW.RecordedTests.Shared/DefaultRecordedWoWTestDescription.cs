namespace WWoW.RecordedTests.Shared;

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using WWoW.RecordedTests.Shared.Abstractions;
using WWoW.RecordedTests.Shared.Abstractions.I;

// A concrete ITestDescription that orchestrates two IBotRunner instances and an optional screen recorder.
public sealed class DefaultRecordedWoWTestDescription : ITestDescription
{
    private readonly Func<IBotRunner> _createForegroundRunner;
    private readonly Func<IBotRunner> _createBackgroundRunner;
    private readonly Func<IScreenRecorder>? _createRecorder;
    private readonly OrchestrationOptions _options;
    private readonly ITestLogger _logger;

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
    }

    public string Name { get; }

    public async Task<OrchestrationResult> ExecuteAsync(ServerInfo server, CancellationToken cancellationToken)
    {
        var context = new RecordedTestContext(Name, server);

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

            // Prepare server state using GM (foreground)
            _logger.Info("[Test] Preparing server state (GM)...");
            await fg.PrepareServerStateAsync(context, cancellationToken);

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

            // Reset server state with GM (foreground)
            _logger.Info("[Test] Resetting server state (GM)...");
            await fg.ResetServerStateAsync(context, cancellationToken);

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
                await fg.ResetServerStateAsync(context, CancellationToken.None);
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
