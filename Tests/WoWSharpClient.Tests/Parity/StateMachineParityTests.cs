using System.IO;
using System.Linq;
using GameData.Core.Enums;
using GameData.Core.Models;
using Microsoft.Extensions.Logging.Abstractions;
using WoWSharpClient.Movement;
using Xunit;

namespace WoWSharpClient.Tests.Parity;

[Collection("Sequential ObjectManager tests")]
public sealed class StateMachineParityTests
{
    [Fact]
    [Trait("Category", "StateMachineParity")]
    public void LoginVerifyWorld_DoesNotSendWorldportAck()
    {
        using var trace = new PacketFlowTraceFixture();
        trace.SeedLocalPlayer(0x300ul, mapId: 0);

        trace.Dispatch(
            Opcode.SMSG_LOGIN_VERIFY_WORLD,
            BuildWorldInfoPacket(mapId: 1u, position: new Position(10f, 20f, 30f), facing: 1.25f));

        Assert.Equal((uint)1, trace.ObjectManager.Player.MapId);
        Assert.Empty(trace.Events.Where(e => e.Kind == "outbound" && e.Opcode == Opcode.MSG_MOVE_WORLDPORT_ACK));
    }

    [Fact]
    [Trait("Category", "StateMachineParity")]
    public void NewWorld_SendsExactlyOneWorldportAck_AfterWorldInfoUpdate()
    {
        using var trace = new PacketFlowTraceFixture();
        trace.SeedLocalPlayer(0x301ul, mapId: 0);

        trace.Dispatch(
            Opcode.SMSG_NEW_WORLD,
            BuildWorldInfoPacket(mapId: 530u, position: new Position(-50f, 75f, 12f), facing: 2.5f));

        var worldInfoIndex = trace.Events.FindIndex(e => e.Kind == "event" && e.Label == "OnLoginVerifyWorld");
        var outboundIndex = trace.Events.FindIndex(e => e.Kind == "outbound" && e.Opcode == Opcode.MSG_MOVE_WORLDPORT_ACK);

        Assert.Equal((uint)530, trace.ObjectManager.Player.MapId);
        Assert.True(worldInfoIndex >= 0);
        Assert.True(outboundIndex > worldInfoIndex);
        Assert.Single(trace.Events.Where(e => e.Kind == "outbound" && e.Opcode == Opcode.MSG_MOVE_WORLDPORT_ACK));
    }

    [Fact]
    [Trait("Category", "StateMachineParity")]
    public void MoveTeleport_AckWaitsForGroundSnap_ButNotSceneData()
    {
        var originalSceneOverride = SceneDataClient.TestEnsureSceneDataAroundOverride;
        SceneDataClient.TestEnsureSceneDataAroundOverride = (_, _, _) => false;

        try
        {
            using var trace = new PacketFlowTraceFixture(
                sceneDataClient: new SceneDataClient(NullLogger<SceneDataClient>.Instance),
                useLocalPhysics: true);
            const ulong playerGuid = 0x302ul;
            trace.SeedLocalPlayer(playerGuid, mapId: 1, position: new Position(1f, 2f, 3f), facing: 0.1f, fixedWorldTimeMs: 5151);
            trace.EnsureTeleportAckFlushSupport();

            trace.Dispatch(
                Opcode.MSG_MOVE_TELEPORT,
                BuildTeleportPacket(
                    playerGuid,
                    new Position(150f, 250f, 350f),
                    facing: 1.75f,
                    clientTimeMs: 1600u));

            Assert.False(trace.FlushTeleportAck());
            trace.MarkTeleportGroundSnapResolved();
            Assert.True(trace.FlushTeleportAck());
            Assert.Single(trace.Events.Where(e => e.Kind == "outbound" && e.Opcode == Opcode.MSG_MOVE_TELEPORT_ACK));
        }
        finally
        {
            SceneDataClient.TestEnsureSceneDataAroundOverride = originalSceneOverride;
        }
    }

    private static byte[] BuildWorldInfoPacket(uint mapId, Position position, float facing)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        writer.Write(mapId);
        writer.Write(position.X);
        writer.Write(position.Y);
        writer.Write(position.Z);
        writer.Write(facing);
        return ms.ToArray();
    }

    private static byte[] BuildTeleportPacket(ulong guid, Position position, float facing, uint clientTimeMs)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        WoWSharpClient.Utils.ReaderUtils.WritePackedGuid(writer, guid);

        var player = new WoWSharpClient.Models.WoWLocalPlayer(new HighGuid(guid))
        {
            Position = position,
            Facing = facing,
            MovementFlags = MovementFlags.MOVEFLAG_NONE,
            FallTime = 0,
        };

        writer.Write(WoWSharpClient.Parsers.MovementPacketHandler.BuildMovementInfoBuffer(player, clientTimeMs, 0u));
        return ms.ToArray();
    }
}
