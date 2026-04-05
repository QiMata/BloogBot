using BotRunner.Clients;

namespace BotRunner.Tests.Clients;

public class SnapshotBatcherTests
{
    [Fact]
    public void Enqueue_Buffers()
    {
        var processed = new List<int>();
        using var batcher = new SnapshotBatcher<int>(
            batch => { processed.AddRange(batch); return Task.CompletedTask; },
            flushInterval: TimeSpan.FromHours(1)); // Long interval so timer won't fire

        batcher.Enqueue(1);
        batcher.Enqueue(2);

        Assert.Equal(2, batcher.BufferedCount);
    }

    [Fact]
    public async Task FlushAsync_ProcessesBatch()
    {
        var processed = new List<int>();
        using var batcher = new SnapshotBatcher<int>(
            batch => { processed.AddRange(batch); return Task.CompletedTask; },
            flushInterval: TimeSpan.FromHours(1));

        batcher.Enqueue(10);
        batcher.Enqueue(20);
        batcher.Enqueue(30);

        await batcher.FlushAsync();

        Assert.Equal(3, processed.Count);
        Assert.Contains(10, processed);
        Assert.Contains(20, processed);
        Assert.Contains(30, processed);
    }

    [Fact]
    public async Task Stats_TrackCorrectly()
    {
        using var batcher = new SnapshotBatcher<string>(
            batch => Task.CompletedTask,
            flushInterval: TimeSpan.FromHours(1));

        batcher.Enqueue("a");
        batcher.Enqueue("b");
        await batcher.FlushAsync();

        batcher.Enqueue("c");
        await batcher.FlushAsync();

        Assert.Equal(2, batcher.BatchesProcessed);
        Assert.Equal(3, batcher.ItemsProcessed);
    }

    [Fact]
    public async Task FlushAsync_NoOp_WhenEmpty()
    {
        int callCount = 0;
        using var batcher = new SnapshotBatcher<int>(
            batch => { callCount++; return Task.CompletedTask; },
            flushInterval: TimeSpan.FromHours(1));

        await batcher.FlushAsync();

        Assert.Equal(0, callCount);
    }
}
