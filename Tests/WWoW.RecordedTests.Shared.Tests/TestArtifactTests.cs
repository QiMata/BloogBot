using FluentAssertions;
using WWoW.RecordedTests.Shared.Abstractions;

namespace WWoW.RecordedTests.Shared.Tests;

public class TestArtifactTests
{
    [Fact]
    public void Constructor_PreservesName()
    {
        var artifact = new TestArtifact("recording.mp4", "/path/recording.mp4");
        artifact.Name.Should().Be("recording.mp4");
    }

    [Fact]
    public void Constructor_PreservesFullPath()
    {
        var artifact = new TestArtifact("recording.mp4", "/path/recording.mp4");
        artifact.FullPath.Should().Be("/path/recording.mp4");
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var a = new TestArtifact("test.mp4", "/a/test.mp4");
        var b = new TestArtifact("test.mp4", "/a/test.mp4");
        a.Should().Be(b);
    }

    [Fact]
    public void Equality_DifferentName_AreNotEqual()
    {
        var a = new TestArtifact("test1.mp4", "/a/test.mp4");
        var b = new TestArtifact("test2.mp4", "/a/test.mp4");
        a.Should().NotBe(b);
    }

    [Fact]
    public void Equality_DifferentPath_AreNotEqual()
    {
        var a = new TestArtifact("test.mp4", "/a/test.mp4");
        var b = new TestArtifact("test.mp4", "/b/test.mp4");
        a.Should().NotBe(b);
    }
}
