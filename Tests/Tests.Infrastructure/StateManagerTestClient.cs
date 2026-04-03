using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using BotCommLayer;
using Communication;
using Google.Protobuf;

namespace Tests.Infrastructure;

/// <summary>
/// Test client for communicating with WoWStateManager on port 8088.
/// Provides snapshot queries, action forwarding, and wait-for-ready helpers.
///
/// Wire protocol: [4-byte int32 LE length][1-byte compression flag][protobuf bytes]
/// in both directions via <see cref="ProtobufCompression"/>.
/// Server expects <see cref="AsyncRequest"/>, replies with <see cref="StateChangeResponse"/>.
/// </summary>
public class StateManagerTestClient : IDisposable
{
    private readonly string _host;
    private readonly int _port;
    private readonly TimeSpan _defaultRequestTimeout;
    private readonly TimeSpan _fullSnapshotRequestTimeout;
    private TcpClient? _tcp;
    private NetworkStream? _stream;
    private ulong _nextId = 1;
    private readonly object _lock = new();
    private readonly SemaphoreSlim _requestGate = new(1, 1);

    public StateManagerTestClient(
        string host = "127.0.0.1",
        int port = 8088,
        TimeSpan? defaultRequestTimeout = null,
        TimeSpan? fullSnapshotRequestTimeout = null)
    {
        _host = host;
        _port = port;
        _defaultRequestTimeout = defaultRequestTimeout ?? TimeSpan.FromSeconds(30);
        _fullSnapshotRequestTimeout = fullSnapshotRequestTimeout ?? TimeSpan.FromMinutes(2);
    }

    /// <summary>
    /// Connect to StateManager. Call once before using query/forward methods.
    /// Enables TCP keepalive to prevent the OS from silently dropping an idle connection.
    /// </summary>
    public async Task ConnectAsync(CancellationToken ct = default)
    {
        _tcp = new TcpClient();
        await _tcp.ConnectAsync(_host, _port, ct);

        // Enable TCP keepalive so the connection survives long idle periods
        _tcp.Client.SetSocketOption(
            SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
        _tcp.NoDelay = true;

        _stream = _tcp.GetStream();
    }

    /// <summary>
    /// Query activity snapshots from StateManager.
    /// If accountName is null/empty, returns snapshots for ALL bots.
    /// </summary>
    public async Task<List<WoWActivitySnapshot>> QuerySnapshotsAsync(string? accountName = null, CancellationToken ct = default)
    {
        var request = new AsyncRequest
        {
            Id = Interlocked.Increment(ref _nextId),
            SnapshotQuery = new SnapshotQueryRequest
            {
                AccountName = accountName ?? ""
            }
        };

        var requestTimeout = string.IsNullOrEmpty(accountName)
            ? _fullSnapshotRequestTimeout
            : _defaultRequestTimeout;

        var response = await SendRequestAsync(request, ct, requestTimeout);
        return response.Snapshots.ToList();
    }

    /// <summary>
    /// Forward an action to a specific bot via StateManager.
    /// The action is queued and delivered on the bot's next poll to port 5002.
    /// </summary>
    public async Task<ResponseResult> ForwardActionAsync(string accountName, ActionMessage action, CancellationToken ct = default)
    {
        var request = new AsyncRequest
        {
            Id = Interlocked.Increment(ref _nextId),
            ActionForward = new ActionForwardRequest
            {
                AccountName = accountName,
                Action = action
            }
        };

        var response = await SendRequestAsync(request, ct, _defaultRequestTimeout);
        return response.Response;
    }

    public async Task<ResponseResult> SetCoordinatorEnabledAsync(bool enabled, CancellationToken ct = default)
    {
        var request = new AsyncRequest
        {
            Id = Interlocked.Increment(ref _nextId),
            StateChange = new StateChangeRequest
            {
                ChangeType = StateChangeType.CoordinatorEnabled,
                RequestParameter = new RequestParameter { IntParam = enabled ? 1 : 0 }
            }
        };

        var response = await SendRequestAsync(request, ct, _defaultRequestTimeout);
        return response.Response;
    }

    /// <summary>
    /// Wait until the specified bot reports "InWorld" screen state.
    /// Polls every <paramref name="pollIntervalMs"/> ms until timeout.
    /// </summary>
    public async Task<WoWActivitySnapshot?> WaitForBotInWorldAsync(
        string accountName, int timeoutMs = 90_000, int pollIntervalMs = 2000, CancellationToken ct = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeoutMs && !ct.IsCancellationRequested)
        {
            var snapshots = await QuerySnapshotsAsync(accountName, ct);
            var snap = snapshots.FirstOrDefault();
            if (snap != null && snap.ScreenState == "InWorld" && !string.IsNullOrEmpty(snap.CharacterName))
                return snap;

            await Task.Delay(pollIntervalMs, ct);
        }
        return null;
    }

    /// <summary>
    /// Wait until ALL configured bots report "InWorld" screen state.
    /// </summary>
    public async Task<List<WoWActivitySnapshot>> WaitForAllBotsInWorldAsync(
        int expectedCount = 2, int timeoutMs = 120_000, int pollIntervalMs = 2000, CancellationToken ct = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeoutMs && !ct.IsCancellationRequested)
        {
            var snapshots = await QuerySnapshotsAsync(null, ct);
            var inWorld = snapshots
                .Where(s => s.ScreenState == "InWorld" && !string.IsNullOrEmpty(s.CharacterName))
                .ToList();

            if (inWorld.Count >= expectedCount)
                return inWorld;

            await Task.Delay(pollIntervalMs, ct);
        }
        return [];
    }

    private async Task<StateChangeResponse> SendRequestAsync(AsyncRequest request, CancellationToken ct, TimeSpan requestTimeout)
    {
        if (_stream == null)
            throw new InvalidOperationException("Not connected. Call ConnectAsync first.");

        // Per-request timeout: prevent indefinite hangs when StateManager is overloaded
        // (e.g. 10 BG bots hammering port 5002 while we query port 8088).
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(requestTimeout);
        var linked = timeoutCts.Token;

        await _requestGate.WaitAsync(linked);
        try
        {
            return await SendRequestCoreAsync(request, linked);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // Per-request timeout hit — reconnect and retry once
            await ReconnectAsync(ct);
            using var retryCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            retryCts.CancelAfter(requestTimeout);
            return await SendRequestCoreAsync(request, retryCts.Token);
        }
        catch (IOException) when (!ct.IsCancellationRequested)
        {
            // Connection was lost — attempt a single reconnect
            await ReconnectAsync(ct);
            return await SendRequestCoreAsync(request, ct);
        }
        catch (SocketException) when (!ct.IsCancellationRequested)
        {
            await ReconnectAsync(ct);
            return await SendRequestCoreAsync(request, ct);
        }
        finally
        {
            _requestGate.Release();
        }
    }

    private async Task<StateChangeResponse> SendRequestCoreAsync(AsyncRequest request, CancellationToken ct)
    {
        byte[] encodedRequest;
        lock (_lock)
        {
            encodedRequest = ProtobufCompression.Encode(request.ToByteArray());
        }

        // Send length + compressed wire payload.
        await _stream!.WriteAsync(encodedRequest.AsMemory(0, 4), ct);
        await _stream.WriteAsync(encodedRequest.AsMemory(4), ct);
        await _stream.FlushAsync(ct);

        // Read response length (wire payload includes compression flag).
        var responseLengthBuf = new byte[4];
        await ReadExactAsync(_stream, responseLengthBuf, 4, ct);
        var responseLength = BitConverter.ToInt32(responseLengthBuf, 0);

        // Read and decode the wire payload.
        var wirePayload = new byte[responseLength];
        await ReadExactAsync(_stream, wirePayload, responseLength, ct);
        var responsePayload = ProtobufCompression.Decode(wirePayload);

        var response = new StateChangeResponse();
        response.MergeFrom(responsePayload);
        return response;
    }

    private async Task ReconnectAsync(CancellationToken ct)
    {
        _stream?.Dispose();
        _tcp?.Dispose();

        _tcp = new TcpClient();
        await _tcp.ConnectAsync(_host, _port, ct);
        _tcp.Client.SetSocketOption(
            SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
        _tcp.NoDelay = true;
        _stream = _tcp.GetStream();
    }

    private static async Task ReadExactAsync(NetworkStream stream, byte[] buffer, int count, CancellationToken ct)
    {
        int totalRead = 0;
        while (totalRead < count)
        {
            int read = await stream.ReadAsync(buffer.AsMemory(totalRead, count - totalRead), ct);
            if (read == 0)
                throw new IOException("Connection closed while reading response.");
            totalRead += read;
        }
    }

    public void Dispose()
    {
        _stream?.Dispose();
        _tcp?.Dispose();
        _requestGate.Dispose();
    }
}
