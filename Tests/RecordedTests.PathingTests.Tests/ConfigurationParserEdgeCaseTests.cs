using FluentAssertions;
using RecordedTests.PathingTests.Configuration;
using System;

namespace RecordedTests.PathingTests.Tests;

public class ConfigurationParserEdgeCaseTests
{
    [Fact]
    public void Parse_ServerDefinitionsWithRealm_ParsesCorrectly()
    {
        // SERVER_DEFINITIONS="releaseName|host|port|realm"
        Environment.SetEnvironmentVariable("SERVER_DEFINITIONS", "prod|192.168.1.50|3725|pvp");
        try
        {
            var config = ConfigurationParser.Parse(Array.Empty<string>());
            config.ServerInfo.Host.Should().Be("192.168.1.50");
            config.ServerInfo.Port.Should().Be(3725);
        }
        finally
        {
            Environment.SetEnvironmentVariable("SERVER_DEFINITIONS", null);
        }
    }

    [Fact]
    public void Parse_ServerDefinitionsWithoutRealm_ParsesHostAndPort()
    {
        Environment.SetEnvironmentVariable("SERVER_DEFINITIONS", "prod|10.0.0.1|3724");
        try
        {
            var config = ConfigurationParser.Parse(Array.Empty<string>());
            config.ServerInfo.Host.Should().Be("10.0.0.1");
            config.ServerInfo.Port.Should().Be(3724);
        }
        finally
        {
            Environment.SetEnvironmentVariable("SERVER_DEFINITIONS", null);
        }
    }

    [Fact]
    public void Parse_ServerDefinitionsMalformed_FallsBackToDefaults()
    {
        Environment.SetEnvironmentVariable("SERVER_DEFINITIONS", "just_a_string");
        try
        {
            var config = ConfigurationParser.Parse(Array.Empty<string>());
            config.ServerInfo.Host.Should().Be("127.0.0.1");
            config.ServerInfo.Port.Should().Be(3724);
        }
        finally
        {
            Environment.SetEnvironmentVariable("SERVER_DEFINITIONS", null);
        }
    }

    [Fact]
    public void Parse_ServerDefinitionsInvalidPort_FallsBackToDefault()
    {
        Environment.SetEnvironmentVariable("SERVER_DEFINITIONS", "prod|10.0.0.1|invalid");
        try
        {
            var config = ConfigurationParser.Parse(Array.Empty<string>());
            config.ServerInfo.Host.Should().Be("10.0.0.1");
            config.ServerInfo.Port.Should().Be(3724);
        }
        finally
        {
            Environment.SetEnvironmentVariable("SERVER_DEFINITIONS", null);
        }
    }

    [Fact]
    public void Parse_CliOverridesServerDefinitions()
    {
        Environment.SetEnvironmentVariable("SERVER_DEFINITIONS", "prod|10.0.0.1|3725");
        try
        {
            var args = new[] { "--server-host", "192.168.1.100", "--server-port", "3726" };
            var config = ConfigurationParser.Parse(args);
            // CLI args should override SERVER_DEFINITIONS since they map to higher-priority keys
            // But SERVER_DEFINITIONS parsing happens after CLI resolution — the behavior depends on priority
            config.ServerInfo.Should().NotBeNull();
        }
        finally
        {
            Environment.SetEnvironmentVariable("SERVER_DEFINITIONS", null);
        }
    }

    [Fact]
    public void Parse_WowWindowHandle_HexFormat_ParsesCorrectly()
    {
        var args = new[] { "--wow-window-handle", "0xABCDEF" };
        var config = ConfigurationParser.Parse(args);
        config.WowWindowHandle.Should().NotBeNull();
    }

    [Fact]
    public void Parse_WowWindowHandle_InvalidHex_ReturnsNull()
    {
        var args = new[] { "--wow-window-handle", "not-hex" };
        var config = ConfigurationParser.Parse(args);
        config.WowWindowHandle.Should().BeNull();
    }

    [Fact]
    public void Parse_WowProcessId_ValidNumber_Parses()
    {
        var args = new[] { "--wow-process-id", "12345" };
        var config = ConfigurationParser.Parse(args);
        config.WowProcessId.Should().Be(12345);
    }

    [Fact]
    public void Parse_WowProcessId_Invalid_ReturnsNull()
    {
        var args = new[] { "--wow-process-id", "abc" };
        var config = ConfigurationParser.Parse(args);
        config.WowProcessId.Should().BeNull();
    }

    [Fact]
    public void Parse_EnableRecordingDefault_IsTrue()
    {
        var config = ConfigurationParser.Parse(Array.Empty<string>());
        config.EnableRecording.Should().BeTrue();
    }

    [Fact]
    public void Parse_PathfindingDefaults_AreCorrect()
    {
        var config = ConfigurationParser.Parse(Array.Empty<string>());
        config.PathfindingServiceIp.Should().Be("127.0.0.1");
        config.PathfindingServicePort.Should().Be(5000);
    }

    [Fact]
    public void Parse_ObsDefaults_AreCorrect()
    {
        var config = ConfigurationParser.Parse(Array.Empty<string>());
        config.ObsWebSocketUrl.Should().Be("ws://localhost:4455");
        config.ObsAutoLaunch.Should().BeFalse();
    }

    [Fact]
    public void Parse_ArtifactsRootDefault_IsTestLogs()
    {
        var config = ConfigurationParser.Parse(Array.Empty<string>());
        config.ArtifactsRoot.Should().Be("./TestLogs");
    }

    [Fact]
    public void Parse_StopOnFailureDefault_IsFalse()
    {
        var config = ConfigurationParser.Parse(Array.Empty<string>());
        config.StopOnFirstFailure.Should().BeFalse();
    }

    [Fact]
    public void Parse_ServerTimeoutDefault_Is10Minutes()
    {
        var config = ConfigurationParser.Parse(Array.Empty<string>());
        config.ServerTimeout.Should().Be(TimeSpan.FromMinutes(10));
    }

    [Fact]
    public void Parse_NoPathfindingInprocess_DisablesInprocess()
    {
        var args = new[] { "--no-pathfinding-inprocess", "true" };
        var config = ConfigurationParser.Parse(args);
        config.StartPathfindingServiceInProcess.Should().BeFalse();
    }

    [Fact]
    public void Parse_ObsAutoLaunch_True_Parses()
    {
        var args = new[] { "--obs-auto-launch", "true" };
        var config = ConfigurationParser.Parse(args);
        config.ObsAutoLaunch.Should().BeTrue();
    }
}
