using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Communication;
using DecisionEngineService;
using Google.Protobuf;

namespace PromptHandlingService.Tests;

public class DecisionEngineReadBinFileTests
{
    [Fact]
    public async Task ReadBinFile_AllowsConcurrentWriter()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), "DecisionEngineTests");
        Directory.CreateDirectory(tempDirectory);
        string tempFile = Path.Combine(tempDirectory, $"{Guid.NewGuid()}.bin");

        var snapshotsToWrite = new[]
        {
            new WoWActivitySnapshot { Timestamp = 1 },
            new WoWActivitySnapshot { Timestamp = 2 }
        };

        using var readyForRead = new ManualResetEventSlim(false);
        using var releaseWriter = new ManualResetEventSlim(false);

        // Writer holds the file open with FileShare.ReadWrite, writes both snapshots,
        // then signals readyForRead. The reader reads while the writer's stream is still open.
        var writerTask = Task.Run(async () =>
        {
            using var writeStream = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);

            foreach (var snapshot in snapshotsToWrite)
            {
                snapshot.WriteDelimitedTo(writeStream);
            }
            await writeStream.FlushAsync();

            // Signal that all data is written but keep the stream open
            readyForRead.Set();

            // Wait for reader to finish before closing the stream
            releaseWriter.Wait(TimeSpan.FromSeconds(5));
        });

        try
        {
            Assert.True(readyForRead.Wait(TimeSpan.FromSeconds(5)));

            var readMethod = typeof(DecisionEngine).GetMethod("ReadBinFileAsync", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(readMethod);

            // Read while the writer's stream is still open (tests FileShare.ReadWrite)
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var readTask = Task.Run(async () =>
            {
                var task = (Task<List<WoWActivitySnapshot>>)readMethod!.Invoke(null, new object[] { tempFile, cts.Token })!;
                return await task;
            });
            var readSnapshots = await readTask;

            Assert.Equal(snapshotsToWrite.Length, readSnapshots.Count);
            Assert.Equal(snapshotsToWrite.Select(s => s.Timestamp), readSnapshots.Select(s => s.Timestamp));
            Assert.True(File.Exists(tempFile));
        }
        finally
        {
            releaseWriter.Set();
            await writerTask;
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }
}
