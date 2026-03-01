using System;
using FluentAssertions;
using WWoW.RecordedTests.PathingTests.Configuration;

namespace WWoW.RecordedTests.PathingTests.Tests;

public class TestConfigurationTests
{
    [Fact]
    public void Defaults_ServerInfo_IsLocalhost3724()
    {
        var config = new TestConfiguration();
        config.ServerInfo.Host.Should().Be("127.0.0.1");
        config.ServerInfo.Port.Should().Be(3724);
    }

    [Fact]
    public void Defaults_PathfindingServicePort_Is5000()
    {
        var config = new TestConfiguration();
        config.PathfindingServicePort.Should().Be(5000);
    }

    [Fact]
    public void Defaults_StartPathfindingServiceInProcess_IsTrue()
    {
        var config = new TestConfiguration();
        config.StartPathfindingServiceInProcess.Should().BeTrue();
    }

    [Fact]
    public void Defaults_ServerTimeout_Is10Minutes()
    {
        var config = new TestConfiguration();
        config.ServerTimeout.Should().Be(TimeSpan.FromMinutes(10));
    }

    [Fact]
    public void Defaults_StopOnFirstFailure_IsFalse()
    {
        var config = new TestConfiguration();
        config.StopOnFirstFailure.Should().BeFalse();
    }

    [Fact]
    public void Defaults_EnableRecording_IsTrue()
    {
        var config = new TestConfiguration();
        config.EnableRecording.Should().BeTrue();
    }

    [Fact]
    public void Defaults_ArtifactsRoot_IsTestLogs()
    {
        var config = new TestConfiguration();
        config.ArtifactsRoot.Should().Be("./TestLogs");
    }

    [Fact]
    public void Defaults_ObsWebSocketUrl_IsLocalhost4455()
    {
        var config = new TestConfiguration();
        config.ObsWebSocketUrl.Should().Be("ws://localhost:4455");
    }

    [Fact]
    public void Validate_AllFieldsMissing_ThrowsWithMultipleErrors()
    {
        var config = new TestConfiguration();

        var act = () => config.Validate();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*GM account*")
            .WithMessage("*GM password*")
            .WithMessage("*GM character*")
            .WithMessage("*Test account*")
            .WithMessage("*Test password*")
            .WithMessage("*Test character*");
    }

    [Fact]
    public void Validate_OnlyGmAccountMissing_ThrowsWithGmAccountError()
    {
        var config = CreateValidConfig();
        config.GmAccount = "";

        var act = () => config.Validate();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*GM account*");
    }

    [Fact]
    public void Validate_RecordingEnabled_NoRecordingTarget_ThrowsWithRecordingTargetError()
    {
        var config = CreateValidConfig();
        config.EnableRecording = true;
        config.WowWindowTitle = null;
        config.WowProcessId = null;
        config.WowWindowHandle = null;

        var act = () => config.Validate();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Recording target*");
    }

    [Fact]
    public void Validate_RecordingDisabled_NoRecordingTargetRequired()
    {
        var config = CreateValidConfig();
        config.EnableRecording = false;
        config.WowWindowTitle = null;
        config.WowProcessId = null;
        config.WowWindowHandle = null;
        config.ObsExecutablePath = null;
        config.ObsRecordingPath = null;

        var act = () => config.Validate();
        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_RecordingEnabled_MissingObsExecutablePath_ThrowsObsError()
    {
        var config = CreateValidConfig();
        config.EnableRecording = true;
        config.WowWindowTitle = "WoW";
        config.ObsExecutablePath = null;

        var act = () => config.Validate();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*OBS_EXECUTABLE_PATH*");
    }

    [Fact]
    public void Validate_AllValid_DoesNotThrow()
    {
        var config = CreateFullyValidConfig();

        var act = () => config.Validate();
        act.Should().NotThrow();
    }

    [Fact]
    public void OrchestrationOptions_ReflectsArtifactsRoot()
    {
        var config = new TestConfiguration { ArtifactsRoot = "/custom/path" };
        config.OrchestrationOptions.ArtifactsRootDirectory.Should().Be("/custom/path");
    }

    [Fact]
    public void OrchestrationOptions_ReflectsServerTimeout()
    {
        var config = new TestConfiguration { ServerTimeout = TimeSpan.FromMinutes(5) };
        config.OrchestrationOptions.ServerAvailabilityTimeout.Should().Be(TimeSpan.FromMinutes(5));
    }

    [Fact]
    public void OrchestrationOptions_DoubleStopIsTrue()
    {
        var config = new TestConfiguration();
        config.OrchestrationOptions.DoubleStopRecorderForSafety.Should().BeTrue();
    }

    private static TestConfiguration CreateValidConfig()
    {
        return new TestConfiguration
        {
            GmAccount = "admin",
            GmPassword = "pass",
            GmCharacter = "AdminChar",
            TestAccount = "test",
            TestPassword = "testpass",
            TestCharacter = "TestChar",
            EnableRecording = false
        };
    }

    private static TestConfiguration CreateFullyValidConfig()
    {
        return new TestConfiguration
        {
            GmAccount = "admin",
            GmPassword = "pass",
            GmCharacter = "AdminChar",
            TestAccount = "test",
            TestPassword = "testpass",
            TestCharacter = "TestChar",
            EnableRecording = true,
            WowWindowTitle = "World of Warcraft",
            ObsExecutablePath = "C:\\obs\\obs64.exe",
            ObsWebSocketUrl = "ws://localhost:4455",
            ObsRecordingPath = "C:\\recordings"
        };
    }
}
