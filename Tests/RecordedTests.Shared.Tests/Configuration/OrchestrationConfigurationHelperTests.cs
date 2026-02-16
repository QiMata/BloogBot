using FluentAssertions;
using RecordedTests.Shared.Abstractions;
using RecordedTests.Shared.Configuration;
using System;
using System.Collections.Generic;

namespace RecordedTests.Shared.Tests.Configuration;

public class OrchestrationConfigurationHelperTests : IDisposable
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
    public void ResolveOrchestrationOptions_WithCliValues_ShouldUseCliValues()
    {
        // Arrange
        SetTestEnvVar("ARTIFACTS_ROOT", "./EnvArtifacts");
        SetTestEnvVar("SERVER_TIMEOUT_MINUTES", "10");
        SetTestEnvVar("DOUBLE_STOP_RECORDER", "false");

        // Act
        var result = OrchestrationConfigurationHelper.ResolveOrchestrationOptions(
            cliArtifactsRoot: "./CliArtifacts",
            cliServerTimeoutMinutes: 15,
            cliDoubleStopRecorder: true);

        // Assert
        result.ArtifactsRootDirectory.Should().Be("./CliArtifacts");
        result.ServerAvailabilityTimeout.Should().Be(TimeSpan.FromMinutes(15));
        result.DoubleStopRecorderForSafety.Should().BeTrue();
    }

    [Fact]
    public void ResolveOrchestrationOptions_WithEnvVariables_ShouldUseEnvValues()
    {
        // Arrange
        SetTestEnvVar("ARTIFACTS_ROOT", "./EnvArtifacts");
        SetTestEnvVar("SERVER_TIMEOUT_MINUTES", "10");
        SetTestEnvVar("DOUBLE_STOP_RECORDER", "true");

        // Act
        var result = OrchestrationConfigurationHelper.ResolveOrchestrationOptions();

        // Assert
        result.ArtifactsRootDirectory.Should().Be("./EnvArtifacts");
        result.ServerAvailabilityTimeout.Should().Be(TimeSpan.FromMinutes(10));
        result.DoubleStopRecorderForSafety.Should().BeTrue();
    }

    [Fact]
    public void ResolveOrchestrationOptions_WithConfigOptions_ShouldUseConfigValues()
    {
        // Arrange
        var configOptions = new OrchestrationOptions
        {
            ArtifactsRootDirectory = "./ConfigArtifacts",
            ServerAvailabilityTimeout = TimeSpan.FromMinutes(20),
            DoubleStopRecorderForSafety = false
        };

        // Act
        var result = OrchestrationConfigurationHelper.ResolveOrchestrationOptions(
            configOptions: configOptions);

        // Assert
        result.ArtifactsRootDirectory.Should().Be("./ConfigArtifacts");
        result.ServerAvailabilityTimeout.Should().Be(TimeSpan.FromMinutes(20));
        result.DoubleStopRecorderForSafety.Should().BeFalse();
    }

    [Fact]
    public void ResolveOrchestrationOptions_WithNothingSet_ShouldUseDefaults()
    {
        // Act
        var result = OrchestrationConfigurationHelper.ResolveOrchestrationOptions();

        // Assert
        var defaultOptions = new OrchestrationOptions();
        result.ArtifactsRootDirectory.Should().Be(defaultOptions.ArtifactsRootDirectory);
        result.ServerAvailabilityTimeout.Should().Be(defaultOptions.ServerAvailabilityTimeout);
        result.DoubleStopRecorderForSafety.Should().Be(defaultOptions.DoubleStopRecorderForSafety);
    }

    [Fact]
    public void ParseFromCommandLine_WithArtifactsRootArg_ShouldParseCorrectly()
    {
        // Arrange
        var args = new[] { "--artifacts-root", "./TestArtifacts" };

        // Act
        var (artifactsRoot, timeoutMinutes, doubleStop) =
            OrchestrationConfigurationHelper.ParseFromCommandLine(args);

        // Assert
        artifactsRoot.Should().Be("./TestArtifacts");
        timeoutMinutes.Should().BeNull();
        doubleStop.Should().BeNull();
    }

    [Fact]
    public void ParseFromCommandLine_WithServerTimeoutArg_ShouldParseCorrectly()
    {
        // Arrange
        var args = new[] { "--server-timeout", "10" };

        // Act
        var (artifactsRoot, timeoutMinutes, doubleStop) =
            OrchestrationConfigurationHelper.ParseFromCommandLine(args);

        // Assert
        artifactsRoot.Should().BeNull();
        timeoutMinutes.Should().Be(10);
        doubleStop.Should().BeNull();
    }

    [Fact]
    public void ParseFromCommandLine_WithDoubleStopRecorderArg_ShouldParseCorrectly()
    {
        // Arrange
        var args = new[] { "--double-stop-recorder" };

        // Act
        var (artifactsRoot, timeoutMinutes, doubleStop) =
            OrchestrationConfigurationHelper.ParseFromCommandLine(args);

        // Assert
        artifactsRoot.Should().BeNull();
        timeoutMinutes.Should().BeNull();
        doubleStop.Should().BeTrue();
    }

    [Fact]
    public void ParseFromCommandLine_WithNoDoubleStopRecorderArg_ShouldParseCorrectly()
    {
        // Arrange
        var args = new[] { "--no-double-stop-recorder" };

        // Act
        var (artifactsRoot, timeoutMinutes, doubleStop) =
            OrchestrationConfigurationHelper.ParseFromCommandLine(args);

        // Assert
        artifactsRoot.Should().BeNull();
        timeoutMinutes.Should().BeNull();
        doubleStop.Should().BeFalse();
    }

    [Fact]
    public void ParseFromCommandLine_WithAllArgs_ShouldParseAll()
    {
        // Arrange
        var args = new[]
        {
            "--artifacts-root", "./TestArtifacts",
            "--server-timeout", "15",
            "--double-stop-recorder"
        };

        // Act
        var (artifactsRoot, timeoutMinutes, doubleStop) =
            OrchestrationConfigurationHelper.ParseFromCommandLine(args);

        // Assert
        artifactsRoot.Should().Be("./TestArtifacts");
        timeoutMinutes.Should().Be(15);
        doubleStop.Should().BeTrue();
    }

    [Fact]
    public void ParseFromCommandLine_WithInvalidServerTimeout_ShouldReturnNull()
    {
        // Arrange
        var args = new[] { "--server-timeout", "not-a-number" };

        // Act
        var (artifactsRoot, timeoutMinutes, doubleStop) =
            OrchestrationConfigurationHelper.ParseFromCommandLine(args);

        // Assert
        timeoutMinutes.Should().BeNull();
    }

    [Fact]
    public void ParseFromCommandLine_WithMissingArgValue_ShouldNotCrash()
    {
        // Arrange
        var args = new[] { "--artifacts-root" }; // Missing value

        // Act
        var (artifactsRoot, timeoutMinutes, doubleStop) =
            OrchestrationConfigurationHelper.ParseFromCommandLine(args);

        // Assert
        artifactsRoot.Should().BeNull();
    }

    [Fact]
    public void ParseFromCommandLine_WithEmptyArgs_ShouldReturnAllNulls()
    {
        // Arrange
        var args = Array.Empty<string>();

        // Act
        var (artifactsRoot, timeoutMinutes, doubleStop) =
            OrchestrationConfigurationHelper.ParseFromCommandLine(args);

        // Assert
        artifactsRoot.Should().BeNull();
        timeoutMinutes.Should().BeNull();
        doubleStop.Should().BeNull();
    }
}
