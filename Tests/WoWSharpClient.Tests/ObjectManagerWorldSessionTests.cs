using System.Collections.Generic;
using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using GameData.Core.Enums;
using GameData.Core.Interfaces;
using GameData.Core.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using static GameData.Core.Enums.UpdateFields;
using WoWSharpClient.Client;
using WoWSharpClient.Handlers;
using WoWSharpClient.Models;
using WoWSharpClient.Parsers;
using WoWSharpClient.Tests.Handlers;
using WoWSharpClient.Tests.Util;
using WoWSharpClient.Utils;

namespace WoWSharpClient.Tests;

[Collection("Sequential ObjectManager tests")]
public class ObjectManagerWorldSessionTests
{
    private readonly ObjectManagerFixture _fixture;

    public ObjectManagerWorldSessionTests(ObjectManagerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void ResetWorldSessionState_ClearsObjectsAndPreservesGuid()
    {
        var objectManager = WoWSharpObjectManager.Instance;
        const ulong playerGuid = 0x1234;
        const ulong unitGuid = 0x5678;

        objectManager.EnterWorld(playerGuid);
        var originalPlayer = objectManager.Player;

        objectManager.QueueUpdate(new WoWSharpObjectManager.ObjectStateUpdate(
            unitGuid,
            WoWSharpObjectManager.ObjectUpdateOperation.Add,
            WoWObjectType.Unit,
            null,
            new Dictionary<uint, object?>()));
        UpdateProcessingHelper.DrainPendingUpdates();

        Assert.NotEmpty(objectManager.Objects);

        objectManager.ResetWorldSessionState("test");

        Assert.False(objectManager.HasEnteredWorld);
        Assert.Empty(objectManager.Objects);
        Assert.Equal(playerGuid, objectManager.PlayerGuid.FullGuid);
        Assert.Equal(playerGuid, objectManager.Player.Guid);
        Assert.NotSame(originalPlayer, objectManager.Player);
    }

    [Fact]
    public void LocalPlayerUpdate_AppliesWithoutPriorAddObject()
    {
        var objectManager = WoWSharpObjectManager.Instance;
        const ulong playerGuid = 0x9;

        objectManager.EnterWorld(playerGuid);

        objectManager.QueueUpdate(new WoWSharpObjectManager.ObjectStateUpdate(
            playerGuid,
            WoWSharpObjectManager.ObjectUpdateOperation.Update,
            WoWObjectType.Player,
            new MovementInfoUpdate
            {
                Guid = playerGuid,
                X = 11f,
                Y = 22f,
                Z = 33f,
                Facing = 1.5f,
                MovementFlags = MovementFlags.MOVEFLAG_NONE,
            },
            new Dictionary<uint, object?>
            {
                [(uint)EUnitFields.UNIT_FIELD_HEALTH] = 75u,
                [(uint)EUnitFields.UNIT_FIELD_MAXHEALTH] = 100u,
            }));

        UpdateProcessingHelper.DrainPendingUpdates();

        Assert.Equal(75u, objectManager.Player.Health);
        Assert.Equal(100u, objectManager.Player.MaxHealth);
        Assert.Equal(11f, objectManager.Player.Position.X);
        Assert.Equal(22f, objectManager.Player.Position.Y);
        Assert.Equal(33f, objectManager.Player.Position.Z);
    }

    [Fact]
    public void SyncTransportPassengerWorldPositions_UpdatesPlayerFromTransportOffset()
    {
        var objectManager = WoWSharpObjectManager.Instance;
        const ulong playerGuid = 0x99;
        const ulong transportGuid = 0xF120000000000123ul;

        objectManager.EnterWorld(playerGuid);

        objectManager.QueueUpdate(new WoWSharpObjectManager.ObjectStateUpdate(
            transportGuid,
            WoWSharpObjectManager.ObjectUpdateOperation.Add,
            WoWObjectType.GameObj,
            new MovementInfoUpdate
            {
                Guid = transportGuid,
                X = 100f,
                Y = 200f,
                Z = 50f,
                Facing = MathF.PI / 2f,
            },
            new Dictionary<uint, object?>()));
        UpdateProcessingHelper.DrainPendingUpdates();

        var transport = (WoWGameObject)objectManager.GetObjectByGuid(transportGuid);
        transport.DisplayId = 455;
        transport.Position = new Position(100f, 200f, 50f);
        transport.Facing = MathF.PI / 2f;

        objectManager.QueueUpdate(new WoWSharpObjectManager.ObjectStateUpdate(
            playerGuid,
            WoWSharpObjectManager.ObjectUpdateOperation.Update,
            WoWObjectType.Player,
            new MovementInfoUpdate
            {
                Guid = playerGuid,
                X = 101f,
                Y = 202f,
                Z = 53f,
                Facing = 2.0f,
                MovementFlags = MovementFlags.MOVEFLAG_ONTRANSPORT,
                TransportGuid = transportGuid,
                TransportOffset = new Position(2f, -1f, 3f),
                TransportOrientation = 2.0f - transport.Facing,
            },
            new Dictionary<uint, object?>()));
        UpdateProcessingHelper.DrainPendingUpdates();

        transport.Position = new Position(120f, 240f, 60f);
        transport.Facing = MathF.PI;

        objectManager.SyncTransportPassengerWorldPositions();

        Assert.Equal(118f, objectManager.Player.Position.X, 3);
        Assert.Equal(241f, objectManager.Player.Position.Y, 3);
        Assert.Equal(63f, objectManager.Player.Position.Z, 3);
        Assert.Equal(MathF.PI + (2.0f - MathF.PI / 2f), objectManager.Player.Facing, 3);
    }

    [Fact]
    public void RemoteUnitAdd_PrimesExtrapolationStateFromMovementBlock()
    {
        ResetObjectManager();

        const ulong playerGuid = 0x55;
        const ulong remoteGuid = 0xF130000000000321ul;

        var objectManager = WoWSharpObjectManager.Instance;
        objectManager.EnterWorld(playerGuid);

        objectManager.QueueUpdate(new WoWSharpObjectManager.ObjectStateUpdate(
            remoteGuid,
            WoWSharpObjectManager.ObjectUpdateOperation.Add,
            WoWObjectType.Unit,
            new MovementInfoUpdate
            {
                Guid = remoteGuid,
                X = 50f,
                Y = 60f,
                Z = 7f,
                Facing = MathF.PI / 2f,
                LastUpdated = 1234,
                MovementFlags = MovementFlags.MOVEFLAG_FORWARD,
                MovementBlockUpdate = new MovementBlockUpdate
                {
                    RunSpeed = 7f,
                    RunBackSpeed = 4.5f,
                },
            },
            new Dictionary<uint, object?>()));

        UpdateProcessingHelper.DrainPendingUpdates();

        var unit = Assert.IsType<WoWUnit>(objectManager.GetObjectByGuid(remoteGuid));
        Assert.NotNull(unit.ExtrapolationBasePosition);
        Assert.Equal(50f, unit.ExtrapolationBasePosition!.X, 3);
        Assert.Equal(60f, unit.ExtrapolationBasePosition.Y, 3);
        Assert.Equal(7f, unit.ExtrapolationBasePosition.Z, 3);
        Assert.Equal(MovementFlags.MOVEFLAG_FORWARD, unit.ExtrapolationFlags);
        Assert.Equal(MathF.PI / 2f, unit.ExtrapolationFacing, 3);
        Assert.Equal(1234u, unit.ExtrapolationTimeMs);

        var predicted = unit.GetExtrapolatedPosition(2234);
        Assert.Equal(50f, predicted.X, 3);
        Assert.Equal(67f, predicted.Y, 3);
        Assert.Equal(7f, predicted.Z, 3);
    }

    [Fact]
    public void MoveKnockBack_ParseStoresImpulseClearsDirectionAndAcks()
    {
        ResetObjectManager();

        const ulong playerGuid = 0x0102030405060711ul;
        const uint movementCounter = 91u;
        const float vSin = 0.6f;
        const float vCos = 0.8f;
        const float hSpeed = 12.5f;
        const float vSpeed = 6.25f;

        var objectManager = WoWSharpObjectManager.Instance;
        objectManager.EnterWorld(playerGuid);

        var player = (WoWLocalPlayer)objectManager.Player;
        player.Position = new Position(15f, 25f, 35f);
        player.Facing = 1.25f;
        player.MovementFlags = MovementFlags.MOVEFLAG_FORWARD | MovementFlags.MOVEFLAG_STRAFE_RIGHT;

        SetPrivateField(_fixture._woWClient.Object, "_worldClient", CreateWorldClientRecorder(out var sentPackets).Object);
        SetPrivateField(objectManager, "_worldTimeTracker", new WorldTimeTracker());

        KnockBackArgs? capturedArgs = null;
        EventHandler<KnockBackArgs> handler = (_, args) => capturedArgs = args;
        WoWSharpEventEmitter.Instance.OnForceMoveKnockBack += handler;

        try
        {
            MovementHandler.HandleUpdateMovement(
                Opcode.SMSG_MOVE_KNOCK_BACK,
                BuildKnockBackPayload(playerGuid, movementCounter, vSin, vCos, hSpeed, vSpeed));
        }
        finally
        {
            WoWSharpEventEmitter.Instance.OnForceMoveKnockBack -= handler;
        }

        Assert.NotNull(capturedArgs);
        Assert.Equal(playerGuid, capturedArgs!.Guid);
        Assert.Equal(movementCounter, capturedArgs.Counter);
        Assert.Equal(vSin, capturedArgs.VSin, 5);
        Assert.Equal(vCos, capturedArgs.VCos, 5);
        Assert.Equal(hSpeed, capturedArgs.HSpeed, 5);
        Assert.Equal(vSpeed, capturedArgs.VSpeed, 5);

        Assert.True(player.MovementFlags.HasFlag(MovementFlags.MOVEFLAG_FALLINGFAR));
        Assert.False(player.MovementFlags.HasFlag(MovementFlags.MOVEFLAG_FORWARD));
        Assert.False(player.MovementFlags.HasFlag(MovementFlags.MOVEFLAG_STRAFE_RIGHT));

        Assert.True(objectManager.TryConsumePendingKnockback(out float vx, out float vy, out float vz));
        Assert.Equal(hSpeed * vCos, vx, 5);
        Assert.Equal(hSpeed * vSin, vy, 5);
        Assert.Equal(vSpeed, vz, 5);
        Assert.False(objectManager.TryConsumePendingKnockback(out _, out _, out _));

        var sent = Assert.Single(sentPackets);
        Assert.Equal(Opcode.CMSG_MOVE_KNOCK_BACK_ACK, sent.opcode);

        using var ms = new MemoryStream(sent.payload);
        using var reader = new BinaryReader(ms);

        Assert.Equal(playerGuid, reader.ReadUInt64());
        Assert.Equal(movementCounter, reader.ReadUInt32());

        var ackMovement = MovementPacketHandler.ParseMovementInfo(reader);
        Assert.Equal(player.Position.X, ackMovement.X, 5);
        Assert.Equal(player.Position.Y, ackMovement.Y, 5);
        Assert.Equal(player.Position.Z, ackMovement.Z, 5);
        Assert.Equal(player.Facing, ackMovement.Facing, 5);
        Assert.True(ackMovement.MovementFlags.HasFlag(MovementFlags.MOVEFLAG_FALLINGFAR));
        Assert.False(ackMovement.MovementFlags.HasFlag(MovementFlags.MOVEFLAG_FORWARD));
        Assert.Equal(ms.Length, ms.Position);
    }

    [Theory]
    [InlineData(Opcode.SMSG_FORCE_WALK_SPEED_CHANGE, Opcode.CMSG_FORCE_WALK_SPEED_CHANGE_ACK, 2.5f)]
    [InlineData(Opcode.SMSG_FORCE_SWIM_BACK_SPEED_CHANGE, Opcode.CMSG_FORCE_SWIM_BACK_SPEED_CHANGE_ACK, 1.25f)]
    [InlineData(Opcode.SMSG_FORCE_TURN_RATE_CHANGE, Opcode.CMSG_FORCE_TURN_RATE_CHANGE_ACK, 3.14159f)]
    public void MissingForceChangeOpcodes_ParseApplyAndAck(
        Opcode serverOpcode,
        Opcode ackOpcode,
        float newValue)
    {
        ResetObjectManager();

        const ulong playerGuid = 0x0102030405060708ul;
        const uint movementCounter = 77u;

        var objectManager = WoWSharpObjectManager.Instance;
        objectManager.EnterWorld(playerGuid);

        var player = (WoWLocalPlayer)objectManager.Player;
        player.Position = new Position(10f, 20f, 30f);
        player.Facing = 1.25f;
        player.MovementFlags = MovementFlags.MOVEFLAG_FORWARD;
        player.WalkSpeed = 1.5f;
        player.SwimBackSpeed = 0.75f;
        player.TurnRate = 2.0f;

        SetPrivateField(_fixture._woWClient.Object, "_worldClient", CreateWorldClientRecorder(out var sentPackets).Object);
        SetPrivateField(objectManager, "_worldTimeTracker", new WorldTimeTracker());

        RequiresAcknowledgementArgs? capturedArgs = null;
        EventHandler<RequiresAcknowledgementArgs> handler = (_, args) => capturedArgs = args;
        SubscribeForceChange(serverOpcode, handler);

        try
        {
            MovementHandler.HandleUpdateMovement(
                serverOpcode,
                BuildGuidCounterSpeedPayload(playerGuid, movementCounter, newValue));
        }
        finally
        {
            UnsubscribeForceChange(serverOpcode, handler);
        }

        Assert.NotNull(capturedArgs);
        Assert.Equal(playerGuid, capturedArgs!.Guid);
        Assert.Equal(movementCounter, capturedArgs.Counter);
        Assert.Equal(newValue, capturedArgs.Speed, 5);

        switch (serverOpcode)
        {
            case Opcode.SMSG_FORCE_WALK_SPEED_CHANGE:
                Assert.Equal(newValue, player.WalkSpeed, 5);
                break;
            case Opcode.SMSG_FORCE_SWIM_BACK_SPEED_CHANGE:
                Assert.Equal(newValue, player.SwimBackSpeed, 5);
                break;
            case Opcode.SMSG_FORCE_TURN_RATE_CHANGE:
                Assert.Equal(newValue, player.TurnRate, 5);
                break;
            default:
                throw new InvalidOperationException($"Unhandled opcode {serverOpcode}");
        }

        var sent = Assert.Single(sentPackets);
        Assert.Equal(ackOpcode, sent.opcode);

        using var ms = new MemoryStream(sent.payload);
        using var reader = new BinaryReader(ms);

        Assert.Equal(playerGuid, reader.ReadUInt64());
        Assert.Equal(movementCounter, reader.ReadUInt32());

        var ackMovement = MovementPacketHandler.ParseMovementInfo(reader);
        Assert.Equal(player.Position.X, ackMovement.X, 5);
        Assert.Equal(player.Position.Y, ackMovement.Y, 5);
        Assert.Equal(player.Position.Z, ackMovement.Z, 5);
        Assert.Equal(player.Facing, ackMovement.Facing, 5);
        Assert.Equal(player.MovementFlags, ackMovement.MovementFlags);
        Assert.Equal(newValue, reader.ReadSingle(), 5);
        Assert.Equal(ms.Length, ms.Position);
    }

    private void ResetObjectManager()
    {
        WoWSharpObjectManager.Instance.Initialize(
            _fixture._woWClient.Object,
            _fixture._pathfindingClient.Object,
            NullLogger<WoWSharpObjectManager>.Instance);
    }

    private static Mock<IWorldClient> CreateWorldClientRecorder(out List<(Opcode opcode, byte[] payload)> sentPackets)
    {
        var packets = new List<(Opcode opcode, byte[] payload)>();
        var mockWorldClient = new Mock<IWorldClient>();
        mockWorldClient
            .Setup(x => x.SendOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .Callback<Opcode, byte[], CancellationToken>((opcode, payload, _) => packets.Add((opcode, payload)))
            .Returns(Task.CompletedTask);
        sentPackets = packets;
        return mockWorldClient;
    }

    private static byte[] BuildGuidCounterSpeedPayload(ulong guid, uint counter, float value)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        ReaderUtils.WritePackedGuid(writer, guid);
        writer.Write(counter);
        writer.Write(value);
        return ms.ToArray();
    }

    private static byte[] BuildKnockBackPayload(ulong guid, uint counter, float vSin, float vCos, float hSpeed, float vSpeed)
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

    private static void SubscribeForceChange(Opcode opcode, EventHandler<RequiresAcknowledgementArgs> handler)
    {
        switch (opcode)
        {
            case Opcode.SMSG_FORCE_WALK_SPEED_CHANGE:
                WoWSharpEventEmitter.Instance.OnForceWalkSpeedChange += handler;
                break;
            case Opcode.SMSG_FORCE_SWIM_BACK_SPEED_CHANGE:
                WoWSharpEventEmitter.Instance.OnForceSwimBackSpeedChange += handler;
                break;
            case Opcode.SMSG_FORCE_TURN_RATE_CHANGE:
                WoWSharpEventEmitter.Instance.OnForceTurnRateChange += handler;
                break;
            default:
                throw new InvalidOperationException($"Unhandled opcode {opcode}");
        }
    }

    private static void UnsubscribeForceChange(Opcode opcode, EventHandler<RequiresAcknowledgementArgs> handler)
    {
        switch (opcode)
        {
            case Opcode.SMSG_FORCE_WALK_SPEED_CHANGE:
                WoWSharpEventEmitter.Instance.OnForceWalkSpeedChange -= handler;
                break;
            case Opcode.SMSG_FORCE_SWIM_BACK_SPEED_CHANGE:
                WoWSharpEventEmitter.Instance.OnForceSwimBackSpeedChange -= handler;
                break;
            case Opcode.SMSG_FORCE_TURN_RATE_CHANGE:
                WoWSharpEventEmitter.Instance.OnForceTurnRateChange -= handler;
                break;
            default:
                throw new InvalidOperationException($"Unhandled opcode {opcode}");
        }
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        var type = target.GetType();
        FieldInfo? field = null;
        while (type != null && field == null)
        {
            field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            type = type.BaseType;
        }
        Assert.NotNull(field);
        field!.SetValue(target, value);
    }
}
