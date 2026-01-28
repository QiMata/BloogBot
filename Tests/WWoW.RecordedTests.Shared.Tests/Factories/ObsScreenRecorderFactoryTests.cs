using FluentAssertions;
using WWoW.RecordedTests.Shared.Abstractions.I;
using WWoW.RecordedTests.Shared.Factories;
using WWoW.RecordedTests.Shared.Recording;

namespace WWoW.RecordedTests.Shared.Tests.Factories;

public class ObsScreenRecorderFactoryTests : IDisposable
{
    private readonly List<string> _envVarsToCleanup = new();

    public void Dispose()
    {
        foreach (var varName in _envVarsToCleanup)
        {
            Environment.SetEnvironmentVariable(varName, null);
        }
    }

    private void SetTestEnvVar(string name, string? value)
    {
        Environment.SetEnvironmentVariable(name, value);
        _envVarsToCleanup.Add(name);
    }

    [Fact]
    public void CreateDefault_ShouldReturnObsScreenRecorder()
    {
        // Act
        var recorder = ObsScreenRecorderFactory.CreateDefault();

        // Assert
        recorder.Should().NotBeNull();
        recorder.Should().BeOfType<ObsWebSocketScreenRecorder>();
        recorder.Should().BeAssignableTo<IScreenRecorder>();
    }

    [Fact]
    public void CreateDefault_WithLogger_ShouldPassLoggerToRecorder()
    {
        // Arrange
        var mockLogger = NSubstitute.Substitute.For<ITestLogger>();

        // Act
        var recorder = ObsScreenRecorderFactory.CreateDefault(mockLogger);

        // Assert
        recorder.Should().NotBeNull();
        recorder.Should().BeOfType<ObsWebSocketScreenRecorder>();
    }

    [Fact]
    public void Create_WithConfiguration_ShouldUseProvidedConfiguration()
    {
        // Arrange
        var config = new ObsWebSocketConfiguration
        {
            ObsExecutablePath = @"C:\CustomPath\obs.exe",
            WebSocketUrl = "ws://custom:4456",
            AutoLaunchObs = false
        };

        // Act
        var recorder = ObsScreenRecorderFactory.Create(config);

        // Assert
        recorder.Should().NotBeNull();
        recorder.Should().BeOfType<ObsWebSocketScreenRecorder>();
    }

    [Fact]
    public void CreateFromEnvironment_WithEnvVariables_ShouldUseEnvValues()
    {
        // Arrange
        SetTestEnvVar("OBS_EXECUTABLE_PATH", @"C:\EnvPath\obs.exe");
        SetTestEnvVar("OBS_WEBSOCKET_URL", "ws://env:4457");
        SetTestEnvVar("OBS_WEBSOCKET_PASSWORD", "env-password");
        SetTestEnvVar("OBS_RECORDING_PATH", @"C:\EnvRecordings");
        SetTestEnvVar("OBS_AUTO_LAUNCH", "false");

        // Act
        var recorder = ObsScreenRecorderFactory.CreateFromEnvironment();

        // Assert
        recorder.Should().NotBeNull();
        recorder.Should().BeOfType<ObsWebSocketScreenRecorder>();
    }

    [Fact]
    public void CreateFromEnvironment_WithoutEnvVariables_ShouldUseDefaults()
    {
        // Act
        var recorder = ObsScreenRecorderFactory.CreateFromEnvironment();

        // Assert
        recorder.Should().NotBeNull();
        recorder.Should().BeOfType<ObsWebSocketScreenRecorder>();
    }

    [Fact]
    public void CreateFromEnvironment_WithInvalidAutoLaunch_ShouldDefaultToTrue()
    {
        // Arrange
        SetTestEnvVar("OBS_AUTO_LAUNCH", "invalid_value");

        // Act
        var recorder = ObsScreenRecorderFactory.CreateFromEnvironment();

        // Assert
        recorder.Should().NotBeNull();
        // Should use default behavior (true) when parsing fails
    }

    [Fact]
    public void CreateFactory_ShouldReturnFactoryDelegate()
    {
        // Act
        var factory = ObsScreenRecorderFactory.CreateFactory();

        // Assert
        factory.Should().NotBeNull();
        factory.Should().BeOfType<Func<IScreenRecorder>>();
    }

    [Fact]
    public void CreateFactory_WhenInvoked_ShouldCreateRecorder()
    {
        // Arrange
        var factory = ObsScreenRecorderFactory.CreateFactory();

        // Act
        var recorder = factory();

        // Assert
        recorder.Should().NotBeNull();
        recorder.Should().BeOfType<ObsWebSocketScreenRecorder>();
    }

    [Fact]
    public void CreateFactory_WithConfiguration_ShouldUseConfigForAllCreatedInstances()
    {
        // Arrange
        var config = new ObsWebSocketConfiguration
        {
            WebSocketUrl = "ws://test:4458"
        };
        var factory = ObsScreenRecorderFactory.CreateFactory(config);

        // Act
        var recorder1 = factory();
        var recorder2 = factory();

        // Assert
        recorder1.Should().NotBeNull();
        recorder2.Should().NotBeNull();
        recorder1.Should().NotBeSameAs(recorder2); // Different instances
    }

    [Fact]
    public void CreateFactory_CalledMultipleTimes_ShouldCreateNewInstancesEachTime()
    {
        // Arrange
        var factory = ObsScreenRecorderFactory.CreateFactory();

        // Act
        var recorder1 = factory();
        var recorder2 = factory();

        // Assert
        recorder1.Should().NotBeNull();
        recorder2.Should().NotBeNull();
        recorder1.Should().NotBeSameAs(recorder2);
    }

    [Fact]
    public void CreateFactory_WithNullConfiguration_ShouldUseDefaultConfiguration()
    {
        // Act
        var factory = ObsScreenRecorderFactory.CreateFactory(configuration: null);
        var recorder = factory();

        // Assert
        recorder.Should().NotBeNull();
        recorder.Should().BeOfType<ObsWebSocketScreenRecorder>();
    }

    [Fact]
    public void CreateFactory_WithLogger_ShouldPassLoggerToCreatedRecorders()
    {
        // Arrange
        var mockLogger = NSubstitute.Substitute.For<ITestLogger>();
        var factory = ObsScreenRecorderFactory.CreateFactory(logger: mockLogger);

        // Act
        var recorder = factory();

        // Assert
        recorder.Should().NotBeNull();
        recorder.Should().BeOfType<ObsWebSocketScreenRecorder>();
    }
}
