using FluentAssertions;
using RecordedTests.Shared.Abstractions;
using RecordedTests.Shared.Storage;

namespace RecordedTests.Shared.Tests.Storage;

public class FileSystemRecordedTestStorageTests : IDisposable
{
    private readonly string _testRootDirectory;
    private readonly FileSystemRecordedTestStorage _storage;

    public FileSystemRecordedTestStorageTests()
    {
        _testRootDirectory = Path.Combine(Path.GetTempPath(), $"FSStorageTests_{Guid.NewGuid()}");
        _storage = new FileSystemRecordedTestStorage(_testRootDirectory);
    }

    public void Dispose()
    {
        _storage?.Dispose();
        if (Directory.Exists(_testRootDirectory))
        {
            Directory.Delete(_testRootDirectory, recursive: true);
        }
    }

    [Fact]
    public void Constructor_ShouldCreateRootDirectory()
    {
        // Assert
        Directory.Exists(_testRootDirectory).Should().BeTrue();
    }

    [Fact]
    public void Constructor_WithNullRootDirectory_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => new FileSystemRecordedTestStorage(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task UploadArtifactAsync_ShouldCopyFileToCorrectLocation()
    {
        // Arrange
        var sourceFile = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.txt");
        File.WriteAllText(sourceFile, "test content");
        try
        {
            var artifact = new TestArtifact("test.txt", sourceFile);
            var timestamp = new DateTimeOffset(2026, 1, 19, 14, 30, 0, TimeSpan.Zero);

            // Act
            var location = await _storage.UploadArtifactAsync(
                artifact,
                "MyTest",
                timestamp,
                CancellationToken.None);

            // Assert
            location.Should().NotBeNullOrWhiteSpace();
            var expectedPath = Path.Combine(_testRootDirectory, "MyTest", "20260119_143000", "test.txt");
            File.Exists(expectedPath).Should().BeTrue();
            File.ReadAllText(expectedPath).Should().Be("test content");
        }
        finally
        {
            if (File.Exists(sourceFile))
                File.Delete(sourceFile);
        }
    }

    [Fact]
    public async Task UploadArtifactAsync_WithSpecialCharactersInTestName_ShouldSanitize()
    {
        // Arrange
        var sourceFile = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.txt");
        File.WriteAllText(sourceFile, "test content");
        try
        {
            var artifact = new TestArtifact("test.txt", sourceFile);
            var timestamp = DateTimeOffset.UtcNow;

            // Act
            var location = await _storage.UploadArtifactAsync(
                artifact,
                "Test:With/Invalid\\Chars<>|",
                timestamp,
                CancellationToken.None);

            // Assert
            // The test name should be sanitized (special chars replaced with _)
            // but the location will contain path separators
            var testNamePart = location.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[0];
            testNamePart.Should().NotContain(":");
            testNamePart.Should().NotContain("<");
            testNamePart.Should().NotContain(">");
            testNamePart.Should().NotContain("|");
        }
        finally
        {
            if (File.Exists(sourceFile))
                File.Delete(sourceFile);
        }
    }

    [Fact]
    public async Task UploadArtifactAsync_WithNullArtifact_ShouldThrowArgumentNullException()
    {
        // Act
        var act = async () => await _storage.UploadArtifactAsync(
            null!,
            "Test",
            DateTimeOffset.UtcNow,
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task DownloadArtifactAsync_ShouldCopyFileToDestination()
    {
        // Arrange
        var sourceFile = Path.Combine(Path.GetTempPath(), $"upload_{Guid.NewGuid()}.txt");
        var downloadFile = Path.Combine(Path.GetTempPath(), $"download_{Guid.NewGuid()}.txt");
        File.WriteAllText(sourceFile, "download test content");
        try
        {
            var artifact = new TestArtifact("download.txt", sourceFile);
            var location = await _storage.UploadArtifactAsync(
                artifact,
                "DownloadTest",
                DateTimeOffset.UtcNow,
                CancellationToken.None);

            // Act
            await _storage.DownloadArtifactAsync(location, downloadFile, CancellationToken.None);

            // Assert
            File.Exists(downloadFile).Should().BeTrue();
            File.ReadAllText(downloadFile).Should().Be("download test content");
        }
        finally
        {
            if (File.Exists(sourceFile))
                File.Delete(sourceFile);
            if (File.Exists(downloadFile))
                File.Delete(downloadFile);
        }
    }

    [Fact]
    public async Task DownloadArtifactAsync_WithNonExistentLocation_ShouldThrowFileNotFoundException()
    {
        // Arrange
        var downloadFile = Path.Combine(Path.GetTempPath(), $"download_{Guid.NewGuid()}.txt");

        // Act
        var act = async () => await _storage.DownloadArtifactAsync(
            "NonExistent/Path/file.txt",
            downloadFile,
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<FileNotFoundException>();
    }

    [Fact]
    public async Task ListArtifactsAsync_WithNoArtifacts_ShouldReturnEmptyList()
    {
        // Act
        var artifacts = await _storage.ListArtifactsAsync("NonExistentTest", CancellationToken.None);

        // Assert
        artifacts.Should().BeEmpty();
    }

    [Fact]
    public async Task ListArtifactsAsync_WithMultipleArtifacts_ShouldReturnAllArtifacts()
    {
        // Arrange
        var sourceFile1 = Path.Combine(Path.GetTempPath(), $"list1_{Guid.NewGuid()}.txt");
        var sourceFile2 = Path.Combine(Path.GetTempPath(), $"list2_{Guid.NewGuid()}.txt");
        File.WriteAllText(sourceFile1, "content1");
        File.WriteAllText(sourceFile2, "content2");
        try
        {
            var artifact1 = new TestArtifact("artifact1.txt", sourceFile1);
            var artifact2 = new TestArtifact("artifact2.txt", sourceFile2);

            await _storage.UploadArtifactAsync(artifact1, "ListTest", DateTimeOffset.UtcNow, CancellationToken.None);
            await Task.Delay(100); // Ensure different timestamps
            await _storage.UploadArtifactAsync(artifact2, "ListTest", DateTimeOffset.UtcNow, CancellationToken.None);

            // Act
            var artifacts = await _storage.ListArtifactsAsync("ListTest", CancellationToken.None);

            // Assert
            artifacts.Should().HaveCount(2);
            artifacts.Should().Contain(loc => loc.Contains("artifact1.txt"));
            artifacts.Should().Contain(loc => loc.Contains("artifact2.txt"));
        }
        finally
        {
            if (File.Exists(sourceFile1))
                File.Delete(sourceFile1);
            if (File.Exists(sourceFile2))
                File.Delete(sourceFile2);
        }
    }

    [Fact]
    public async Task DeleteArtifactAsync_ShouldRemoveFile()
    {
        // Arrange
        var sourceFile = Path.Combine(Path.GetTempPath(), $"delete_{Guid.NewGuid()}.txt");
        File.WriteAllText(sourceFile, "delete test");
        try
        {
            var artifact = new TestArtifact("delete.txt", sourceFile);
            var location = await _storage.UploadArtifactAsync(
                artifact,
                "DeleteTest",
                DateTimeOffset.UtcNow,
                CancellationToken.None);

            var fullPath = Path.Combine(_testRootDirectory, location);
            File.Exists(fullPath).Should().BeTrue();

            // Act
            await _storage.DeleteArtifactAsync(location, CancellationToken.None);

            // Assert
            File.Exists(fullPath).Should().BeFalse();
        }
        finally
        {
            if (File.Exists(sourceFile))
                File.Delete(sourceFile);
        }
    }

    [Fact]
    public async Task DeleteArtifactAsync_ShouldCleanUpEmptyDirectories()
    {
        // Arrange
        var sourceFile = Path.Combine(Path.GetTempPath(), $"cleanup_{Guid.NewGuid()}.txt");
        File.WriteAllText(sourceFile, "cleanup test");
        try
        {
            var artifact = new TestArtifact("cleanup.txt", sourceFile);
            var location = await _storage.UploadArtifactAsync(
                artifact,
                "CleanupTest",
                DateTimeOffset.UtcNow,
                CancellationToken.None);

            // Act
            await _storage.DeleteArtifactAsync(location, CancellationToken.None);

            // Assert
            var testDirectory = Path.Combine(_testRootDirectory, "CleanupTest");
            // The timestamp directory should be removed if it's empty
            Directory.Exists(testDirectory).Should().BeFalse();
        }
        finally
        {
            if (File.Exists(sourceFile))
                File.Delete(sourceFile);
        }
    }

    [Fact]
    public async Task DeleteArtifactAsync_WithNonExistentFile_ShouldNotThrow()
    {
        // Act
        var act = async () => await _storage.DeleteArtifactAsync(
            "NonExistent/file.txt",
            CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
    }
}
