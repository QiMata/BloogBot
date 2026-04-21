using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using BotRunner.Clients;
using BotRunner.Interfaces;
using Communication;
using GameData.Core.Enums;
using GameData.Core.Interfaces;
using GameData.Core.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Buffers.Binary;
using System.IO;
using System.Reactive.Subjects;
using WoWSharpClient.Client;
using WoWSharpClient.Networking.ClientComponents;
using WoWSharpClient.Networking.ClientComponents.I;

namespace BotRunner.Tests;

public class BotRunnerServiceSnapshotTests
{
    [Fact]
    public void PopulateSnapshotFromObjectManager_MirrorsNearbyEntitiesIntoMovementData()
    {
        var playerPosition = new Position(100f, 200f, 30f);
        var player = CreatePlayer(playerPosition);
        var nearbyUnit = CreateUnit(2UL, new Position(110f, 205f, 30f), "Nearby Guard");
        var farUnit = CreateUnit(3UL, new Position(200f, 200f, 30f), "Far Guard");
        var nearbyGameObject = CreateGameObject(4UL, 176495u, new Position(108f, 198f, 30f), "Zeppelin");
        var farGameObject = CreateGameObject(5UL, 999999u, new Position(180f, 200f, 30f), "Far Object");

        var objectManager = new Mock<IObjectManager>(MockBehavior.Loose);
        objectManager.SetupGet(x => x.EventHandler).Returns(new Mock<IWoWEventHandler>(MockBehavior.Loose).Object);
        objectManager.SetupGet(x => x.HasEnteredWorld).Returns(true);
        objectManager.SetupGet(x => x.IsInMapTransition).Returns(false);
        objectManager.SetupGet(x => x.Player).Returns(player.Object);
        objectManager.SetupGet(x => x.Objects).Returns(new IWoWObject[] { player.Object, nearbyUnit.Object, farUnit.Object, nearbyGameObject.Object, farGameObject.Object });
        objectManager.SetupGet(x => x.Units).Returns(new IWoWUnit[] { player.Object, nearbyUnit.Object, farUnit.Object });
        objectManager.SetupGet(x => x.GameObjects).Returns(new IWoWGameObject[] { nearbyGameObject.Object, farGameObject.Object });
        objectManager.SetupGet(x => x.KnownSpellIds).Returns(Array.Empty<uint>());
        objectManager.Setup(x => x.GetContainedItems()).Returns(Array.Empty<IWoWItem>());

        var service = new BotRunnerService(
            objectManager.Object,
            new CharacterStateUpdateClient(NullLogger.Instance),
            new Mock<IDependencyContainer>(MockBehavior.Loose).Object);

        InvokePopulateSnapshot(service);
        var snapshot = ReadActivitySnapshot(service);

        Assert.NotNull(snapshot.MovementData);
        Assert.Single(snapshot.NearbyUnits);
        Assert.Single(snapshot.NearbyObjects);
        Assert.Single(snapshot.MovementData.NearbyUnits);
        Assert.Single(snapshot.MovementData.NearbyGameObjects);

        Assert.Equal(nearbyUnit.Object.Guid, snapshot.NearbyUnits[0].GameObject.Base.Guid);
        Assert.Equal(nearbyUnit.Object.Guid, snapshot.MovementData.NearbyUnits[0].Guid);

        Assert.Equal(nearbyGameObject.Object.Guid, snapshot.NearbyObjects[0].Base.Guid);
        Assert.Equal(nearbyGameObject.Object.ScaleX, snapshot.NearbyObjects[0].Base.ScaleX);
        Assert.Equal((uint)nearbyGameObject.Object.DynamicFlags, snapshot.NearbyObjects[0].DynamicFlags);
        Assert.Equal(nearbyGameObject.Object.FactionTemplate, snapshot.NearbyObjects[0].FactionTemplate);
        Assert.Equal(nearbyGameObject.Object.ArtKit, snapshot.NearbyObjects[0].ArtKit);
        Assert.Equal(nearbyGameObject.Object.AnimProgress, snapshot.NearbyObjects[0].AnimProgress);
        Assert.Equal(nearbyGameObject.Object.Guid, snapshot.MovementData.NearbyGameObjects[0].Guid);
        Assert.Equal(nearbyGameObject.Object.Entry, snapshot.MovementData.NearbyGameObjects[0].Entry);
        Assert.Equal(nearbyGameObject.Object.Name, snapshot.MovementData.NearbyGameObjects[0].Name);
        Assert.Equal(nearbyGameObject.Object.ScaleX, snapshot.MovementData.NearbyGameObjects[0].Scale);
        Assert.Equal(nearbyGameObject.Object.AnimProgress, snapshot.MovementData.NearbyGameObjects[0].AnimProgress);
        Assert.True(snapshot.MovementData.NearbyGameObjects[0].DistanceToPlayer > 0f);
    }

    [Fact]
    public void PopulateSnapshotFromObjectManager_SkipsNearbyEntitiesWhenTransitionStartsMidPopulate()
    {
        // Regression guard for the FG BG-transfer crash: a cross-map transfer that
        // begins after the top-of-populate guard passes must still prevent the
        // nearby-unit / nearby-object enumeration from dereferencing freed WoW
        // pointers. We simulate the race by letting IsInMapTransition return
        // false on the first read (top guard) and true on every subsequent read
        // (the in-loop guard added alongside this test).
        var playerPosition = new Position(100f, 200f, 30f);
        var player = CreatePlayer(playerPosition);
        var nearbyUnit = CreateUnit(2UL, new Position(110f, 205f, 30f), "Nearby Guard");
        var nearbyGameObject = CreateGameObject(4UL, 176495u, new Position(108f, 198f, 30f), "Zeppelin");

        var transitionCallCount = 0;
        var objectManager = new Mock<IObjectManager>(MockBehavior.Loose);
        objectManager.SetupGet(x => x.EventHandler).Returns(new Mock<IWoWEventHandler>(MockBehavior.Loose).Object);
        objectManager.SetupGet(x => x.HasEnteredWorld).Returns(true);
        objectManager.SetupGet(x => x.IsInMapTransition)
            .Returns(() => Interlocked.Increment(ref transitionCallCount) > 1);
        objectManager.SetupGet(x => x.Player).Returns(player.Object);
        objectManager.SetupGet(x => x.Objects).Returns(new IWoWObject[] { player.Object, nearbyUnit.Object, nearbyGameObject.Object });
        objectManager.SetupGet(x => x.Units).Returns(new IWoWUnit[] { player.Object, nearbyUnit.Object });
        objectManager.SetupGet(x => x.GameObjects).Returns(new IWoWGameObject[] { nearbyGameObject.Object });
        objectManager.SetupGet(x => x.KnownSpellIds).Returns(Array.Empty<uint>());
        objectManager.Setup(x => x.GetContainedItems()).Returns(Array.Empty<IWoWItem>());

        var service = new BotRunnerService(
            objectManager.Object,
            new CharacterStateUpdateClient(NullLogger.Instance),
            new Mock<IDependencyContainer>(MockBehavior.Loose).Object);

        InvokePopulateSnapshot(service);
        var snapshot = ReadActivitySnapshot(service);

        Assert.Empty(snapshot.NearbyUnits);
        Assert.Empty(snapshot.NearbyObjects);
        if (snapshot.MovementData != null)
        {
            Assert.Empty(snapshot.MovementData.NearbyUnits);
            Assert.Empty(snapshot.MovementData.NearbyGameObjects);
        }
        Assert.True(transitionCallCount >= 2,
            $"Expected at least two IsInMapTransition reads (top guard + in-loop guard), got {transitionCallCount}.");
    }

    [Fact]
    public void Start_WhenInMapTransition_StillPublishesTransferringSnapshot()
    {
        var player = CreatePlayer(new Position(1500f, -4200f, 30f), mapId: 529u);
        var objectManager = new Mock<IObjectManager>(MockBehavior.Loose);
        objectManager.SetupGet(x => x.EventHandler).Returns(new Mock<IWoWEventHandler>(MockBehavior.Loose).Object);
        objectManager.SetupGet(x => x.HasEnteredWorld).Returns(true);
        objectManager.SetupGet(x => x.IsInMapTransition).Returns(true);
        objectManager.SetupGet(x => x.Player).Returns(player.Object);
        objectManager.SetupGet(x => x.Objects).Returns(new IWoWObject[] { player.Object });
        objectManager.SetupGet(x => x.Units).Returns(new IWoWUnit[] { player.Object });
        objectManager.SetupGet(x => x.GameObjects).Returns(Array.Empty<IWoWGameObject>());
        objectManager.SetupGet(x => x.KnownSpellIds).Returns(Array.Empty<uint>());
        objectManager.Setup(x => x.GetContainedItems()).Returns(Array.Empty<IWoWItem>());

        var updateClient = new CapturingCharacterStateUpdateClient();
        var service = new BotRunnerService(
            objectManager.Object,
            updateClient,
            new Mock<IDependencyContainer>(MockBehavior.Loose).Object);

        try
        {
            service.Start();
            Assert.True(updateClient.WaitForSend(TimeSpan.FromSeconds(2)), "Expected a state update while the bot is transferring maps.");
        }
        finally
        {
            service.Stop();
        }

        var snapshot = Assert.IsType<WoWActivitySnapshot>(updateClient.LastSnapshot);
        Assert.True(updateClient.SendCount >= 1, "Expected at least one state update during the transition loop.");
        Assert.Equal(BotConnectionState.BotTransferring, snapshot.ConnectionState);
        Assert.True(snapshot.IsMapTransition);
        Assert.Equal((uint)529, snapshot.CurrentMapId);
        Assert.Equal("InWorld", snapshot.ScreenState);
        Assert.Equal("SnapshotBot", snapshot.CharacterName);
    }

    [Fact]
    public void SubscribeToMessageEvents_BuffersWorldStateUpdateMessages()
    {
        var eventHandler = new Mock<IWoWEventHandler>(MockBehavior.Loose);
        var objectManager = new Mock<IObjectManager>(MockBehavior.Loose);
        objectManager.SetupGet(x => x.EventHandler).Returns(eventHandler.Object);

        var service = new BotRunnerService(
            objectManager.Object,
            new CharacterStateUpdateClient(NullLogger.Instance),
            new Mock<IDependencyContainer>(MockBehavior.Loose).Object);

        eventHandler.Raise(
            handler => handler.OnWorldStateUpdate += null!,
            eventHandler.Object,
            new WorldState { StateId = 0x6F0, StateValue = 1600 });

        var recentMessages = ReadRecentChatMessages(service);

        Assert.Contains("[WORLDSTATE] 0x6F0/1776=1600", recentMessages);
    }

    [Fact]
    public void SubscribeToMessageEvents_BuffersWorldStateInitSummary()
    {
        var eventHandler = new Mock<IWoWEventHandler>(MockBehavior.Loose);
        var objectManager = new Mock<IObjectManager>(MockBehavior.Loose);
        objectManager.SetupGet(x => x.EventHandler).Returns(eventHandler.Object);

        var service = new BotRunnerService(
            objectManager.Object,
            new CharacterStateUpdateClient(NullLogger.Instance),
            new Mock<IDependencyContainer>(MockBehavior.Loose).Object);

        eventHandler.Raise(
            handler => handler.OnWorldStatesInit += null!,
            eventHandler.Object,
            new List<WorldState>
            {
                new() { StateId = 0x6F1, StateValue = 1 },
                new() { StateId = 0x6F0, StateValue = 0 },
            });

        var recentMessages = ReadRecentChatMessages(service);

        Assert.Contains(
            "[WORLDSTATE_INIT] 0x6F0/1776=0; 0x6F1/1777=1",
            recentMessages);
    }

    [Fact]
    public void SubscribeToMessageEvents_BuffersBattlegroundStatusMessages()
    {
        var eventHandler = new Mock<IWoWEventHandler>(MockBehavior.Loose);
        var objectManager = new Mock<IObjectManager>(MockBehavior.Loose);
        objectManager.SetupGet(x => x.EventHandler).Returns(eventHandler.Object);

        var (agentFactory, subjects, _, _) = CreateBattlegroundAgentFactory();
        var service = new BotRunnerService(
            objectManager.Object,
            new CharacterStateUpdateClient(NullLogger.Instance),
            new Mock<IDependencyContainer>(MockBehavior.Loose).Object,
            agentFactoryAccessor: () => agentFactory.Object);

        subjects[Opcode.SMSG_BATTLEFIELD_STATUS].OnNext(
            BuildBattlefieldStatusPayload(queueSlot: 1u, mapId: 529u, statusId: BattlegroundStatusId.WaitJoin, extraTimeMs: 30000u));

        var recentMessages = ReadRecentChatMessages(service);

        Assert.Contains(
            "[BATTLEGROUND_STATUS] queueSlot=1 bgType=529 status=WaitJoin mapId=529 acceptMs=30000",
            recentMessages);
    }

    [Fact]
    public void SubscribeToMessageEvents_BuffersBattlegroundScoreboardMessages()
    {
        var eventHandler = new Mock<IWoWEventHandler>(MockBehavior.Loose);
        var objectManager = new Mock<IObjectManager>(MockBehavior.Loose);
        objectManager.SetupGet(x => x.EventHandler).Returns(eventHandler.Object);

        var (agentFactory, subjects, _, _) = CreateBattlegroundAgentFactory();
        var service = new BotRunnerService(
            objectManager.Object,
            new CharacterStateUpdateClient(NullLogger.Instance),
            new Mock<IDependencyContainer>(MockBehavior.Loose).Object,
            agentFactoryAccessor: () => agentFactory.Object);

        subjects[Opcode.MSG_PVP_LOG_DATA].OnNext(BuildPvPLogPayload(
            isFinished: true,
            winner: 0,
            scores:
            [
                new PvPPlayerScore(0x1UL, 0u, 3u, 1u, 45u),
                new PvPPlayerScore(0x2UL, 2u, 5u, 0u, 60u),
            ]));

        var recentMessages = ReadRecentChatMessages(service);

        Assert.Contains(
            "[PVP_LOG] finished=1 players=2 sample=0x1:0/3/1/45; 0x2:2/5/0/60",
            recentMessages);
    }

    private static void InvokePopulateSnapshot(BotRunnerService service)
    {
        var method = typeof(BotRunnerService).GetMethod("PopulateSnapshotFromObjectManager", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method!.Invoke(service, null);
    }

    private static WoWActivitySnapshot ReadActivitySnapshot(BotRunnerService service)
    {
        var field = typeof(BotRunnerService).GetField("_activitySnapshot", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsType<WoWActivitySnapshot>(field!.GetValue(service));
    }

    private static IReadOnlyCollection<string> ReadRecentChatMessages(BotRunnerService service)
    {
        var field = typeof(BotRunnerService).GetField("_recentChatMessages", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsType<Queue<string>>(field!.GetValue(service)).ToArray();
    }

    private static (Mock<IAgentFactory> AgentFactory, Dictionary<Opcode, Subject<ReadOnlyMemory<byte>>> Subjects, Mock<IWorldClient> WorldClient, BattlegroundNetworkClientComponent BattlegroundAgent) CreateBattlegroundAgentFactory()
    {
        var subjects = new Dictionary<Opcode, Subject<ReadOnlyMemory<byte>>>();
        var worldClient = new Mock<IWorldClient>(MockBehavior.Loose);
        worldClient
            .Setup(client => client.RegisterOpcodeHandler(It.IsAny<Opcode>()))
            .Returns((Opcode opcode) =>
            {
                if (!subjects.TryGetValue(opcode, out var subject))
                {
                    subject = new Subject<ReadOnlyMemory<byte>>();
                    subjects[opcode] = subject;
                }

                return subject;
            });

        var battlegroundAgent = new BattlegroundNetworkClientComponent(worldClient.Object, NullLogger<BattlegroundNetworkClientComponent>.Instance);
        var agentFactory = new Mock<IAgentFactory>(MockBehavior.Loose);
        agentFactory.SetupGet(factory => factory.BattlegroundAgent).Returns(battlegroundAgent);
        return (agentFactory, subjects, worldClient, battlegroundAgent);
    }

    private static ReadOnlyMemory<byte> BuildBattlefieldStatusPayload(
        uint queueSlot,
        uint mapId,
        BattlegroundStatusId statusId,
        uint? extraTimeMs = null)
    {
        using var ms = new MemoryStream();
        var buffer = new byte[4];

        BinaryPrimitives.WriteUInt32LittleEndian(buffer, queueSlot);
        ms.Write(buffer, 0, 4);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer, mapId);
        ms.Write(buffer, 0, 4);
        ms.WriteByte(0);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer, 0u);
        ms.Write(buffer, 0, 4);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer, (uint)statusId);
        ms.Write(buffer, 0, 4);

        if (extraTimeMs.HasValue)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(buffer, extraTimeMs.Value);
            ms.Write(buffer, 0, 4);
        }

        return ms.ToArray();
    }

    private static ReadOnlyMemory<byte> BuildPvPLogPayload(
        bool isFinished,
        byte winner,
        IReadOnlyList<PvPPlayerScore> scores)
    {
        using var ms = new MemoryStream();
        ms.WriteByte(0);
        ms.WriteByte(isFinished ? (byte)1 : (byte)0);
        if (isFinished)
            ms.WriteByte(winner);

        var buffer = new byte[8];
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(0, 4), (uint)scores.Count);
        ms.Write(buffer, 0, 4);

        foreach (var score in scores)
        {
            BinaryPrimitives.WriteUInt64LittleEndian(buffer, score.PlayerGuid);
            ms.Write(buffer, 0, 8);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(0, 4), score.KillingBlows);
            ms.Write(buffer, 0, 4);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(0, 4), score.HonorableKills);
            ms.Write(buffer, 0, 4);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(0, 4), score.Deaths);
            ms.Write(buffer, 0, 4);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(0, 4), score.BonusHonor);
            ms.Write(buffer, 0, 4);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(0, 4), 0u);
            ms.Write(buffer, 0, 4);
        }

        return ms.ToArray();
    }

    private static Mock<IWoWLocalPlayer> CreatePlayer(Position position, uint mapId = 1u)
    {
        var player = new Mock<IWoWLocalPlayer>(MockBehavior.Loose);
        player.SetupGet(x => x.Guid).Returns(1UL);
        player.SetupGet(x => x.Name).Returns("SnapshotBot");
        player.SetupGet(x => x.ObjectType).Returns(WoWObjectType.Player);
        player.SetupGet(x => x.Position).Returns(position);
        player.SetupGet(x => x.Facing).Returns(1.25f);
        player.SetupGet(x => x.ScaleX).Returns(1f);
        player.SetupGet(x => x.Entry).Returns(0u);
        player.SetupGet(x => x.MapId).Returns(mapId);
        player.SetupGet(x => x.Level).Returns(10u);
        player.SetupGet(x => x.Health).Returns(100u);
        player.SetupGet(x => x.MaxHealth).Returns(100u);
        player.SetupGet(x => x.InGhostForm).Returns(false);
        player.SetupGet(x => x.Copper).Returns(0u);
        player.SetupGet(x => x.CorpseRecoveryDelaySeconds).Returns(0);
        player.SetupGet(x => x.Inventory).Returns(Array.Empty<uint>());
        player.SetupGet(x => x.QuestLog).Returns(Array.Empty<QuestSlot>());
        player.SetupGet(x => x.SkillInfo).Returns(Array.Empty<SkillInfo>());
        player.SetupGet(x => x.Powers).Returns(new Dictionary<Powers, uint>());
        player.SetupGet(x => x.MaxPowers).Returns(new Dictionary<Powers, uint>());
        player.SetupGet(x => x.Bytes0).Returns(Array.Empty<uint>());
        player.SetupGet(x => x.Bytes1).Returns(Array.Empty<uint>());
        player.SetupGet(x => x.Bytes2).Returns(Array.Empty<uint>());
        player.SetupGet(x => x.AuraFields).Returns(Array.Empty<uint>());
        return player;
    }

    private static Mock<IWoWUnit> CreateUnit(ulong guid, Position position, string name)
    {
        var unit = new Mock<IWoWUnit>(MockBehavior.Loose);
        unit.SetupGet(x => x.Guid).Returns(guid);
        unit.SetupGet(x => x.Name).Returns(name);
        unit.SetupGet(x => x.ObjectType).Returns(WoWObjectType.Unit);
        unit.SetupGet(x => x.Position).Returns(position);
        unit.SetupGet(x => x.Facing).Returns(0.5f);
        unit.SetupGet(x => x.ScaleX).Returns(1f);
        unit.SetupGet(x => x.Entry).Returns(123u);
        unit.SetupGet(x => x.Level).Returns(20u);
        unit.SetupGet(x => x.Health).Returns(80u);
        unit.SetupGet(x => x.MaxHealth).Returns(100u);
        unit.SetupGet(x => x.Powers).Returns(new Dictionary<Powers, uint>());
        unit.SetupGet(x => x.MaxPowers).Returns(new Dictionary<Powers, uint>());
        unit.SetupGet(x => x.Bytes0).Returns(Array.Empty<uint>());
        unit.SetupGet(x => x.Bytes1).Returns(Array.Empty<uint>());
        unit.SetupGet(x => x.Bytes2).Returns(Array.Empty<uint>());
        unit.SetupGet(x => x.AuraFields).Returns(Array.Empty<uint>());
        return unit;
    }

    private static Mock<IWoWGameObject> CreateGameObject(ulong guid, uint entry, Position position, string name)
    {
        var gameObject = new Mock<IWoWGameObject>(MockBehavior.Loose);
        gameObject.SetupGet(x => x.Guid).Returns(guid);
        gameObject.SetupGet(x => x.Name).Returns(name);
        gameObject.SetupGet(x => x.ObjectType).Returns(WoWObjectType.GameObj);
        gameObject.SetupGet(x => x.Position).Returns(position);
        gameObject.SetupGet(x => x.Facing).Returns(0.75f);
        gameObject.SetupGet(x => x.ScaleX).Returns(1.25f);
        gameObject.SetupGet(x => x.Entry).Returns(entry);
        gameObject.SetupGet(x => x.DisplayId).Returns(456u);
        gameObject.SetupGet(x => x.TypeId).Returns(15u);
        gameObject.SetupGet(x => x.Flags).Returns(17u);
        gameObject.SetupGet(x => x.GoState).Returns(GOState.Ready);
        gameObject.SetupGet(x => x.DynamicFlags).Returns(DynamicFlags.CanBeLooted | DynamicFlags.TappedByMe);
        gameObject.SetupGet(x => x.FactionTemplate).Returns(35u);
        gameObject.SetupGet(x => x.Level).Returns(1u);
        gameObject.SetupGet(x => x.ArtKit).Returns(9u);
        gameObject.SetupGet(x => x.AnimProgress).Returns(123u);
        return gameObject;
    }

    private sealed class CapturingCharacterStateUpdateClient : CharacterStateUpdateClient
    {
        private readonly ManualResetEventSlim _sentSignal = new(false);
        private WoWActivitySnapshot? _lastSnapshot;
        private int _sendCount;

        public CapturingCharacterStateUpdateClient()
            : base(NullLogger.Instance)
        {
        }

        public WoWActivitySnapshot? LastSnapshot => _lastSnapshot;

        public int SendCount => _sendCount;

        public bool WaitForSend(TimeSpan timeout) => _sentSignal.Wait(timeout);

        public override Task<WoWActivitySnapshot> SendMemberStateUpdateAsync(
            WoWActivitySnapshot update,
            CancellationToken ct = default)
        {
            _lastSnapshot = update.Clone();
            Interlocked.Increment(ref _sendCount);
            _sentSignal.Set();
            return Task.FromResult<WoWActivitySnapshot>(null!);
        }
    }
}
