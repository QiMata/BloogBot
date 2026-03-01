using System;
using System.IO;
using FluentAssertions;
using WWoW.RecordedTests.Shared;

namespace WWoW.RecordedTests.Shared.Tests;

public class ArtifactPathHelperTests : IDisposable
{
    private readonly string _tempRoot;

    public ArtifactPathHelperTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"ArtifactPathHelperTests_{Guid.NewGuid():N}");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }

    [Fact]
    public void PrepareArtifactDirectories_ValidInputs_CreatesRunDirectory()
    {
        var startedAt = new DateTimeOffset(2026, 3, 15, 10, 30, 45, TimeSpan.Zero);
        var result = ArtifactPathHelper.PrepareArtifactDirectories(_tempRoot, "MyTest", startedAt);

        Directory.Exists(result.TestRunDirectory).Should().BeTrue();
    }

    [Fact]
    public void PrepareArtifactDirectories_ValidInputs_ReturnsCorrectPaths()
    {
        var startedAt = new DateTimeOffset(2026, 3, 15, 10, 30, 45, TimeSpan.Zero);
        var result = ArtifactPathHelper.PrepareArtifactDirectories(_tempRoot, "MyTest", startedAt);

        result.ArtifactsRootDirectory.Should().Be(_tempRoot);
        result.SanitizedTestName.Should().Be("MyTest");
        result.TestRootDirectory.Should().Be(Path.Combine(_tempRoot, "MyTest"));
        result.TestRunDirectory.Should().Be(Path.Combine(_tempRoot, "MyTest", "20260315_103045"));
    }

    [Fact]
    public void PrepareArtifactDirectories_TimestampFormatIsUtc()
    {
        var startedAt = new DateTimeOffset(2026, 6, 1, 14, 0, 0, TimeSpan.FromHours(5));
        var result = ArtifactPathHelper.PrepareArtifactDirectories(_tempRoot, "UtcTest", startedAt);

        result.TestRunDirectory.Should().EndWith("20260601_090000");
    }

    [Fact]
    public void PrepareArtifactDirectories_SanitizesTestName()
    {
        var startedAt = DateTimeOffset.UtcNow;
        var result = ArtifactPathHelper.PrepareArtifactDirectories(_tempRoot, "Test:With<Invalid>Chars", startedAt);

        result.SanitizedTestName.Should().NotContain(":");
        result.SanitizedTestName.Should().NotContain("<");
        result.SanitizedTestName.Should().NotContain(">");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void PrepareArtifactDirectories_NullOrWhitespaceRoot_ThrowsArgumentException(string? rootDir)
    {
        var act = () => ArtifactPathHelper.PrepareArtifactDirectories(rootDir!, "Test", DateTimeOffset.UtcNow);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void PrepareArtifactDirectories_NullTestName_UsesFallback()
    {
        var startedAt = DateTimeOffset.UtcNow;
        var result = ArtifactPathHelper.PrepareArtifactDirectories(_tempRoot, null!, startedAt);

        result.SanitizedTestName.Should().Be("RecordedTest");
    }

    [Fact]
    public void SanitizeName_ValidName_ReturnsUnchanged()
    {
        ArtifactPathHelper.SanitizeName("SimpleTestName").Should().Be("SimpleTestName");
    }

    [Fact]
    public void SanitizeName_NameWithInvalidChars_ReplacesWithUnderscore()
    {
        var result = ArtifactPathHelper.SanitizeName("Test:Name<With>Invalid|Chars");
        result.Should().NotContain(":");
        result.Should().NotContain("<");
        result.Should().NotContain(">");
        result.Should().NotContain("|");
        result.Should().Contain("_");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void SanitizeName_NullOrWhitespace_ReturnsFallback(string? value)
    {
        ArtifactPathHelper.SanitizeName(value).Should().Be("RecordedTest");
    }

    [Fact]
    public void SanitizeName_NameWithDotsAndDashes_PreservesThem()
    {
        ArtifactPathHelper.SanitizeName("Test-Name.v2").Should().Be("Test-Name.v2");
    }

    [Fact]
    public void ArtifactPathInfo_EqualityByValue()
    {
        var a = new ArtifactPathHelper.ArtifactPathInfo("/root", "test", "/root/test", "/root/test/run1");
        var b = new ArtifactPathHelper.ArtifactPathInfo("/root", "test", "/root/test", "/root/test/run1");

        a.Should().Be(b);
    }
}
