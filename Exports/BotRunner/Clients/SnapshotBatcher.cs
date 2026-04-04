using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace BotRunner.Clients;

/// <summary>
/// Batches incoming bot snapshots for efficient StateManager processing.
/// Buffers snapshots for a configurable window (default 50ms), then processes
/// the entire batch as a unit. Reduces lock contention from O(n) individual
/// operations to O(1) batch operations.
/// </summary>
public class SnapshotBatcher<T> : IDisposable
{
    private readonly ConcurrentBag<T> _buffer = new();
    private readonly Func<IReadOnlyList<T>, Task> _processBatch;
    private readonly Timer _flushTimer;
    private readonly int _maxBatchSize;
    private int _disposed;

    /// <summary>Total batches processed.</summary>
    public long BatchesProcessed { get; private set; }

    /// <summary>Total items processed across all batches.</summary>
    public long ItemsProcessed { get; private set; }

    /// <summary>Items currently buffered.</summary>
    public int BufferedCount => _buffer.Count;

    public SnapshotBatcher(
        Func<IReadOnlyList<T>, Task> processBatch,
        TimeSpan? flushInterval = null,
        int maxBatchSize = 500)
    {
        _processBatch = processBatch;
        _maxBatchSize = maxBatchSize;
        var interval = flushInterval ?? TimeSpan.FromMilliseconds(50);
        _flushTimer = new Timer(_ => _ = FlushAsync(), null, interval, interval);
    }

    /// <summary>Add a snapshot to the buffer.</summary>
    public void Enqueue(T snapshot)
    {
        _buffer.Add(snapshot);

        // Flush immediately if we hit max batch size
        if (_buffer.Count >= _maxBatchSize)
            _ = FlushAsync();
    }

    /// <summary>Flush the current buffer and process as a batch.</summary>
    public async Task FlushAsync()
    {
        if (_buffer.IsEmpty) return;

        var batch = new List<T>();
        while (_buffer.TryTake(out var item))
        {
            batch.Add(item);
            if (batch.Count >= _maxBatchSize) break;
        }

        if (batch.Count > 0)
        {
            await _processBatch(batch);
            BatchesProcessed++;
            ItemsProcessed += batch.Count;
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1) return;
        _flushTimer.Dispose();
        _ = FlushAsync(); // Final flush
    }
}
