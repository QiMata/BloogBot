using System;
using System.IO;
using PromptHandlingService.Cache;

namespace PromptHandlingService.Tests;

public class PromptCacheTests
{
    [Fact]
    public void PromptCache_Dispose_ReleasesDatabaseFile()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"prompt-cache-{Guid.NewGuid():N}.db");

        FileStream? lockedStream = null;
        using (var cache = new PromptCache(dbPath))
        {
            cache.AddPrevious("prompt", "response");

            var exceptionWhileOpen = Record.Exception(() =>
            {
                lockedStream = new FileStream(dbPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            });

            if (OperatingSystem.IsWindows())
            {
                Assert.NotNull(exceptionWhileOpen);
                Assert.IsType<IOException>(exceptionWhileOpen);
            }
            else
            {
                Assert.Null(exceptionWhileOpen);
                lockedStream?.Dispose();
                lockedStream = null;
            }
        }

        lockedStream?.Dispose();

        var exclusiveOpen = Record.Exception(() =>
        {
            using var stream = new FileStream(dbPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        });

        Assert.Null(exclusiveOpen);

        var deleteException = Record.Exception(() => File.Delete(dbPath));
        Assert.Null(deleteException);
        Assert.False(File.Exists(dbPath));
    }
}
