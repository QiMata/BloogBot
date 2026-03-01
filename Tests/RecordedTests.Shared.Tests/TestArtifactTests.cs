using FluentAssertions;
using RecordedTests.Shared.Abstractions;

namespace RecordedTests.Shared.Tests;

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

    [Fact]
    public void Deconstruct_ReturnsComponents()
    {
        var artifact = new TestArtifact("file.mp4", "/full/path.mp4");
        var (name, fullPath) = artifact;
        name.Should().Be("file.mp4");
        fullPath.Should().Be("/full/path.mp4");
    }
}
