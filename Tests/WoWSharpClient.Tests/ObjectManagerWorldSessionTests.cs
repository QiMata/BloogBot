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
using WoWSharpClient.Movement;
using WoWSharpClient.Tests.Handlers;
using WoWSharpClient.Tests.Util;
using WoWSharpClient.Utils;

namespace WoWSharpClient.Tests;

[Collection("Sequential ObjectManager tests")]
public class ObjectManagerWorldSessionTests
{
    private static readonly HandlerContext ctx = new(WoWSharpObjectManager.Instance, WoWSharpEventEmitter.Instance);
    private readonly ObjectManagerFixture _fixture;

    public ObjectManagerWorldSessionTests(ObjectManagerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void Initialize_UseLocalPhysicsWithoutSceneData_DoesNotFallbackToPathfindingClient()
    {
        var objectManager = WoWSharpObjectManager.Instance;

        objectManager.Initialize(
            _fixture._woWClient.Object,
            _fixture._pathfindingClient.Object,
            NullLogger<WoWSharpObjectManager>.Instance,
            sceneDataClient: null,
            useLocalPhysics: true);

        var sceneDataClientField = typeof(WoWSharpObjectManager).GetField("_sceneDataClient", BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(sceneDataClientField);
        Assert.Null(sceneDataClientField!.GetValue(objectManager));
    }

    [Fact]
    public void EnterWorld_UseLocalPhysicsWithoutSceneData_InitializesMovementController()
    {
        var objectManager = WoWSharpObjectManager.Instance;

        objectManager.Initialize(
            _fixture._woWClient.Object,
            _fixture._pathfindingClient.Object,
            NullLogger<WoWSharpObjectManager>.Instance,
            sceneDataClient: null,
            useLocalPhysics: true);

        objectManager.EnterWorld(0xAABBCCDDuL);

        var controller = GetPrivateField<MovementController>(objectManager, "_movementController");
        Assert.NotNull(controller);
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
    public async Task EnterWorld_RetriesPendingPlayerLoginUntilWorldVerified()
    {
        ResetObjectManager();

        const ulong playerGuid = 0x9988776655443322ul;
        var objectManager = WoWSharpObjectManager.Instance;
        SetPrivateField(_fixture._woWClient.Object, "_worldClient", CreatePlayerLoginRecorder(out var loginRequests).Object);

        var originalDelay = WoWSharpObjectManager.WorldEntryRetryDelay;
        WoWSharpObjectManager.WorldEntryRetryDelay = TimeSpan.FromMilliseconds(50);
        try
        {
            objectManager.EnterWorld(playerGuid);

            WaitForCondition(() => loginRequests.Count >= 2, timeoutMs: 1000);

            Assert.True(objectManager.HasPendingWorldEntry);
            Assert.Equal(playerGuid, objectManager.PendingWorldEntryGuid);
            Assert.All(loginRequests, guid => Assert.Equal(playerGuid, guid));

            var requestCountBeforeVerify = loginRequests.Count;
            WoWSharpEventEmitter.Instance.FireOnLoginVerifyWorld(new WorldInfo
            {
                MapId = 1,
                PositionX = 10f,
                PositionY = 20f,
                PositionZ = 30f,
                Facing = 1.25f
            });

            await Task.Delay(TimeSpan.FromMilliseconds(150));

            Assert.False(objectManager.HasPendingWorldEntry);
            Assert.Equal(requestCountBeforeVerify, loginRequests.Count);
        }
        finally
        {
            WoWSharpObjectManager.WorldEntryRetryDelay = originalDelay;
            objectManager.ResetWorldSessionState("EnterWorld_RetriesPendingPlayerLoginUntilWorldVerified");
        }
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
    public void DirectMonsterMove_ActivatesSplineAndMovesUnit()
    {
        ResetObjectManager();

        const ulong playerGuid = 0x201;
        const ulong remoteGuid = 0xF130000000000777ul;

        var objectManager = WoWSharpObjectManager.Instance;
        objectManager.EnterWorld(playerGuid);
        objectManager.QueueUpdate(new WoWSharpObjectManager.ObjectStateUpdate(
            remoteGuid,
            WoWSharpObjectManager.ObjectUpdateOperation.Add,
            WoWObjectType.Unit,
            null,
            new Dictionary<uint, object?>()));
        UpdateProcessingHelper.DrainPendingUpdates();

        uint startTime = unchecked((uint)Environment.TickCount64 + 2000u);
        MovementHandler.HandleUpdateMovement(
            Opcode.SMSG_MONSTER_MOVE,
            BuildMonsterMovePayload(
                remoteGuid,
                new Position(0f, 0f, 0f),
                startTime,
                durationMs: 1000u,
                points: [new Position(10f, 0f, 0f)]),
            ctx);
        UpdateProcessingHelper.DrainPendingUpdates();
        WaitForCondition(() => Splines.Instance.HasActiveSpline(remoteGuid));

        var unit = Assert.IsType<WoWUnit>(objectManager.GetObjectByGuid(remoteGuid));
        Assert.Equal(startTime, unit.LastUpdated);
        Assert.True(Splines.Instance.HasActiveSpline(remoteGuid));

        Splines.Instance.Update(500f);

        Assert.Equal(5f, unit.Position.X, 2);
        Assert.Equal(0f, unit.Position.Y, 2);
        Assert.Equal(0f, unit.Position.Z, 2);
        Assert.Equal(0f, unit.Facing, 2);

        Splines.Instance.Remove(remoteGuid);
    }

    [Fact]
    public void DirectMonsterMoveTransport_StepsLocalOffsetAndSyncsWorldPosition()
    {
        ResetObjectManager();

        const ulong playerGuid = 0x202;
        const ulong remoteGuid = 0xF130000000000778ul;
        const ulong transportGuid = 0xF120000000000456ul;

        var objectManager = WoWSharpObjectManager.Instance;
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
        objectManager.QueueUpdate(new WoWSharpObjectManager.ObjectStateUpdate(
            remoteGuid,
            WoWSharpObjectManager.ObjectUpdateOperation.Add,
            WoWObjectType.Unit,
            null,
            new Dictionary<uint, object?>()));
        UpdateProcessingHelper.DrainPendingUpdates();

        var transport = Assert.IsType<WoWGameObject>(objectManager.GetObjectByGuid(transportGuid));
        transport.Position = new Position(100f, 200f, 50f);
        transport.Facing = MathF.PI / 2f;

        uint startTime = unchecked((uint)Environment.TickCount64 + 2000u);
        MovementHandler.HandleUpdateMovement(
            Opcode.SMSG_MONSTER_MOVE_TRANSPORT,
            BuildMonsterMoveTransportPayload(
                remoteGuid,
                transportGuid,
                new Position(2f, -1f, 3f),
                startTime,
                durationMs: 1000u,
                points: [new Position(4f, -1f, 3f)]),
            ctx);
        UpdateProcessingHelper.DrainPendingUpdates();
        WaitForCondition(() =>
        {
            var movedUnit = objectManager.GetObjectByGuid(remoteGuid) as WoWUnit;
            return movedUnit?.TransportGuid == transportGuid && Splines.Instance.HasActiveSpline(remoteGuid);
        });

        var unit = Assert.IsType<WoWUnit>(objectManager.GetObjectByGuid(remoteGuid));
        Assert.Equal(transportGuid, unit.TransportGuid);
        Assert.Equal(2f, unit.TransportOffset.X, 2);
        Assert.Equal(-1f, unit.TransportOffset.Y, 2);
        Assert.Equal(3f, unit.TransportOffset.Z, 2);
        Assert.Equal(101f, unit.Position.X, 2);
        Assert.Equal(202f, unit.Position.Y, 2);
        Assert.Equal(53f, unit.Position.Z, 2);

        Splines.Instance.Update(500f);

        Assert.Equal(3f, unit.TransportOffset.X, 2);
        Assert.Equal(-1f, unit.TransportOffset.Y, 2);
        Assert.Equal(3f, unit.TransportOffset.Z, 2);
        Assert.Equal(101f, unit.Position.X, 2);
        Assert.Equal(203f, unit.Position.Y, 2);
        Assert.Equal(53f, unit.Position.Z, 2);
        Assert.Equal(0f, unit.TransportOrientation, 2);
        Assert.Equal(MathF.PI / 2f, unit.Facing, 2);

        Splines.Instance.Remove(remoteGuid);
    }

    [Fact]
    public void DirectMonsterMove_GameObjectTransportSplineMovesPassengers()
    {
        ResetObjectManager();

        const ulong playerGuid = 0x203;
        const ulong transportGuid = 0xF120000000000789ul;

        var objectManager = WoWSharpObjectManager.Instance;
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
                TransportOrientation = 2.0f - (MathF.PI / 2f),
            },
            new Dictionary<uint, object?>()));
        UpdateProcessingHelper.DrainPendingUpdates();

        var transport = Assert.IsType<WoWGameObject>(objectManager.GetObjectByGuid(transportGuid));
        transport.Position = new Position(100f, 200f, 50f);
        transport.Facing = MathF.PI / 2f;

        uint startTime = unchecked((uint)Environment.TickCount64 + 2000u);
        MovementHandler.HandleUpdateMovement(
            Opcode.SMSG_MONSTER_MOVE,
            BuildMonsterMovePayload(
                transportGuid,
                new Position(100f, 200f, 50f),
                startTime,
                durationMs: 1000u,
                points: [new Position(100f, 200f, 60f)]),
            ctx);
        UpdateProcessingHelper.DrainPendingUpdates();
        WaitForCondition(() => Splines.Instance.HasActiveSpline(transportGuid));

        var player = Assert.IsType<WoWLocalPlayer>(objectManager.Player);
        Assert.Equal(50f, transport.Position.Z, 2);
        Assert.Equal(53f, player.Position.Z, 2);

        Splines.Instance.Update(500f);

        Assert.Equal(55f, transport.Position.Z, 2);
        Assert.Equal(101f, player.Position.X, 2);
        Assert.Equal(202f, player.Position.Y, 2);
        Assert.Equal(58f, player.Position.Z, 2);
        Assert.Equal(2f, player.TransportOffset.X, 2);
        Assert.Equal(-1f, player.TransportOffset.Y, 2);
        Assert.Equal(3f, player.TransportOffset.Z, 2);

        Splines.Instance.Remove(transportGuid);
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
                BuildKnockBackPayload(playerGuid, movementCounter, vSin, vCos, hSpeed, vSpeed),
                ctx);
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
    [InlineData(Opcode.SMSG_FORCE_RUN_SPEED_CHANGE, Opcode.CMSG_FORCE_RUN_SPEED_CHANGE_ACK, 8.5f)]
    [InlineData(Opcode.SMSG_FORCE_RUN_BACK_SPEED_CHANGE, Opcode.CMSG_FORCE_RUN_BACK_SPEED_CHANGE_ACK, 4.25f)]
    [InlineData(Opcode.SMSG_FORCE_SWIM_SPEED_CHANGE, Opcode.CMSG_FORCE_SWIM_SPEED_CHANGE_ACK, 5.5f)]
    [InlineData(Opcode.SMSG_FORCE_WALK_SPEED_CHANGE, Opcode.CMSG_FORCE_WALK_SPEED_CHANGE_ACK, 2.5f)]
    [InlineData(Opcode.SMSG_FORCE_SWIM_BACK_SPEED_CHANGE, Opcode.CMSG_FORCE_SWIM_BACK_SPEED_CHANGE_ACK, 1.25f)]
    [InlineData(Opcode.SMSG_FORCE_TURN_RATE_CHANGE, Opcode.CMSG_FORCE_TURN_RATE_CHANGE_ACK, 3.14159f)]
    public void ForceSpeedChangeOpcodes_ParseApplyAndAck(
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
                BuildGuidCounterSpeedPayload(playerGuid, movementCounter, newValue),
                ctx);
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
            case Opcode.SMSG_FORCE_RUN_SPEED_CHANGE:
                Assert.Equal(newValue, player.RunSpeed, 5);
                break;
            case Opcode.SMSG_FORCE_RUN_BACK_SPEED_CHANGE:
                Assert.Equal(newValue, player.RunBackSpeed, 5);
                break;
            case Opcode.SMSG_FORCE_SWIM_SPEED_CHANGE:
                Assert.Equal(newValue, player.SwimSpeed, 5);
                break;
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

    [Theory]
    [InlineData(Opcode.SMSG_MOVE_WATER_WALK, Opcode.CMSG_MOVE_WATER_WALK_ACK, MovementFlags.MOVEFLAG_WATERWALKING, true)]
    [InlineData(Opcode.SMSG_MOVE_LAND_WALK, Opcode.CMSG_MOVE_WATER_WALK_ACK, MovementFlags.MOVEFLAG_WATERWALKING, false)]
    [InlineData(Opcode.SMSG_MOVE_SET_HOVER, Opcode.CMSG_MOVE_HOVER_ACK, MovementFlags.MOVEFLAG_HOVER, true)]
    [InlineData(Opcode.SMSG_MOVE_UNSET_HOVER, Opcode.CMSG_MOVE_HOVER_ACK, MovementFlags.MOVEFLAG_HOVER, false)]
    [InlineData(Opcode.SMSG_MOVE_FEATHER_FALL, Opcode.CMSG_MOVE_FEATHER_FALL_ACK, MovementFlags.MOVEFLAG_SAFE_FALL, true)]
    [InlineData(Opcode.SMSG_MOVE_NORMAL_FALL, Opcode.CMSG_MOVE_FEATHER_FALL_ACK, MovementFlags.MOVEFLAG_SAFE_FALL, false)]
    public void ServerControlledMovementFlagChanges_ParseApplyAndAck(
        Opcode serverOpcode,
        Opcode ackOpcode,
        MovementFlags flag,
        bool apply)
    {
        ResetObjectManager();

        const ulong playerGuid = 0x1020304050607080ul;
        const uint movementCounter = 91u;

        var objectManager = WoWSharpObjectManager.Instance;
        objectManager.EnterWorld(playerGuid);

        var player = (WoWLocalPlayer)objectManager.Player;
        player.Position = new Position(10f, 20f, 30f);
        player.Facing = 1.25f;
        player.MovementFlags = MovementFlags.MOVEFLAG_FORWARD;

        SetPrivateField(_fixture._woWClient.Object, "_worldClient", CreateWorldClientRecorder(out var sentPackets).Object);
        SetPrivateField(objectManager, "_worldTimeTracker", new WorldTimeTracker());

        MovementHandler.HandleUpdateMovement(
            serverOpcode,
            BuildGuidCounterPayload(playerGuid, movementCounter),
            ctx);

        if (apply)
            Assert.True(player.MovementFlags.HasFlag(flag));
        else
            Assert.False(player.MovementFlags.HasFlag(flag));

        var sent = Assert.Single(sentPackets);
        Assert.Equal(ackOpcode, sent.opcode);

        using var ms = new MemoryStream(sent.payload);
        using var reader = new BinaryReader(ms);

        Assert.Equal(playerGuid, reader.ReadUInt64());
        Assert.Equal(movementCounter, reader.ReadUInt32());

        var ackMovement = MovementPacketHandler.ParseMovementInfo(reader);
        if (apply)
            Assert.True(ackMovement.MovementFlags.HasFlag(flag));
        else
            Assert.False(ackMovement.MovementFlags.HasFlag(flag));
        Assert.Equal(ms.Length, ms.Position);
    }

    [Theory]
    [InlineData(Opcode.SMSG_SPLINE_SET_RUN_SPEED, 8.5f)]
    [InlineData(Opcode.SMSG_SPLINE_SET_RUN_BACK_SPEED, 4.25f)]
    [InlineData(Opcode.SMSG_SPLINE_SET_SWIM_SPEED, 5.5f)]
    [InlineData(Opcode.SMSG_SPLINE_SET_WALK_SPEED, 2.0f)]
    [InlineData(Opcode.SMSG_SPLINE_SET_SWIM_BACK_SPEED, 1.5f)]
    [InlineData(Opcode.SMSG_SPLINE_SET_TURN_RATE, 3.2f)]
    public void SplineSpeedOpcodes_UpdateRemoteUnitState(
        Opcode serverOpcode,
        float newValue)
    {
        ResetObjectManager();

        const ulong playerGuid = 0x123;
        const ulong remoteGuid = 0xF130000000001111ul;

        var objectManager = WoWSharpObjectManager.Instance;
        objectManager.EnterWorld(playerGuid);
        objectManager.QueueUpdate(new WoWSharpObjectManager.ObjectStateUpdate(
            remoteGuid,
            WoWSharpObjectManager.ObjectUpdateOperation.Add,
            WoWObjectType.Unit,
            null,
            new Dictionary<uint, object?>()));
        UpdateProcessingHelper.DrainPendingUpdates();

        var unit = Assert.IsType<WoWUnit>(objectManager.GetObjectByGuid(remoteGuid));

        MovementHandler.HandleUpdateMovement(
            serverOpcode,
            BuildGuidSpeedPayload(remoteGuid, newValue),
            ctx);

        switch (serverOpcode)
        {
            case Opcode.SMSG_SPLINE_SET_RUN_SPEED:
                Assert.Equal(newValue, unit.RunSpeed, 5);
                break;
            case Opcode.SMSG_SPLINE_SET_RUN_BACK_SPEED:
                Assert.Equal(newValue, unit.RunBackSpeed, 5);
                break;
            case Opcode.SMSG_SPLINE_SET_SWIM_SPEED:
                Assert.Equal(newValue, unit.SwimSpeed, 5);
                break;
            case Opcode.SMSG_SPLINE_SET_WALK_SPEED:
                Assert.Equal(newValue, unit.WalkSpeed, 5);
                break;
            case Opcode.SMSG_SPLINE_SET_SWIM_BACK_SPEED:
                Assert.Equal(newValue, unit.SwimBackSpeed, 5);
                break;
            case Opcode.SMSG_SPLINE_SET_TURN_RATE:
                Assert.Equal(newValue, unit.TurnRate, 5);
                break;
            default:
                throw new InvalidOperationException($"Unhandled opcode {serverOpcode}");
        }
    }

    [Theory]
    [InlineData(Opcode.SMSG_SPLINE_MOVE_WATER_WALK, MovementFlags.MOVEFLAG_WATERWALKING, true)]
    [InlineData(Opcode.SMSG_SPLINE_MOVE_LAND_WALK, MovementFlags.MOVEFLAG_WATERWALKING, false)]
    [InlineData(Opcode.SMSG_SPLINE_MOVE_FEATHER_FALL, MovementFlags.MOVEFLAG_SAFE_FALL, true)]
    [InlineData(Opcode.SMSG_SPLINE_MOVE_NORMAL_FALL, MovementFlags.MOVEFLAG_SAFE_FALL, false)]
    [InlineData(Opcode.SMSG_SPLINE_MOVE_SET_HOVER, MovementFlags.MOVEFLAG_HOVER, true)]
    [InlineData(Opcode.SMSG_SPLINE_MOVE_UNSET_HOVER, MovementFlags.MOVEFLAG_HOVER, false)]
    [InlineData(Opcode.SMSG_SPLINE_MOVE_START_SWIM, MovementFlags.MOVEFLAG_SWIMMING, true)]
    [InlineData(Opcode.SMSG_SPLINE_MOVE_STOP_SWIM, MovementFlags.MOVEFLAG_SWIMMING, false)]
    public void SplineFlagOpcodes_UpdateRemoteUnitState(
        Opcode serverOpcode,
        MovementFlags flag,
        bool apply)
    {
        ResetObjectManager();

        const ulong playerGuid = 0x124;
        const ulong remoteGuid = 0xF130000000001112ul;

        var objectManager = WoWSharpObjectManager.Instance;
        objectManager.EnterWorld(playerGuid);
        objectManager.QueueUpdate(new WoWSharpObjectManager.ObjectStateUpdate(
            remoteGuid,
            WoWSharpObjectManager.ObjectUpdateOperation.Add,
            WoWObjectType.Unit,
            null,
            new Dictionary<uint, object?>()));
        UpdateProcessingHelper.DrainPendingUpdates();

        var unit = Assert.IsType<WoWUnit>(objectManager.GetObjectByGuid(remoteGuid));

        MovementHandler.HandleUpdateMovement(serverOpcode, BuildPackedGuidPayload(remoteGuid), ctx);

        if (apply)
            Assert.True(unit.MovementFlags.HasFlag(flag));
        else
            Assert.False(unit.MovementFlags.HasFlag(flag));
    }

    [Theory]
    [InlineData(Opcode.MSG_MOVE_ROOT, MovementFlags.MOVEFLAG_ROOT, true)]
    [InlineData(Opcode.MSG_MOVE_UNROOT, MovementFlags.MOVEFLAG_ROOT, false)]
    [InlineData(Opcode.MSG_MOVE_WATER_WALK, MovementFlags.MOVEFLAG_WATERWALKING, true)]
    [InlineData(Opcode.MSG_MOVE_WATER_WALK, MovementFlags.MOVEFLAG_WATERWALKING, false)]
    [InlineData(Opcode.MSG_MOVE_HOVER, MovementFlags.MOVEFLAG_HOVER, true)]
    [InlineData(Opcode.MSG_MOVE_HOVER, MovementFlags.MOVEFLAG_HOVER, false)]
    [InlineData(Opcode.MSG_MOVE_FEATHER_FALL, MovementFlags.MOVEFLAG_SAFE_FALL, true)]
    [InlineData(Opcode.MSG_MOVE_FEATHER_FALL, MovementFlags.MOVEFLAG_SAFE_FALL, false)]
    [InlineData(Opcode.MSG_MOVE_SET_WALK_MODE, MovementFlags.MOVEFLAG_WALK_MODE, true)]
    [InlineData(Opcode.MSG_MOVE_SET_RUN_MODE, MovementFlags.MOVEFLAG_WALK_MODE, false)]
    [InlineData(Opcode.MSG_MOVE_START_SWIM, MovementFlags.MOVEFLAG_SWIMMING, true)]
    [InlineData(Opcode.MSG_MOVE_STOP_SWIM, MovementFlags.MOVEFLAG_SWIMMING, false)]
    public void ObserverMovementFlagOpcodes_UpdateRemoteUnitState(
        Opcode serverOpcode,
        MovementFlags flag,
        bool apply)
    {
        ResetObjectManager();

        const ulong playerGuid = 0x125;
        const ulong remoteGuid = 0xF130000000001113ul;

        var objectManager = WoWSharpObjectManager.Instance;
        objectManager.EnterWorld(playerGuid);
        objectManager.QueueUpdate(new WoWSharpObjectManager.ObjectStateUpdate(
            remoteGuid,
            WoWSharpObjectManager.ObjectUpdateOperation.Add,
            WoWObjectType.Unit,
            null,
            new Dictionary<uint, object?>()));
        UpdateProcessingHelper.DrainPendingUpdates();

        var unit = Assert.IsType<WoWUnit>(objectManager.GetObjectByGuid(remoteGuid));
        var movementFlags = apply ? flag : MovementFlags.MOVEFLAG_NONE;

        MovementHandler.HandleUpdateMovement(
            serverOpcode,
            BuildMessageMovePayload(remoteGuid, movementFlags, new Position(45f, 55f, 65f), 1.75f),
            ctx);
        UpdateProcessingHelper.DrainPendingUpdates();

        if (apply)
            Assert.True(unit.MovementFlags.HasFlag(flag));
        else
            Assert.False(unit.MovementFlags.HasFlag(flag));

        Assert.Equal(45f, unit.Position.X, 5);
        Assert.Equal(55f, unit.Position.Y, 5);
        Assert.Equal(65f, unit.Position.Z, 5);
        Assert.Equal(1.75f, unit.Facing, 5);
    }

    [Theory]
    [InlineData(Opcode.MSG_MOVE_START_PITCH_UP, 0.35f)]
    [InlineData(Opcode.MSG_MOVE_START_PITCH_DOWN, -0.55f)]
    [InlineData(Opcode.MSG_MOVE_STOP_PITCH, 0f)]
    [InlineData(Opcode.MSG_MOVE_SET_PITCH, 0.8f)]
    public void ObserverMovementPitchOpcodes_UpdateRemoteUnitSwimPitch(
        Opcode serverOpcode,
        float swimPitch)
    {
        ResetObjectManager();

        const ulong playerGuid = 0x127;
        const ulong remoteGuid = 0xF130000000001115ul;

        var objectManager = WoWSharpObjectManager.Instance;
        objectManager.EnterWorld(playerGuid);
        objectManager.QueueUpdate(new WoWSharpObjectManager.ObjectStateUpdate(
            remoteGuid,
            WoWSharpObjectManager.ObjectUpdateOperation.Add,
            WoWObjectType.Unit,
            null,
            new Dictionary<uint, object?>()));
        UpdateProcessingHelper.DrainPendingUpdates();

        var unit = Assert.IsType<WoWUnit>(objectManager.GetObjectByGuid(remoteGuid));

        MovementHandler.HandleUpdateMovement(
            serverOpcode,
            BuildMessageMovePayload(
                remoteGuid,
                MovementFlags.MOVEFLAG_SWIMMING,
                new Position(46f, 56f, 66f),
                0.5f,
                swimPitch),
            ctx);
        UpdateProcessingHelper.DrainPendingUpdates();

        Assert.True(unit.MovementFlags.HasFlag(MovementFlags.MOVEFLAG_SWIMMING));
        Assert.Equal(swimPitch, unit.SwimPitch, 5);
        Assert.Equal(46f, unit.Position.X, 5);
        Assert.Equal(56f, unit.Position.Y, 5);
        Assert.Equal(66f, unit.Position.Z, 5);
        Assert.Equal(0.5f, unit.Facing, 5);
    }

    [Theory]
    [InlineData(Opcode.MSG_MOVE_SET_RUN_SPEED, 8.5f)]
    [InlineData(Opcode.MSG_MOVE_SET_RUN_BACK_SPEED, 4.25f)]
    [InlineData(Opcode.MSG_MOVE_SET_WALK_SPEED, 2.0f)]
    [InlineData(Opcode.MSG_MOVE_SET_SWIM_SPEED, 5.5f)]
    [InlineData(Opcode.MSG_MOVE_SET_SWIM_BACK_SPEED, 1.5f)]
    [InlineData(Opcode.MSG_MOVE_SET_TURN_RATE, 3.2f)]
    public void ObserverMovementSpeedOpcodes_UpdateRemoteUnitState(
        Opcode serverOpcode,
        float newValue)
    {
        ResetObjectManager();

        const ulong playerGuid = 0x126;
        const ulong remoteGuid = 0xF130000000001114ul;

        var objectManager = WoWSharpObjectManager.Instance;
        objectManager.EnterWorld(playerGuid);
        objectManager.QueueUpdate(new WoWSharpObjectManager.ObjectStateUpdate(
            remoteGuid,
            WoWSharpObjectManager.ObjectUpdateOperation.Add,
            WoWObjectType.Unit,
            null,
            new Dictionary<uint, object?>()));
        UpdateProcessingHelper.DrainPendingUpdates();

        var unit = Assert.IsType<WoWUnit>(objectManager.GetObjectByGuid(remoteGuid));

        MovementHandler.HandleUpdateMovement(
            serverOpcode,
            BuildMessageMoveSpeedPayload(remoteGuid, MovementFlags.MOVEFLAG_FORWARD, new Position(70f, 80f, 90f), 0.25f, newValue),
            ctx);
        UpdateProcessingHelper.DrainPendingUpdates();

        switch (serverOpcode)
        {
            case Opcode.MSG_MOVE_SET_RUN_SPEED:
                Assert.Equal(newValue, unit.RunSpeed, 5);
                break;
            case Opcode.MSG_MOVE_SET_RUN_BACK_SPEED:
                Assert.Equal(newValue, unit.RunBackSpeed, 5);
                break;
            case Opcode.MSG_MOVE_SET_WALK_SPEED:
                Assert.Equal(newValue, unit.WalkSpeed, 5);
                break;
            case Opcode.MSG_MOVE_SET_SWIM_SPEED:
                Assert.Equal(newValue, unit.SwimSpeed, 5);
                break;
            case Opcode.MSG_MOVE_SET_SWIM_BACK_SPEED:
                Assert.Equal(newValue, unit.SwimBackSpeed, 5);
                break;
            case Opcode.MSG_MOVE_SET_TURN_RATE:
                Assert.Equal(newValue, unit.TurnRate, 5);
                break;
            default:
                throw new InvalidOperationException($"Unhandled opcode {serverOpcode}");
        }

        Assert.Equal(70f, unit.Position.X, 5);
        Assert.Equal(80f, unit.Position.Y, 5);
        Assert.Equal(90f, unit.Position.Z, 5);
        Assert.Equal(0.25f, unit.Facing, 5);
    }

    [Fact]
    public void MoveToward_Airborne_RefreshesWaypointWithoutChangingFlagsOrFacing()
    {
        ResetObjectManager();

        const ulong playerGuid = 0x128;

        var objectManager = WoWSharpObjectManager.Instance;
        objectManager.EnterWorld(playerGuid);

        var player = Assert.IsType<WoWLocalPlayer>(objectManager.Player);
        player.Position = new Position(10f, 20f, 30f);
        player.Facing = 0f;
        player.MovementFlags = MovementFlags.MOVEFLAG_FORWARD | MovementFlags.MOVEFLAG_FALLINGFAR;

        objectManager.MoveToward(new Position(10f, 35f, 30f));

        Assert.Equal(MovementFlags.MOVEFLAG_FORWARD | MovementFlags.MOVEFLAG_FALLINGFAR, player.MovementFlags);
        Assert.Equal(0f, player.Facing, 3);

        var controller = GetPrivateField<MovementController>(objectManager, "_movementController");
        Assert.NotNull(controller.CurrentWaypoint);
        Assert.Equal(10f, controller.CurrentWaypoint!.X, 3);
        Assert.Equal(35f, controller.CurrentWaypoint!.Y, 3);
        Assert.Equal(30f, controller.CurrentWaypoint!.Z, 3);
    }

    [Fact]
    public void MoveTowardWithFacing_Airborne_PreservesFlagsAndCurrentFacing()
    {
        ResetObjectManager();

        const ulong playerGuid = 0x129;

        var objectManager = WoWSharpObjectManager.Instance;
        objectManager.EnterWorld(playerGuid);

        var player = Assert.IsType<WoWLocalPlayer>(objectManager.Player);
        player.Position = new Position(-5f, -10f, 40f);
        player.Facing = 0.25f;
        player.MovementFlags = MovementFlags.MOVEFLAG_BACKWARD | MovementFlags.MOVEFLAG_FALLINGFAR;

        objectManager.MoveToward(new Position(-15f, -20f, 42f), 1.75f);

        Assert.Equal(MovementFlags.MOVEFLAG_BACKWARD | MovementFlags.MOVEFLAG_FALLINGFAR, player.MovementFlags);
        Assert.Equal(0.25f, player.Facing, 3);

        var controller = GetPrivateField<MovementController>(objectManager, "_movementController");
        Assert.NotNull(controller.CurrentWaypoint);
        Assert.Equal(-15f, controller.CurrentWaypoint!.X, 3);
        Assert.Equal(-20f, controller.CurrentWaypoint!.Y, 3);
        Assert.Equal(42f, controller.CurrentWaypoint!.Z, 3);
    }

    [Fact]
    public void MoveTowardWithFacing_IdleInControl_SendsSetFacingBeforeForwardIntent()
    {
        _fixture._woWClient.Reset();
        var sentPackets = new List<(Opcode opcode, byte[] payload)>();
        _fixture._woWClient
            .Setup(c => c.SendMovementOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .Callback<Opcode, byte[], CancellationToken>((opcode, payload, _) => sentPackets.Add((opcode, payload)))
            .Returns(Task.CompletedTask);

        ResetObjectManager();

        const ulong playerGuid = 0x12A;

        var objectManager = WoWSharpObjectManager.Instance;
        objectManager.EnterWorld(playerGuid);

        SetPrivateField(objectManager, "_isInControl", true);
        SetPrivateField(objectManager, "_isBeingTeleported", false);

        var player = Assert.IsType<WoWLocalPlayer>(objectManager.Player);
        player.Position = new Position(5f, 10f, 20f);
        player.Facing = 0.10f;
        player.MovementFlags = MovementFlags.MOVEFLAG_NONE;

        objectManager.MoveToward(new Position(15f, 20f, 20f), 1.25f);

        Assert.Single(sentPackets);
        Assert.Equal(Opcode.MSG_MOVE_SET_FACING, sentPackets[0].opcode);
        Assert.Equal(1.25f, player.Facing, 3);
        Assert.Equal(MovementFlags.MOVEFLAG_FORWARD, player.MovementFlags);

        var controller = GetPrivateField<MovementController>(objectManager, "_movementController");
        Assert.NotNull(controller.CurrentWaypoint);
        Assert.Equal(15f, controller.CurrentWaypoint!.X, 3);
        Assert.Equal(20f, controller.CurrentWaypoint!.Y, 3);
        Assert.Equal(20f, controller.CurrentWaypoint!.Z, 3);
    }

    [Fact]
    public void MoveTowardWithFacing_AlreadyMovingForward_SendsSetFacingOnRedirect()
    {
        _fixture._woWClient.Reset();
        var sentPackets = new List<(Opcode opcode, byte[] payload)>();
        _fixture._woWClient
            .Setup(c => c.SendMovementOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .Callback<Opcode, byte[], CancellationToken>((opcode, payload, _) => sentPackets.Add((opcode, payload)))
            .Returns(Task.CompletedTask);

        ResetObjectManager();

        const ulong playerGuid = 0x12B;

        var objectManager = WoWSharpObjectManager.Instance;
        objectManager.EnterWorld(playerGuid);

        SetPrivateField(objectManager, "_isInControl", true);
        SetPrivateField(objectManager, "_isBeingTeleported", false);

        var player = Assert.IsType<WoWLocalPlayer>(objectManager.Player);
        player.Position = new Position(5f, 10f, 20f);
        player.Facing = 0.50f;
        // Already moving forward (simulating mid-route state)
        player.MovementFlags = MovementFlags.MOVEFLAG_FORWARD;

        // Redirect: new facing differs by > dampen threshold
        objectManager.MoveToward(new Position(25f, 5f, 20f), 2.80f);

        // Should emit SET_FACING even though already moving (large facing change > 0.10 rad)
        Assert.Single(sentPackets);
        Assert.Equal(Opcode.MSG_MOVE_SET_FACING, sentPackets[0].opcode);
        Assert.Equal(2.80f, player.Facing, 3);
        Assert.Equal(MovementFlags.MOVEFLAG_FORWARD, player.MovementFlags);
    }

    [Fact]
    public void MoveTowardWithFacing_AlreadyMovingForward_SubThresholdFacingChange_NoSetFacingPacket()
    {
        _fixture._woWClient.Reset();
        var sentPackets = new List<(Opcode opcode, byte[] payload)>();
        _fixture._woWClient
            .Setup(c => c.SendMovementOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .Callback<Opcode, byte[], CancellationToken>((opcode, payload, _) => sentPackets.Add((opcode, payload)))
            .Returns(Task.CompletedTask);

        ResetObjectManager();

        const ulong playerGuid = 0x12C;

        var objectManager = WoWSharpObjectManager.Instance;
        objectManager.EnterWorld(playerGuid);

        SetPrivateField(objectManager, "_isInControl", true);
        SetPrivateField(objectManager, "_isBeingTeleported", false);

        var player = Assert.IsType<WoWLocalPlayer>(objectManager.Player);
        player.Position = new Position(5f, 10f, 20f);
        player.Facing = 0.50f;
        // Already moving forward
        player.MovementFlags = MovementFlags.MOVEFLAG_FORWARD;

        // Small facing change (0.08 rad) stays below the WoW.exe 0.1 rad packet gate.
        // Local facing should update, but no explicit SET_FACING packet should be sent.
        objectManager.MoveToward(new Position(15f, 20f, 20f), 0.58f);

        Assert.Empty(sentPackets);
        Assert.Equal(0.58f, player.Facing, 3);
        Assert.Equal(MovementFlags.MOVEFLAG_FORWARD, player.MovementFlags);
    }

    [Fact]
    public void NotifyTeleportIncoming_ClearsMovementFlagsToNone()
    {
        _fixture._woWClient.Reset();
        _fixture._woWClient
            .Setup(c => c.SendMovementOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        ResetObjectManager();

        const ulong playerGuid = 0x12D;

        var objectManager = WoWSharpObjectManager.Instance;
        objectManager.EnterWorld(playerGuid);

        SetPrivateField(objectManager, "_isInControl", true);
        SetPrivateField(objectManager, "_isBeingTeleported", false);

        var player = Assert.IsType<WoWLocalPlayer>(objectManager.Player);
        player.Position = new Position(100f, 200f, 50f);
        player.Facing = 1.5f;
        // Simulate active movement before teleport
        player.MovementFlags = MovementFlags.MOVEFLAG_FORWARD;

        // Teleport incoming — should clear all movement flags
        objectManager.NotifyTeleportIncoming(75f);

        Assert.Equal(MovementFlags.MOVEFLAG_NONE, player.MovementFlags);
    }

    private void ResetObjectManager()
    {
        WoWSharpObjectManager.Instance.Initialize(
            _fixture._woWClient.Object,
            _fixture._pathfindingClient.Object,
            NullLogger<WoWSharpObjectManager>.Instance,
            useLocalPhysics: true);
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

    private static Mock<IWorldClient> CreatePlayerLoginRecorder(out List<ulong> loginRequests)
    {
        var requests = new List<ulong>();
        var mockWorldClient = new Mock<IWorldClient>();
        mockWorldClient
            .Setup(x => x.SendPlayerLoginAsync(It.IsAny<ulong>(), It.IsAny<CancellationToken>()))
            .Callback<ulong, CancellationToken>((guid, _) => requests.Add(guid))
            .Returns(Task.CompletedTask);
        mockWorldClient
            .Setup(x => x.SendMoveWorldPortAckAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        loginRequests = requests;
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

    private static byte[] BuildGuidCounterPayload(ulong guid, uint counter)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        ReaderUtils.WritePackedGuid(writer, guid);
        writer.Write(counter);
        return ms.ToArray();
    }

    private static byte[] BuildGuidSpeedPayload(ulong guid, float value)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        ReaderUtils.WritePackedGuid(writer, guid);
        writer.Write(value);
        return ms.ToArray();
    }

    private static byte[] BuildPackedGuidPayload(ulong guid)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        ReaderUtils.WritePackedGuid(writer, guid);
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

    private static byte[] BuildMessageMovePayload(
        ulong guid,
        MovementFlags movementFlags,
        Position position,
        float facing,
        float? swimPitch = null)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        ReaderUtils.WritePackedGuid(writer, guid);
        writer.Write(BuildMovementInfoPayload(guid, movementFlags, position, facing, swimPitch));
        return ms.ToArray();
    }

    private static byte[] BuildMessageMoveSpeedPayload(
        ulong guid,
        MovementFlags movementFlags,
        Position position,
        float facing,
        float speed)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        ReaderUtils.WritePackedGuid(writer, guid);
        writer.Write(BuildMovementInfoPayload(guid, movementFlags, position, facing));
        writer.Write(speed);
        return ms.ToArray();
    }

    private static byte[] BuildMovementInfoPayload(
        ulong guid,
        MovementFlags movementFlags,
        Position position,
        float facing,
        float? swimPitch = null)
    {
        var player = new WoWLocalPlayer(new HighGuid(guid))
        {
            MovementFlags = movementFlags,
            Position = position,
            Facing = facing,
            FallTime = 0,
            SwimPitch = swimPitch ?? 0f,
        };

        return MovementPacketHandler.BuildMovementInfoBuffer(
            player,
            clientTimeMs: 1234u,
            fallTimeMs: 0u);
    }

    private static byte[] BuildMonsterMovePayload(
        ulong guid,
        Position start,
        uint startTime,
        uint durationMs,
        IReadOnlyList<Position> points)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        ReaderUtils.WritePackedGuid(writer, guid);
        WriteMonsterMoveBody(writer, start, startTime, durationMs, points);
        return ms.ToArray();
    }

    private static byte[] BuildMonsterMoveTransportPayload(
        ulong guid,
        ulong transportGuid,
        Position localStart,
        uint startTime,
        uint durationMs,
        IReadOnlyList<Position> points)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        ReaderUtils.WritePackedGuid(writer, guid);
        ReaderUtils.WritePackedGuid(writer, transportGuid);
        WriteMonsterMoveBody(writer, localStart, startTime, durationMs, points);
        return ms.ToArray();
    }

    private static void WriteMonsterMoveBody(
        BinaryWriter writer,
        Position start,
        uint startTime,
        uint durationMs,
        IReadOnlyList<Position> points)
    {
        Assert.NotEmpty(points);

        writer.Write(start.X);
        writer.Write(start.Y);
        writer.Write(start.Z);
        writer.Write(startTime);
        writer.Write((byte)SplineType.Normal);
        writer.Write((uint)SplineFlags.Runmode);
        writer.Write(durationMs);
        writer.Write((uint)points.Count);

        Position destination = points[^1];
        writer.Write(destination.X);
        writer.Write(destination.Y);
        writer.Write(destination.Z);

        for (int i = 0; i < points.Count - 1; i++)
            writer.Write(PackMonsterMoveOffset(destination - points[i]));
    }

    private static uint PackMonsterMoveOffset(Position offset)
    {
        uint packed = 0;
        packed |= (uint)((int)(offset.X / 0.25f) & 0x7FF);
        packed |= (uint)(((int)(offset.Y / 0.25f) & 0x7FF) << 11);
        packed |= (uint)(((int)(offset.Z / 0.25f) & 0x3FF) << 22);
        return packed;
    }

    private static void SubscribeForceChange(Opcode opcode, EventHandler<RequiresAcknowledgementArgs> handler)
    {
        switch (opcode)
        {
            case Opcode.SMSG_FORCE_WALK_SPEED_CHANGE:
                WoWSharpEventEmitter.Instance.OnForceWalkSpeedChange += handler;
                break;
            case Opcode.SMSG_FORCE_RUN_SPEED_CHANGE:
                WoWSharpEventEmitter.Instance.OnForceRunSpeedChange += handler;
                break;
            case Opcode.SMSG_FORCE_RUN_BACK_SPEED_CHANGE:
                WoWSharpEventEmitter.Instance.OnForceRunBackSpeedChange += handler;
                break;
            case Opcode.SMSG_FORCE_SWIM_SPEED_CHANGE:
                WoWSharpEventEmitter.Instance.OnForceSwimSpeedChange += handler;
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
            case Opcode.SMSG_FORCE_RUN_SPEED_CHANGE:
                WoWSharpEventEmitter.Instance.OnForceRunSpeedChange -= handler;
                break;
            case Opcode.SMSG_FORCE_RUN_BACK_SPEED_CHANGE:
                WoWSharpEventEmitter.Instance.OnForceRunBackSpeedChange -= handler;
                break;
            case Opcode.SMSG_FORCE_SWIM_SPEED_CHANGE:
                WoWSharpEventEmitter.Instance.OnForceSwimSpeedChange -= handler;
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

    private static T GetPrivateField<T>(object target, string fieldName) where T : class
    {
        var type = target.GetType();
        FieldInfo? field = null;
        while (type != null && field == null)
        {
            field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            type = type.BaseType;
        }

        Assert.NotNull(field);
        var value = field!.GetValue(target);
        return Assert.IsType<T>(value);
    }

    private static void WaitForCondition(Func<bool> predicate, int timeoutMs = 1000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            if (predicate())
                return;

            Thread.Sleep(10);
        }

        Assert.True(predicate());
    }
}
