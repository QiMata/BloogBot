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
        using var startSecondWrite = new ManualResetEventSlim(false);
        using var releaseWriter = new ManualResetEventSlim(false);

        var writerTask = Task.Run(async () =>
        {
            using var writeStream = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);

            snapshotsToWrite[0].WriteDelimitedTo(writeStream);
            await writeStream.FlushAsync();

            readyForRead.Set();
            Assert.True(startSecondWrite.Wait(TimeSpan.FromSeconds(5)));

            using var bufferStream = new MemoryStream();
            snapshotsToWrite[1].WriteDelimitedTo(bufferStream);
            var bytes = bufferStream.ToArray();

            int midpoint = Math.Max(1, bytes.Length / 2);
            await writeStream.WriteAsync(bytes.AsMemory(0, midpoint));
            await writeStream.FlushAsync();
            await Task.Delay(50);
            await writeStream.WriteAsync(bytes.AsMemory(midpoint));
            await writeStream.FlushAsync();

            releaseWriter.Wait(TimeSpan.FromSeconds(5));
        });

        try
        {
            Assert.True(readyForRead.Wait(TimeSpan.FromSeconds(5)));

            var readMethod = typeof(DecisionEngine).GetMethod("ReadBinFile", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(readMethod);

            startSecondWrite.Set();
            await Task.Delay(10);

            var readTask = Task.Run(() => (List<WoWActivitySnapshot>)readMethod!.Invoke(null, new object[] { tempFile })!);
            var readSnapshots = await readTask;

            Assert.Equal(snapshotsToWrite.Length, readSnapshots.Count);
            Assert.Equal(snapshotsToWrite.Select(s => s.Timestamp), readSnapshots.Select(s => s.Timestamp));
            Assert.True(File.Exists(tempFile));
        }
        finally
        {
            startSecondWrite.Set();
            releaseWriter.Set();
            await writerTask;
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }
}
