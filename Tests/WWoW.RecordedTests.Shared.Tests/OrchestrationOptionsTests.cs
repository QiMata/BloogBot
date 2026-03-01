using System;
using FluentAssertions;
using WWoW.RecordedTests.Shared.Abstractions;

namespace WWoW.RecordedTests.Shared.Tests;

public class OrchestrationOptionsTests
{
    [Fact]
    public void Defaults_ServerAvailabilityTimeout_Is5Minutes()
    {
        var options = new OrchestrationOptions();
        options.ServerAvailabilityTimeout.Should().Be(TimeSpan.FromMinutes(5));
    }

    [Fact]
    public void Defaults_ArtifactsRootDirectory_IsTestLogs()
    {
        var options = new OrchestrationOptions();
        options.ArtifactsRootDirectory.Should().Be(".\\TestLogs");
    }

    [Fact]
    public void Defaults_DoubleStopRecorderForSafety_IsTrue()
    {
        var options = new OrchestrationOptions();
        options.DoubleStopRecorderForSafety.Should().BeTrue();
    }

    [Fact]
    public void Init_ServerAvailabilityTimeout_IsApplied()
    {
        var options = new OrchestrationOptions { ServerAvailabilityTimeout = TimeSpan.FromSeconds(30) };
        options.ServerAvailabilityTimeout.Should().Be(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public void Init_ArtifactsRootDirectory_IsApplied()
    {
        var options = new OrchestrationOptions { ArtifactsRootDirectory = "/custom/path" };
        options.ArtifactsRootDirectory.Should().Be("/custom/path");
    }

    [Fact]
    public void Init_DoubleStopRecorderForSafety_CanBeDisabled()
    {
        var options = new OrchestrationOptions { DoubleStopRecorderForSafety = false };
        options.DoubleStopRecorderForSafety.Should().BeFalse();
    }
}
