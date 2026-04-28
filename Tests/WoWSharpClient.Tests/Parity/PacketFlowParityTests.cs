using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GameData.Core.Enums;
using GameData.Core.Models;
using WoWSharpClient.Handlers;
using WoWSharpClient.Models;
using WoWSharpClient.Parsers;
using WoWSharpClient.Utils;
using Xunit;
using static GameData.Core.Enums.UpdateFields;

namespace WoWSharpClient.Tests.Parity;

[Collection("Sequential ObjectManager tests")]
public sealed class PacketFlowParityTests
{
    [Fact]
    [Trait("Category", "PacketFlowParity")]
    public void UpdateObjectAdd_RemoteUnit_MutatesStateWithoutOutboundPacket()
    {
        using var trace = new PacketFlowTraceFixture();
        trace.SeedLocalPlayer(0x120ul);

        const ulong remoteGuid = 0xF130000000001001ul;
        trace.Dispatch(
            Opcode.SMSG_UPDATE_OBJECT,
            BuildCreateObjectPacket(
                remoteGuid,
                WoWObjectType.Unit,
                movementPosition: new Position(40f, 50f, 60f),
                movementFacing: 1.5f,
                fields: new SortedDictionary<uint, object?>
                {
                    [(uint)EObjectFields.OBJECT_FIELD_ENTRY] = 88u,
                    [(uint)EObjectFields.OBJECT_FIELD_SCALE_X] = 1.0f,
                    [(uint)EUnitFields.UNIT_FIELD_HEALTH] = 777u,
                    [(uint)EUnitFields.UNIT_FIELD_MAXHEALTH] = 1000u,
                    [(uint)EUnitFields.UNIT_FIELD_LEVEL] = 12u,
                }));

        var unit = Assert.IsType<WoWUnit>(trace.ObjectManager.GetObjectByGuid(remoteGuid));
        Assert.Equal(777u, unit.Health);
        Assert.Equal(12u, unit.Level);
        Assert.Equal(40f, unit.Position.X, 3);
        Assert.Equal(50f, unit.Position.Y, 3);
        Assert.Equal(60f, unit.Position.Z, 3);
        Assert.Empty(Outbound(trace, Opcode.MSG_MOVE_WORLDPORT_ACK));
        Assert.Empty(trace.Events.Where(e => e.Kind == "outbound"));
        Assert.Equal(
            [
                (WoWSharpObjectManager.TestMutationStage.FieldsApplied, "create"),
                (WoWSharpObjectManager.TestMutationStage.MovementApplied, "create")
            ],
            trace.Events
                .Where(e => e.Kind == "mutation" && e.Guid == remoteGuid)
                .Select(e => (e.MutationStage!.Value, e.Context!))
                .ToArray());
    }

    [Fact]
    [Trait("Category", "PacketFlowParity")]
    public void UpdateObjectUpdate_LocalPlayer_MutatesStateWithoutOutboundPacket()
    {
        using var trace = new PacketFlowTraceFixture();
        const ulong playerGuid = 0x122ul;
        trace.SeedLocalPlayer(playerGuid, position: new Position(1f, 2f, 3f), facing: 0.1f);
        ((WoWLocalPlayer)trace.ObjectManager.Player).Health = 10u;

        trace.Dispatch(
            Opcode.SMSG_UPDATE_OBJECT,
            BuildPartialThenMovementPacket(
                playerGuid,
                new SortedDictionary<uint, object?>
                {
                    [(uint)EUnitFields.UNIT_FIELD_HEALTH] = 600u,
                    [(uint)EUnitFields.UNIT_FIELD_MAXHEALTH] = 1200u,
                },
                new Position(70f, 80f, 90f),
                2.25f));

        var player = Assert.IsType<WoWLocalPlayer>(trace.ObjectManager.Player);
        Assert.Equal(600u, player.Health);
        Assert.Equal(1f, player.Position.X, 3);
        Assert.Equal(2f, player.Position.Y, 3);
        Assert.Equal(3f, player.Position.Z, 3);
        Assert.Equal(0.1f, player.Facing, 3);
        Assert.Equal(
            [
                (WoWSharpObjectManager.TestMutationStage.FieldsApplied, "update"),
                (WoWSharpObjectManager.TestMutationStage.MovementApplied, "update")
            ],
            trace.Events
                .Where(e => e.Kind == "mutation" && e.Guid == playerGuid)
                .Select(e => (e.MutationStage!.Value, e.Context!))
                .ToArray());
        Assert.Empty(trace.Events.Where(e => e.Kind == "outbound"));
    }

    [Fact]
    [Trait("Category", "PacketFlowParity")]
    public void ForceRunSpeedChange_QueuesDeferredAck_ThenFlushesWithUpdatedState()
    {
        using var trace = new PacketFlowTraceFixture();
        const ulong playerGuid = 0x200ul;
        trace.SeedLocalPlayer(playerGuid, position: new Position(10f, 20f, 30f), facing: 0.5f);

        trace.Dispatch(Opcode.SMSG_FORCE_RUN_SPEED_CHANGE, BuildGuidCounterSpeedPacket(playerGuid, counter: 7u, speed: 9.5f));

        var player = Assert.IsType<WoWLocalPlayer>(trace.ObjectManager.Player);
        Assert.Equal(7.0f, player.RunSpeed, 3);
        Assert.Empty(trace.Events.Where(e => e.Kind == "outbound"));

        Assert.Equal(1, trace.FlushDeferredMovementChanges(gameTimeMs: 2000u));

        Assert.Equal(9.5f, player.RunSpeed, 3);
        var outbound = Assert.Single(Outbound(trace, Opcode.CMSG_FORCE_RUN_SPEED_CHANGE_ACK));
        var expectedPayload = MovementPacketHandler.BuildForceSpeedChangeAck(player, 7u, 2000u, 9.5f);
        Assert.Equal(EncodeRawClientPacket(Opcode.CMSG_FORCE_RUN_SPEED_CHANGE_ACK, expectedPayload), EncodeRawClientPacket(outbound.Opcode!.Value, outbound.Payload!));
    }

    // Pins the queue-first deferred-ACK contract from
    // docs/physics/smsg_force_speed_change_handler.md and packet_ack_timing.md
    // Q2 across the FIVE remaining speed-change variants. WoW.exe's first-stage
    // handler stages the change into a movement queue (slots 0x14-0x19) and
    // does not call any send helper inline; the apply + ACK both happen in the
    // later flush. The original ForceRunSpeedChange test only covers run speed;
    // this extends parity coverage to walk/run-back/swim/swim-back/turn-rate.
    [Theory]
    [InlineData(Opcode.SMSG_FORCE_RUN_BACK_SPEED_CHANGE, Opcode.CMSG_FORCE_RUN_BACK_SPEED_CHANGE_ACK, "RunBack")]
    [InlineData(Opcode.SMSG_FORCE_SWIM_SPEED_CHANGE, Opcode.CMSG_FORCE_SWIM_SPEED_CHANGE_ACK, "Swim")]
    [InlineData(Opcode.SMSG_FORCE_SWIM_BACK_SPEED_CHANGE, Opcode.CMSG_FORCE_SWIM_BACK_SPEED_CHANGE_ACK, "SwimBack")]
    [InlineData(Opcode.SMSG_FORCE_WALK_SPEED_CHANGE, Opcode.CMSG_FORCE_WALK_SPEED_CHANGE_ACK, "Walk")]
    [InlineData(Opcode.SMSG_FORCE_TURN_RATE_CHANGE, Opcode.CMSG_FORCE_TURN_RATE_CHANGE_ACK, "TurnRate")]
    [Trait("Category", "PacketFlowParity")]
    public void ForceSpeedChangeFamily_QueuesDeferredAck_ThenFlushesWithUpdatedState(
        Opcode inboundOpcode,
        Opcode ackOpcode,
        string speedField)
    {
        using var trace = new PacketFlowTraceFixture();
        const ulong playerGuid = 0x210ul;
        trace.SeedLocalPlayer(playerGuid, position: new Position(10f, 20f, 30f), facing: 0.5f);
        var player = Assert.IsType<WoWLocalPlayer>(trace.ObjectManager.Player);

        const float newSpeed = 8.75f;
        trace.Dispatch(inboundOpcode, BuildGuidCounterSpeedPacket(playerGuid, counter: 13u, speed: newSpeed));

        // Queue-first: no outbound packet emitted from the inbound leaf.
        Assert.Empty(trace.Events.Where(e => e.Kind == "outbound"));
        Assert.NotEqual(newSpeed, GetSpeedField(player, speedField));

        Assert.Equal(1, trace.FlushDeferredMovementChanges(gameTimeMs: 5000u));

        Assert.Equal(newSpeed, GetSpeedField(player, speedField), 3);
        var outbound = Assert.Single(Outbound(trace, ackOpcode));
        var expectedPayload = MovementPacketHandler.BuildForceSpeedChangeAck(player, 13u, 5000u, newSpeed);
        Assert.Equal(EncodeRawClientPacket(ackOpcode, expectedPayload), EncodeRawClientPacket(outbound.Opcode!.Value, outbound.Payload!));
    }

    private static float GetSpeedField(WoWLocalPlayer player, string field) => field switch
    {
        "RunBack" => player.RunBackSpeed,
        "Walk" => player.WalkSpeed,
        "Swim" => player.SwimSpeed,
        "SwimBack" => player.SwimBackSpeed,
        "TurnRate" => player.TurnRate,
        _ => throw new InvalidOperationException($"Unknown speed field {field}"),
    };

    [Fact]
    [Trait("Category", "PacketFlowParity")]
    public void ForceMoveRoot_QueuesDeferredAck_ThenFlushesWithUpdatedState()
    {
        using var trace = new PacketFlowTraceFixture();
        const ulong playerGuid = 0x201ul;
        trace.SeedLocalPlayer(playerGuid, position: new Position(11f, 22f, 33f), facing: 1.0f);
        var player = Assert.IsType<WoWLocalPlayer>(trace.ObjectManager.Player);
        player.MovementFlags = MovementFlags.MOVEFLAG_FORWARD;

        trace.Dispatch(Opcode.SMSG_FORCE_MOVE_ROOT, BuildGuidCounterPacket(playerGuid, counter: 9u));

        Assert.False(player.MovementFlags.HasFlag(MovementFlags.MOVEFLAG_ROOT));
        Assert.Empty(trace.Events.Where(e => e.Kind == "outbound"));

        Assert.Equal(1, trace.FlushDeferredMovementChanges(gameTimeMs: 3000u));

        Assert.True(player.MovementFlags.HasFlag(MovementFlags.MOVEFLAG_ROOT));
        Assert.False(player.MovementFlags.HasFlag(MovementFlags.MOVEFLAG_FORWARD));
        var outbound = Assert.Single(Outbound(trace, Opcode.CMSG_FORCE_MOVE_ROOT_ACK));
        var expectedPayload = MovementPacketHandler.BuildForceMoveAck(player, 9u, 3000u);
        Assert.Equal(EncodeRawClientPacket(Opcode.CMSG_FORCE_MOVE_ROOT_ACK, expectedPayload), EncodeRawClientPacket(outbound.Opcode!.Value, outbound.Payload!));
    }

    // Pins the queue-first deferred-ACK contract from
    // docs/physics/smsg_move_flag_toggle_handler.md across the THREE
    // movement-flag-toggle opcode pairs. WoW.exe's first-stage handler
    // for SMSG_MOVE_{WATER_WALK,LAND_WALK,SET_HOVER,UNSET_HOVER,
    // FEATHER_FALL,NORMAL_FALL} stages the change via the same
    // deferred-movement queue as ROOT/UNROOT and the speed family; the
    // matching CMSG_MOVE_{WATER_WALK,HOVER,FEATHER_FALL}_ACK fires only
    // on FlushDeferredMovementChanges with a trailing 1.0f marker for
    // set/apply or 0.0f for clear/remove (pinned in
    // MovementPacketHandler.BuildMovementFlagToggleAck and the
    // AckBinaryParityTests golden corpus). This regression test covers
    // the timing half of the audit's "PASS layout / PARTIAL timing"
    // entries so a future divergence in the queue-first dispatch path
    // is caught at the unit-test level, not in live integration.
    [Theory]
    [InlineData(Opcode.SMSG_MOVE_WATER_WALK, Opcode.CMSG_MOVE_WATER_WALK_ACK, MovementFlags.MOVEFLAG_WATERWALKING, true)]
    [InlineData(Opcode.SMSG_MOVE_LAND_WALK, Opcode.CMSG_MOVE_WATER_WALK_ACK, MovementFlags.MOVEFLAG_WATERWALKING, false)]
    [InlineData(Opcode.SMSG_MOVE_SET_HOVER, Opcode.CMSG_MOVE_HOVER_ACK, MovementFlags.MOVEFLAG_HOVER, true)]
    [InlineData(Opcode.SMSG_MOVE_UNSET_HOVER, Opcode.CMSG_MOVE_HOVER_ACK, MovementFlags.MOVEFLAG_HOVER, false)]
    [InlineData(Opcode.SMSG_MOVE_FEATHER_FALL, Opcode.CMSG_MOVE_FEATHER_FALL_ACK, MovementFlags.MOVEFLAG_SAFE_FALL, true)]
    [InlineData(Opcode.SMSG_MOVE_NORMAL_FALL, Opcode.CMSG_MOVE_FEATHER_FALL_ACK, MovementFlags.MOVEFLAG_SAFE_FALL, false)]
    [Trait("Category", "PacketFlowParity")]
    public void MovementFlagToggleFamily_QueuesDeferredAck_ThenFlushesWithUpdatedFlag(
        Opcode inboundOpcode,
        Opcode ackOpcode,
        MovementFlags toggledFlag,
        bool apply)
    {
        using var trace = new PacketFlowTraceFixture();
        const ulong playerGuid = 0x220ul;
        trace.SeedLocalPlayer(playerGuid, position: new Position(15f, 25f, 35f), facing: 1.25f);
        var player = Assert.IsType<WoWLocalPlayer>(trace.ObjectManager.Player);

        // Start in the opposite state of what the inbound opcode requests so we can
        // observe the flush actually mutates the flag (apply=true → starts cleared;
        // apply=false → starts set).
        player.MovementFlags = apply ? MovementFlags.MOVEFLAG_NONE : toggledFlag;

        const uint counter = 17u;
        trace.Dispatch(inboundOpcode, BuildGuidCounterPacket(playerGuid, counter));

        // Queue-first: no outbound packet emitted from the inbound leaf,
        // and the flag has not yet been mutated.
        Assert.Empty(trace.Events.Where(e => e.Kind == "outbound"));
        Assert.Equal(!apply, player.MovementFlags.HasFlag(toggledFlag));

        const uint flushTimeMs = 6000u;
        Assert.Equal(1, trace.FlushDeferredMovementChanges(gameTimeMs: flushTimeMs));

        Assert.Equal(apply, player.MovementFlags.HasFlag(toggledFlag));
        var outbound = Assert.Single(Outbound(trace, ackOpcode));
        var expectedPayload = MovementPacketHandler.BuildMovementFlagToggleAck(player, counter, flushTimeMs, apply);
        Assert.Equal(EncodeRawClientPacket(ackOpcode, expectedPayload), EncodeRawClientPacket(outbound.Opcode!.Value, outbound.Payload!));
    }

    [Fact]
    [Trait("Category", "PacketFlowParity")]
    public void MoveKnockBack_StagesImpulse_BeforeAckFlush()
    {
        using var trace = new PacketFlowTraceFixture();
        const ulong playerGuid = 0x202ul;
        trace.SeedLocalPlayer(playerGuid, position: new Position(12f, 24f, 36f), facing: 1.5f);
        var player = Assert.IsType<WoWLocalPlayer>(trace.ObjectManager.Player);
        player.MovementFlags = MovementFlags.MOVEFLAG_FORWARD | MovementFlags.MOVEFLAG_STRAFE_LEFT;

        trace.Dispatch(
            Opcode.SMSG_MOVE_KNOCK_BACK,
            BuildKnockBackPacket(playerGuid, counter: 11u, vSin: 0.6f, vCos: 0.8f, hSpeed: 5.5f, vSpeed: 3.25f));

        Assert.True(player.MovementFlags.HasFlag(MovementFlags.MOVEFLAG_JUMPING));
        Assert.False(player.MovementFlags.HasFlag(MovementFlags.MOVEFLAG_FALLINGFAR));
        Assert.True(player.MovementFlags.HasFlag(MovementFlags.MOVEFLAG_FORWARD));
        Assert.True(player.MovementFlags.HasFlag(MovementFlags.MOVEFLAG_STRAFE_LEFT));
        Assert.Empty(trace.Events.Where(e => e.Kind == "outbound"));

        Assert.True(trace.ConsumeKnockbackAndFlushAck(gameTimeMs: 4000u));

        var outbound = Assert.Single(Outbound(trace, Opcode.CMSG_MOVE_KNOCK_BACK_ACK));
        var expectedPayload = MovementPacketHandler.BuildForceMoveAck(player, 11u, 4000u);
        Assert.Equal(EncodeRawClientPacket(Opcode.CMSG_MOVE_KNOCK_BACK_ACK, expectedPayload), EncodeRawClientPacket(outbound.Opcode!.Value, outbound.Payload!));
    }

    [Fact]
    [Trait("Category", "PacketFlowParity")]
    public void MoveTeleport_UpdatesPlayerState_ThenFlushesDeferredAck()
    {
        using var trace = new PacketFlowTraceFixture();
        const ulong playerGuid = 0x203ul;
        trace.SeedLocalPlayer(playerGuid, mapId: 1, position: new Position(1f, 2f, 3f), facing: 0.1f, fixedWorldTimeMs: 4242);
        trace.EnsureTeleportAckFlushSupport();

        var player = Assert.IsType<WoWLocalPlayer>(trace.ObjectManager.Player);
        player.MovementFlags =
            MovementFlags.MOVEFLAG_FORWARD
            | MovementFlags.MOVEFLAG_JUMPING
            | MovementFlags.MOVEFLAG_FALLINGFAR
            | MovementFlags.MOVEFLAG_SWIMMING;

        trace.Dispatch(
            Opcode.MSG_MOVE_TELEPORT,
            BuildTeleportPacket(
                playerGuid,
                new Position(100f, 200f, 300f),
                facing: 2.0f,
                clientTimeMs: 1234u));

        Assert.Equal(100f, player.Position.X, 3);
        Assert.Equal(200f, player.Position.Y, 3);
        Assert.Equal(300f, player.Position.Z, 3);
        Assert.Equal(MovementFlags.MOVEFLAG_NONE, player.MovementFlags);
        Assert.Empty(trace.Events.Where(e => e.Kind == "outbound"));

        var teleportEvent = Assert.Single(trace.Events.Where(e => e.Kind == "event" && e.Label == "OnTeleport"));
        // Binary parity (docs/physics/state_teleport.md): no need to wait for the
        // physics ground-snap; the ACK fires once the readiness gates pass.
        Assert.True(trace.FlushTeleportAck());

        var outbound = Assert.Single(Outbound(trace, Opcode.MSG_MOVE_TELEPORT_ACK));
        var expectedPayload = MovementPacketHandler.BuildMoveTeleportAckPayload(player, teleportEvent.Counter!.Value, 4242u);
        Assert.Equal(EncodeRawClientPacket(Opcode.MSG_MOVE_TELEPORT_ACK, expectedPayload), EncodeRawClientPacket(outbound.Opcode!.Value, outbound.Payload!));
    }

    [Fact]
    [Trait("Category", "PacketFlowParity")]
    public void NewWorld_UpdatesWorldState_AndSendsSingleWorldportAck()
    {
        using var trace = new PacketFlowTraceFixture();
        trace.SeedLocalPlayer(0x204ul, mapId: 0, position: new Position(0f, 0f, 0f));

        trace.Dispatch(
            Opcode.SMSG_NEW_WORLD,
            BuildWorldInfoPacket(mapId: 530u, position: new Position(-1042.5f, 2010.25f, 88.75f), facing: 0.75f));

        var player = Assert.IsType<WoWLocalPlayer>(trace.ObjectManager.Player);
        Assert.Equal((uint)530, player.MapId);
        Assert.Equal(-1042.5f, player.Position.X, 3);
        Assert.Equal(2010.25f, player.Position.Y, 3);
        Assert.Equal(88.75f, player.Position.Z, 3);
        Assert.Equal(0.75f, player.Facing, 3);

        var outbound = Assert.Single(Outbound(trace, Opcode.MSG_MOVE_WORLDPORT_ACK));
        Assert.Equal(EncodeRawClientPacket(Opcode.MSG_MOVE_WORLDPORT_ACK, []), EncodeRawClientPacket(outbound.Opcode!.Value, outbound.Payload!));
        Assert.True(IndexOf(trace, "event", "OnLoginVerifyWorld") < IndexOf(trace, "outbound", Opcode.MSG_MOVE_WORLDPORT_ACK.ToString()));
    }

    [Fact]
    [Trait("Category", "PacketFlowParity")]
    public void MonsterMove_UpdatesRemoteSplineState_WithoutAck()
    {
        using var trace = new PacketFlowTraceFixture();
        trace.SeedLocalPlayer(0x205ul);

        const ulong remoteGuid = 0xF130000000000777ul;
        trace.AddRemoteUnit(remoteGuid);

        trace.Dispatch(
            Opcode.SMSG_MONSTER_MOVE,
            BuildLinearMonsterMovePacket(
                remoteGuid,
                start: new Position(25f, 35f, 45f),
                startTimeMs: 555u,
                durationMs: 1200u,
                points:
                [
                    new Position(30f, 40f, 45f),
                    new Position(35f, 45f, 46f)
                ]));

        var unit = Assert.IsType<WoWUnit>(trace.ObjectManager.GetObjectByGuid(remoteGuid));
        Assert.Equal(25f, unit.Position.X, 3);
        Assert.Equal(35f, unit.Position.Y, 3);
        Assert.Equal(45f, unit.Position.Z, 3);
        Assert.Equal(1200u, unit.SplineTimestamp);
        Assert.Equal(2, unit.SplinePoints.Count);
        Assert.Empty(trace.Events.Where(e => e.Kind == "outbound"));
    }

    private static PacketFlowTraceEvent[] Outbound(PacketFlowTraceFixture trace, Opcode opcode)
        => trace.Events.Where(e => e.Kind == "outbound" && e.Opcode == opcode).ToArray();

    private static int IndexOf(PacketFlowTraceFixture trace, string kind, string label)
        => trace.Events.FindIndex(e => e.Kind == kind && e.Label == label);

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

    private static byte[] BuildGuidCounterPacket(ulong guid, uint counter)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        ReaderUtils.WritePackedGuid(writer, guid);
        writer.Write(counter);
        return ms.ToArray();
    }

    private static byte[] BuildGuidCounterSpeedPacket(ulong guid, uint counter, float speed)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        ReaderUtils.WritePackedGuid(writer, guid);
        writer.Write(counter);
        writer.Write(speed);
        return ms.ToArray();
    }

    private static byte[] BuildKnockBackPacket(ulong guid, uint counter, float vSin, float vCos, float hSpeed, float vSpeed)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        ReaderUtils.WritePackedGuid(writer, guid);
        writer.Write(counter);
        writer.Write(vSin);
        writer.Write(vCos);
        writer.Write(hSpeed);
        writer.Write(vSpeed);
        return ms.ToArray();
    }

    private static byte[] BuildTeleportPacket(ulong guid, Position position, float facing, uint clientTimeMs)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        ReaderUtils.WritePackedGuid(writer, guid);

        var player = new WoWLocalPlayer(new HighGuid(guid))
        {
            Position = position,
            Facing = facing,
            MovementFlags = MovementFlags.MOVEFLAG_NONE,
            FallTime = 0,
        };

        writer.Write(MovementPacketHandler.BuildMovementInfoBuffer(player, clientTimeMs, 0u));
        return ms.ToArray();
    }

    private static byte[] BuildLinearMonsterMovePacket(
        ulong guid,
        Position start,
        uint startTimeMs,
        uint durationMs,
        IReadOnlyList<Position> points)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        ReaderUtils.WritePackedGuid(writer, guid);
        writer.Write(start.X);
        writer.Write(start.Y);
        writer.Write(start.Z);
        writer.Write(startTimeMs);
        writer.Write((byte)SplineType.Normal);
        writer.Write((uint)SplineFlags.Runmode);
        writer.Write(durationMs);
        writer.Write((uint)points.Count);

        var destination = points[^1];
        writer.Write(destination.X);
        writer.Write(destination.Y);
        writer.Write(destination.Z);

        for (int i = 0; i < points.Count - 1; i++)
        {
            writer.Write(PackMonsterMoveOffset(destination - points[i]));
        }

        return ms.ToArray();
    }

    private static uint PackMonsterMoveOffset(Position offset)
    {
        const float scale = 4f;
        const int mask11 = (1 << 11) - 1;
        const int mask10 = (1 << 10) - 1;

        int x = (int)MathF.Round(offset.X * scale);
        int y = (int)MathF.Round(offset.Y * scale);
        int z = (int)MathF.Round(offset.Z * scale);

        return (uint)(((x & mask11) << 21) | ((y & mask11) << 10) | (z & mask10));
    }

    private static byte[] BuildCreateObjectPacket(
        ulong guid,
        WoWObjectType objectType,
        Position movementPosition,
        float movementFacing,
        SortedDictionary<uint, object?> fields)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        writer.Write(1u);
        writer.Write((byte)0);
        writer.Write((byte)ObjectUpdateType.CREATE_OBJECT);
        ReaderUtils.WritePackedGuid(writer, guid);
        writer.Write((byte)objectType);
        writer.Write((byte)ObjectUpdateFlags.UPDATEFLAG_HAS_POSITION);
        writer.Write(movementPosition.X);
        writer.Write(movementPosition.Y);
        writer.Write(movementPosition.Z);
        writer.Write(movementFacing);
        WriteValuesUpdateBlock(writer, fields);
        return ms.ToArray();
    }

    private static byte[] BuildPartialThenMovementPacket(
        ulong guid,
        SortedDictionary<uint, object?> fields,
        Position movementPosition,
        float movementFacing)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        writer.Write(2u);
        writer.Write((byte)0);

        writer.Write((byte)ObjectUpdateType.PARTIAL);
        ReaderUtils.WritePackedGuid(writer, guid);
        WriteValuesUpdateBlock(writer, fields);

        writer.Write((byte)ObjectUpdateType.MOVEMENT);
        ReaderUtils.WritePackedGuid(writer, guid);
        writer.Write(BuildMovementInfoPayload(guid, MovementFlags.MOVEFLAG_FORWARD, movementPosition, movementFacing));
        return ms.ToArray();
    }

    private static byte[] BuildMovementInfoPayload(ulong guid, MovementFlags movementFlags, Position position, float facing)
    {
        var player = new WoWLocalPlayer(new HighGuid(guid))
        {
            MovementFlags = movementFlags,
            Position = position,
            Facing = facing,
            FallTime = 0,
        };

        return MovementPacketHandler.BuildMovementInfoBuffer(player, clientTimeMs: 1234u, fallTimeMs: 0u);
    }

    private static void WriteValuesUpdateBlock(BinaryWriter writer, SortedDictionary<uint, object?> fields)
    {
        var maxIndex = fields.Keys.Max();
        var blockCount = checked((byte)(maxIndex / 32 + 1));
        var mask = new byte[blockCount * 4];
        foreach (var fieldIndex in fields.Keys)
        {
            mask[fieldIndex / 8] |= (byte)(1 << (int)(fieldIndex % 8));
        }

        writer.Write(blockCount);
        writer.Write(mask);

        foreach (var field in fields)
        {
            switch (field.Value)
            {
                case uint uintValue:
                    writer.Write(uintValue);
                    break;
                case int intValue:
                    writer.Write(intValue);
                    break;
                case float floatValue:
                    writer.Write(floatValue);
                    break;
                case byte[] bytes when bytes.Length == 4:
                    writer.Write(bytes);
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported field payload for 0x{field.Key:X}: {field.Value?.GetType().Name ?? "null"}");
            }
        }
    }

    private static byte[] EncodeRawClientPacket(Opcode opcode, byte[] payload)
    {
        var packet = new byte[4 + payload.Length];
        BitConverter.GetBytes((uint)opcode).CopyTo(packet, 0);
        payload.CopyTo(packet, 4);
        return packet;
    }
}
