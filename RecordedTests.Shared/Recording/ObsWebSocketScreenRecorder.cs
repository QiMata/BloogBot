using System.Diagnostics;
using OBSWebsocketDotNet;
using OBSWebsocketDotNet.Types;
using RecordedTests.Shared.Abstractions;
using RecordedTests.Shared.Abstractions.I;

namespace RecordedTests.Shared.Recording;

/// <summary>
/// OBS-backed screen recorder that uses obs-websocket protocol for remote control.
/// Requires OBS Studio with obs-websocket plugin (v5.x) to be installed.
/// </summary>
public sealed class ObsWebSocketScreenRecorder : IScreenRecorder
{
    private readonly ObsWebSocketConfiguration _configuration;
    private readonly ITestLogger? _logger;
    private readonly OBSWebsocket _obs;
    private Process? _obsProcess;
    private bool _isRecording;
    private string? _lastRecordingPath;
    private bool _isConnected;

    public ObsWebSocketScreenRecorder(
        ObsWebSocketConfiguration configuration,
        ITestLogger? logger = null)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger;
        _obs = new OBSWebsocket();

        // Subscribe to events for logging
        _obs.Connected += (_, _) =>
        {
            _isConnected = true;
            _logger?.Info("OBS WebSocket connected");
        };

        _obs.Disconnected += (_, args) =>
        {
            _isConnected = false;
            _logger?.Info($"OBS WebSocket disconnected: {args.DisconnectReason}");
        };

        _obs.RecordStateChanged += (_, args) =>
        {
            _logger?.Info($"OBS recording state changed: {args.OutputState.State}");
            if (args.OutputState.State == OutputState.OBS_WEBSOCKET_OUTPUT_STOPPED)
            {
                _lastRecordingPath = args.OutputState.OutputPath;
                _isRecording = false;
            }
            else if (args.OutputState.State == OutputState.OBS_WEBSOCKET_OUTPUT_STARTED)
            {
                _isRecording = true;
            }
        };
    }

    public async Task LaunchAsync(CancellationToken cancellationToken)
    {
        _logger?.Info($"Launching OBS from path: {_configuration.ObsExecutablePath}");

        if (_configuration.AutoLaunchObs)
        {
            if (!File.Exists(_configuration.ObsExecutablePath))
            {
                throw new FileNotFoundException(
                    $"OBS executable not found at: {_configuration.ObsExecutablePath}. " +
                    "Ensure OBS Studio is installed or disable recording with --disable-recording.");
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = _configuration.ObsExecutablePath,
                Arguments = _configuration.ObsLaunchArguments,
                UseShellExecute = false,
                CreateNoWindow = false
            };

            _obsProcess = Process.Start(startInfo);

            if (_obsProcess == null)
            {
                throw new InvalidOperationException("Failed to launch OBS process");
            }

            _logger?.Info($"OBS process started with PID: {_obsProcess.Id}");

            // Wait for OBS to initialize
            await Task.Delay(_configuration.ObsStartupDelay, cancellationToken);
        }

        // Connect to OBS WebSocket
        _logger?.Info($"Connecting to OBS WebSocket at {_configuration.WebSocketUrl}");

        try
        {
            // Extract host and port from URL (ws://host:port or wss://host:port)
            var uri = new Uri(_configuration.WebSocketUrl);
            var host = uri.Host;
            var port = uri.Port > 0 ? uri.Port : 4455;

            // Connect with timeout
            var connectionTimeout = TimeSpan.FromSeconds(10);
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(connectionTimeout);

            var connectTask = Task.Run(() =>
            {
                _obs.ConnectAsync($"ws://{host}:{port}", _configuration.WebSocketPassword ?? string.Empty);
            }, cts.Token);

            await connectTask;

            // Wait for connection to be established
            var waitTime = TimeSpan.Zero;
            var maxWait = connectionTimeout;
            while (!_isConnected && waitTime < maxWait)
            {
                await Task.Delay(100, cancellationToken);
                waitTime += TimeSpan.FromMilliseconds(100);
            }

            if (!_isConnected)
            {
                throw new InvalidOperationException(
                    $"Failed to connect to OBS WebSocket at {_configuration.WebSocketUrl} within {connectionTimeout.TotalSeconds}s. " +
                    "Ensure OBS is running with WebSocket server enabled (Tools -> obs-websocket Settings).");
            }

            _logger?.Info("Connected to OBS WebSocket successfully");
        }
        catch (AuthFailureException ex)
        {
            throw new InvalidOperationException(
                $"OBS WebSocket authentication failed. Check your password. Details: {ex.Message}", ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new InvalidOperationException(
                $"Failed to connect to OBS WebSocket: {ex.Message}. " +
                "Ensure OBS is running with WebSocket server enabled.", ex);
        }
    }

    public async Task ConfigureTargetAsync(RecordingTarget target, CancellationToken cancellationToken)
    {
        _logger?.Info($"Configuring OBS capture source for target type: {target.TargetType}");

        if (!_isConnected)
        {
            throw new InvalidOperationException("Not connected to OBS WebSocket. Call LaunchAsync first.");
        }

        // Get current scene
        var currentScene = _obs.GetCurrentProgramScene();
        _logger?.Info($"Current OBS scene: {currentScene}");

        // For now, we assume the user has configured OBS with the appropriate capture source
        // A more advanced implementation would programmatically create/configure sources
        // based on the target type (window capture, game capture, etc.)

        switch (target.TargetType)
        {
            case RecordingTargetType.WindowByTitle:
                _logger?.Info($"  Target: Window by title '{target.WindowTitle}'");
                _logger?.Info("  Note: Ensure OBS has a Window Capture source configured for this window");
                break;
            case RecordingTargetType.ProcessId:
                _logger?.Info($"  Target: Process ID {target.ProcessId}");
                _logger?.Info("  Note: Ensure OBS has a Game Capture or Window Capture source configured");
                break;
            case RecordingTargetType.WindowHandle:
                _logger?.Info($"  Target: Window handle {target.WindowHandle}");
                _logger?.Info("  Note: Ensure OBS has a Window Capture source configured");
                break;
            case RecordingTargetType.Screen:
                _logger?.Info($"  Target: Screen index {target.ScreenIndex ?? 0}");
                _logger?.Info("  Note: Ensure OBS has a Display Capture source configured");
                break;
        }

        await Task.Delay(200, cancellationToken);
        _logger?.Info("OBS capture target acknowledged");
    }

    public async Task StartAsync(IRecordedTestContext context, CancellationToken cancellationToken)
    {
        _logger?.Info($"Starting OBS recording for test: {context.TestName}");

        if (!_isConnected)
        {
            throw new InvalidOperationException("Not connected to OBS WebSocket. Call LaunchAsync first.");
        }

        if (_isRecording)
        {
            _logger?.Warn("Recording already in progress, stopping first");
            await StopAsync(cancellationToken);
            await Task.Delay(500, cancellationToken); // Brief delay between stop and start
        }

        try
        {
            _obs.StartRecord();
            _isRecording = true;
            _logger?.Info("OBS recording started");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to start OBS recording: {ex.Message}", ex);
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (!_isRecording)
        {
            _logger?.Info("No recording in progress, skip stop");
            return;
        }

        if (!_isConnected)
        {
            _logger?.Warn("Not connected to OBS WebSocket, cannot stop recording properly");
            _isRecording = false;
            return;
        }

        _logger?.Info("Stopping OBS recording");

        try
        {
            // StopRecord returns the output path
            var outputPath = _obs.StopRecord();
            _lastRecordingPath = outputPath;
            _isRecording = false;

            _logger?.Info($"OBS recording stopped, saved to: {_lastRecordingPath}");
        }
        catch (Exception ex)
        {
            _logger?.Error($"Error stopping OBS recording: {ex.Message}", ex);
            _isRecording = false;
            // Note: In case of error, _lastRecordingPath may be set by the RecordStateChanged event
        }

        await Task.CompletedTask;
    }

    public async Task<TestArtifact> MoveLastRecordingAsync(
        string destinationDirectory,
        string desiredFileNameWithoutExtension,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_lastRecordingPath))
        {
            throw new InvalidOperationException("No recording available to move. Did you call StopAsync?");
        }

        _logger?.Info($"Moving recording from {_lastRecordingPath} to {destinationDirectory}");

        // Wait for OBS to finish writing the file
        await Task.Delay(_configuration.PostRecordingDelay, cancellationToken);

        // Determine source file extension
        var sourceExtension = Path.GetExtension(_lastRecordingPath);
        var destinationFileName = $"{desiredFileNameWithoutExtension}{sourceExtension}";
        var destinationPath = Path.Combine(destinationDirectory, destinationFileName);

        // Ensure destination directory exists
        Directory.CreateDirectory(destinationDirectory);

        // Wait for file to be available (OBS might still be writing)
        var retries = 0;
        const int maxRetries = 30;
        while (!File.Exists(_lastRecordingPath) && retries < maxRetries)
        {
            _logger?.Info($"Waiting for recording file to be available (attempt {retries + 1}/{maxRetries})");
            await Task.Delay(1000, cancellationToken);
            retries++;
        }

        if (!File.Exists(_lastRecordingPath))
        {
            throw new FileNotFoundException(
                $"Recording file not found after {maxRetries} attempts: {_lastRecordingPath}");
        }

        // Wait until file is not locked (OBS releases it)
        retries = 0;
        while (retries < maxRetries)
        {
            try
            {
                using var fs = new FileStream(_lastRecordingPath, FileMode.Open, FileAccess.Read, FileShare.None);
                break; // File is accessible
            }
            catch (IOException)
            {
                _logger?.Info($"Recording file still locked, waiting... (attempt {retries + 1}/{maxRetries})");
                await Task.Delay(1000, cancellationToken);
                retries++;
            }
        }

        // Move the file
        File.Move(_lastRecordingPath, destinationPath, overwrite: true);
        _logger?.Info($"Recording moved to: {destinationPath}");

        return new TestArtifact(destinationFileName, destinationPath);
    }

    public async ValueTask DisposeAsync()
    {
        _logger?.Info("Disposing OBS screen recorder");

        try
        {
            if (_isRecording)
            {
                await StopAsync(CancellationToken.None);
            }
        }
        catch (Exception ex)
        {
            _logger?.Error("Error stopping recording during dispose", ex);
        }

        // Disconnect from WebSocket
        try
        {
            if (_isConnected)
            {
                _obs.Disconnect();
                _isConnected = false;
            }
        }
        catch (Exception ex)
        {
            _logger?.Error("Error disconnecting from OBS WebSocket", ex);
        }

        if (_configuration.AutoLaunchObs && _obsProcess != null)
        {
            try
            {
                if (!_obsProcess.HasExited)
                {
                    _logger?.Info("Closing OBS process");
                    _obsProcess.Kill(entireProcessTree: true);
                    await _obsProcess.WaitForExitAsync();
                }

                _obsProcess.Dispose();
            }
            catch (Exception ex)
            {
                _logger?.Error("Error closing OBS process", ex);
            }
        }
    }
}

/// <summary>
/// Configuration for OBS WebSocket screen recorder.
/// </summary>
public sealed class ObsWebSocketConfiguration
{
    /// <summary>
    /// Path to OBS Studio executable. Default: C:\Program Files\obs-studio\bin\64bit\obs64.exe
    /// </summary>
    public string ObsExecutablePath { get; init; } = @"C:\Program Files\obs-studio\bin\64bit\obs64.exe";

    /// <summary>
    /// Command-line arguments to pass to OBS on launch.
    /// </summary>
    public string ObsLaunchArguments { get; init; } = "--minimize-to-tray";

    /// <summary>
    /// Whether to automatically launch OBS if not running. Default: true.
    /// </summary>
    public bool AutoLaunchObs { get; init; } = true;

    /// <summary>
    /// OBS WebSocket server URL. Default: ws://localhost:4455
    /// </summary>
    public string WebSocketUrl { get; init; } = "ws://localhost:4455";

    /// <summary>
    /// OBS WebSocket password. Leave null or empty if no password is set.
    /// </summary>
    public string? WebSocketPassword { get; init; }

    /// <summary>
    /// Directory where OBS saves recordings. Should match OBS settings.
    /// </summary>
    public string RecordingOutputPath { get; init; } = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);

    /// <summary>
    /// Delay after launching OBS before attempting WebSocket connection.
    /// </summary>
    public TimeSpan ObsStartupDelay { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Delay after stopping recording before moving the file.
    /// OBS needs time to finalize the recording.
    /// </summary>
    public TimeSpan PostRecordingDelay { get; init; } = TimeSpan.FromSeconds(3);
}
