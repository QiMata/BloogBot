using GameData.Core.Models;
using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace BotRunner.Clients;

/// <summary>
/// Async wrapper around blocking CalculatePath P/Invoke.
/// Queues requests into a Channel, worker threads dequeue and process.
/// Callers await instead of blocking the tick loop.
/// </summary>
public class AsyncPathfindingWrapper : IDisposable
{
    private readonly Channel<PathRequest> _requestChannel;
    private readonly Task[] _workerTasks;
    private readonly CancellationTokenSource _cts = new();
    private readonly Func<uint, Position, Position, float, float, Position[]> _calculatePath;

    public AsyncPathfindingWrapper(
        Func<uint, Position, Position, float, float, Position[]> calculatePath,
        int workerCount = 4)
    {
        _calculatePath = calculatePath;
        _requestChannel = Channel.CreateBounded<PathRequest>(new BoundedChannelOptions(1000)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = false,
        });

        _workerTasks = new Task[workerCount];
        for (int i = 0; i < workerCount; i++)
        {
            _workerTasks[i] = Task.Run(() => WorkerLoop(_cts.Token));
        }
    }

    /// <summary>
    /// Queue an async path calculation.
    /// </summary>
    public async Task<Position[]> CalculatePathAsync(
        uint mapId, Position start, Position end,
        float capsuleRadius = 0.3064f, float capsuleHeight = 2.0313f,
        CancellationToken ct = default)
    {
        var request = new PathRequest(mapId, start, end, capsuleRadius, capsuleHeight);

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, _cts.Token);
        await _requestChannel.Writer.WriteAsync(request, linkedCts.Token);

        return await request.Completion.Task;
    }

    /// <summary>Pending requests in queue.</summary>
    public int QueueDepth => _requestChannel.Reader.Count;

    private async Task WorkerLoop(CancellationToken ct)
    {
        await foreach (var request in _requestChannel.Reader.ReadAllAsync(ct))
        {
            try
            {
                var result = _calculatePath(
                    request.MapId, request.Start, request.End,
                    request.CapsuleRadius, request.CapsuleHeight);
                request.Completion.SetResult(result);
            }
            catch (Exception ex)
            {
                request.Completion.SetException(ex);
            }
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _requestChannel.Writer.Complete();
        try { Task.WaitAll(_workerTasks, TimeSpan.FromSeconds(5)); }
        catch (AggregateException ex) when (ex.InnerExceptions.All(e => e is TaskCanceledException or OperationCanceledException)) { }
        _cts.Dispose();
    }

    private class PathRequest(uint mapId, Position start, Position end, float capsuleRadius, float capsuleHeight)
    {
        public uint MapId { get; } = mapId;
        public Position Start { get; } = start;
        public Position End { get; } = end;
        public float CapsuleRadius { get; } = capsuleRadius;
        public float CapsuleHeight { get; } = capsuleHeight;
        public TaskCompletionSource<Position[]> Completion { get; } = new();
    }
}
