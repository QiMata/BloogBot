using FluentAssertions;
using RecordedTests.Shared.Configuration;
using System;
using System.Collections.Generic;

namespace RecordedTests.Shared.Tests.Configuration;

public class ConfigurationResolverTests : IDisposable
{
    private readonly List<string> _envVarsToCleanup = new();

    public void Dispose()
    {
        // Clean up environment variables set during tests
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
    public void ResolveString_WithCliValue_ShouldReturnCliValue()
    {
        // Arrange
        SetTestEnvVar("TEST_VAR", "env_value");

        // Act
        var result = ConfigurationResolver.ResolveString(
            cliValue: "cli_value",
            envVarName: "TEST_VAR",
            configValue: "config_value",
            defaultValue: "default_value");

        // Assert
        result.Should().Be("cli_value");
    }

    [Fact]
    public void ResolveString_WithoutCliValue_ShouldReturnEnvValue()
    {
        // Arrange
        SetTestEnvVar("TEST_VAR", "env_value");

        // Act
        var result = ConfigurationResolver.ResolveString(
            cliValue: null,
            envVarName: "TEST_VAR",
            configValue: "config_value",
            defaultValue: "default_value");

        // Assert
        result.Should().Be("env_value");
    }

    [Fact]
    public void ResolveString_WithoutCliAndEnv_ShouldReturnConfigValue()
    {
        // Act
        var result = ConfigurationResolver.ResolveString(
            cliValue: null,
            envVarName: "NONEXISTENT_VAR",
            configValue: "config_value",
            defaultValue: "default_value");

        // Assert
        result.Should().Be("config_value");
    }

    [Fact]
    public void ResolveString_WithNothingSet_ShouldReturnDefaultValue()
    {
        // Act
        var result = ConfigurationResolver.ResolveString(
            cliValue: null,
            envVarName: "NONEXISTENT_VAR",
            configValue: null,
            defaultValue: "default_value");

        // Assert
        result.Should().Be("default_value");
    }

    [Fact]
    public void ResolveString_WithEmptyStrings_ShouldFallThrough()
    {
        // Arrange
        SetTestEnvVar("TEST_VAR", "");

        // Act
        var result = ConfigurationResolver.ResolveString(
            cliValue: "",
            envVarName: "TEST_VAR",
            configValue: "",
            defaultValue: "default_value");

        // Assert
        result.Should().Be("default_value");
    }

    [Fact]
    public void ResolveInt_WithCliValue_ShouldReturnCliValue()
    {
        // Arrange
        SetTestEnvVar("TEST_INT", "200");

        // Act
        var result = ConfigurationResolver.ResolveInt(
            cliValue: 100,
            envVarName: "TEST_INT",
            configValue: 300,
            defaultValue: 400);

        // Assert
        result.Should().Be(100);
    }

    [Fact]
    public void ResolveInt_WithoutCliValue_ShouldReturnEnvValue()
    {
        // Arrange
        SetTestEnvVar("TEST_INT", "200");

        // Act
        var result = ConfigurationResolver.ResolveInt(
            cliValue: null,
            envVarName: "TEST_INT",
            configValue: 300,
            defaultValue: 400);

        // Assert
        result.Should().Be(200);
    }

    [Fact]
    public void ResolveInt_WithInvalidEnvValue_ShouldFallThroughToConfig()
    {
        // Arrange
        SetTestEnvVar("TEST_INT", "not_a_number");

        // Act
        var result = ConfigurationResolver.ResolveInt(
            cliValue: null,
            envVarName: "TEST_INT",
            configValue: 300,
            defaultValue: 400);

        // Assert
        result.Should().Be(300);
    }

    [Fact]
    public void ResolveInt_WithNothingSet_ShouldReturnDefaultValue()
    {
        // Act
        var result = ConfigurationResolver.ResolveInt(
            cliValue: null,
            envVarName: "NONEXISTENT_INT",
            configValue: null,
            defaultValue: 400);

        // Assert
        result.Should().Be(400);
    }

    [Fact]
    public void ResolveBool_WithCliValue_ShouldReturnCliValue()
    {
        // Arrange
        SetTestEnvVar("TEST_BOOL", "false");

        // Act
        var result = ConfigurationResolver.ResolveBool(
            cliValue: true,
            envVarName: "TEST_BOOL",
            configValue: false,
            defaultValue: false);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void ResolveBool_WithoutCliValue_ShouldReturnEnvValue()
    {
        // Arrange
        SetTestEnvVar("TEST_BOOL", "true");

        // Act
        var result = ConfigurationResolver.ResolveBool(
            cliValue: null,
            envVarName: "TEST_BOOL",
            configValue: false,
            defaultValue: false);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void ResolveBool_WithInvalidEnvValue_ShouldFallThroughToConfig()
    {
        // Arrange
        SetTestEnvVar("TEST_BOOL", "not_a_bool");

        // Act
        var result = ConfigurationResolver.ResolveBool(
            cliValue: null,
            envVarName: "TEST_BOOL",
            configValue: true,
            defaultValue: false);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void ResolveTimeSpan_WithCliValue_ShouldReturnCliValue()
    {
        // Arrange
        SetTestEnvVar("TEST_TIMESPAN", "120");

        // Act
        var result = ConfigurationResolver.ResolveTimeSpan(
            cliValue: TimeSpan.FromSeconds(60),
            envVarName: "TEST_TIMESPAN",
            configValue: TimeSpan.FromSeconds(180),
            defaultValue: TimeSpan.FromSeconds(240));

        // Assert
        result.Should().Be(TimeSpan.FromSeconds(60));
    }

    [Fact]
    public void ResolveTimeSpan_WithoutCliValue_ShouldReturnEnvValueInSeconds()
    {
        // Arrange
        SetTestEnvVar("TEST_TIMESPAN", "120");

        // Act
        var result = ConfigurationResolver.ResolveTimeSpan(
            cliValue: null,
            envVarName: "TEST_TIMESPAN",
            configValue: TimeSpan.FromSeconds(180),
            defaultValue: TimeSpan.FromSeconds(240));

        // Assert
        result.Should().Be(TimeSpan.FromSeconds(120));
    }

    [Fact]
    public void ResolveTimeSpan_WithInvalidEnvValue_ShouldFallThroughToConfig()
    {
        // Arrange
        SetTestEnvVar("TEST_TIMESPAN", "not_a_number");

        // Act
        var result = ConfigurationResolver.ResolveTimeSpan(
            cliValue: null,
            envVarName: "TEST_TIMESPAN",
            configValue: TimeSpan.FromSeconds(180),
            defaultValue: TimeSpan.FromSeconds(240));

        // Assert
        result.Should().Be(TimeSpan.FromSeconds(180));
    }
}
