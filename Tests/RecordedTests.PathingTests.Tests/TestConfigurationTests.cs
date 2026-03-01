using System;
using FluentAssertions;
using RecordedTests.PathingTests.Configuration;

namespace RecordedTests.PathingTests.Tests;

public class TestConfigurationTests
{
    // =========================================================================
    // Default values
    // =========================================================================

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
    public void Defaults_ObsAutoLaunch_IsFalse()
    {
        var config = new TestConfiguration();
        config.ObsAutoLaunch.Should().BeFalse();
    }

    // =========================================================================
    // Validate — missing required fields
    // =========================================================================

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
    public void Validate_OnlyTestCharacterMissing_ThrowsWithTestCharacterError()
    {
        var config = CreateValidConfig();
        config.TestCharacter = "";

        var act = () => config.Validate();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Test character*");
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
    public void Validate_RecordingEnabled_WithWindowTitle_NoRecordingTargetError()
    {
        var config = CreateValidConfig();
        config.EnableRecording = true;
        config.WowWindowTitle = "World of Warcraft";

        // Should not throw about recording target, but may still throw about OBS config
        var act = () => config.Validate();

        // The exception, if thrown, should NOT mention recording target
        try
        {
            act();
        }
        catch (InvalidOperationException ex)
        {
            ex.Message.Should().NotContain("Recording target is required");
        }
    }

    [Fact]
    public void Validate_RecordingEnabled_WithProcessId_NoRecordingTargetError()
    {
        var config = CreateValidConfig();
        config.EnableRecording = true;
        config.WowProcessId = 12345;

        try
        {
            config.Validate();
        }
        catch (InvalidOperationException ex)
        {
            ex.Message.Should().NotContain("Recording target is required");
        }
    }

    [Fact]
    public void Validate_RecordingEnabled_WithWindowHandle_NoRecordingTargetError()
    {
        var config = CreateValidConfig();
        config.EnableRecording = true;
        config.WowWindowHandle = new IntPtr(0xABCD);

        try
        {
            config.Validate();
        }
        catch (InvalidOperationException ex)
        {
            ex.Message.Should().NotContain("Recording target is required");
        }
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
    public void Validate_RecordingEnabled_MissingObsRecordingPath_ThrowsObsError()
    {
        var config = CreateValidConfig();
        config.EnableRecording = true;
        config.WowWindowTitle = "WoW";
        config.ObsExecutablePath = "C:\\obs.exe";
        config.ObsRecordingPath = null;

        var act = () => config.Validate();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*OBS_RECORDING_PATH*");
    }

    [Fact]
    public void Validate_AllValid_DoesNotThrow()
    {
        var config = CreateFullyValidConfig();

        var act = () => config.Validate();
        act.Should().NotThrow();
    }

    // =========================================================================
    // OrchestrationOptions computed property
    // =========================================================================

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

    // =========================================================================
    // Helpers
    // =========================================================================

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
