using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using BotRunner.Clients;
using GameData.Core.Enums;
using GameData.Core.Interfaces;
using GameData.Core.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using WoWSharpClient.Client;
using WoWSharpClient.Handlers;
using WoWSharpClient.Models;
using WoWSharpClient.Movement;
using WoWSharpClient.Tests.Util;
using WoWSharpClient.Utils;

namespace WoWSharpClient.Tests.Parity;

internal sealed class PacketFlowTraceFixture : IDisposable
{
    private readonly WoWClient _client = new();
    private readonly Mock<IWorldClient> _worldClient = new();
    private readonly Mock<PathfindingClient> _pathfindingClient = new();

    public PacketFlowTraceFixture(SceneDataClient? sceneDataClient = null, bool useLocalPhysics = false)
    {
        SetPrivateField(_client, "_worldClient", _worldClient.Object);

        _worldClient.SetupGet(x => x.IsConnected).Returns(true);
        _worldClient.SetupGet(x => x.IsAuthenticated).Returns(true);
        _worldClient
            .Setup(x => x.SendOpcodeAsync(It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .Callback<Opcode, byte[], CancellationToken>((opcode, payload, _) =>
                Events.Add(new PacketFlowTraceEvent("outbound", opcode.ToString(), opcode, [.. payload])))
            .Returns(Task.CompletedTask);

        ObjectManager.StopGameLoop();
        EventEmitter.Reset();
        ObjectManager.Initialize(
            _client,
            _pathfindingClient.Object,
            NullLogger<WoWSharpObjectManager>.Instance,
            EventEmitter,
            sceneDataClient,
            useLocalPhysics);

        ObjectManager.TestMutationObserver = trace =>
            Events.Add(new PacketFlowTraceEvent(
                "mutation",
                trace.Stage.ToString(),
                Guid: trace.Guid,
                Context: trace.Context,
                MutationStage: trace.Stage));

        EventEmitter.OnLoginVerifyWorld += (_, worldInfo) =>
            Events.Add(new PacketFlowTraceEvent(
                "event",
                "OnLoginVerifyWorld",
                Guid: ObjectManager.Player?.Guid,
                WorldInfo: worldInfo));

        EventEmitter.OnForceRunSpeedChange += (_, args) =>
            Events.Add(new PacketFlowTraceEvent("event", "OnForceRunSpeedChange", Guid: args.Guid, Counter: args.Counter, NumericValue: args.Speed));

        EventEmitter.OnForceMoveRoot += (_, args) =>
            Events.Add(new PacketFlowTraceEvent("event", "OnForceMoveRoot", Guid: args.Guid, Counter: args.Counter));

        EventEmitter.OnForceMoveUnroot += (_, args) =>
            Events.Add(new PacketFlowTraceEvent("event", "OnForceMoveUnroot", Guid: args.Guid, Counter: args.Counter));

        EventEmitter.OnForceMoveKnockBack += (_, args) =>
            Events.Add(new PacketFlowTraceEvent("event", "OnForceMoveKnockBack", Guid: args.Guid, Counter: args.Counter));

        EventEmitter.OnTeleport += (_, args) =>
            Events.Add(new PacketFlowTraceEvent("event", "OnTeleport", Guid: args.Guid, Counter: args.Counter));

        EventEmitter.OnClientControlUpdate += (_, args) =>
            Events.Add(new PacketFlowTraceEvent("event", "OnClientControlUpdate", Guid: args.Guid, BooleanValue: args.CanControl));

        HandlerContext = new HandlerContext(ObjectManager, EventEmitter);
    }

    public WoWSharpObjectManager ObjectManager => WoWSharpObjectManager.Instance;

    public WoWSharpEventEmitter EventEmitter { get; } = new();

    public HandlerContext HandlerContext { get; }

    public List<PacketFlowTraceEvent> Events { get; } = [];

    public void Dispose()
    {
        ObjectManager.TestMutationObserver = null;
        ObjectManager.StopGameLoop();
        ObjectManager.ResetWorldSessionState(nameof(PacketFlowTraceFixture), preservePlayerGuid: false);
        EventEmitter.Reset();
    }

    public void SeedLocalPlayer(
        ulong guid,
        uint mapId = 1,
        Position? position = null,
        float facing = 0f,
        long fixedWorldTimeMs = 1000)
    {
        ObjectManager.PlayerGuid = new HighGuid(guid);
        var player = (WoWLocalPlayer)ObjectManager.Player;
        player.MapId = mapId;
        player.Position = position ?? new Position(0f, 0f, 0f);
        player.Facing = facing;
        player.WalkSpeed = 2.5f;
        player.RunSpeed = 7.0f;
        player.RunBackSpeed = 4.5f;
        player.SwimSpeed = 4.722222f;
        player.SwimBackSpeed = 2.5f;
        player.TurnRate = MathF.PI;

        SetProperty(ObjectManager, "HasEnteredWorld", true);
        InvokePrivateMethod(ObjectManager, "ClearPendingWorldEntry");
        SetPrivateField(ObjectManager, "_isInControl", true);
        SetPrivateField(ObjectManager, "_hasExplicitClientControlLockout", false);
        SetPrivateField(ObjectManager, "_isBeingTeleported", false);
        SetPrivateField(ObjectManager, "_worldTimeTracker", new WorldTimeTracker(() => fixedWorldTimeMs));
    }

    public void EnsureTeleportAckFlushSupport()
    {
        var player = (WoWLocalPlayer)ObjectManager.Player;
        var controller = new MovementController(_client, player, objectManager: ObjectManager);
        SetPrivateField(ObjectManager, "_movementController", controller);
    }

    public void MarkTeleportGroundSnapResolved()
    {
        var controller = GetPrivateField<MovementController>(ObjectManager, "_movementController");
        SetPrivateField(controller, "_needsGroundSnap", false);
    }

    public void AddRemoteUnit(ulong guid)
    {
        ObjectManager.QueueUpdate(new WoWSharpObjectManager.ObjectStateUpdate(
            guid,
            WoWSharpObjectManager.ObjectUpdateOperation.Add,
            WoWObjectType.Unit,
            null,
            []));
        UpdateProcessingHelper.DrainPendingUpdates();
    }

    public void Dispatch(Opcode opcode, byte[] payload)
    {
        Events.Add(new PacketFlowTraceEvent("inbound", opcode.ToString(), opcode, [.. payload]));
        Events.Add(new PacketFlowTraceEvent("dispatch", ResolveHandlerLabel(opcode), opcode));
        ResolveHandler(opcode)(opcode, payload, HandlerContext);

        if (opcode is Opcode.SMSG_UPDATE_OBJECT
            or Opcode.SMSG_COMPRESSED_UPDATE_OBJECT
            or Opcode.SMSG_MONSTER_MOVE
            or Opcode.SMSG_MONSTER_MOVE_TRANSPORT
            or Opcode.MSG_MOVE_TELEPORT
            or Opcode.MSG_MOVE_TELEPORT_ACK)
        {
            UpdateProcessingHelper.DrainPendingUpdates();
        }
    }

    public int FlushDeferredMovementChanges(uint gameTimeMs)
    {
        Events.Add(new PacketFlowTraceEvent("flush", "DeferredMovementChanges", NumericValue: gameTimeMs));
        return ObjectManager.FlushPendingDeferredMovementChanges(gameTimeMs);
    }

    public bool ConsumeKnockbackAndFlushAck(uint gameTimeMs)
    {
        if (!ObjectManager.TryConsumePendingKnockback(out var vx, out var vy, out var vz))
        {
            return false;
        }

        Events.Add(new PacketFlowTraceEvent(
            "consume",
            "KnockBackImpulse",
            NumericValue: gameTimeMs,
            Position: new Position(vx, vy, vz)));

        return ObjectManager.TryFlushPendingKnockbackAck(gameTimeMs);
    }

    public bool FlushTeleportAck()
    {
        Events.Add(new PacketFlowTraceEvent("flush", "TeleportAck"));
        return ObjectManager.TryFlushPendingTeleportAck();
    }

    public void SetIsBeingTeleported(bool value) => SetPrivateField(ObjectManager, "_isBeingTeleported", value);

    public bool IsInControl() => GetPrivateField<bool>(ObjectManager, "_isInControl");

    public bool IsBeingTeleported() => GetPrivateField<bool>(ObjectManager, "_isBeingTeleported");

    public void ReconcilePlayerControlState() => InvokePrivateMethod(ObjectManager, "ReconcilePlayerControlState");

    private static Action<Opcode, byte[], HandlerContext> ResolveHandler(Opcode opcode)
        => opcode switch
        {
            Opcode.SMSG_LOGIN_VERIFY_WORLD => LoginHandler.HandleLoginVerifyWorld,
            Opcode.SMSG_NEW_WORLD => LoginHandler.HandleNewWorld,
            Opcode.SMSG_CLIENT_CONTROL_UPDATE => ClientControlHandler.HandleClientControlUpdate,
            Opcode.SMSG_UPDATE_OBJECT or Opcode.SMSG_COMPRESSED_UPDATE_OBJECT => ObjectUpdateHandler.HandleUpdateObject,
            Opcode.SMSG_FORCE_RUN_SPEED_CHANGE or Opcode.SMSG_FORCE_MOVE_ROOT or Opcode.SMSG_FORCE_MOVE_UNROOT or Opcode.SMSG_MOVE_KNOCK_BACK
                or Opcode.MSG_MOVE_TELEPORT or Opcode.MSG_MOVE_TELEPORT_ACK or Opcode.SMSG_MONSTER_MOVE
                or Opcode.SMSG_MONSTER_MOVE_TRANSPORT => MovementHandler.HandleUpdateMovement,
            _ => throw new NotSupportedException($"Opcode {opcode} is not wired in PacketFlowTraceFixture.")
        };

    private static string ResolveHandlerLabel(Opcode opcode)
        => opcode switch
        {
            Opcode.SMSG_LOGIN_VERIFY_WORLD => "LoginHandler.HandleLoginVerifyWorld",
            Opcode.SMSG_NEW_WORLD => "LoginHandler.HandleNewWorld",
            Opcode.SMSG_CLIENT_CONTROL_UPDATE => "ClientControlHandler.HandleClientControlUpdate",
            Opcode.SMSG_UPDATE_OBJECT or Opcode.SMSG_COMPRESSED_UPDATE_OBJECT => "ObjectUpdateHandler.HandleUpdateObject",
            _ => "MovementHandler.HandleUpdateMovement"
        };

    private static void SetPrivateField(object target, string fieldName, object? value)
    {
        var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        if (field == null)
            throw new InvalidOperationException($"Field '{fieldName}' was not found on {target.GetType().Name}.");

        field.SetValue(target, value);
    }

    private static void SetProperty(object target, string propertyName, object? value)
    {
        var property = target.GetType().GetProperty(
            propertyName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (property == null)
            throw new InvalidOperationException($"Property '{propertyName}' was not found on {target.GetType().Name}.");

        property.SetValue(target, value);
    }

    private static T GetPrivateField<T>(object target, string fieldName)
    {
        var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        if (field == null)
            throw new InvalidOperationException($"Field '{fieldName}' was not found on {target.GetType().Name}.");

        return (T)field.GetValue(target)!;
    }

    private static void InvokePrivateMethod(object target, string methodName)
    {
        var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        if (method == null)
            throw new InvalidOperationException($"Method '{methodName}' was not found on {target.GetType().Name}.");

        method.Invoke(target, null);
    }
}

internal sealed record PacketFlowTraceEvent(
    string Kind,
    string Label,
    Opcode? Opcode = null,
    byte[]? Payload = null,
    ulong? Guid = null,
    uint? Counter = null,
    float? NumericValue = null,
    bool? BooleanValue = null,
    Position? Position = null,
    WorldInfo? WorldInfo = null,
    string? Context = null,
    WoWSharpObjectManager.TestMutationStage? MutationStage = null);
