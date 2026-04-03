using System.Net;
using System.Net.Sockets;
using BotCommLayer;
using Communication;
using Google.Protobuf;
using Tests.Infrastructure;

namespace BotRunner.Tests;

public sealed class StateManagerTestClientTimeoutTests
{
    [Fact]
    public async Task QuerySnapshots_AllAccounts_UsesExtendedTimeoutBudget()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        var serverTask = Task.Run(async () =>
        {
            using var acceptedClient = await listener.AcceptTcpClientAsync();
            using var stream = acceptedClient.GetStream();

            await ReadMessageAsync(stream);
            await Task.Delay(250);

            var response = new StateChangeResponse();
            response.Snapshots.Add(new WoWActivitySnapshot
            {
                AccountName = "TESTBOT1",
                CharacterName = "Testbot",
                ScreenState = "InWorld",
                IsObjectManagerValid = true,
                Player = new Game.WoWPlayer
                {
                    Unit = new Game.WoWUnit
                    {
                        MaxHealth = 100,
                        GameObject = new Game.WoWGameObject
                        {
                            Base = new Game.WoWObject
                            {
                                Position = new Game.Position { X = 1f, Y = 2f, Z = 3f }
                            }
                        }
                    }
                }
            });

            await WriteMessageAsync(stream, response);
        });

        using var client = new StateManagerTestClient(
            port: port,
            defaultRequestTimeout: TimeSpan.FromMilliseconds(100),
            fullSnapshotRequestTimeout: TimeSpan.FromMilliseconds(500));

        await client.ConnectAsync();
        var snapshots = await client.QuerySnapshotsAsync();

        Assert.Single(snapshots);
        Assert.Equal("TESTBOT1", snapshots[0].AccountName);

        await serverTask;
    }

    [Fact]
    public async Task QuerySnapshots_SingleAccount_RetainsDefaultTimeoutBudget()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        var serverTask = Task.Run(async () =>
        {
            using var acceptedClient = await listener.AcceptTcpClientAsync();
            using var stream = acceptedClient.GetStream();

            await ReadMessageAsync(stream);
            await Task.Delay(250);
        });

        using var client = new StateManagerTestClient(
            port: port,
            defaultRequestTimeout: TimeSpan.FromMilliseconds(100),
            fullSnapshotRequestTimeout: TimeSpan.FromMilliseconds(500));

        await client.ConnectAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => client.QuerySnapshotsAsync("TESTBOT1"));
        await serverTask;
    }

    private static async Task ReadMessageAsync(NetworkStream stream)
    {
        var lengthBuffer = new byte[4];
        await ReadExactAsync(stream, lengthBuffer);
        var payloadLength = BitConverter.ToInt32(lengthBuffer, 0);

        var payload = new byte[payloadLength];
        await ReadExactAsync(stream, payload);

        var request = new AsyncRequest();
        request.MergeFrom(ProtobufCompression.Decode(payload));
    }

    private static async Task WriteMessageAsync(NetworkStream stream, StateChangeResponse response)
    {
        var encoded = ProtobufCompression.Encode(response.ToByteArray());
        await stream.WriteAsync(encoded);
        await stream.FlushAsync();
    }

    private static async Task ReadExactAsync(NetworkStream stream, byte[] buffer)
    {
        var totalRead = 0;
        while (totalRead < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(totalRead, buffer.Length - totalRead));
            if (read == 0)
                throw new IOException("Connection closed while reading.");

            totalRead += read;
        }
    }
}
