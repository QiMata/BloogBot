using FluentAssertions;
using WWoW.RecordedTests.Shared.Abstractions;

namespace WWoW.RecordedTests.Shared.Tests;

public class OrchestrationResultTests
{
    [Fact]
    public void Success_WithMessage_PreservesValues()
    {
        var result = new OrchestrationResult(true, "All tests passed");
        result.Success.Should().BeTrue();
        result.Message.Should().Be("All tests passed");
    }

    [Fact]
    public void Failure_WithMessage_PreservesValues()
    {
        var result = new OrchestrationResult(false, "Test failed");
        result.Success.Should().BeFalse();
        result.Message.Should().Be("Test failed");
    }

    [Fact]
    public void RecordingArtifact_DefaultsToNull()
    {
        var result = new OrchestrationResult(true, "ok");
        result.RecordingArtifact.Should().BeNull();
    }

    [Fact]
    public void TestRunDirectory_DefaultsToNull()
    {
        var result = new OrchestrationResult(true, "ok");
        result.TestRunDirectory.Should().BeNull();
    }

    [Fact]
    public void RecordingArtifact_WhenProvided_IsPreserved()
    {
        var artifact = new TestArtifact("video.mp4", "/path/to/video.mp4");
        var result = new OrchestrationResult(true, "ok", artifact);
        result.RecordingArtifact.Should().Be(artifact);
    }

    [Fact]
    public void TestRunDirectory_WhenProvided_IsPreserved()
    {
        var result = new OrchestrationResult(true, "ok", TestRunDirectory: "/run/dir");
        result.TestRunDirectory.Should().Be("/run/dir");
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var a = new OrchestrationResult(true, "ok", null, "/dir");
        var b = new OrchestrationResult(true, "ok", null, "/dir");
        a.Should().Be(b);
    }

    [Fact]
    public void Equality_DifferentSuccess_AreNotEqual()
    {
        var a = new OrchestrationResult(true, "ok");
        var b = new OrchestrationResult(false, "ok");
        a.Should().NotBe(b);
    }

    [Fact]
    public void Equality_DifferentMessage_AreNotEqual()
    {
        var a = new OrchestrationResult(true, "ok");
        var b = new OrchestrationResult(true, "different");
        a.Should().NotBe(b);
    }
}
