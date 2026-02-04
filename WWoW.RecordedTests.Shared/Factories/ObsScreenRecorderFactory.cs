using WWoW.RecordedTests.Shared.Abstractions.I;
using WWoW.RecordedTests.Shared.Recording;

namespace WWoW.RecordedTests.Shared.Factories;

/// <summary>
/// Factory for creating OBS WebSocket-based screen recorders.
/// </summary>
public static class ObsScreenRecorderFactory
{
    /// <summary>
    /// Creates an OBS screen recorder with default configuration.
    /// </summary>
    /// <param name="logger">Optional logger for recorder operations.</param>
    /// <returns>A configured OBS screen recorder instance.</returns>
    public static IScreenRecorder CreateDefault(ITestLogger? logger = null)
    {
        return new ObsWebSocketScreenRecorder(new ObsWebSocketConfiguration(), logger);
    }

    /// <summary>
    /// Creates an OBS screen recorder with custom configuration.
    /// </summary>
    /// <param name="configuration">OBS WebSocket configuration.</param>
    /// <param name="logger">Optional logger for recorder operations.</param>
    /// <returns>A configured OBS screen recorder instance.</returns>
    public static IScreenRecorder Create(
        ObsWebSocketConfiguration configuration,
        ITestLogger? logger = null)
    {
        return new ObsWebSocketScreenRecorder(configuration, logger);
    }

    /// <summary>
    /// Creates an OBS screen recorder with configuration from environment variables.
    /// </summary>
    /// <param name="logger">Optional logger for recorder operations.</param>
    /// <returns>A configured OBS screen recorder instance.</returns>
    public static IScreenRecorder CreateFromEnvironment(ITestLogger? logger = null)
    {
        var config = new ObsWebSocketConfiguration
        {
            ObsExecutablePath = Environment.GetEnvironmentVariable("OBS_EXECUTABLE_PATH")
                ?? new ObsWebSocketConfiguration().ObsExecutablePath,
            WebSocketUrl = Environment.GetEnvironmentVariable("OBS_WEBSOCKET_URL")
                ?? "ws://localhost:4455",
            WebSocketPassword = Environment.GetEnvironmentVariable("OBS_WEBSOCKET_PASSWORD"),
            RecordingOutputPath = Environment.GetEnvironmentVariable("OBS_RECORDING_PATH")
                ?? Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
            AutoLaunchObs = !bool.TryParse(
                Environment.GetEnvironmentVariable("OBS_AUTO_LAUNCH"),
                out var autoLaunch) || autoLaunch
        };

        return new ObsWebSocketScreenRecorder(config, logger);
    }

    /// <summary>
    /// Creates a factory delegate that produces OBS recorders.
    /// </summary>
    /// <param name="configuration">OBS configuration to use for all created recorders.</param>
    /// <param name="logger">Optional logger for recorder operations.</param>
    /// <returns>A factory delegate.</returns>
    public static Func<IScreenRecorder> CreateFactory(
        ObsWebSocketConfiguration? configuration = null,
        ITestLogger? logger = null)
    {
        configuration ??= new ObsWebSocketConfiguration();
        return () => new ObsWebSocketScreenRecorder(configuration, logger);
    }
}
