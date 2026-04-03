using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using BotCommLayer;
using Communication;
using Game;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.IPC;

/// <summary>
/// Tests for the TCP protobuf pipeline between BotRunner (client) and StateManager (server).
/// Validates: round-trip, compression, action injection, concurrent clients, reconnection.
///
/// Uses a minimal TestSocketServer that echoes back the request with an injected action,
/// matching the real CharacterStateSocketListener pattern.
/// </summary>
public class ProtobufSocketPipelineTests : IDisposable
{
    private readonly ITestOutputHelper _output;

    public ProtobufSocketPipelineTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public void Dispose() { }

    private static int GetFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    // =========================================================================
    // Test server that echoes snapshot back with an injected action
    // =========================================================================

    private sealed class TestSocketServer : ProtobufSocketServer<WoWActivitySnapshot, WoWActivitySnapshot>
    {
        public int RequestCount;
        public ActionMessage? InjectedAction { get; set; }
        public ConcurrentBag<string> ReceivedAccounts { get; } = new();

        public TestSocketServer(string ip, int port)
            : base(ip, port, NullLogger.Instance) { }

        protected override WoWActivitySnapshot HandleRequest(WoWActivitySnapshot request)
        {
            Interlocked.Increment(ref RequestCount);
            ReceivedAccounts.Add(request.AccountName);

            // Echo back the snapshot with an injected action (mimics StateManager)
            var response = request.Clone();
            if (InjectedAction != null)
                response.CurrentAction = InjectedAction.Clone();

            return response;
        }
    }

    // =========================================================================
    // 1. Basic round-trip: send snapshot → receive response with action
    // =========================================================================

    [Fact]
    public void SendSnapshot_ReceivesResponseWithAction()
    {
        var port = GetFreePort();
        using var server = new TestSocketServer("127.0.0.1", port);
        server.InjectedAction = new ActionMessage
        {
            ActionType = ActionType.Goto,
            Parameters = { new RequestParameter { FloatParam = 100f } }
        };

        Thread.Sleep(100); // Let server start

        using var client = new ProtobufSocketClient<WoWActivitySnapshot, WoWActivitySnapshot>(
            "127.0.0.1", port, NullLogger.Instance);

        var snapshot = new WoWActivitySnapshot
        {
            AccountName = "TESTBOT1",
            IsObjectManagerValid = true,
        };

        var response = client.SendMessage(snapshot);

        Assert.Equal("TESTBOT1", response.AccountName);
        Assert.NotNull(response.CurrentAction);
        Assert.Equal(ActionType.Goto, response.CurrentAction.ActionType);
        Assert.Single(response.CurrentAction.Parameters);
        Assert.Equal(100f, response.CurrentAction.Parameters[0].FloatParam);
        Assert.Equal(1, server.RequestCount);
    }

    [Fact]
    public void DeferredConnect_ClientCanBeConstructedBeforeServerStarts()
    {
        var port = GetFreePort();
        using var client = new ProtobufSocketClient<WoWActivitySnapshot, WoWActivitySnapshot>(
            "127.0.0.1", port, NullLogger.Instance, connectImmediately: false);

        using var server = new TestSocketServer("127.0.0.1", port);
        Thread.Sleep(100);

        var snapshot = new WoWActivitySnapshot
        {
            AccountName = "DEFERRED",
            IsObjectManagerValid = true,
        };

        var response = client.SendMessage(snapshot);

        Assert.Equal("DEFERRED", response.AccountName);
        Assert.Equal(1, server.RequestCount);
    }

    // =========================================================================
    // 2. Multiple sequential messages on same connection
    // =========================================================================

    [Fact]
    public void MultipleMessages_SameConnection_AllSucceed()
    {
        var port = GetFreePort();
        using var server = new TestSocketServer("127.0.0.1", port);
        Thread.Sleep(100);

        using var client = new ProtobufSocketClient<WoWActivitySnapshot, WoWActivitySnapshot>(
            "127.0.0.1", port, NullLogger.Instance);

        for (int i = 0; i < 50; i++)
        {
            var snapshot = new WoWActivitySnapshot
            {
                AccountName = $"BOT{i}",
            };

            var response = client.SendMessage(snapshot);
            Assert.Equal($"BOT{i}", response.AccountName);
        }

        Assert.Equal(50, server.RequestCount);
    }

    // =========================================================================
    // 3. Multiple concurrent clients
    // =========================================================================

    [Fact]
    public void ConcurrentClients_AllReceiveResponses()
    {
        var port = GetFreePort();
        using var server = new TestSocketServer("127.0.0.1", port);
        server.InjectedAction = new ActionMessage { ActionType = ActionType.Wait };
        Thread.Sleep(100);

        const int clientCount = 10;
        var tasks = new Task[clientCount];
        var errors = new ConcurrentBag<string>();

        for (int i = 0; i < clientCount; i++)
        {
            int clientId = i;
            tasks[i] = Task.Run(() =>
            {
                try
                {
                    using var client = new ProtobufSocketClient<WoWActivitySnapshot, WoWActivitySnapshot>(
                        "127.0.0.1", port, NullLogger.Instance);

                    for (int j = 0; j < 10; j++)
                    {
                        var snapshot = new WoWActivitySnapshot
                        {
                            AccountName = $"BOT{clientId}",
                        };

                        var response = client.SendMessage(snapshot);
                        if (response.AccountName != $"BOT{clientId}")
                            errors.Add($"Client {clientId} msg {j}: expected BOT{clientId}, got {response.AccountName}");
                        if (response.CurrentAction?.ActionType != ActionType.Wait)
                            errors.Add($"Client {clientId} msg {j}: missing injected action");
                    }
                }
                catch (Exception ex)
                {
                    errors.Add($"Client {clientId} exception: {ex.Message}");
                }
            });
        }

        Task.WaitAll(tasks, TimeSpan.FromSeconds(30));
        Assert.Empty(errors);
        Assert.Equal(100, server.RequestCount); // 10 clients × 10 messages
        Assert.Equal(10, server.ReceivedAccounts.Distinct().Count());
    }

    // =========================================================================
    // 4. Large snapshot with compression
    // =========================================================================

    [Fact]
    public void LargeSnapshot_CompressedRoundTrip_PreservesData()
    {
        var port = GetFreePort();
        using var server = new TestSocketServer("127.0.0.1", port);
        Thread.Sleep(100);

        using var client = new ProtobufSocketClient<WoWActivitySnapshot, WoWActivitySnapshot>(
            "127.0.0.1", port, NullLogger.Instance);

        // Build a large snapshot with many nearby units (triggers compression)
        var snapshot = new WoWActivitySnapshot
        {
            AccountName = "LOADTEST",
            IsObjectManagerValid = true,
        };

        // Add 50 nearby units to push the message above compression threshold
        for (int i = 0; i < 50; i++)
        {
            snapshot.NearbyUnits.Add(new WoWUnit
            {
                Health = (uint)(100u + i),
                MaxHealth = 200,
                TargetGuid = (ulong)(1000u + i),
                GameObject = new WoWGameObject
                {
                    Name = $"Ragefire Trogg #{i}",
                    Base = new WoWObject
                    {
                        Guid = (ulong)(5000u + i),
                        MapId = 389,
                        Position = new Position { X = -10f + i, Y = -40f, Z = -20f }
                    }
                }
            });
        }

        var rawSize = snapshot.ToByteArray().Length;
        _output.WriteLine($"Snapshot raw size: {rawSize} bytes ({snapshot.NearbyUnits.Count} units)");
        Assert.True(rawSize > 1024, $"Snapshot should be large enough for compression ({rawSize} bytes)");

        var response = client.SendMessage(snapshot);

        Assert.Equal("LOADTEST", response.AccountName);
        Assert.Equal(50, response.NearbyUnits.Count);
        Assert.Equal("Ragefire Trogg #0", response.NearbyUnits[0].GameObject.Name);
        Assert.Equal("Ragefire Trogg #49", response.NearbyUnits[49].GameObject.Name);
    }

    // =========================================================================
    // 5. Compression encode/decode directly
    // =========================================================================

    [Fact]
    public void Compression_SmallMessage_NotCompressed()
    {
        var small = new WoWActivitySnapshot { AccountName = "A" };
        var raw = small.ToByteArray();
        var encoded = ProtobufCompression.Encode(raw);

        // 4-byte length prefix + 1-byte flag (0x00) + raw payload
        Assert.Equal(4 + 1 + raw.Length, encoded.Length);
        Assert.Equal(0x00, encoded[4]); // Not compressed

        var decoded = ProtobufCompression.Decode(encoded.AsSpan(4).ToArray());
        Assert.Equal(raw, decoded);
    }

    [Fact]
    public void Compression_LargeMessage_Compressed()
    {
        var large = new WoWActivitySnapshot { AccountName = "LOADTEST" };
        for (int i = 0; i < 100; i++)
            large.NearbyUnits.Add(new WoWUnit
            {
                Health = 100,
                GameObject = new WoWGameObject { Name = $"Unit {i}" }
            });

        var raw = large.ToByteArray();
        Assert.True(raw.Length > 1024);

        var encoded = ProtobufCompression.Encode(raw);
        // Compressed: 4-byte length + 1-byte flag (0x01) + GZip data
        Assert.Equal(0x01, encoded[4]); // Compressed
        Assert.True(encoded.Length < 4 + 1 + raw.Length, "Compressed should be smaller");

        var wirePayload = encoded.AsSpan(4).ToArray();
        var decoded = ProtobufCompression.Decode(wirePayload);
        Assert.Equal(raw, decoded);
    }

    // =========================================================================
    // 6. Empty snapshot round-trip (edge case)
    // =========================================================================

    [Fact]
    public void EmptySnapshot_RoundTrip_Succeeds()
    {
        var port = GetFreePort();
        using var server = new TestSocketServer("127.0.0.1", port);
        Thread.Sleep(100);

        using var client = new ProtobufSocketClient<WoWActivitySnapshot, WoWActivitySnapshot>(
            "127.0.0.1", port, NullLogger.Instance);

        var snapshot = new WoWActivitySnapshot(); // All defaults
        var response = client.SendMessage(snapshot);

        Assert.Equal("", response.AccountName);
        Assert.Equal(1, server.RequestCount);
    }

    // =========================================================================
    // 7. Snapshot preserves player data through pipeline
    // =========================================================================

    [Fact]
    public void Snapshot_WithPlayerData_PreservedThroughPipeline()
    {
        var port = GetFreePort();
        using var server = new TestSocketServer("127.0.0.1", port);
        Thread.Sleep(100);

        using var client = new ProtobufSocketClient<WoWActivitySnapshot, WoWActivitySnapshot>(
            "127.0.0.1", port, NullLogger.Instance);

        var snapshot = new WoWActivitySnapshot
        {
            AccountName = "RFCBOT1",
            IsObjectManagerValid = true,
            PartyLeaderGuid = 0xDEADBEEF,
            Player = new WoWPlayer
            {
                Unit = new WoWUnit
                {
                    Health = 213,
                    MaxHealth = 213,
                    TargetGuid = 0x1234,
                    GameObject = new WoWGameObject
                    {
                        Name = "Lokgaa",
                        Base = new WoWObject
                        {
                            Guid = 0xABCD,
                            MapId = 389,
                            Position = new Position { X = -13f, Y = -45f, Z = -22f }
                        }
                    }
                }
            }
        };

        var response = client.SendMessage(snapshot);

        Assert.Equal("RFCBOT1", response.AccountName);
        Assert.Equal(0xDEADBEEF, (ulong)response.PartyLeaderGuid);
        Assert.NotNull(response.Player?.Unit?.GameObject?.Base);
        Assert.Equal(389u, response.Player.Unit.GameObject.Base.MapId);
        Assert.Equal(-13f, response.Player.Unit.GameObject.Base.Position.X);
        Assert.Equal("Lokgaa", response.Player.Unit.GameObject.Name);
        Assert.Equal(213u, response.Player.Unit.Health);
        Assert.Equal(0x1234u, (uint)response.Player.Unit.TargetGuid);
    }

    // =========================================================================
    // 8. Server stop/restart — client reconnects
    // =========================================================================

    [Fact(Skip = "TCP TIME_WAIT on port reuse")]
    public void ServerRestart_ClientReconnects()
    {
        var port = GetFreePort();
        using var server1 = new TestSocketServer("127.0.0.1", port);
        Thread.Sleep(100);

        using var client = new ProtobufSocketClient<WoWActivitySnapshot, WoWActivitySnapshot>(
            "127.0.0.1", port, NullLogger.Instance);

        // First message succeeds
        var response1 = client.SendMessage(new WoWActivitySnapshot { AccountName = "BEFORE" });
        Assert.Equal("BEFORE", response1.AccountName);

        // Stop server
        server1.Stop();
        Thread.Sleep(500);

        // Start new server on same port
        using var server2 = new TestSocketServer("127.0.0.1", port);
        Thread.Sleep(500);

        // Client should reconnect and succeed
        var response2 = client.SendMessage(new WoWActivitySnapshot { AccountName = "AFTER" });
        Assert.Equal("AFTER", response2.AccountName);
        Assert.Equal(1, server2.RequestCount);
    }

    // =========================================================================
    // 9. Action message parameter types preserved
    // =========================================================================

    [Fact]
    public void ActionParameters_AllTypes_PreservedThroughPipeline()
    {
        var port = GetFreePort();
        using var server = new TestSocketServer("127.0.0.1", port);
        server.InjectedAction = new ActionMessage
        {
            ActionType = ActionType.StartDungeoneering,
            Parameters =
            {
                new RequestParameter { IntParam = 1 },         // isLeader
                new RequestParameter { IntParam = 389 },       // mapId
                new RequestParameter { FloatParam = 3.14f },   // x
                new RequestParameter { StringParam = "test" }, // label
                new RequestParameter { LongParam = 0xDEAD },   // guid
            }
        };

        Thread.Sleep(100);
        using var client = new ProtobufSocketClient<WoWActivitySnapshot, WoWActivitySnapshot>(
            "127.0.0.1", port, NullLogger.Instance);

        var response = client.SendMessage(new WoWActivitySnapshot { AccountName = "TEST" });

        Assert.NotNull(response.CurrentAction);
        Assert.Equal(ActionType.StartDungeoneering, response.CurrentAction.ActionType);
        Assert.Equal(5, response.CurrentAction.Parameters.Count);
        Assert.Equal(1, response.CurrentAction.Parameters[0].IntParam);
        Assert.Equal(389, response.CurrentAction.Parameters[1].IntParam);
        Assert.InRange(response.CurrentAction.Parameters[2].FloatParam, 3.13f, 3.15f);
        Assert.Equal("test", response.CurrentAction.Parameters[3].StringParam);
        Assert.Equal(0xDEAD, response.CurrentAction.Parameters[4].LongParam);
    }

    // =========================================================================
    // 10. Rapid-fire messages don't corrupt stream
    // =========================================================================

    [Fact]
    public void RapidFireMessages_NoStreamCorruption()
    {
        var port = GetFreePort();
        using var server = new TestSocketServer("127.0.0.1", port);
        Thread.Sleep(100);

        using var client = new ProtobufSocketClient<WoWActivitySnapshot, WoWActivitySnapshot>(
            "127.0.0.1", port, NullLogger.Instance);

        // Send 200 messages as fast as possible (no delay between sends)
        for (int i = 0; i < 200; i++)
        {
            var response = client.SendMessage(new WoWActivitySnapshot
            {
                AccountName = $"RAPID{i}",
            });

            Assert.Equal($"RAPID{i}", response.AccountName);
        }

        Assert.Equal(200, server.RequestCount);
    }
}
