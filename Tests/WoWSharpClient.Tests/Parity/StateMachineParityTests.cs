using System.IO;
using System.Linq;
using GameData.Core.Enums;
using GameData.Core.Models;
using Microsoft.Extensions.Logging.Abstractions;
using WoWSharpClient.Movement;
using WoWSharpClient.Models;
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

    // Pins binary-parity ACK gating per docs/physics/state_teleport.md.
    // WoW.exe gates MSG_MOVE_TELEPORT_ACK on its 0x468570 readiness function,
    // NOT on a physics ground-snap. Once the player has client control and the
    // teleport target is resolved, the ACK fires regardless of _needsGroundSnap.
    [Fact]
    [Trait("Category", "StateMachineParity")]
    public void MoveTeleport_AckFiresAfterControlGrant_RegardlessOfGroundSnapOrSceneData()
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

            trace.Dispatch(Opcode.SMSG_CLIENT_CONTROL_UPDATE, BuildClientControlPacket(playerGuid, canControl: true));

            // Binary parity: ACK fires now even though scene data is unresolved
            // and the physics ground-snap is still pending.
            Assert.True(trace.FlushTeleportAck());
            Assert.Single(trace.Events.Where(e => e.Kind == "outbound" && e.Opcode == Opcode.MSG_MOVE_TELEPORT_ACK));
        }
        finally
        {
            SceneDataClient.TestEnsureSceneDataAroundOverride = originalSceneOverride;
        }
    }

    [Theory]
    [InlineData(Opcode.SMSG_FORCE_MOVE_ROOT, Opcode.CMSG_FORCE_MOVE_ROOT_ACK, true, "OnForceMoveRoot")]
    [InlineData(Opcode.SMSG_FORCE_MOVE_UNROOT, Opcode.CMSG_FORCE_MOVE_UNROOT_ACK, false, "OnForceMoveUnroot")]
    [Trait("Category", "StateMachineParity")]
    public void ForceMoveRootOpcodes_StageStateUntilDeferredFlush(
        Opcode serverOpcode,
        Opcode ackOpcode,
        bool applyRoot,
        string eventLabel)
    {
        using var trace = new PacketFlowTraceFixture();
        const ulong playerGuid = 0x305ul;
        trace.SeedLocalPlayer(playerGuid, mapId: 1, position: new Position(14f, 28f, 6f), facing: 0.5f);

        var player = Assert.IsType<WoWLocalPlayer>(trace.ObjectManager.Player);
        player.MovementFlags = applyRoot
            ? (MovementFlags.MOVEFLAG_FORWARD | MovementFlags.MOVEFLAG_STRAFE_LEFT)
            : (MovementFlags.MOVEFLAG_FORWARD | MovementFlags.MOVEFLAG_ROOT);

        trace.Dispatch(serverOpcode, BuildGuidCounterPacket(playerGuid, counter: 17u));

        if (applyRoot)
        {
            Assert.False(player.MovementFlags.HasFlag(MovementFlags.MOVEFLAG_ROOT));
            Assert.True(player.MovementFlags.HasFlag(MovementFlags.MOVEFLAG_FORWARD));
            Assert.True(player.MovementFlags.HasFlag(MovementFlags.MOVEFLAG_STRAFE_LEFT));
        }
        else
        {
            Assert.True(player.MovementFlags.HasFlag(MovementFlags.MOVEFLAG_ROOT));
            Assert.True(player.MovementFlags.HasFlag(MovementFlags.MOVEFLAG_FORWARD));
        }

        Assert.Empty(trace.Events.Where(e => e.Kind == "outbound"));

        var eventIndex = trace.Events.FindIndex(e => e.Kind == "event" && e.Label == eventLabel);
        Assert.True(eventIndex >= 0);

        Assert.Equal(1, trace.FlushDeferredMovementChanges(gameTimeMs: 3000u));

        var outbound = Assert.Single(trace.Events.Where(e => e.Kind == "outbound" && e.Opcode == ackOpcode));
        var outboundIndex = trace.Events.FindIndex(e => ReferenceEquals(e, outbound));
        Assert.True(outboundIndex > eventIndex);

        if (applyRoot)
        {
            Assert.True(player.MovementFlags.HasFlag(MovementFlags.MOVEFLAG_ROOT));
            Assert.False(player.MovementFlags.HasFlag(MovementFlags.MOVEFLAG_FORWARD));
            Assert.False(player.MovementFlags.HasFlag(MovementFlags.MOVEFLAG_STRAFE_LEFT));
        }
        else
        {
            Assert.False(player.MovementFlags.HasFlag(MovementFlags.MOVEFLAG_ROOT));
            Assert.True(player.MovementFlags.HasFlag(MovementFlags.MOVEFLAG_FORWARD));
        }
    }

    [Fact]
    [Trait("Category", "StateMachineParity")]
    public void MoveKnockBack_StagesImpulseUntilConsumedThenAcks()
    {
        using var trace = new PacketFlowTraceFixture();
        const ulong playerGuid = 0x306ul;
        trace.SeedLocalPlayer(playerGuid, mapId: 1, position: new Position(16f, 32f, 8f), facing: 1.0f);

        var player = Assert.IsType<WoWLocalPlayer>(trace.ObjectManager.Player);
        player.MovementFlags = MovementFlags.MOVEFLAG_FORWARD | MovementFlags.MOVEFLAG_STRAFE_RIGHT;

        trace.Dispatch(
            Opcode.SMSG_MOVE_KNOCK_BACK,
            BuildKnockBackPacket(playerGuid, counter: 23u, vSin: -0.5f, vCos: 0.25f, hSpeed: 7.5f, vSpeed: 4.0f));

        Assert.True(player.MovementFlags.HasFlag(MovementFlags.MOVEFLAG_FALLINGFAR));
        Assert.False(player.MovementFlags.HasFlag(MovementFlags.MOVEFLAG_FORWARD));
        Assert.False(player.MovementFlags.HasFlag(MovementFlags.MOVEFLAG_STRAFE_RIGHT));
        Assert.Empty(trace.Events.Where(e => e.Kind == "outbound"));

        var eventIndex = trace.Events.FindIndex(e => e.Kind == "event" && e.Label == "OnForceMoveKnockBack");
        Assert.True(eventIndex >= 0);

        Assert.True(trace.ConsumeKnockbackAndFlushAck(gameTimeMs: 4000u));

        var consumeIndex = trace.Events.FindIndex(e => e.Kind == "consume" && e.Label == "KnockBackImpulse");
        var outbound = Assert.Single(trace.Events.Where(e => e.Kind == "outbound" && e.Opcode == Opcode.CMSG_MOVE_KNOCK_BACK_ACK));
        var outboundIndex = trace.Events.FindIndex(e => ReferenceEquals(e, outbound));

        Assert.True(consumeIndex > eventIndex);
        Assert.True(outboundIndex > consumeIndex);
        Assert.False(trace.ConsumeKnockbackAndFlushAck(gameTimeMs: 4001u));
    }

    [Fact]
    [Trait("Category", "StateMachineParity")]
    public void ClientControlUpdate_LocalPlayer_FollowsCanControlAndBlocksReconcile()
    {
        using var trace = new PacketFlowTraceFixture();
        const ulong playerGuid = 0x303ul;
        trace.SeedLocalPlayer(playerGuid, mapId: 1, position: new Position(12f, 18f, 4f), facing: 0.25f);
        trace.SetIsBeingTeleported(true);

        trace.Dispatch(Opcode.SMSG_CLIENT_CONTROL_UPDATE, BuildClientControlPacket(playerGuid, canControl: false));

        var lossEvent = Assert.Single(trace.Events.Where(e => e.Kind == "event" && e.Label == "OnClientControlUpdate"));
        Assert.Equal(playerGuid, lossEvent.Guid);
        Assert.False(lossEvent.BooleanValue);
        Assert.False(trace.IsInControl());
        Assert.True(trace.IsBeingTeleported());

        trace.ReconcilePlayerControlState();
        Assert.False(trace.IsInControl());

        trace.Dispatch(Opcode.SMSG_CLIENT_CONTROL_UPDATE, BuildClientControlPacket(playerGuid, canControl: true));

        Assert.True(trace.IsInControl());
        Assert.False(trace.IsBeingTeleported());
        Assert.Equal(
            new bool?[] { false, true },
            trace.Events
                .Where(e => e.Kind == "event" && e.Label == "OnClientControlUpdate")
                .Select(e => e.BooleanValue)
                .ToArray());
    }

    [Fact]
    [Trait("Category", "StateMachineParity")]
    public void ClientControlUpdate_RemoteGuid_DoesNotChangeLocalControlState()
    {
        using var trace = new PacketFlowTraceFixture();
        const ulong playerGuid = 0x304ul;
        trace.SeedLocalPlayer(playerGuid, mapId: 1, position: new Position(8f, 9f, 10f), facing: 0.75f);
        trace.SetIsBeingTeleported(true);

        trace.Dispatch(Opcode.SMSG_CLIENT_CONTROL_UPDATE, BuildClientControlPacket(0x9ABCul, canControl: false));

        Assert.True(trace.IsInControl());
        Assert.True(trace.IsBeingTeleported());
        var controlEvent = Assert.Single(trace.Events.Where(e => e.Kind == "event" && e.Label == "OnClientControlUpdate"));
        Assert.Equal(0x9ABCul, controlEvent.Guid);
        Assert.False(controlEvent.BooleanValue);
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

    private static byte[] BuildClientControlPacket(ulong guid, bool canControl)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        WoWSharpClient.Utils.ReaderUtils.WritePackedGuid(writer, guid);
        writer.Write(canControl);
        return ms.ToArray();
    }

    private static byte[] BuildGuidCounterPacket(ulong guid, uint counter)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        WoWSharpClient.Utils.ReaderUtils.WritePackedGuid(writer, guid);
        writer.Write(counter);
        return ms.ToArray();
    }

    private static byte[] BuildKnockBackPacket(ulong guid, uint counter, float vSin, float vCos, float hSpeed, float vSpeed)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        WoWSharpClient.Utils.ReaderUtils.WritePackedGuid(writer, guid);
        writer.Write(counter);
        writer.Write(vSin);
        writer.Write(vCos);
        writer.Write(hSpeed);
        writer.Write(vSpeed);
        return ms.ToArray();
    }
}
