using FluentAssertions;
using RecordedTests.PathingTests.Configuration;
using RecordedTests.Shared.Abstractions;

namespace RecordedTests.PathingTests.Tests;

public class ProgramTests
{
    [Fact]
    public void ParseConfiguration_TrueNasApiFlag_PopulatesConfiguration()
    {
        // Arrange
        var args = new[] { "--truenas-api", "https://truenas.local/" };

        // Act
        var config = ConfigurationParser.Parse(args);

        // Assert
        config.Should().NotBeNull();
        config.TrueNasApiUrl.Should().Be("https://truenas.local/");
    }

    [Fact]
    public void ParseConfiguration_TrueNasApiKeyFlag_PopulatesConfiguration()
    {
        // Arrange
        var args = new[] { "--truenas-api-key", "test-api-key-12345" };

        // Act
        var config = ConfigurationParser.Parse(args);

        // Assert
        config.Should().NotBeNull();
        config.TrueNasApiKey.Should().Be("test-api-key-12345");
    }

    [Fact]
    public void ParseConfiguration_ServerHostAndPort_PopulatesServerInfo()
    {
        // Arrange
        var args = new[] { "--server-host", "192.168.1.100", "--server-port", "3725" };

        // Act
        var config = ConfigurationParser.Parse(args);

        // Assert
        config.Should().NotBeNull();
        config.ServerInfo.Host.Should().Be("192.168.1.100");
        config.ServerInfo.Port.Should().Be(3725);
    }

    [Fact]
    public void ParseConfiguration_ArtifactsRootFlag_PopulatesOrchestrationOptions()
    {
        // Arrange
        var args = new[] { "--artifacts-root", "/custom/artifacts" };

        // Act
        var config = ConfigurationParser.Parse(args);

        // Assert
        config.Should().NotBeNull();
        config.OrchestrationOptions.Should().NotBeNull();
        config.OrchestrationOptions.ArtifactsRootDirectory.Should().Be("/custom/artifacts");
    }

    [Fact]
    public void ParseConfiguration_ServerTimeoutFlag_PopulatesOrchestrationOptions()
    {
        // Arrange
        var args = new[] { "--server-timeout", "10" };

        // Act
        var config = ConfigurationParser.Parse(args);

        // Assert
        config.Should().NotBeNull();
        config.OrchestrationOptions.Should().NotBeNull();
        config.OrchestrationOptions.ServerAvailabilityTimeout.Should().Be(TimeSpan.FromMinutes(10));
    }

    [Fact]
    public void ParseConfiguration_DoubleStopRecorderForSafety_IsAlwaysTrue()
    {
        // Arrange
        var args = Array.Empty<string>();

        // Act
        var config = ConfigurationParser.Parse(args);

        // Assert - DoubleStopRecorderForSafety is hardcoded to true in TestConfiguration
        config.Should().NotBeNull();
        config.OrchestrationOptions.Should().NotBeNull();
        config.OrchestrationOptions.DoubleStopRecorderForSafety.Should().BeTrue();
    }

    [Fact]
    public void ParseConfiguration_MultipleFlags_PopulatesAllCorrectly()
    {
        // Arrange
        var args = new[]
        {
            "--truenas-api", "https://truenas.local/",
            "--truenas-api-key", "my-key",
            "--server-host", "localhost",
            "--server-port", "3724",
            "--artifacts-root", "/test/artifacts",
            "--server-timeout", "15",
            "--gm-account", "admin",
            "--gm-password", "adminpass",
            "--gm-character", "GmChar",
            "--test-account", "testuser",
            "--test-password", "testpass",
            "--test-character", "TestChar"
        };

        // Act
        var config = ConfigurationParser.Parse(args);

        // Assert
        config.Should().NotBeNull();
        config.TrueNasApiUrl.Should().Be("https://truenas.local/");
        config.TrueNasApiKey.Should().Be("my-key");
        config.ServerInfo.Host.Should().Be("localhost");
        config.ServerInfo.Port.Should().Be(3724);
        config.OrchestrationOptions.ArtifactsRootDirectory.Should().Be("/test/artifacts");
        config.OrchestrationOptions.ServerAvailabilityTimeout.Should().Be(TimeSpan.FromMinutes(15));
        config.GmAccount.Should().Be("admin");
        config.GmPassword.Should().Be("adminpass");
        config.GmCharacter.Should().Be("GmChar");
        config.TestAccount.Should().Be("testuser");
        config.TestPassword.Should().Be("testpass");
        config.TestCharacter.Should().Be("TestChar");
    }

    [Fact]
    public void ParseConfiguration_EmptyArgs_ReturnsDefaultConfiguration()
    {
        // Arrange
        var args = Array.Empty<string>();

        // Act
        var config = ConfigurationParser.Parse(args);

        // Assert
        config.Should().NotBeNull();
        config.TrueNasApiUrl.Should().BeNull();
        config.TrueNasApiKey.Should().BeNull();
        config.ServerInfo.Host.Should().Be("127.0.0.1");
        config.ServerInfo.Port.Should().Be(3724);
        config.OrchestrationOptions.Should().NotBeNull();
    }

    [Fact]
    public void ParseConfiguration_PathfindingServiceFlags_PopulatesConfiguration()
    {
        // Arrange
        var args = new[] { "--pathfinding-ip", "10.0.0.5", "--pathfinding-port", "6000" };

        // Act
        var config = ConfigurationParser.Parse(args);

        // Assert
        config.Should().NotBeNull();
        config.PathfindingServiceIp.Should().Be("10.0.0.5");
        config.PathfindingServicePort.Should().Be(6000);
    }

    [Fact]
    public void ParseConfiguration_ObsFlags_PopulatesConfiguration()
    {
        // Arrange
        var args = new[]
        {
            "--obs-executable", @"C:\OBS\obs64.exe",
            "--obs-websocket-url", "ws://localhost:4455",
            "--obs-password", "secret",
            "--obs-recording-path", @"C:\Recordings"
        };

        // Act
        var config = ConfigurationParser.Parse(args);

        // Assert
        config.Should().NotBeNull();
        config.ObsExecutablePath.Should().Be(@"C:\OBS\obs64.exe");
        config.ObsWebSocketUrl.Should().Be("ws://localhost:4455");
        config.ObsWebSocketPassword.Should().Be("secret");
        config.ObsRecordingPath.Should().Be(@"C:\Recordings");
    }

    [Fact]
    public void ParseConfiguration_TestFilterFlag_PopulatesConfiguration()
    {
        // Arrange
        var args = new[] { "--test-filter", "StoneTalon" };

        // Act
        var config = ConfigurationParser.Parse(args);

        // Assert
        config.Should().NotBeNull();
        config.TestFilter.Should().Be("StoneTalon");
    }

    [Fact]
    public void ParseConfiguration_CategoryFilterFlag_PopulatesConfiguration()
    {
        // Arrange
        var args = new[] { "--category", "CrossContinent" };

        // Act
        var config = ConfigurationParser.Parse(args);

        // Assert
        config.Should().NotBeNull();
        config.CategoryFilter.Should().Be("CrossContinent");
    }

    [Fact]
    public void ParseConfiguration_DisableRecordingFlag_DisablesRecording()
    {
        // Arrange - CLI flags require explicit value with Microsoft.Extensions.Configuration.CommandLine
        var args = new[] { "--disable-recording", "true" };

        // Act
        var config = ConfigurationParser.Parse(args);

        // Assert
        config.Should().NotBeNull();
        config.EnableRecording.Should().BeFalse();
    }

    [Fact]
    public void ParseConfiguration_DisableRecordingOverridesEnable_WhenBothSet()
    {
        // Arrange - both flags set with explicit values, disable should take precedence
        var args = new[] { "--enable-recording", "true", "--disable-recording", "true" };

        // Act
        var config = ConfigurationParser.Parse(args);

        // Assert
        config.Should().NotBeNull();
        config.EnableRecording.Should().BeFalse();
    }

    [Fact]
    public void ParseConfiguration_WowWindowTitleFlag_PopulatesConfiguration()
    {
        // Arrange
        var args = new[] { "--wow-window-title", "World of Warcraft" };

        // Act
        var config = ConfigurationParser.Parse(args);

        // Assert
        config.Should().NotBeNull();
        config.WowWindowTitle.Should().Be("World of Warcraft");
    }

    [Fact]
    public void ParseConfiguration_WowWindowHandleFlag_PopulatesConfiguration()
    {
        // Arrange
        var args = new[] { "--wow-window-handle", "0x12345678" };

        // Act
        var config = ConfigurationParser.Parse(args);

        // Assert
        config.Should().NotBeNull();
        config.WowWindowHandle.Should().NotBeNull();
    }

    [Fact]
    public void ParseConfiguration_StopOnFailureFlag_PopulatesConfiguration()
    {
        // Arrange
        var args = new[] { "--stop-on-failure", "true" };

        // Act
        var config = ConfigurationParser.Parse(args);

        // Assert
        config.Should().NotBeNull();
        config.StopOnFirstFailure.Should().BeTrue();
    }

    [Fact]
    public void Validate_MissingGmAccount_ThrowsValidationError()
    {
        // Arrange
        var config = new TestConfiguration
        {
            GmAccount = "",
            GmPassword = "pass",
            GmCharacter = "char",
            TestAccount = "test",
            TestPassword = "testpass",
            TestCharacter = "testchar",
            EnableRecording = false
        };

        // Act
        var act = () => config.Validate();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*GM account is required*");
    }

    [Fact]
    public void Validate_RecordingEnabledWithoutObsConfig_ThrowsValidationError()
    {
        // Arrange
        var config = new TestConfiguration
        {
            GmAccount = "admin",
            GmPassword = "pass",
            GmCharacter = "char",
            TestAccount = "test",
            TestPassword = "testpass",
            TestCharacter = "testchar",
            EnableRecording = true,
            WowWindowTitle = "WoW",
            ObsExecutablePath = null, // Missing OBS config
            ObsWebSocketUrl = "", // Empty
            ObsRecordingPath = null
        };

        // Act
        var act = () => config.Validate();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*OBS*required when recording is enabled*");
    }

    [Fact]
    public void Validate_RecordingDisabled_DoesNotRequireObsConfig()
    {
        // Arrange
        var config = new TestConfiguration
        {
            GmAccount = "admin",
            GmPassword = "pass",
            GmCharacter = "char",
            TestAccount = "test",
            TestPassword = "testpass",
            TestCharacter = "testchar",
            EnableRecording = false // Recording disabled
        };

        // Act
        var act = () => config.Validate();

        // Assert - should not throw (no OBS config required when recording is disabled)
        act.Should().NotThrow();
    }

    [Fact]
    public void OrchestrationOptions_ReflectsArtifactsRoot()
    {
        // Arrange
        var config = new TestConfiguration
        {
            ArtifactsRoot = "/my/custom/path"
        };

        // Act
        var options = config.OrchestrationOptions;

        // Assert
        options.ArtifactsRootDirectory.Should().Be("/my/custom/path");
    }

    [Fact]
    public void OrchestrationOptions_ReflectsServerTimeout()
    {
        // Arrange
        var config = new TestConfiguration
        {
            ServerTimeout = TimeSpan.FromMinutes(20)
        };

        // Act
        var options = config.OrchestrationOptions;

        // Assert
        options.ServerAvailabilityTimeout.Should().Be(TimeSpan.FromMinutes(20));
    }
}
