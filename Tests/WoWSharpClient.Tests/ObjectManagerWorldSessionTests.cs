using System.Collections.Generic;
using System;
using System.IO;
using System.Linq;
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
        objectManager.ResetWorldSessionState("LocalPlayerUpdate_AppliesWithoutPriorAddObject");
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
    public void LocalPlayerUpdate_WithoutPriorAdd_TakesControlAndClearsTransition()
    {
        ResetObjectManager();

        const ulong playerGuid = 0x91;
        var objectManager = WoWSharpObjectManager.Instance;
        SetPrivateField(_fixture._woWClient.Object, "_worldClient", CreateSetActiveMoverRecorder(out var activeMoverRequests).Object);

        objectManager.EnterWorld(playerGuid);
        InvokePrivateMethod(objectManager, "ClearPendingWorldEntry");

        var player = Assert.IsType<WoWLocalPlayer>(objectManager.Player);
        player.MapId = 1;
        SetPrivateField(objectManager, "_isInControl", false);
        SetPrivateField(objectManager, "_isBeingTeleported", true);

        objectManager.QueueUpdate(new WoWSharpObjectManager.ObjectStateUpdate(
            playerGuid,
            WoWSharpObjectManager.ObjectUpdateOperation.Update,
            WoWObjectType.Player,
            new MovementInfoUpdate
            {
                Guid = playerGuid,
                X = 14f,
                Y = 24f,
                Z = 34f,
                Facing = 1.75f,
                MovementFlags = MovementFlags.MOVEFLAG_NONE,
            },
            new Dictionary<uint, object?>
            {
                [(uint)EUnitFields.UNIT_FIELD_HEALTH] = 80u,
                [(uint)EUnitFields.UNIT_FIELD_MAXHEALTH] = 100u,
            }));

        UpdateProcessingHelper.DrainPendingUpdates();

        Assert.True(GetPrivateFieldValue<bool>(objectManager, "_isInControl"));
        Assert.False(GetPrivateFieldValue<bool>(objectManager, "_isBeingTeleported"));
        Assert.Single(activeMoverRequests);
        Assert.Equal(playerGuid, activeMoverRequests[0]);
    }

    [Fact]
    public void TryRecoverStaleWorldEntryTransition_ClearsTransition_WhenWorldDataIsHydratedButGateStaysLatched()
    {
        ResetObjectManager();

        const ulong playerGuid = 0xABCD;
        var objectManager = WoWSharpObjectManager.Instance;

        objectManager.EnterWorld(playerGuid);
        InvokePrivateMethod(objectManager, "ClearPendingWorldEntry");

        var player = Assert.IsType<WoWLocalPlayer>(objectManager.Player);
        player.MapId = 1;
        player.Health = 350u;
        player.MaxHealth = 500u;
        player.Position = new Position(1990.4f, -4794.1f, 55.9f);

        SetPrivateField(objectManager, "_isBeingTeleported", true);
        SetPrivateField(objectManager, "_isInControl", false);
        SetPrivateField(objectManager, "_hasExplicitClientControlLockout", false);
        SetPrivateField(
            objectManager,
            "_staleWorldEntryTransitionCandidateSinceUtc",
            DateTime.UtcNow - TimeSpan.FromSeconds(3));

        var recovered = InvokePrivateMethod<bool>(objectManager, "TryRecoverStaleWorldEntryTransition");

        Assert.True(recovered);
        Assert.False(GetPrivateFieldValue<bool>(objectManager, "_isBeingTeleported"));
        Assert.True(GetPrivateFieldValue<bool>(objectManager, "_isInControl"));
    }

    [Fact]
    public void TryRecoverStaleWorldEntryTransition_DoesNotClearActiveGroundSnap_WhenControlAlreadyReturned()
    {
        ResetObjectManager();

        const ulong playerGuid = 0xDCBA;
        var objectManager = WoWSharpObjectManager.Instance;

        objectManager.EnterWorld(playerGuid);
        InvokePrivateMethod(objectManager, "ClearPendingWorldEntry");

        var player = Assert.IsType<WoWLocalPlayer>(objectManager.Player);
        player.MapId = 1;
        player.Health = 350u;
        player.MaxHealth = 500u;
        player.Position = new Position(1990.4f, -4794.1f, 55.9f);

        var controller = GetPrivateField<MovementController>(objectManager, "_movementController");
        SetPrivateField(controller, "_needsGroundSnap", true);
        SetPrivateField(objectManager, "_isBeingTeleported", true);
        SetPrivateField(objectManager, "_isInControl", true);
        SetPrivateField(objectManager, "_hasExplicitClientControlLockout", false);
        SetPrivateField(
            objectManager,
            "_staleWorldEntryTransitionCandidateSinceUtc",
            DateTime.UtcNow - TimeSpan.FromSeconds(3));

        var recovered = InvokePrivateMethod<bool>(objectManager, "TryRecoverStaleWorldEntryTransition");

        Assert.False(recovered);
        Assert.True(GetPrivateFieldValue<bool>(objectManager, "_isBeingTeleported"));
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
        WaitForCondition(() => objectManager.SplineCtrl.HasActiveSpline(remoteGuid));

        var unit = Assert.IsType<WoWUnit>(objectManager.GetObjectByGuid(remoteGuid));
        Assert.Equal(startTime, unit.LastUpdated);
        Assert.True(objectManager.SplineCtrl.HasActiveSpline(remoteGuid));

        objectManager.SplineCtrl.Update(500f);

        Assert.Equal(5f, unit.Position.X, 2);
        Assert.Equal(0f, unit.Position.Y, 2);
        Assert.Equal(0f, unit.Position.Z, 2);
        Assert.Equal(0f, unit.Facing, 2);

        objectManager.SplineCtrl.Remove(remoteGuid);
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
            return movedUnit?.TransportGuid == transportGuid && objectManager.SplineCtrl.HasActiveSpline(remoteGuid);
        });

        var unit = Assert.IsType<WoWUnit>(objectManager.GetObjectByGuid(remoteGuid));
        Assert.Equal(transportGuid, unit.TransportGuid);
        Assert.Equal(2f, unit.TransportOffset.X, 2);
        Assert.Equal(-1f, unit.TransportOffset.Y, 2);
        Assert.Equal(3f, unit.TransportOffset.Z, 2);
        Assert.Equal(101f, unit.Position.X, 2);
        Assert.Equal(202f, unit.Position.Y, 2);
        Assert.Equal(53f, unit.Position.Z, 2);

        objectManager.SplineCtrl.Update(500f);

        Assert.Equal(3f, unit.TransportOffset.X, 2);
        Assert.Equal(-1f, unit.TransportOffset.Y, 2);
        Assert.Equal(3f, unit.TransportOffset.Z, 2);
        Assert.Equal(101f, unit.Position.X, 2);
        Assert.Equal(203f, unit.Position.Y, 2);
        Assert.Equal(53f, unit.Position.Z, 2);
        Assert.Equal(0f, unit.TransportOrientation, 2);
        Assert.Equal(MathF.PI / 2f, unit.Facing, 2);

        objectManager.SplineCtrl.Remove(remoteGuid);
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
        WaitForCondition(() => objectManager.SplineCtrl.HasActiveSpline(transportGuid));

        var player = Assert.IsType<WoWLocalPlayer>(objectManager.Player);
        Assert.Equal(50f, transport.Position.Z, 2);
        Assert.Equal(53f, player.Position.Z, 2);

        objectManager.SplineCtrl.Update(500f);

        Assert.Equal(55f, transport.Position.Z, 2);
        Assert.Equal(101f, player.Position.X, 2);
        Assert.Equal(202f, player.Position.Y, 2);
        Assert.Equal(58f, player.Position.Z, 2);
        Assert.Equal(2f, player.TransportOffset.X, 2);
        Assert.Equal(-1f, player.TransportOffset.Y, 2);
        Assert.Equal(3f, player.TransportOffset.Z, 2);

        objectManager.SplineCtrl.Remove(transportGuid);
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
    [Trait("Category", "MovementParity")]
    [Trait("ParityLayer", "DeterministicBgProtocol")]
    public void MoveKnockBack_ParseStoresImpulseClearsDirectionAndDefersAck()
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

        Assert.Empty(sentPackets);
    }

    [Fact]
    [Trait("Category", "MovementParity")]
    [Trait("ParityLayer", "DeterministicBgProtocol")]
    public void MoveKnockBack_ServerPacketFeedsMovementControllerNextFrameAndAcksAfterImpulse()
    {
        _fixture._woWClient.Reset();
        var movementPackets = new List<(Opcode opcode, byte[] payload)>();
        _fixture._woWClient
            .Setup(c => c.SendMovementOpcodeAsync(
                It.IsAny<Opcode>(),
                It.IsAny<byte[]>(),
                It.IsAny<CancellationToken>()))
            .Callback<Opcode, byte[], CancellationToken>((opcode, payload, _) => movementPackets.Add((opcode, payload)))
            .Returns(Task.CompletedTask);

        ResetObjectManager();

        const ulong playerGuid = 0x0102030405060712ul;
        const uint movementCounter = 92u;
        const float vSin = -0.25f;
        const float vCos = 0.75f;
        const float hSpeed = 10.0f;
        const float vSpeed = 5.0f;

        NativePhysics.PhysicsInput? capturedInput = null;
        NativeLocalPhysics.TestStepOverride = input =>
        {
            capturedInput = input;
            return new NativePhysics.PhysicsOutput
            {
                X = input.X + (input.Vx * input.DeltaTime),
                Y = input.Y + (input.Vy * input.DeltaTime),
                Z = input.Z + (input.Vz * input.DeltaTime),
                Orientation = input.Orientation,
                Pitch = input.Pitch,
                Vx = input.Vx,
                Vy = input.Vy,
                Vz = input.Vz,
                GroundZ = input.Z,
                GroundNx = 0f,
                GroundNy = 0f,
                GroundNz = 1f,
                MoveFlags = (uint)MovementFlags.MOVEFLAG_FALLINGFAR,
                FallTime = 50,
            };
        };

        try
        {
            var objectManager = WoWSharpObjectManager.Instance;
            objectManager.EnterWorld(playerGuid);
            InvokePrivateMethod(objectManager, "ClearPendingWorldEntry");

            var player = Assert.IsType<WoWLocalPlayer>(objectManager.Player);
            player.Position = new Position(15f, 25f, 35f);
            player.Facing = 1.25f;
            player.MovementFlags = MovementFlags.MOVEFLAG_FORWARD | MovementFlags.MOVEFLAG_STRAFE_RIGHT;

            SetPrivateField(_fixture._woWClient.Object, "_worldClient", CreateWorldClientRecorder(out var ackPackets).Object);
            SetPrivateField(objectManager, "_worldTimeTracker", new WorldTimeTracker());
            var controller = GetPrivateField<MovementController>(objectManager, "_movementController");
            SetPrivateField(controller, "_needsGroundSnap", false);
            SetPrivateField(controller, "_lastPhysicsPosition", new System.Numerics.Vector3(
                player.Position.X,
                player.Position.Y,
                player.Position.Z));
            SetPrivateField(controller, "_lastPhysicsMapId", player.MapId);

            MovementHandler.HandleUpdateMovement(
                Opcode.SMSG_MOVE_KNOCK_BACK,
                BuildKnockBackPayload(playerGuid, movementCounter, vSin, vCos, hSpeed, vSpeed),
                ctx);

            Assert.Empty(ackPackets);
            Assert.True(player.MovementFlags.HasFlag(MovementFlags.MOVEFLAG_FALLINGFAR));
            Assert.False(player.MovementFlags.HasFlag(MovementFlags.MOVEFLAG_FORWARD));
            Assert.False(player.MovementFlags.HasFlag(MovementFlags.MOVEFLAG_STRAFE_RIGHT));

            var ackOrigin = new Position(player.Position.X, player.Position.Y, player.Position.Z);
            controller.Update(0.05f, 1000);

            var ack = Assert.Single(ackPackets);
            Assert.Equal(Opcode.CMSG_MOVE_KNOCK_BACK_ACK, ack.opcode);

            Assert.NotNull(capturedInput);
            Assert.Equal(hSpeed * vCos, capturedInput!.Value.Vx, 3);
            Assert.Equal(hSpeed * vSin, capturedInput.Value.Vy, 3);
            Assert.Equal(vSpeed, capturedInput.Value.Vz, 3);
            Assert.Equal((uint)MovementFlags.MOVEFLAG_FALLINGFAR, capturedInput.Value.MoveFlags);
            Assert.False(objectManager.TryConsumePendingKnockback(out _, out _, out _));

            using (var ackStream = new MemoryStream(ack.payload))
            using (var ackReader = new BinaryReader(ackStream))
            {
                Assert.Equal(playerGuid, ackReader.ReadUInt64());
                Assert.Equal(movementCounter, ackReader.ReadUInt32());

                var ackMovement = MovementPacketHandler.ParseMovementInfo(ackReader);
                Assert.Equal(ackOrigin.X, ackMovement.X, 5);
                Assert.Equal(ackOrigin.Y, ackMovement.Y, 5);
                Assert.Equal(ackOrigin.Z, ackMovement.Z, 5);
                Assert.Equal(player.Facing, ackMovement.Facing, 5);
                Assert.True(ackMovement.MovementFlags.HasFlag(MovementFlags.MOVEFLAG_FALLINGFAR));
                Assert.False(ackMovement.MovementFlags.HasFlag(MovementFlags.MOVEFLAG_FORWARD));
                Assert.Equal(ackStream.Length, ackStream.Position);
            }

            var movement = Assert.Single(movementPackets);
            Assert.Equal(Opcode.MSG_MOVE_HEARTBEAT, movement.opcode);
            using var ms = new MemoryStream(movement.payload);
            using var reader = new BinaryReader(ms);
            var movementInfo = MovementPacketHandler.ParseMovementInfo(reader);
            Assert.True(movementInfo.MovementFlags.HasFlag(MovementFlags.MOVEFLAG_FALLINGFAR));
            Assert.False(movementInfo.MovementFlags.HasFlag(MovementFlags.MOVEFLAG_FORWARD));
            Assert.False(movementInfo.MovementFlags.HasFlag(MovementFlags.MOVEFLAG_STRAFE_RIGHT));
        }
        finally
        {
            NativeLocalPhysics.TestStepOverride = null;
        }
    }

    [Theory]
    [InlineData(Opcode.SMSG_FORCE_RUN_SPEED_CHANGE, Opcode.CMSG_FORCE_RUN_SPEED_CHANGE_ACK, 8.5f)]
    [InlineData(Opcode.SMSG_FORCE_RUN_BACK_SPEED_CHANGE, Opcode.CMSG_FORCE_RUN_BACK_SPEED_CHANGE_ACK, 4.25f)]
    [InlineData(Opcode.SMSG_FORCE_SWIM_SPEED_CHANGE, Opcode.CMSG_FORCE_SWIM_SPEED_CHANGE_ACK, 5.5f)]
    [InlineData(Opcode.SMSG_FORCE_WALK_SPEED_CHANGE, Opcode.CMSG_FORCE_WALK_SPEED_CHANGE_ACK, 2.5f)]
    [InlineData(Opcode.SMSG_FORCE_SWIM_BACK_SPEED_CHANGE, Opcode.CMSG_FORCE_SWIM_BACK_SPEED_CHANGE_ACK, 1.25f)]
    [InlineData(Opcode.SMSG_FORCE_TURN_RATE_CHANGE, Opcode.CMSG_FORCE_TURN_RATE_CHANGE_ACK, 3.14159f)]
    [Trait("Category", "MovementParity")]
    [Trait("ParityLayer", "DeterministicBgProtocol")]
    public void ForceSpeedChangeOpcodes_DeferMutationAndAckUntilControllerUpdate(
        Opcode serverOpcode,
        Opcode ackOpcode,
        float newValue)
    {
        _fixture._woWClient.Reset();
        _fixture._woWClient
            .Setup(c => c.SendMovementOpcodeAsync(
                It.IsAny<Opcode>(),
                It.IsAny<byte[]>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        ResetObjectManager();

        const ulong playerGuid = 0x0102030405060708ul;
        const uint movementCounter = 77u;

        var objectManager = WoWSharpObjectManager.Instance;
        objectManager.EnterWorld(playerGuid);

        var player = (WoWLocalPlayer)objectManager.Player;
        player.Position = new Position(10f, 20f, 30f);
        player.Facing = 1.25f;
        player.MovementFlags = MovementFlags.MOVEFLAG_FORWARD;
        player.RunSpeed = 7.0f;
        player.RunBackSpeed = 4.5f;
        player.SwimSpeed = 4.722222f;
        player.WalkSpeed = 1.5f;
        player.SwimBackSpeed = 0.75f;
        player.TurnRate = 2.0f;

        SetPrivateField(_fixture._woWClient.Object, "_worldClient", CreateWorldClientRecorder(out var sentPackets).Object);
        SetPrivateField(objectManager, "_worldTimeTracker", new WorldTimeTracker());
        var controller = GetPrivateField<MovementController>(objectManager, "_movementController");
        SetPrivateField(controller, "_needsGroundSnap", false);
        SetPrivateField(controller, "_lastPhysicsPosition", new System.Numerics.Vector3(
            player.Position.X,
            player.Position.Y,
            player.Position.Z));
        SetPrivateField(controller, "_lastPhysicsMapId", player.MapId);

        var oldValue = serverOpcode switch
        {
            Opcode.SMSG_FORCE_RUN_SPEED_CHANGE => player.RunSpeed,
            Opcode.SMSG_FORCE_RUN_BACK_SPEED_CHANGE => player.RunBackSpeed,
            Opcode.SMSG_FORCE_SWIM_SPEED_CHANGE => player.SwimSpeed,
            Opcode.SMSG_FORCE_WALK_SPEED_CHANGE => player.WalkSpeed,
            Opcode.SMSG_FORCE_SWIM_BACK_SPEED_CHANGE => player.SwimBackSpeed,
            Opcode.SMSG_FORCE_TURN_RATE_CHANGE => player.TurnRate,
            _ => throw new InvalidOperationException($"Unhandled opcode {serverOpcode}"),
        };

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
                Assert.Equal(oldValue, player.RunSpeed, 5);
                break;
            case Opcode.SMSG_FORCE_RUN_BACK_SPEED_CHANGE:
                Assert.Equal(oldValue, player.RunBackSpeed, 5);
                break;
            case Opcode.SMSG_FORCE_SWIM_SPEED_CHANGE:
                Assert.Equal(oldValue, player.SwimSpeed, 5);
                break;
            case Opcode.SMSG_FORCE_WALK_SPEED_CHANGE:
                Assert.Equal(oldValue, player.WalkSpeed, 5);
                break;
            case Opcode.SMSG_FORCE_SWIM_BACK_SPEED_CHANGE:
                Assert.Equal(oldValue, player.SwimBackSpeed, 5);
                break;
            case Opcode.SMSG_FORCE_TURN_RATE_CHANGE:
                Assert.Equal(oldValue, player.TurnRate, 5);
                break;
            default:
                throw new InvalidOperationException($"Unhandled opcode {serverOpcode}");
        }

        Assert.Empty(sentPackets);

        var ackOrigin = new Position(player.Position.X, player.Position.Y, player.Position.Z);
        var ackFacing = player.Facing;
        var ackFlags = player.MovementFlags;
        controller.Update(0.05f, 1000);

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
        Assert.Equal(ackOrigin.X, ackMovement.X, 5);
        Assert.Equal(ackOrigin.Y, ackMovement.Y, 5);
        Assert.Equal(ackOrigin.Z, ackMovement.Z, 5);
        Assert.Equal(ackFacing, ackMovement.Facing, 5);
        Assert.Equal(ackFlags, ackMovement.MovementFlags);
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
    [Trait("Category", "MovementParity")]
    [Trait("ParityLayer", "DeterministicBgProtocol")]
    public void ServerControlledMovementFlagChanges_DeferMutationAndAckUntilControllerUpdate(
        Opcode serverOpcode,
        Opcode ackOpcode,
        MovementFlags flag,
        bool apply)
    {
        _fixture._woWClient.Reset();
        _fixture._woWClient
            .Setup(c => c.SendMovementOpcodeAsync(
                It.IsAny<Opcode>(),
                It.IsAny<byte[]>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

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
        var controller = GetPrivateField<MovementController>(objectManager, "_movementController");
        SetPrivateField(controller, "_needsGroundSnap", false);
        SetPrivateField(controller, "_lastPhysicsPosition", new System.Numerics.Vector3(
            player.Position.X,
            player.Position.Y,
            player.Position.Z));
        SetPrivateField(controller, "_lastPhysicsMapId", player.MapId);

        MovementHandler.HandleUpdateMovement(
            serverOpcode,
            BuildGuidCounterPayload(playerGuid, movementCounter),
            ctx);

        Assert.False(player.MovementFlags.HasFlag(flag));

        Assert.Empty(sentPackets);

        controller.Update(0.05f, 1000);

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
        Assert.Equal(apply ? 1.0f : 0.0f, reader.ReadSingle(), 5);
        Assert.Equal(ms.Length, ms.Position);
    }

    [Theory]
    [InlineData(Opcode.SMSG_FORCE_MOVE_ROOT, Opcode.CMSG_FORCE_MOVE_ROOT_ACK, true)]
    [InlineData(Opcode.SMSG_FORCE_MOVE_UNROOT, Opcode.CMSG_FORCE_MOVE_UNROOT_ACK, false)]
    [Trait("Category", "MovementParity")]
    [Trait("ParityLayer", "DeterministicBgProtocol")]
    public void ForceMoveRootOpcodes_DeferMutationAndAckUntilControllerUpdate(
        Opcode serverOpcode,
        Opcode ackOpcode,
        bool expectRooted)
    {
        _fixture._woWClient.Reset();
        _fixture._woWClient
            .Setup(c => c.SendMovementOpcodeAsync(
                It.IsAny<Opcode>(),
                It.IsAny<byte[]>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        ResetObjectManager();

        const ulong playerGuid = 0x102030405060707Ful;
        const uint movementCounter = 95u;

        var objectManager = WoWSharpObjectManager.Instance;
        objectManager.EnterWorld(playerGuid);

        var player = (WoWLocalPlayer)objectManager.Player;
        player.Position = new Position(10f, 20f, 30f);
        player.Facing = 1.25f;
        player.MovementFlags = expectRooted
            ? MovementFlags.MOVEFLAG_FORWARD
            : (MovementFlags.MOVEFLAG_FORWARD | MovementFlags.MOVEFLAG_ROOT);

        SetPrivateField(_fixture._woWClient.Object, "_worldClient", CreateWorldClientRecorder(out var sentPackets).Object);
        SetPrivateField(objectManager, "_worldTimeTracker", new WorldTimeTracker());
        var controller = GetPrivateField<MovementController>(objectManager, "_movementController");
        SetPrivateField(controller, "_needsGroundSnap", false);
        SetPrivateField(controller, "_lastPhysicsPosition", new System.Numerics.Vector3(
            player.Position.X,
            player.Position.Y,
            player.Position.Z));
        SetPrivateField(controller, "_lastPhysicsMapId", player.MapId);

        MovementHandler.HandleUpdateMovement(
            serverOpcode,
            BuildGuidCounterPayload(playerGuid, movementCounter),
            ctx);

        if (expectRooted)
            Assert.False(player.MovementFlags.HasFlag(MovementFlags.MOVEFLAG_ROOT));
        else
            Assert.True(player.MovementFlags.HasFlag(MovementFlags.MOVEFLAG_ROOT));

        Assert.Empty(sentPackets);

        controller.Update(0.05f, 1000);

        if (expectRooted)
            Assert.True(player.MovementFlags.HasFlag(MovementFlags.MOVEFLAG_ROOT));
        else
            Assert.False(player.MovementFlags.HasFlag(MovementFlags.MOVEFLAG_ROOT));

        var sent = Assert.Single(sentPackets);
        Assert.Equal(ackOpcode, sent.opcode);

        using var ms = new MemoryStream(sent.payload);
        using var reader = new BinaryReader(ms);
        Assert.Equal(playerGuid, reader.ReadUInt64());
        Assert.Equal(movementCounter, reader.ReadUInt32());
        var ackMovement = MovementPacketHandler.ParseMovementInfo(reader);

        if (expectRooted)
            Assert.True(ackMovement.MovementFlags.HasFlag(MovementFlags.MOVEFLAG_ROOT));
        else
            Assert.False(ackMovement.MovementFlags.HasFlag(MovementFlags.MOVEFLAG_ROOT));
        Assert.Equal(ms.Length, ms.Position);
    }

    [Theory]
    [InlineData(Opcode.SMSG_FORCE_MOVE_ROOT, Opcode.CMSG_FORCE_MOVE_ROOT_ACK, true)]
    [InlineData(Opcode.SMSG_FORCE_MOVE_UNROOT, Opcode.CMSG_FORCE_MOVE_UNROOT_ACK, false)]
    [Trait("Category", "MovementParity")]
    [Trait("ParityLayer", "DeterministicBgProtocol")]
    public void CompressedForceMoveRootOpcodes_DeferMutationAndAckUntilControllerUpdate(
        Opcode serverOpcode,
        Opcode ackOpcode,
        bool expectRooted)
    {
        _fixture._woWClient.Reset();
        _fixture._woWClient
            .Setup(c => c.SendMovementOpcodeAsync(
                It.IsAny<Opcode>(),
                It.IsAny<byte[]>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        ResetObjectManager();

        const ulong playerGuid = 0x1020304050607081ul;
        const uint movementCounter = 96u;

        var objectManager = WoWSharpObjectManager.Instance;
        objectManager.EnterWorld(playerGuid);

        var player = (WoWLocalPlayer)objectManager.Player;
        player.Position = new Position(10f, 20f, 30f);
        player.Facing = 1.25f;
        player.MovementFlags = expectRooted
            ? MovementFlags.MOVEFLAG_FORWARD
            : (MovementFlags.MOVEFLAG_FORWARD | MovementFlags.MOVEFLAG_ROOT);

        SetPrivateField(_fixture._woWClient.Object, "_worldClient", CreateWorldClientRecorder(out var sentPackets).Object);
        SetPrivateField(objectManager, "_worldTimeTracker", new WorldTimeTracker());
        var controller = GetPrivateField<MovementController>(objectManager, "_movementController");
        SetPrivateField(controller, "_needsGroundSnap", false);
        SetPrivateField(controller, "_lastPhysicsPosition", new System.Numerics.Vector3(
            player.Position.X,
            player.Position.Y,
            player.Position.Z));
        SetPrivateField(controller, "_lastPhysicsMapId", player.MapId);

        var payload = BuildCompressedMovePacket(
            BuildCompressedMoveEntry(serverOpcode, playerGuid, BuildSingleUIntPayload(movementCounter)));
        MovementHandler.HandleUpdateMovement(Opcode.SMSG_COMPRESSED_MOVES, payload, ctx);

        if (expectRooted)
            Assert.False(player.MovementFlags.HasFlag(MovementFlags.MOVEFLAG_ROOT));
        else
            Assert.True(player.MovementFlags.HasFlag(MovementFlags.MOVEFLAG_ROOT));

        Assert.Empty(sentPackets);

        controller.Update(0.05f, 1000);

        if (expectRooted)
            Assert.True(player.MovementFlags.HasFlag(MovementFlags.MOVEFLAG_ROOT));
        else
            Assert.False(player.MovementFlags.HasFlag(MovementFlags.MOVEFLAG_ROOT));

        var sent = Assert.Single(sentPackets);
        Assert.Equal(ackOpcode, sent.opcode);

        using var ms = new MemoryStream(sent.payload);
        using var reader = new BinaryReader(ms);
        Assert.Equal(playerGuid, reader.ReadUInt64());
        Assert.Equal(movementCounter, reader.ReadUInt32());
        var ackMovement = MovementPacketHandler.ParseMovementInfo(reader);

        if (expectRooted)
            Assert.True(ackMovement.MovementFlags.HasFlag(MovementFlags.MOVEFLAG_ROOT));
        else
            Assert.False(ackMovement.MovementFlags.HasFlag(MovementFlags.MOVEFLAG_ROOT));
        Assert.Equal(ms.Length, ms.Position);
    }

    [Theory]
    [InlineData(Opcode.SMSG_MOVE_WATER_WALK, Opcode.CMSG_MOVE_WATER_WALK_ACK, MovementFlags.MOVEFLAG_WATERWALKING, true)]
    [InlineData(Opcode.SMSG_MOVE_LAND_WALK, Opcode.CMSG_MOVE_WATER_WALK_ACK, MovementFlags.MOVEFLAG_WATERWALKING, false)]
    [InlineData(Opcode.SMSG_MOVE_SET_HOVER, Opcode.CMSG_MOVE_HOVER_ACK, MovementFlags.MOVEFLAG_HOVER, true)]
    [InlineData(Opcode.SMSG_MOVE_UNSET_HOVER, Opcode.CMSG_MOVE_HOVER_ACK, MovementFlags.MOVEFLAG_HOVER, false)]
    [InlineData(Opcode.SMSG_MOVE_FEATHER_FALL, Opcode.CMSG_MOVE_FEATHER_FALL_ACK, MovementFlags.MOVEFLAG_SAFE_FALL, true)]
    [InlineData(Opcode.SMSG_MOVE_NORMAL_FALL, Opcode.CMSG_MOVE_FEATHER_FALL_ACK, MovementFlags.MOVEFLAG_SAFE_FALL, false)]
    [Trait("Category", "MovementParity")]
    [Trait("ParityLayer", "DeterministicBgProtocol")]
    public void CompressedServerControlledMovementFlagChanges_DeferMutationAndAckUntilControllerUpdate(
        Opcode serverOpcode,
        Opcode ackOpcode,
        MovementFlags flag,
        bool apply)
    {
        _fixture._woWClient.Reset();
        _fixture._woWClient
            .Setup(c => c.SendMovementOpcodeAsync(
                It.IsAny<Opcode>(),
                It.IsAny<byte[]>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        ResetObjectManager();

        const ulong playerGuid = 0x1020304050607082ul;
        const uint movementCounter = 97u;

        var objectManager = WoWSharpObjectManager.Instance;
        objectManager.EnterWorld(playerGuid);

        var player = (WoWLocalPlayer)objectManager.Player;
        player.Position = new Position(10f, 20f, 30f);
        player.Facing = 1.25f;
        player.MovementFlags = MovementFlags.MOVEFLAG_FORWARD;

        SetPrivateField(_fixture._woWClient.Object, "_worldClient", CreateWorldClientRecorder(out var sentPackets).Object);
        SetPrivateField(objectManager, "_worldTimeTracker", new WorldTimeTracker());
        var controller = GetPrivateField<MovementController>(objectManager, "_movementController");
        SetPrivateField(controller, "_needsGroundSnap", false);
        SetPrivateField(controller, "_lastPhysicsPosition", new System.Numerics.Vector3(
            player.Position.X,
            player.Position.Y,
            player.Position.Z));
        SetPrivateField(controller, "_lastPhysicsMapId", player.MapId);

        var payload = BuildCompressedMovePacket(
            BuildCompressedMoveEntry(serverOpcode, playerGuid, BuildSingleUIntPayload(movementCounter)));
        MovementHandler.HandleUpdateMovement(Opcode.SMSG_COMPRESSED_MOVES, payload, ctx);

        Assert.False(player.MovementFlags.HasFlag(flag));

        Assert.Empty(sentPackets);

        controller.Update(0.05f, 1000);

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
        Assert.Equal(apply ? 1.0f : 0.0f, reader.ReadSingle(), 5);
        Assert.Equal(ms.Length, ms.Position);
    }

    [Theory]
    [InlineData(Opcode.SMSG_FORCE_RUN_SPEED_CHANGE, Opcode.CMSG_FORCE_RUN_SPEED_CHANGE_ACK, 8.5f)]
    [InlineData(Opcode.SMSG_FORCE_RUN_BACK_SPEED_CHANGE, Opcode.CMSG_FORCE_RUN_BACK_SPEED_CHANGE_ACK, 4.25f)]
    [InlineData(Opcode.SMSG_FORCE_SWIM_SPEED_CHANGE, Opcode.CMSG_FORCE_SWIM_SPEED_CHANGE_ACK, 5.5f)]
    [InlineData(Opcode.SMSG_FORCE_WALK_SPEED_CHANGE, Opcode.CMSG_FORCE_WALK_SPEED_CHANGE_ACK, 2.5f)]
    [InlineData(Opcode.SMSG_FORCE_SWIM_BACK_SPEED_CHANGE, Opcode.CMSG_FORCE_SWIM_BACK_SPEED_CHANGE_ACK, 1.25f)]
    [InlineData(Opcode.SMSG_FORCE_TURN_RATE_CHANGE, Opcode.CMSG_FORCE_TURN_RATE_CHANGE_ACK, 3.14159f)]
    [Trait("Category", "MovementParity")]
    [Trait("ParityLayer", "DeterministicBgProtocol")]
    public void CompressedForceSpeedChangeOpcodes_DeferMutationAndAckUntilControllerUpdate(
        Opcode serverOpcode,
        Opcode ackOpcode,
        float newValue)
    {
        _fixture._woWClient.Reset();
        _fixture._woWClient
            .Setup(c => c.SendMovementOpcodeAsync(
                It.IsAny<Opcode>(),
                It.IsAny<byte[]>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        ResetObjectManager();

        const ulong playerGuid = 0x1020304050607083ul;
        const uint movementCounter = 98u;

        var objectManager = WoWSharpObjectManager.Instance;
        objectManager.EnterWorld(playerGuid);

        var player = (WoWLocalPlayer)objectManager.Player;
        player.Position = new Position(10f, 20f, 30f);
        player.Facing = 1.25f;
        player.MovementFlags = MovementFlags.MOVEFLAG_FORWARD;
        player.RunSpeed = 7.0f;
        player.RunBackSpeed = 4.5f;
        player.SwimSpeed = 4.722222f;
        player.WalkSpeed = 1.5f;
        player.SwimBackSpeed = 0.75f;
        player.TurnRate = 2.0f;

        SetPrivateField(_fixture._woWClient.Object, "_worldClient", CreateWorldClientRecorder(out var sentPackets).Object);
        SetPrivateField(objectManager, "_worldTimeTracker", new WorldTimeTracker());
        var controller = GetPrivateField<MovementController>(objectManager, "_movementController");
        SetPrivateField(controller, "_needsGroundSnap", false);
        SetPrivateField(controller, "_lastPhysicsPosition", new System.Numerics.Vector3(
            player.Position.X,
            player.Position.Y,
            player.Position.Z));
        SetPrivateField(controller, "_lastPhysicsMapId", player.MapId);

        var oldValue = serverOpcode switch
        {
            Opcode.SMSG_FORCE_RUN_SPEED_CHANGE => player.RunSpeed,
            Opcode.SMSG_FORCE_RUN_BACK_SPEED_CHANGE => player.RunBackSpeed,
            Opcode.SMSG_FORCE_SWIM_SPEED_CHANGE => player.SwimSpeed,
            Opcode.SMSG_FORCE_WALK_SPEED_CHANGE => player.WalkSpeed,
            Opcode.SMSG_FORCE_SWIM_BACK_SPEED_CHANGE => player.SwimBackSpeed,
            Opcode.SMSG_FORCE_TURN_RATE_CHANGE => player.TurnRate,
            _ => throw new InvalidOperationException($"Unhandled opcode {serverOpcode}"),
        };

        var payload = BuildCompressedMovePacket(
            BuildCompressedMoveEntry(serverOpcode, playerGuid, BuildCounterAndFloatPayload(movementCounter, newValue)));
        MovementHandler.HandleUpdateMovement(Opcode.SMSG_COMPRESSED_MOVES, payload, ctx);

        switch (serverOpcode)
        {
            case Opcode.SMSG_FORCE_RUN_SPEED_CHANGE:
                Assert.Equal(oldValue, player.RunSpeed, 5);
                break;
            case Opcode.SMSG_FORCE_RUN_BACK_SPEED_CHANGE:
                Assert.Equal(oldValue, player.RunBackSpeed, 5);
                break;
            case Opcode.SMSG_FORCE_SWIM_SPEED_CHANGE:
                Assert.Equal(oldValue, player.SwimSpeed, 5);
                break;
            case Opcode.SMSG_FORCE_WALK_SPEED_CHANGE:
                Assert.Equal(oldValue, player.WalkSpeed, 5);
                break;
            case Opcode.SMSG_FORCE_SWIM_BACK_SPEED_CHANGE:
                Assert.Equal(oldValue, player.SwimBackSpeed, 5);
                break;
            case Opcode.SMSG_FORCE_TURN_RATE_CHANGE:
                Assert.Equal(oldValue, player.TurnRate, 5);
                break;
            default:
                throw new InvalidOperationException($"Unhandled opcode {serverOpcode}");
        }

        Assert.Empty(sentPackets);

        var ackOrigin = new Position(player.Position.X, player.Position.Y, player.Position.Z);
        var ackFacing = player.Facing;
        var ackFlags = player.MovementFlags;
        controller.Update(0.05f, 1000);

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
        Assert.Equal(ackOrigin.X, ackMovement.X, 5);
        Assert.Equal(ackOrigin.Y, ackMovement.Y, 5);
        Assert.Equal(ackOrigin.Z, ackMovement.Z, 5);
        Assert.Equal(ackFacing, ackMovement.Facing, 5);
        Assert.Equal(ackFlags, ackMovement.MovementFlags);
        Assert.Equal(newValue, reader.ReadSingle(), 5);
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
    [InlineData(Opcode.SMSG_SPLINE_SET_RUN_SPEED, 8.5f)]
    [InlineData(Opcode.SMSG_SPLINE_SET_RUN_BACK_SPEED, 4.25f)]
    [InlineData(Opcode.SMSG_SPLINE_SET_SWIM_SPEED, 5.5f)]
    [InlineData(Opcode.SMSG_SPLINE_SET_WALK_SPEED, 2.0f)]
    [InlineData(Opcode.SMSG_SPLINE_SET_SWIM_BACK_SPEED, 1.5f)]
    [InlineData(Opcode.SMSG_SPLINE_SET_TURN_RATE, 3.2f)]
    public void CompressedSplineSpeedOpcodes_UpdateRemoteUnitState(
        Opcode serverOpcode,
        float newValue)
    {
        ResetObjectManager();

        const ulong playerGuid = 0x224;
        const ulong remoteGuid = 0xF130000000001212ul;

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
        var payload = BuildCompressedMovePacket(
            BuildCompressedMoveEntry(serverOpcode, remoteGuid, BuildSingleFloatPayload(newValue)));
        MovementHandler.HandleUpdateMovement(Opcode.SMSG_COMPRESSED_MOVES, payload, ctx);

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
    [InlineData(Opcode.SMSG_SPLINE_MOVE_SET_RUN_MODE, MovementFlags.MOVEFLAG_WALK_MODE, false)]
    [InlineData(Opcode.SMSG_SPLINE_MOVE_SET_WALK_MODE, MovementFlags.MOVEFLAG_WALK_MODE, true)]
    [InlineData(Opcode.SMSG_SPLINE_MOVE_ROOT, MovementFlags.MOVEFLAG_ROOT, true)]
    [InlineData(Opcode.SMSG_SPLINE_MOVE_UNROOT, MovementFlags.MOVEFLAG_ROOT, false)]
    [InlineData(Opcode.SMSG_SPLINE_MOVE_WATER_WALK, MovementFlags.MOVEFLAG_WATERWALKING, true)]
    [InlineData(Opcode.SMSG_SPLINE_MOVE_LAND_WALK, MovementFlags.MOVEFLAG_WATERWALKING, false)]
    [InlineData(Opcode.SMSG_SPLINE_MOVE_FEATHER_FALL, MovementFlags.MOVEFLAG_SAFE_FALL, true)]
    [InlineData(Opcode.SMSG_SPLINE_MOVE_NORMAL_FALL, MovementFlags.MOVEFLAG_SAFE_FALL, false)]
    [InlineData(Opcode.SMSG_SPLINE_MOVE_SET_HOVER, MovementFlags.MOVEFLAG_HOVER, true)]
    [InlineData(Opcode.SMSG_SPLINE_MOVE_UNSET_HOVER, MovementFlags.MOVEFLAG_HOVER, false)]
    [InlineData(Opcode.SMSG_SPLINE_MOVE_START_SWIM, MovementFlags.MOVEFLAG_SWIMMING, true)]
    [InlineData(Opcode.SMSG_SPLINE_MOVE_STOP_SWIM, MovementFlags.MOVEFLAG_SWIMMING, false)]
    public void CompressedSplineFlagOpcodes_UpdateRemoteUnitState(
        Opcode serverOpcode,
        MovementFlags flag,
        bool apply)
    {
        ResetObjectManager();

        const ulong playerGuid = 0x225;
        const ulong remoteGuid = 0xF130000000001213ul;

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
        unit.MovementFlags = apply ? MovementFlags.MOVEFLAG_FORWARD : (MovementFlags.MOVEFLAG_FORWARD | flag);

        var payload = BuildCompressedMovePacket(
            BuildCompressedMoveEntry(serverOpcode, remoteGuid));
        MovementHandler.HandleUpdateMovement(Opcode.SMSG_COMPRESSED_MOVES, payload, ctx);

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
    public void HandleMovementControllerStuckRecovery_RepeatedSameAnchorLevel2_EscalatesToStrafeJump()
    {
        ResetObjectManager();

        const ulong playerGuid = 0x12A0;

        var objectManager = WoWSharpObjectManager.Instance;
        objectManager.EnterWorld(playerGuid);

        SetPrivateField(objectManager, "_isInControl", true);
        SetPrivateField(objectManager, "_isBeingTeleported", false);

        var player = Assert.IsType<WoWLocalPlayer>(objectManager.Player);
        var stuckAnchor = new Position(10f, 20f, 30f);
        player.Position = stuckAnchor;
        player.MovementFlags = MovementFlags.MOVEFLAG_FORWARD;
        player.Facing = 1.50f;
        SetPrivateField(objectManager, "_lastGlobalStuckRecoveryTick", Environment.TickCount64 - 5000);
        SetPrivateField(objectManager, "_globalStuckRecoveryRepeatCount", 2);
        SetPrivateField(objectManager, "_lastGlobalStuckRecoveryAnchor", new Position(stuckAnchor.X, stuckAnchor.Y, stuckAnchor.Z));
        InvokePrivateMethod(objectManager, "HandleMovementControllerStuckRecovery", 2, stuckAnchor);

        Assert.True(
            player.MovementFlags.HasFlag(MovementFlags.MOVEFLAG_STRAFE_LEFT)
            || player.MovementFlags.HasFlag(MovementFlags.MOVEFLAG_STRAFE_RIGHT));
        Assert.False(player.MovementFlags.HasFlag(MovementFlags.MOVEFLAG_FORWARD));
        Assert.Equal(3, GetPrivateFieldValue<int>(objectManager, "_globalStuckRecoveryRepeatCount"));
    }

    [Fact]
    public void HandleMovementControllerStuckRecovery_NewAnchor_ResetsBackToTurnJump()
    {
        ResetObjectManager();

        const ulong playerGuid = 0x12A1;

        var objectManager = WoWSharpObjectManager.Instance;
        objectManager.EnterWorld(playerGuid);

        SetPrivateField(objectManager, "_isInControl", true);
        SetPrivateField(objectManager, "_isBeingTeleported", false);

        var player = Assert.IsType<WoWLocalPlayer>(objectManager.Player);
        var firstAnchor = new Position(10f, 20f, 30f);
        player.Position = firstAnchor;

        player.MovementFlags = MovementFlags.MOVEFLAG_FORWARD;
        SetPrivateField(objectManager, "_lastGlobalStuckRecoveryTick", Environment.TickCount64 - 5000);
        SetPrivateField(objectManager, "_globalStuckRecoveryRepeatCount", 2);
        SetPrivateField(objectManager, "_lastGlobalStuckRecoveryAnchor", new Position(firstAnchor.X, firstAnchor.Y, firstAnchor.Z));
        InvokePrivateMethod(objectManager, "HandleMovementControllerStuckRecovery", 2, firstAnchor);

        Assert.True(
            player.MovementFlags.HasFlag(MovementFlags.MOVEFLAG_STRAFE_LEFT)
            || player.MovementFlags.HasFlag(MovementFlags.MOVEFLAG_STRAFE_RIGHT));
        Assert.False(player.MovementFlags.HasFlag(MovementFlags.MOVEFLAG_FORWARD));

        var secondAnchor = new Position(18f, 26f, 30f);
        player.Position = secondAnchor;
        player.MovementFlags = MovementFlags.MOVEFLAG_FORWARD;
        SetPrivateField(objectManager, "_lastGlobalStuckRecoveryTick", Environment.TickCount64 - 5000);
        InvokePrivateMethod(objectManager, "HandleMovementControllerStuckRecovery", 2, secondAnchor);

        Assert.True(player.MovementFlags.HasFlag(MovementFlags.MOVEFLAG_FORWARD));
        Assert.False(player.MovementFlags.HasFlag(MovementFlags.MOVEFLAG_STRAFE_LEFT));
        Assert.False(player.MovementFlags.HasFlag(MovementFlags.MOVEFLAG_STRAFE_RIGHT));
        Assert.Equal(1, GetPrivateFieldValue<int>(objectManager, "_globalStuckRecoveryRepeatCount"));
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
    [Trait("Category", "MovementParity")]
    [Trait("ParityLayer", "DeterministicBgProtocol")]
    public void ForceStopImmediate_BlocksStopPacketBeforeGameObjectUse()
    {
        ResetObjectManager();

        const ulong playerGuid = 0x12CF;
        const ulong nodeGuid = 0xF11000000000ABCDul;

        SetPrivateField(_fixture._woWClient.Object, "_worldClient", CreateDelayedWorldClientRecorder(
            out var sentPackets,
            opcode => opcode == Opcode.MSG_MOVE_STOP ? 40 : 1).Object);
        _fixture._woWClient
            .Setup(c => c.SendMovementOpcodeAsync(
                It.IsAny<Opcode>(),
                It.IsAny<byte[]>(),
                It.IsAny<CancellationToken>()))
            .Returns(async (Opcode opcode, byte[] payload, CancellationToken cancellationToken) =>
            {
                var delay = opcode == Opcode.MSG_MOVE_STOP ? 40 : 1;
                await Task.Delay(delay, cancellationToken);
                sentPackets.Add((opcode, payload));
            });

        var objectManager = WoWSharpObjectManager.Instance;
        objectManager.EnterWorld(playerGuid);
        InvokePrivateMethod(objectManager, "ClearPendingWorldEntry");

        SetPrivateField(objectManager, "_worldTimeTracker", new WorldTimeTracker());
        SetPrivateField(objectManager, "_isInControl", true);
        SetPrivateField(objectManager, "_isBeingTeleported", false);

        var player = Assert.IsType<WoWLocalPlayer>(objectManager.Player);
        player.Position = new Position(5f, 10f, 20f);
        player.Facing = 0.75f;
        player.MovementFlags = MovementFlags.MOVEFLAG_FORWARD;

        var controller = GetPrivateField<MovementController>(objectManager, "_movementController");
        SetPrivateField(controller, "_lastSentFlags", MovementFlags.MOVEFLAG_FORWARD);

        ((IObjectManager)objectManager).ForceStopImmediate();
        objectManager.InteractWithGameObject(nodeGuid);

        Assert.Collection(sentPackets,
            packet => Assert.Equal(Opcode.MSG_MOVE_STOP, packet.opcode),
            packet => Assert.Equal(Opcode.CMSG_GAMEOBJ_USE, packet.opcode));
        Assert.Equal(MovementFlags.MOVEFLAG_NONE, player.MovementFlags);

        using var ms = new MemoryStream(sentPackets[0].payload);
        using var reader = new BinaryReader(ms);
        var movementInfo = MovementPacketHandler.ParseMovementInfo(reader);
        Assert.Equal(MovementFlags.MOVEFLAG_NONE, movementInfo.MovementFlags);
        Assert.Equal(player.Position.X, movementInfo.X, 3);
        Assert.Equal(player.Position.Y, movementInfo.Y, 3);
        Assert.Equal(player.Position.Z, movementInfo.Z, 3);
    }

    [Fact]
    public void InteractWithGameObject_BattlegroundBanner_SendsCaptureSpellAfterGameObjectUse()
    {
        ResetObjectManager();

        const ulong playerGuid = 0x12D0;
        const ulong bannerGuid = 0xF11000000000BEEFul;

        SetPrivateField(_fixture._woWClient.Object, "_worldClient", CreateWorldClientRecorder(out var sentPackets).Object);

        var objectManager = WoWSharpObjectManager.Instance;
        objectManager.EnterWorld(playerGuid);
        InvokePrivateMethod(objectManager, "ClearPendingWorldEntry");

        var player = Assert.IsType<WoWLocalPlayer>(objectManager.Player);
        player.Position = new Position(977.08f, 1046.54f, -44.83f);

        var banner = new WoWGameObject(new HighGuid(bannerGuid))
        {
            Entry = 180088u,
            Position = new Position(977.08f, 1046.54f, -44.83f)
        };

        var objects = GetPrivateField<List<WoWObject>>(objectManager, "_objects");
        var objectsLock = GetPrivateField<object>(objectManager, "_objectsLock");
        lock (objectsLock)
        {
            objects.Add(banner);
        }

        objectManager.InteractWithGameObject(bannerGuid);

        Assert.Collection(sentPackets,
            packet => Assert.Equal(Opcode.CMSG_GAMEOBJ_USE, packet.opcode),
            packet =>
            {
                Assert.Equal(Opcode.CMSG_CAST_SPELL, packet.opcode);

                using var ms = new MemoryStream(packet.payload);
                using var reader = new BinaryReader(ms);
                Assert.Equal(21651u, reader.ReadUInt32());
                Assert.Equal(0x0800u, reader.ReadUInt16());
                Assert.Equal(bannerGuid, ReaderUtils.ReadPackedGuid(reader));
            });
    }

    [Fact]
    public void InteractWithGameObject_NonBattlegroundBanner_OnlySendsGameObjectUse()
    {
        ResetObjectManager();

        const ulong playerGuid = 0x12D1;
        const ulong objectGuid = 0xF11000000000FADEul;

        SetPrivateField(_fixture._woWClient.Object, "_worldClient", CreateWorldClientRecorder(out var sentPackets).Object);

        var objectManager = WoWSharpObjectManager.Instance;
        objectManager.EnterWorld(playerGuid);
        InvokePrivateMethod(objectManager, "ClearPendingWorldEntry");

        var player = Assert.IsType<WoWLocalPlayer>(objectManager.Player);
        player.Position = new Position(977.08f, 1046.54f, -44.83f);

        var mailbox = new WoWGameObject(new HighGuid(objectGuid))
        {
            Entry = 176296u,
            Position = new Position(977.08f, 1046.54f, -44.83f)
        };

        var objects = GetPrivateField<List<WoWObject>>(objectManager, "_objects");
        var objectsLock = GetPrivateField<object>(objectManager, "_objectsLock");
        lock (objectsLock)
        {
            objects.Add(mailbox);
        }

        objectManager.InteractWithGameObject(objectGuid);

        Assert.Collection(sentPackets,
            packet => Assert.Equal(Opcode.CMSG_GAMEOBJ_USE, packet.opcode));
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
        // Simulate active movement and airborne/swimming state before teleport.
        player.MovementFlags =
            MovementFlags.MOVEFLAG_FORWARD
            | MovementFlags.MOVEFLAG_JUMPING
            | MovementFlags.MOVEFLAG_FALLINGFAR
            | MovementFlags.MOVEFLAG_SWIMMING;

        // Teleport incoming — should clear all movement flags
        objectManager.NotifyTeleportIncoming(75f);

        Assert.Equal(MovementFlags.MOVEFLAG_NONE, player.MovementFlags);
    }

    [Fact]
    public void TryFlushPendingTeleportAck_WaitsForUpdatesAndGroundSnap_ButNotSceneData()
    {
        _fixture._woWClient.Reset();
        SetPrivateField(_fixture._woWClient.Object, "_worldClient", CreateWorldClientRecorder(out var sentPackets).Object);

        var originalSceneOverride = SceneDataClient.TestEnsureSceneDataAroundOverride;
        var sceneReady = false;
        SceneDataClient.TestEnsureSceneDataAroundOverride = (_, _, _) => sceneReady;

        try
        {
            var objectManager = WoWSharpObjectManager.Instance;
            var sceneDataClient = new SceneDataClient(NullLogger<SceneDataClient>.Instance);
            objectManager.Initialize(
                _fixture._woWClient.Object,
                _fixture._pathfindingClient.Object,
                NullLogger<WoWSharpObjectManager>.Instance,
                sceneDataClient: sceneDataClient,
                useLocalPhysics: true);

            const ulong playerGuid = 0x12E0;
            objectManager.EnterWorld(playerGuid);
            InvokePrivateMethod(objectManager, "ClearPendingWorldEntry");

            SetPrivateField(objectManager, "_worldTimeTracker", new WorldTimeTracker());
            SetPrivateField(objectManager, "_isInControl", true);
            SetPrivateField(objectManager, "_isBeingTeleported", false);

            var player = Assert.IsType<WoWLocalPlayer>(objectManager.Player);
            player.MapId = 389;
            player.Position = new Position(3f, -11f, -18f);
            player.MovementFlags = MovementFlags.MOVEFLAG_NONE;

            objectManager.NotifyTeleportIncoming(-18f);
            player.Position = new Position(3f, -11f, -18f);

            InvokePrivateMethod(objectManager, "EventEmitter_OnTeleport", null, new RequiresAcknowledgementArgs(playerGuid, 42));

            objectManager.QueueUpdate(new WoWSharpObjectManager.ObjectStateUpdate(
                playerGuid,
                WoWSharpObjectManager.ObjectUpdateOperation.Update,
                WoWObjectType.Player,
                new MovementInfoUpdate
                {
                    Guid = playerGuid,
                    X = 3f,
                    Y = -11f,
                    Z = -18f,
                    Facing = 1.5f,
                    MovementFlags = MovementFlags.MOVEFLAG_NONE,
                },
                new Dictionary<uint, object?>()));

            Assert.False(InvokePrivateMethod<bool>(objectManager, "TryFlushPendingTeleportAck"));
            Assert.Empty(sentPackets);

            UpdateProcessingHelper.DrainPendingUpdates();
            Assert.False(InvokePrivateMethod<bool>(objectManager, "TryFlushPendingTeleportAck"));
            Assert.Empty(sentPackets);

            var controller = GetPrivateField<MovementController>(objectManager, "_movementController");
            SetPrivateField(controller, "_needsGroundSnap", false);

            Assert.True(InvokePrivateMethod<bool>(objectManager, "TryFlushPendingTeleportAck"));
            Assert.Single(sentPackets);
            Assert.Equal(Opcode.MSG_MOVE_TELEPORT_ACK, sentPackets[0].opcode);
            Assert.False(GetPrivateFieldValue<bool>(objectManager, "_isBeingTeleported"));
        }
        finally
        {
            SceneDataClient.TestEnsureSceneDataAroundOverride = originalSceneOverride;
        }
    }

    [Fact]
    public void ReconcilePlayerControlState_RestoresControlWhenStable()
    {
        ResetObjectManager();

        const ulong playerGuid = 0x12E0;

        var objectManager = WoWSharpObjectManager.Instance;
        objectManager.EnterWorld(playerGuid);
        InvokePrivateMethod(objectManager, "ClearPendingWorldEntry");

        var player = Assert.IsType<WoWLocalPlayer>(objectManager.Player);
        player.MapId = 389;
        player.Position = new Position(3f, -11f, -16.8f);

        SetPrivateField(objectManager, "_isBeingTeleported", false);
        SetPrivateField(objectManager, "_isInControl", false);

        InvokePrivateMethod(objectManager, "ReconcilePlayerControlState");

        Assert.True(GetPrivateFieldValue<bool>(objectManager, "_isInControl"));
    }

    [Fact]
    public void ClientControlUpdate_LocalPlayer_TracksCanControlAndBlocksReconcile()
    {
        ResetObjectManager();

        const ulong playerGuid = 0x12E2;
        var objectManager = WoWSharpObjectManager.Instance;
        objectManager.EnterWorld(playerGuid);
        InvokePrivateMethod(objectManager, "ClearPendingWorldEntry");

        var player = Assert.IsType<WoWLocalPlayer>(objectManager.Player);
        player.MapId = 1;
        player.Position = new Position(4f, 5f, 6f);

        SetPrivateField(objectManager, "_isInControl", true);
        SetPrivateField(objectManager, "_isBeingTeleported", true);

        ClientControlHandler.HandleClientControlUpdate(
            Opcode.SMSG_CLIENT_CONTROL_UPDATE,
            BuildClientControlPacket(playerGuid, canControl: false),
            new HandlerContext(objectManager, WoWSharpEventEmitter.Instance));

        Assert.False(GetPrivateFieldValue<bool>(objectManager, "_isInControl"));
        Assert.True(GetPrivateFieldValue<bool>(objectManager, "_isBeingTeleported"));
        Assert.True(GetPrivateFieldValue<bool>(objectManager, "_hasExplicitClientControlLockout"));

        InvokePrivateMethod(objectManager, "ReconcilePlayerControlState");
        Assert.False(GetPrivateFieldValue<bool>(objectManager, "_isInControl"));

        ClientControlHandler.HandleClientControlUpdate(
            Opcode.SMSG_CLIENT_CONTROL_UPDATE,
            BuildClientControlPacket(playerGuid, canControl: true),
            new HandlerContext(objectManager, WoWSharpEventEmitter.Instance));

        Assert.True(GetPrivateFieldValue<bool>(objectManager, "_isInControl"));
        Assert.False(GetPrivateFieldValue<bool>(objectManager, "_isBeingTeleported"));
        Assert.False(GetPrivateFieldValue<bool>(objectManager, "_hasExplicitClientControlLockout"));
    }

    [Fact]
    public void ClientControlUpdate_RemoteGuid_DoesNotAffectLocalControl()
    {
        ResetObjectManager();

        const ulong playerGuid = 0x12E3;
        var objectManager = WoWSharpObjectManager.Instance;
        objectManager.EnterWorld(playerGuid);
        InvokePrivateMethod(objectManager, "ClearPendingWorldEntry");

        SetPrivateField(objectManager, "_isInControl", true);
        SetPrivateField(objectManager, "_isBeingTeleported", true);

        ClientControlHandler.HandleClientControlUpdate(
            Opcode.SMSG_CLIENT_CONTROL_UPDATE,
            BuildClientControlPacket(0x998877ul, canControl: false),
            new HandlerContext(objectManager, WoWSharpEventEmitter.Instance));

        Assert.True(GetPrivateFieldValue<bool>(objectManager, "_isInControl"));
        Assert.True(GetPrivateFieldValue<bool>(objectManager, "_isBeingTeleported"));
        Assert.False(GetPrivateFieldValue<bool>(objectManager, "_hasExplicitClientControlLockout"));
    }

    [Fact]
    public void PhysicsEnvironmentFlags_UsesObjectManagerFallbackWhenControllerStateIsUnresolved()
    {
        ResetObjectManager();

        const ulong playerGuid = 0x12E1;

        var objectManager = WoWSharpObjectManager.Instance;
        objectManager.EnterWorld(playerGuid);
        InvokePrivateMethod(objectManager, "ClearPendingWorldEntry");

        var player = Assert.IsType<WoWLocalPlayer>(objectManager.Player);
        player.MapId = 389;
        player.Position = new Position(3f, -11f, -16.8f);

        InvokePrivateMethod(
            objectManager,
            "RecordResolvedEnvironmentState",
            (uint)389,
            new Position(3f, -11f, -16.8f),
            SceneEnvironmentFlags.Indoors);

        var controller = GetPrivateField<MovementController>(objectManager, "_movementController");
        SetPrivateField(controller, "_hasResolvedEnvironmentState", false);
        SetPrivateField(controller, "_lastResolvedEnvironmentFlags", SceneEnvironmentFlags.None);
        SetPrivateField(controller, "_lastResolvedEnvironmentMapId", (uint)389);

        Assert.Equal(SceneEnvironmentFlags.Indoors, objectManager.PhysicsEnvironmentFlags);
        Assert.True(objectManager.PhysicsIsIndoors);

        player.Position = new Position(20f, -11f, -16.8f);
        Assert.Equal(SceneEnvironmentFlags.None, objectManager.PhysicsEnvironmentFlags);
    }

    [Fact]
    public void PhysicsEnvironmentFlags_PrefersNearbyCachedFlagsWhenControllerResolvesNone()
    {
        ResetObjectManager();

        const ulong playerGuid = 0x12E2;

        var objectManager = WoWSharpObjectManager.Instance;
        objectManager.EnterWorld(playerGuid);
        InvokePrivateMethod(objectManager, "ClearPendingWorldEntry");

        var player = Assert.IsType<WoWLocalPlayer>(objectManager.Player);
        player.MapId = 389;
        player.Position = new Position(3f, -11f, -16.8f);

        InvokePrivateMethod(
            objectManager,
            "RecordResolvedEnvironmentState",
            (uint)389,
            new Position(3f, -11f, -16.8f),
            SceneEnvironmentFlags.Indoors);

        var controller = GetPrivateField<MovementController>(objectManager, "_movementController");
        SetPrivateField(controller, "<LastEnvironmentFlags>k__BackingField", SceneEnvironmentFlags.None);
        SetPrivateField(controller, "_lastResolvedEnvironmentFlags", SceneEnvironmentFlags.None);
        SetPrivateField(controller, "_lastResolvedEnvironmentMapId", (uint)389);
        SetPrivateField(controller, "_hasResolvedEnvironmentState", true);

        Assert.Equal(SceneEnvironmentFlags.Indoors, objectManager.PhysicsEnvironmentFlags);
        Assert.True(objectManager.PhysicsIsIndoors);

        player.Position = new Position(20f, -11f, -16.8f);
        Assert.Equal(SceneEnvironmentFlags.None, objectManager.PhysicsEnvironmentFlags);
    }

    [Fact]
    public void RecordResolvedEnvironmentState_DoesNotClearNearbyIndoorCacheWithNone()
    {
        ResetObjectManager();

        const ulong playerGuid = 0x12E3;

        var objectManager = WoWSharpObjectManager.Instance;
        objectManager.EnterWorld(playerGuid);
        InvokePrivateMethod(objectManager, "ClearPendingWorldEntry");

        var player = Assert.IsType<WoWLocalPlayer>(objectManager.Player);
        player.MapId = 389;
        player.Position = new Position(3f, -11f, -16.8f);

        InvokePrivateMethod(
            objectManager,
            "RecordResolvedEnvironmentState",
            (uint)389,
            new Position(3f, -11f, -16.8f),
            SceneEnvironmentFlags.Indoors);

        InvokePrivateMethod(
            objectManager,
            "RecordResolvedEnvironmentState",
            (uint)389,
            new Position(3.2f, -11.1f, -16.8f),
            SceneEnvironmentFlags.None);

        var controller = GetPrivateField<MovementController>(objectManager, "_movementController");
        SetPrivateField(controller, "_hasResolvedEnvironmentState", false);
        SetPrivateField(controller, "_lastResolvedEnvironmentFlags", SceneEnvironmentFlags.None);
        SetPrivateField(controller, "_lastResolvedEnvironmentMapId", (uint)389);

        Assert.Equal(SceneEnvironmentFlags.Indoors, objectManager.PhysicsEnvironmentFlags);

        player.Position = new Position(30f, -11f, -16.8f);
        InvokePrivateMethod(
            objectManager,
            "RecordResolvedEnvironmentState",
            (uint)389,
            new Position(30f, -11f, -16.8f),
            SceneEnvironmentFlags.None);

        Assert.Equal(SceneEnvironmentFlags.None, objectManager.PhysicsEnvironmentFlags);
    }

    [Fact]
    public void PhysicsEnvironmentFlags_TriggersPassiveProbeWhenControllerStateIsUnresolved()
    {
        ResetObjectManager();

        const ulong playerGuid = 0x12E4;
        var stepCallCount = 0;

        NativeLocalPhysics.TestStepOverride = input =>
        {
            stepCallCount++;
            Assert.Equal(0f, input.DeltaTime);
            Assert.Equal(389u, input.MapId);
            Assert.Equal(3f, input.X, 3);
            Assert.Equal(-11f, input.Y, 3);
            Assert.Equal(-18f, input.Z, 3);

            return new NativePhysics.PhysicsOutput
            {
                X = input.X,
                Y = input.Y,
                Z = input.Z + 1.193f,
                Orientation = input.Orientation,
                Pitch = input.Pitch,
                Vx = 0f,
                Vy = 0f,
                Vz = 0f,
                GroundZ = input.Z + 1.193f,
                GroundNx = 0f,
                GroundNy = 0f,
                GroundNz = 1f,
                MoveFlags = input.MoveFlags,
                FallTime = 0,
                EnvironmentFlags = (uint)SceneEnvironmentFlags.Indoors,
            };
        };

        try
        {
            var objectManager = WoWSharpObjectManager.Instance;
            objectManager.EnterWorld(playerGuid);
            InvokePrivateMethod(objectManager, "ClearPendingWorldEntry");
            SetPrivateField(objectManager, "_isBeingTeleported", false);

            var player = Assert.IsType<WoWLocalPlayer>(objectManager.Player);
            player.MapId = 389;
            player.Position = new Position(3f, -11f, -18f);

            var controller = GetPrivateField<MovementController>(objectManager, "_movementController");
            SetPrivateField(controller, "_hasResolvedEnvironmentState", false);
            SetPrivateField(controller, "<LastEnvironmentFlags>k__BackingField", SceneEnvironmentFlags.None);
            SetPrivateField(controller, "_lastResolvedEnvironmentFlags", SceneEnvironmentFlags.None);
            SetPrivateField(controller, "_lastResolvedEnvironmentMapId", (uint)389);

            Assert.Equal(SceneEnvironmentFlags.Indoors, objectManager.PhysicsEnvironmentFlags);
            Assert.True(objectManager.PhysicsIsIndoors);
            Assert.Equal(1, stepCallCount);

            _ = objectManager.PhysicsEnvironmentFlags;
            Assert.Equal(1, stepCallCount);
        }
        finally
        {
            NativeLocalPhysics.TestStepOverride = null;
        }
    }

    [Fact]
    public void ProcessUpdatesAsync_DuringTeleport_IgnoresStaleLocalPlayerMovementUpdate()
    {
        ResetObjectManager();

        const ulong playerGuid = 0x12E;

        var objectManager = WoWSharpObjectManager.Instance;
        objectManager.EnterWorld(playerGuid);

        SetPrivateField(objectManager, "_isInControl", true);
        SetPrivateField(objectManager, "_isBeingTeleported", false);

        var player = Assert.IsType<WoWLocalPlayer>(objectManager.Player);
        player.Position = new Position(-618.518f, -4251.67f, 38.718f);
        player.MapId = 1;
        player.MovementFlags = MovementFlags.MOVEFLAG_FORWARD;

        objectManager.NotifyTeleportIncoming(-18f);

        // Mirror MovementHandler's direct local-player position write for the teleport target.
        player.Position = new Position(3f, -11f, -18f);
        player.MapId = 389;

        objectManager.QueueUpdate(new WoWSharpObjectManager.ObjectStateUpdate(
            playerGuid,
            WoWSharpObjectManager.ObjectUpdateOperation.Update,
            WoWObjectType.Player,
            new MovementInfoUpdate
            {
                X = -618.518f,
                Y = -4251.67f,
                Z = 38.718f,
                Facing = 1.5f,
                MovementFlags = MovementFlags.MOVEFLAG_FORWARD,
            },
            new Dictionary<uint, object?>()));

        UpdateProcessingHelper.DrainPendingUpdates();

        Assert.Equal(3f, player.Position.X, 3);
        Assert.Equal(-11f, player.Position.Y, 3);
        Assert.Equal(-18f, player.Position.Z, 3);
        Assert.Equal((uint)389, player.MapId);
    }

    [Fact]
    public void ProcessUpdatesAsync_DuringTeleport_AcceptsTeleportDestinationMovementUpdate()
    {
        ResetObjectManager();

        const ulong playerGuid = 0x12F;

        var objectManager = WoWSharpObjectManager.Instance;
        objectManager.EnterWorld(playerGuid);

        SetPrivateField(objectManager, "_isInControl", true);
        SetPrivateField(objectManager, "_isBeingTeleported", false);

        var player = Assert.IsType<WoWLocalPlayer>(objectManager.Player);
        player.Position = new Position(-618.518f, -4251.67f, 38.718f);
        player.MapId = 1;
        player.MovementFlags = MovementFlags.MOVEFLAG_FORWARD;

        objectManager.NotifyTeleportIncoming(-18f);

        // Mirror MovementHandler's direct local-player position write for the teleport target.
        player.Position = new Position(3f, -11f, -18f);
        player.MapId = 389;

        objectManager.QueueUpdate(new WoWSharpObjectManager.ObjectStateUpdate(
            playerGuid,
            WoWSharpObjectManager.ObjectUpdateOperation.Update,
            WoWObjectType.Player,
            new MovementInfoUpdate
            {
                X = 3.5f,
                Y = -10.5f,
                Z = -18f,
                Facing = 2.0f,
                MovementFlags = MovementFlags.MOVEFLAG_FORWARD,
            },
            new Dictionary<uint, object?>()));

        UpdateProcessingHelper.DrainPendingUpdates();

        Assert.Equal(3.5f, player.Position.X, 3);
        Assert.Equal(-10.5f, player.Position.Y, 3);
        Assert.Equal(-18f, player.Position.Z, 3);
    }

    [Fact]
    public void ProcessUpdatesAsync_CreateSkillInfo_DoesNotFireOnSkillUpdated()
    {
        ResetObjectManager();

        const ulong playerGuid = 0x130;

        var objectManager = WoWSharpObjectManager.Instance;
        objectManager.EnterWorld(playerGuid);

        SkillUpdatedArgs? updatedArgs = null;
        var updateCount = 0;
        EventHandler<SkillUpdatedArgs> handler = (_, args) =>
        {
            updateCount++;
            updatedArgs = args;
        };
        WoWSharpEventEmitter.Instance.OnSkillUpdated += handler;

        try
        {
            objectManager.QueueUpdate(new WoWSharpObjectManager.ObjectStateUpdate(
                playerGuid,
                WoWSharpObjectManager.ObjectUpdateOperation.Add,
                WoWObjectType.Player,
                null,
                new Dictionary<uint, object?>
                {
                    [(uint)EPlayerFields.PLAYER_SKILL_INFO_1_1] = (uint)Skills.FISHING,
                    [(uint)EPlayerFields.PLAYER_SKILL_INFO_1_1 + 1] = 75u | (150u << 16),
                }));

            UpdateProcessingHelper.DrainPendingUpdates();

            Assert.Equal(0, updateCount);
            Assert.Null(updatedArgs);
        }
        finally
        {
            WoWSharpEventEmitter.Instance.OnSkillUpdated -= handler;
            objectManager.ResetWorldSessionState("ProcessUpdatesAsync_CreateSkillInfo_DoesNotFireOnSkillUpdated");
        }
    }

    [Fact]
    public void ProcessUpdatesAsync_SkillInfoUpdate_FiresOnSkillUpdated()
    {
        ResetObjectManager();

        const ulong playerGuid = 0x131;

        var objectManager = WoWSharpObjectManager.Instance;
        objectManager.EnterWorld(playerGuid);

        var player = Assert.IsType<WoWLocalPlayer>(objectManager.Player);
        player.SkillInfo[0].SkillInt1 = (uint)Skills.FISHING;
        player.SkillInfo[0].SkillInt2 = 75u | (150u << 16);

        SkillUpdatedArgs? updatedArgs = null;
        var updateCount = 0;
        EventHandler<SkillUpdatedArgs> handler = (_, args) =>
        {
            updateCount++;
            updatedArgs = args;
        };
        WoWSharpEventEmitter.Instance.OnSkillUpdated += handler;

        try
        {
            objectManager.QueueUpdate(new WoWSharpObjectManager.ObjectStateUpdate(
                playerGuid,
                WoWSharpObjectManager.ObjectUpdateOperation.Update,
                WoWObjectType.Player,
                null,
                new Dictionary<uint, object?>
                {
                    [(uint)EPlayerFields.PLAYER_SKILL_INFO_1_1 + 1] = 76u | (150u << 16),
                }));

            UpdateProcessingHelper.DrainPendingUpdates();

            Assert.Equal(1, updateCount);
            Assert.NotNull(updatedArgs);
            Assert.Equal((uint)Skills.FISHING, updatedArgs!.SkillId);
            Assert.Equal((uint)75, updatedArgs.OldValue);
            Assert.Equal((uint)76, updatedArgs.NewValue);
            Assert.Equal((uint)150, updatedArgs.MaxValue);
        }
        finally
        {
            WoWSharpEventEmitter.Instance.OnSkillUpdated -= handler;
            objectManager.ResetWorldSessionState("ProcessUpdatesAsync_SkillInfoUpdate_FiresOnSkillUpdated");
        }
    }

    [Fact]
    public void EventEmitter_OnForceTimeSkipped_LocalPlayer_AdvancesMovementTimeBase()
    {
        ResetObjectManager();

        const ulong playerGuid = 0x132;

        var objectManager = WoWSharpObjectManager.Instance;
        objectManager.EnterWorld(playerGuid);

        var tracker = new WorldTimeTracker(() => 1000);
        SetPrivateField(objectManager, "_worldTimeTracker", tracker);

        InvokePrivateMethod(objectManager, "EventEmitter_OnForceTimeSkipped", null, new RequiresAcknowledgementArgs(playerGuid, 250));
        Assert.Equal(1250d, tracker.NowMS.TotalMilliseconds);

        InvokePrivateMethod(objectManager, "EventEmitter_OnForceTimeSkipped", null, new RequiresAcknowledgementArgs(playerGuid + 1, 100));
        Assert.Equal(1250d, tracker.NowMS.TotalMilliseconds);
    }

    [Fact]
    public void EventEmitter_OnCharacterJumpStart_LocalPlayer_SetsJumpingAndResetsFallTime()
    {
        ResetObjectManager();

        const ulong playerGuid = 0x131;

        var objectManager = WoWSharpObjectManager.Instance;
        objectManager.EnterWorld(playerGuid);

        var player = Assert.IsType<WoWLocalPlayer>(objectManager.Player);
        player.MovementFlags = MovementFlags.MOVEFLAG_FORWARD;
        player.FallTime = 91;

        var controller = GetPrivateField<MovementController>(objectManager, "_movementController");
        SetPrivateField(controller, "_fallTimeMs", 91u);

        InvokePrivateMethod(objectManager, "EventEmitter_OnCharacterJumpStart", null, new CharacterActionArgs(playerGuid));

        Assert.Equal(MovementFlags.MOVEFLAG_FORWARD | MovementFlags.MOVEFLAG_JUMPING, player.MovementFlags);
        Assert.Equal(0u, player.FallTime);
        Assert.Equal(0u, GetPrivateFieldValue<uint>(controller, "_fallTimeMs"));
    }

    [Fact]
    public void EventEmitter_OnCharacterFallLand_LocalPlayer_ClearsAirborneStateAndPreservesDirectionalIntent()
    {
        ResetObjectManager();

        const ulong playerGuid = 0x132;

        var objectManager = WoWSharpObjectManager.Instance;
        objectManager.EnterWorld(playerGuid);

        var player = Assert.IsType<WoWLocalPlayer>(objectManager.Player);
        player.MovementFlags = MovementFlags.MOVEFLAG_FORWARD | MovementFlags.MOVEFLAG_JUMPING | MovementFlags.MOVEFLAG_FALLINGFAR;
        player.FallTime = 177;

        var controller = GetPrivateField<MovementController>(objectManager, "_movementController");
        SetPrivateField(controller, "_fallTimeMs", 177u);

        InvokePrivateMethod(objectManager, "EventEmitter_OnCharacterFallLand", null, new CharacterActionArgs(playerGuid));

        Assert.Equal(MovementFlags.MOVEFLAG_FORWARD, player.MovementFlags);
        Assert.Equal(0u, player.FallTime);
        Assert.Equal(0u, GetPrivateFieldValue<uint>(controller, "_fallTimeMs"));
    }

    [Fact]
    public void PollKnownBattlegroundAreaTriggersForLocalPlayer_EnteringWsgCaptureZone_SendsAreaTriggerOncePerEntry()
    {
        ResetObjectManager();

        var objectManager = WoWSharpObjectManager.Instance;
        var sentTriggerIds = new List<uint>();
        _fixture._woWClient
            .Setup(c => c.SendAreaTriggerAsync(It.IsAny<uint>(), It.IsAny<CancellationToken>()))
            .Callback<uint, CancellationToken>((triggerId, _) => sentTriggerIds.Add(triggerId))
            .Returns(Task.CompletedTask);
        SetPrivateField(_fixture._woWClient.Object, "_worldClient", new Mock<IWorldClient>().Object);
        objectManager.ResetWorldSessionState("PollKnownBattlegroundAreaTriggersForLocalPlayer");

        var player = Assert.IsType<WoWLocalPlayer>(objectManager.Player);
        player.MapId = 489;
        player.Position = new Position(930.85f, 1431.57f, 345.54f);

        objectManager.PollKnownBattlegroundAreaTriggersForLocalPlayer();
        Assert.Empty(sentTriggerIds);

        player.Position = new Position(918.50f, 1434.04f, 346.05f);
        objectManager.PollKnownBattlegroundAreaTriggersForLocalPlayer();

        Assert.Equal([3647u], sentTriggerIds);

        objectManager.PollKnownBattlegroundAreaTriggersForLocalPlayer();
        Assert.Equal([3647u], sentTriggerIds);

        player.Position = new Position(930.85f, 1431.57f, 345.54f);
        objectManager.PollKnownBattlegroundAreaTriggersForLocalPlayer();
        Assert.Equal([3647u], sentTriggerIds);

        player.Position = new Position(918.50f, 1434.04f, 346.05f);
        objectManager.PollKnownBattlegroundAreaTriggersForLocalPlayer();
        Assert.Equal([3647u, 3647u], sentTriggerIds);
    }

    [Fact]
    public void PollKnownBattlegroundAreaTriggersForLocalPlayer_PendingWorldEntry_SuppressesAreaTriggerSend()
    {
        ResetObjectManager();

        var objectManager = WoWSharpObjectManager.Instance;
        var sentTriggerIds = new List<uint>();
        _fixture._woWClient
            .Setup(c => c.SendAreaTriggerAsync(It.IsAny<uint>(), It.IsAny<CancellationToken>()))
            .Callback<uint, CancellationToken>((triggerId, _) => sentTriggerIds.Add(triggerId))
            .Returns(Task.CompletedTask);
        SetPrivateField(_fixture._woWClient.Object, "_worldClient", new Mock<IWorldClient>().Object);

        const ulong playerGuid = 0xCAFE;
        objectManager.EnterWorld(playerGuid);

        var player = Assert.IsType<WoWLocalPlayer>(objectManager.Player);
        player.MapId = 489;
        player.Position = new Position(918.50f, 1434.04f, 346.05f);

        objectManager.PollKnownBattlegroundAreaTriggersForLocalPlayer();

        Assert.Empty(sentTriggerIds);
    }

    [Fact]
    public void PollKnownBattlegroundAreaTriggersForLocalPlayer_EnteringAbBlacksmithTrigger_SendsAreaTriggerPairOncePerEntry()
    {
        ResetObjectManager();

        var objectManager = WoWSharpObjectManager.Instance;
        var sentTriggerIds = new List<uint>();
        _fixture._woWClient
            .Setup(c => c.SendAreaTriggerAsync(It.IsAny<uint>(), It.IsAny<CancellationToken>()))
            .Callback<uint, CancellationToken>((triggerId, _) => sentTriggerIds.Add(triggerId))
            .Returns(Task.CompletedTask);
        SetPrivateField(_fixture._woWClient.Object, "_worldClient", new Mock<IWorldClient>().Object);
        objectManager.ResetWorldSessionState("PollKnownBattlegroundAreaTriggersForLocalPlayer");

        var player = Assert.IsType<WoWLocalPlayer>(objectManager.Player);
        player.MapId = 529;
        player.Position = new Position(977.08f, 1046.54f, -44.83f);

        objectManager.PollKnownBattlegroundAreaTriggersForLocalPlayer();
        Assert.Empty(sentTriggerIds);

        player.Position = new Position(997.12f, 1001.31f, -31.39f);
        objectManager.PollKnownBattlegroundAreaTriggersForLocalPlayer();

        Assert.Equal([3808u, 3809u], sentTriggerIds);

        objectManager.PollKnownBattlegroundAreaTriggersForLocalPlayer();
        Assert.Equal([3808u, 3809u], sentTriggerIds);

        player.Position = new Position(977.08f, 1046.54f, -44.83f);
        objectManager.PollKnownBattlegroundAreaTriggersForLocalPlayer();
        Assert.Equal([3808u, 3809u], sentTriggerIds);

        player.Position = new Position(997.12f, 1001.31f, -31.39f);
        objectManager.PollKnownBattlegroundAreaTriggersForLocalPlayer();
        Assert.Equal([3808u, 3809u, 3808u, 3809u], sentTriggerIds);
    }

    private void ResetObjectManager()
    {
        WoWSharpObjectManager.Instance.Initialize(
            _fixture._woWClient.Object,
            _fixture._pathfindingClient.Object,
            NullLogger<WoWSharpObjectManager>.Instance,
            WoWSharpEventEmitter.Instance,
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

    private static Mock<IWorldClient> CreateDelayedWorldClientRecorder(
        out List<(Opcode opcode, byte[] payload)> sentPackets,
        Func<Opcode, int> delayMs)
    {
        var packets = new List<(Opcode opcode, byte[] payload)>();
        var mockWorldClient = new Mock<IWorldClient>();
        mockWorldClient
            .Setup(x => x.SendOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .Returns(async (Opcode opcode, byte[] payload, CancellationToken cancellationToken) =>
            {
                var delay = delayMs(opcode);
                if (delay > 0)
                    await Task.Delay(delay, cancellationToken);

                packets.Add((opcode, payload));
            });
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

    private static Mock<IWorldClient> CreateSetActiveMoverRecorder(out List<ulong> activeMoverRequests)
    {
        var requests = new List<ulong>();
        var mockWorldClient = new Mock<IWorldClient>();
        mockWorldClient
            .Setup(x => x.SendSetActiveMoverAsync(It.IsAny<ulong>(), It.IsAny<CancellationToken>()))
            .Callback<ulong, CancellationToken>((guid, _) => requests.Add(guid))
            .Returns(Task.CompletedTask);
        activeMoverRequests = requests;
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

    private static byte[] BuildClientControlPacket(ulong guid, bool canControl)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        ReaderUtils.WritePackedGuid(writer, guid);
        writer.Write(canControl);
        return ms.ToArray();
    }

    private static byte[] BuildCompressedMovePacket(params byte[][] entries)
    {
        var decompressedPayload = entries.SelectMany(entry => entry).ToArray();
        var compressedPayload = PacketManager.Compress(decompressedPayload);

        using var packetStream = new MemoryStream();
        using var packetWriter = new BinaryWriter(packetStream);
        packetWriter.Write((uint)decompressedPayload.Length);
        packetWriter.Write(compressedPayload);
        packetWriter.Flush();

        return packetStream.ToArray();
    }

    private static byte[] BuildCompressedMoveEntry(Opcode opcode, ulong guid, byte[]? payload = null)
    {
        using var bodyStream = new MemoryStream();
        using (var bodyWriter = new BinaryWriter(bodyStream, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            bodyWriter.Write((ushort)opcode);
            ReaderUtils.WritePackedGuid(bodyWriter, guid);
            if (payload is { Length: > 0 })
                bodyWriter.Write(payload);
            bodyWriter.Flush();
        }

        var body = bodyStream.ToArray();
        Assert.InRange(body.Length, 0, byte.MaxValue);

        using var entryStream = new MemoryStream();
        using var entryWriter = new BinaryWriter(entryStream);
        entryWriter.Write((byte)body.Length);
        entryWriter.Write(body);
        entryWriter.Flush();
        return entryStream.ToArray();
    }

    private static byte[] BuildSingleFloatPayload(float value)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        writer.Write(value);
        return ms.ToArray();
    }

    private static byte[] BuildSingleUIntPayload(uint value)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        writer.Write(value);
        return ms.ToArray();
    }

    private static byte[] BuildCounterAndFloatPayload(uint counter, float value)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
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

    private static T GetPrivateFieldValue<T>(object target, string fieldName)
    {
        var type = target.GetType();
        FieldInfo? field = null;
        while (type != null && field == null)
        {
            field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            type = type.BaseType;
        }

        Assert.NotNull(field);
        return Assert.IsType<T>(field!.GetValue(target));
    }

    private static void InvokePrivateMethod(object target, string methodName, params object?[] args)
    {
        var type = target.GetType();
        MethodInfo? method = null;
        while (type != null && method == null)
        {
            method = type.GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            type = type.BaseType;
        }

        Assert.NotNull(method);
        method!.Invoke(target, args);
    }

    private static T InvokePrivateMethod<T>(object target, string methodName, params object?[] args)
    {
        var type = target.GetType();
        MethodInfo? method = null;
        while (type != null && method == null)
        {
            method = type.GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            type = type.BaseType;
        }

        Assert.NotNull(method);
        return Assert.IsType<T>(method!.Invoke(target, args));
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
