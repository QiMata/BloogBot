using BotRunner.Tasks;
using BotRunner.Tasks.Battlegrounds;
using BotRunner.Travel;
using GameData.Core.Enums;
using GameData.Core.Frames;
using GameData.Core.Interfaces;
using GameData.Core.Models;
using Moq;
using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace BotRunner.Tests.Combat;

public class BattlemasterDataTests
{
    [Fact]
    public void FindBattlemaster_UsesRealCityMastersByTitle()
    {
        var hordeWsg = BattlemasterData.FindBattlemaster(
            BattlemasterData.BattlegroundType.WarsongGulch,
            DungeonEntryData.DungeonFaction.Horde);
        var hordeAb = BattlemasterData.FindBattlemaster(
            BattlemasterData.BattlegroundType.ArathiBasin,
            DungeonEntryData.DungeonFaction.Horde);
        var hordeAv = BattlemasterData.FindBattlemaster(
            BattlemasterData.BattlegroundType.AlteracValley,
            DungeonEntryData.DungeonFaction.Horde);
        var allianceWsg = BattlemasterData.FindBattlemaster(
            BattlemasterData.BattlegroundType.WarsongGulch,
            DungeonEntryData.DungeonFaction.Alliance);
        var allianceAb = BattlemasterData.FindBattlemaster(
            BattlemasterData.BattlegroundType.ArathiBasin,
            DungeonEntryData.DungeonFaction.Alliance);
        var allianceAv = BattlemasterData.FindBattlemaster(
            BattlemasterData.BattlegroundType.AlteracValley,
            DungeonEntryData.DungeonFaction.Alliance);

        AssertBattlemaster(hordeWsg, "Brakgul Deathbringer", "Warsong Gulch Battlemaster", 3890, 4765, 1u);
        AssertBattlemaster(hordeAb, "Deze Snowbane", "Arathi Basin Battlemaster", 15006, 4761, 1u);
        AssertBattlemaster(hordeAv, "Kartra Bloodsnarl", "Alterac Valley Battlemaster", 14942, 4764, 1u);
        AssertBattlemaster(allianceWsg, "Elfarran", "Warsong Gulch Battlemaster", 14981, 54614, 0u);
        AssertBattlemaster(allianceAb, "Lady Hoteshem", "Arathi Basin Battlemaster", 15008, 54625, 0u);
        AssertBattlemaster(allianceAv, "Thelman Slatefist", "Alterac Valley Battlemaster", 7410, 42893, 0u);
    }

    private static void AssertBattlemaster(
        BattlemasterData.BattlemasterLocation? battlemaster,
        string expectedName,
        string expectedTitle,
        uint expectedEntry,
        uint expectedSpawnGuid,
        uint expectedMapId)
    {
        Assert.NotNull(battlemaster);
        Assert.Equal(expectedName, battlemaster!.NpcName);
        Assert.Equal(expectedTitle, battlemaster.NpcTitle);
        Assert.Equal(expectedEntry, battlemaster.NpcEntry);
        Assert.Equal(expectedSpawnGuid, battlemaster.SpawnGuid);
        Assert.Equal(expectedMapId, battlemaster.MapId);
    }

    [Theory]
    [InlineData(BattlemasterData.BattlegroundType.WarsongGulch, 10)]
    [InlineData(BattlemasterData.BattlegroundType.ArathiBasin, 20)]
    [InlineData(BattlemasterData.BattlegroundType.AlteracValley, 51)]
    public void GetMinimumLevel_ReturnsBattlegroundSpecificRequirement(
        BattlemasterData.BattlegroundType battlegroundType,
        int expectedMinimumLevel)
    {
        Assert.Equal(expectedMinimumLevel, BattlemasterData.GetMinimumLevel(battlegroundType));
        Assert.Equal(expectedMinimumLevel, BattlemasterData.GetMinimumLevel((uint)battlegroundType));
    }
}

public class BattlegroundQueueTaskTests
{
    [Fact]
    public void Update_SelectsExpectedBattlemasterEntry_WhenMultipleFlaggedBattlemastersAreNearby()
    {
        var (ctx, om, stack) = AtomicTaskTestHelpers.CreateContext();
        var player = AtomicTaskTestHelpers.CreatePlayer(new Position(1981f, -4788f, 56f));
        player.Setup(p => p.MapId).Returns(1u);
        player.Setup(p => p.Race).Returns(Race.Orc);
        player.Setup(p => p.Gender).Returns(Gender.Female);

        var alteracMaster = CreateBattlemaster(
            guid: 0xA1UL,
            entry: BattlemasterData.OrgrimmarAv.NpcEntry,
            name: BattlemasterData.OrgrimmarAv.NpcName,
            position: BattlemasterData.OrgrimmarAv.Position,
            npcFlags: NPCFlags.UNIT_NPC_FLAG_BATTLEMASTER);
        var arathiMaster = CreateBattlemaster(
            guid: 0xA2UL,
            entry: BattlemasterData.OrgrimmarAb.NpcEntry,
            name: BattlemasterData.OrgrimmarAb.NpcName,
            position: BattlemasterData.OrgrimmarAb.Position,
            npcFlags: NPCFlags.UNIT_NPC_FLAG_BATTLEMASTER);
        var warsongMaster = CreateBattlemaster(
            guid: 0xA3UL,
            entry: BattlemasterData.OrgrimmarWsg.NpcEntry,
            name: BattlemasterData.OrgrimmarWsg.NpcName,
            position: BattlemasterData.OrgrimmarWsg.Position,
            npcFlags: NPCFlags.UNIT_NPC_FLAG_BATTLEMASTER);

        om.Setup(o => o.Player).Returns(player.Object);
        om.Setup(o => o.Units).Returns([alteracMaster.Object, arathiMaster.Object, warsongMaster.Object]);

        var task = new BattlegroundQueueTask(ctx.Object, BattlemasterData.BattlegroundType.WarsongGulch, 489);
        stack.Push(task);

        task.Update();

        Assert.Equal(warsongMaster.Object.Guid, GetPrivateField<ulong>(task, "_bmGuid"));
        Assert.Equal("MoveToBattlemaster", GetPrivateField<object>(task, "_state")?.ToString());
        Assert.Single(stack);
    }

    [Fact]
    public void Update_FallsBackToExpectedEntry_WhenNpcFlagsHaveNotArrivedYet()
    {
        var (ctx, om, stack) = AtomicTaskTestHelpers.CreateContext();
        var player = AtomicTaskTestHelpers.CreatePlayer(new Position(1981f, -4788f, 56f));
        player.Setup(p => p.MapId).Returns(1u);
        player.Setup(p => p.Race).Returns(Race.Orc);
        player.Setup(p => p.Gender).Returns(Gender.Female);

        var wrongFlaggedMaster = CreateBattlemaster(
            guid: 0xB1UL,
            entry: BattlemasterData.OrgrimmarAv.NpcEntry,
            name: BattlemasterData.OrgrimmarAv.NpcName,
            position: BattlemasterData.OrgrimmarAv.Position,
            npcFlags: NPCFlags.UNIT_NPC_FLAG_BATTLEMASTER);
        var expectedMasterWithoutFlags = CreateBattlemaster(
            guid: 0xB2UL,
            entry: BattlemasterData.OrgrimmarWsg.NpcEntry,
            name: BattlemasterData.OrgrimmarWsg.NpcName,
            position: BattlemasterData.OrgrimmarWsg.Position,
            npcFlags: NPCFlags.UNIT_NPC_FLAG_NONE);

        om.Setup(o => o.Player).Returns(player.Object);
        om.Setup(o => o.Units).Returns([wrongFlaggedMaster.Object, expectedMasterWithoutFlags.Object]);

        var task = new BattlegroundQueueTask(ctx.Object, BattlemasterData.BattlegroundType.WarsongGulch, 489);
        stack.Push(task);

        task.Update();

        Assert.Equal(expectedMasterWithoutFlags.Object.Guid, GetPrivateField<ulong>(task, "_bmGuid"));
        Assert.Equal("MoveToBattlemaster", GetPrivateField<object>(task, "_state")?.ToString());
        Assert.Single(stack);
    }

    [Fact]
    public void Update_GroupLeaderWithoutBgClient_SelectsBattlemasterGossipAndQueuesViaForegroundUi()
    {
        var (ctx, om, stack) = AtomicTaskTestHelpers.CreateContext();
        var player = AtomicTaskTestHelpers.CreatePlayer(new Position(1981f, -4788f, 56f));
        player.Setup(p => p.MapId).Returns(1u);
        player.Setup(p => p.Race).Returns(Race.Orc);
        player.Setup(p => p.Gender).Returns(Gender.Female);
        player.Setup(p => p.Guid).Returns(0xFEEDUL);

        var gossipFrame = new Mock<IGossipFrame>();
        gossipFrame.SetupGet(g => g.IsOpen).Returns(true);

        om.Setup(o => o.Player).Returns(player.Object);
        om.Setup(o => o.PartyLeaderGuid).Returns(0xFEEDUL);
        om.Setup(o => o.Party1Guid).Returns(0xBEEFUL);
        om.Setup(o => o.GossipFrame).Returns(gossipFrame.Object);
        om.Setup(o => o.InteractWithNpcAsync(0xA3UL, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var task = new BattlegroundQueueTask(ctx.Object, BattlemasterData.BattlegroundType.WarsongGulch, 489);
        SetPrivateField(task, "_bmGuid", 0xA3UL);
        SetPrivateField(task, "_state", Enum.Parse(GetPrivateField<object>(task, "_state")!.GetType(), "InteractAndQueue"));
        stack.Push(task);
        ResetSharedWaits(task);

        task.Update();

        om.Verify(o => o.InteractWithNpcAsync(0xA3UL, It.IsAny<CancellationToken>()), Times.Once);
        gossipFrame.Verify(g => g.SelectFirstGossipOfType(DialogType.battlemaster), Times.Once);
        om.Verify(o => o.JoinBattleGroundQueue(), Times.Once);
        Assert.Equal("WaitForInvite", GetPrivateField<object>(task, "_state")?.ToString());
    }

    [Fact]
    public void Update_GroupMemberWaitsForLeaderInsteadOfTalkingToBattlemaster()
    {
        var (ctx, om, stack) = AtomicTaskTestHelpers.CreateContext();
        var player = AtomicTaskTestHelpers.CreatePlayer(new Position(1981f, -4788f, 56f));
        player.Setup(p => p.MapId).Returns(1u);
        player.Setup(p => p.Race).Returns(Race.Orc);
        player.Setup(p => p.Gender).Returns(Gender.Female);
        player.Setup(p => p.Guid).Returns(0x2222UL);

        om.Setup(o => o.Player).Returns(player.Object);
        om.Setup(o => o.PartyLeaderGuid).Returns(0x1111UL);
        om.Setup(o => o.InteractWithNpcAsync(It.IsAny<ulong>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var task = new BattlegroundQueueTask(ctx.Object, BattlemasterData.BattlegroundType.WarsongGulch, 489);
        SetPrivateField(task, "_bmGuid", 0xA3UL);
        SetPrivateField(task, "_state", Enum.Parse(GetPrivateField<object>(task, "_state")!.GetType(), "InteractAndQueue"));
        stack.Push(task);
        ResetSharedWaits(task);

        task.Update();

        om.Verify(o => o.InteractWithNpcAsync(It.IsAny<ulong>(), It.IsAny<CancellationToken>()), Times.Never);
        om.Verify(o => o.JoinBattleGroundQueue(), Times.Never);
        Assert.Equal("WaitForInvite", GetPrivateField<object>(task, "_state")?.ToString());
    }

    [Fact]
    public void Update_AlteracValleyGroupMember_QueuesIndividuallyInsteadOfWaitingForLeader()
    {
        var (ctx, om, stack) = AtomicTaskTestHelpers.CreateContext();
        var player = AtomicTaskTestHelpers.CreatePlayer(new Position(1981f, -4788f, 56f));
        player.Setup(p => p.MapId).Returns(1u);
        player.Setup(p => p.Race).Returns(Race.Orc);
        player.Setup(p => p.Gender).Returns(Gender.Female);
        player.Setup(p => p.Guid).Returns(0x2222UL);

        om.Setup(o => o.Player).Returns(player.Object);
        om.Setup(o => o.PartyLeaderGuid).Returns(0x1111UL);
        om.Setup(o => o.InteractWithNpcAsync(0xA1UL, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var task = new BattlegroundQueueTask(ctx.Object, BattlemasterData.BattlegroundType.AlteracValley, 30);
        SetPrivateField(task, "_bmGuid", 0xA1UL);
        SetPrivateField(task, "_state", Enum.Parse(GetPrivateField<object>(task, "_state")!.GetType(), "InteractAndQueue"));
        stack.Push(task);
        ResetSharedWaits(task);

        task.Update();

        om.Verify(o => o.InteractWithNpcAsync(0xA1UL, It.IsAny<CancellationToken>()), Times.Once);
        om.Verify(o => o.JoinBattleGroundQueue(), Times.Once);
        Assert.Equal("WaitForInvite", GetPrivateField<object>(task, "_state")?.ToString());
    }

    [Theory]
    [InlineData(BattlemasterData.BattlegroundType.WarsongGulch, true)]
    [InlineData(BattlemasterData.BattlegroundType.AlteracValley, false)]
    public void ShouldQueueAsGroup_MatchesBattlegroundQueueMode(
        BattlemasterData.BattlegroundType battlegroundType,
        bool expected)
    {
        var (ctx, om, _) = AtomicTaskTestHelpers.CreateContext();
        var player = AtomicTaskTestHelpers.CreatePlayer(new Position(0f, 0f, 0f));
        player.Setup(p => p.Guid).Returns(0xFEEDUL);

        om.Setup(o => o.Player).Returns(player.Object);
        om.Setup(o => o.PartyLeaderGuid).Returns(0xFEEDUL);
        om.Setup(o => o.Party1Guid).Returns(0xBEEFUL);

        var expectedMapId = battlegroundType == BattlemasterData.BattlegroundType.AlteracValley ? 30u : 489u;
        var task = new BattlegroundQueueTask(ctx.Object, battlegroundType, expectedMapId);

        Assert.Equal(expected, InvokePrivateBool(task, "ShouldQueueAsGroup"));
    }

    [Theory]
    [InlineData(BattlemasterData.BattlegroundType.WarsongGulch, true)]
    [InlineData(BattlemasterData.BattlegroundType.AlteracValley, false)]
    public void ShouldWaitForLeaderGroupQueue_MatchesBattlegroundQueueMode(
        BattlemasterData.BattlegroundType battlegroundType,
        bool expected)
    {
        var (ctx, om, _) = AtomicTaskTestHelpers.CreateContext();
        var player = AtomicTaskTestHelpers.CreatePlayer(new Position(0f, 0f, 0f));
        player.Setup(p => p.Guid).Returns(0x2222UL);

        om.Setup(o => o.Player).Returns(player.Object);
        om.Setup(o => o.PartyLeaderGuid).Returns(0x1111UL);

        var expectedMapId = battlegroundType == BattlemasterData.BattlegroundType.AlteracValley ? 30u : 489u;
        var task = new BattlegroundQueueTask(ctx.Object, battlegroundType, expectedMapId);

        Assert.Equal(expected, InvokePrivateBool(task, "ShouldWaitForLeaderGroupQueue"));
    }

    [Fact]
    public void Update_ForegroundWaitForInvite_AttemptsUiAccept()
    {
        var (ctx, om, stack) = AtomicTaskTestHelpers.CreateContext();
        var player = AtomicTaskTestHelpers.CreatePlayer(new Position(1981f, -4788f, 56f));
        player.Setup(p => p.MapId).Returns(1u);

        om.Setup(o => o.Player).Returns(player.Object);

        var task = new BattlegroundQueueTask(ctx.Object, BattlemasterData.BattlegroundType.WarsongGulch, 489);
        SetPrivateField(task, "_state", Enum.Parse(GetPrivateField<object>(task, "_state")!.GetType(), "WaitForInvite"));
        stack.Push(task);
        ResetSharedWaits(task);

        task.Update();

        om.Verify(o => o.AcceptBattlegroundInvite(), Times.Once);
        Assert.Equal("WaitForInvite", GetPrivateField<object>(task, "_state")?.ToString());
    }

    [Fact]
    public void Update_WaitForInvitePastRetryThreshold_ReacquiresBattlemasterBeforeRequeue()
    {
        var (ctx, om, stack) = AtomicTaskTestHelpers.CreateContext();
        var player = AtomicTaskTestHelpers.CreatePlayer(new Position(1981f, -4788f, 56f));
        player.Setup(p => p.MapId).Returns(1u);

        om.Setup(o => o.Player).Returns(player.Object);

        var task = new BattlegroundQueueTask(ctx.Object, BattlemasterData.BattlegroundType.AlteracValley, 30);
        SetPrivateField(task, "_bmGuid", 0xDEADBEEFul);
        SetPrivateField(task, "_state", Enum.Parse(GetPrivateField<object>(task, "_state")!.GetType(), "WaitForInvite"));
        SetPrivateField(task, "_stateEnteredAt", DateTime.UtcNow - TimeSpan.FromSeconds(50));
        stack.Push(task);
        ResetSharedWaits(task);

        task.Update();

        Assert.Equal("FindBattlemaster", GetPrivateField<object>(task, "_state")?.ToString());
        Assert.Equal(1, GetPrivateField<int>(task, "_inviteRetryAttempts"));
        Assert.Equal(0UL, GetPrivateField<ulong>(task, "_bmGuid"));
    }

    [Fact]
    public void Update_WaitForInviteAtRetryCap_DoesNotRequeueAgain()
    {
        var (ctx, om, stack) = AtomicTaskTestHelpers.CreateContext();
        var player = AtomicTaskTestHelpers.CreatePlayer(new Position(1981f, -4788f, 56f));
        player.Setup(p => p.MapId).Returns(1u);

        om.Setup(o => o.Player).Returns(player.Object);

        var task = new BattlegroundQueueTask(ctx.Object, BattlemasterData.BattlegroundType.AlteracValley, 30);
        SetPrivateField(task, "_state", Enum.Parse(GetPrivateField<object>(task, "_state")!.GetType(), "WaitForInvite"));
        SetPrivateField(task, "_stateEnteredAt", DateTime.UtcNow - TimeSpan.FromSeconds(50));
        SetPrivateField(task, "_inviteRetryAttempts", 3);
        stack.Push(task);
        ResetSharedWaits(task);

        task.Update();

        Assert.Equal("WaitForInvite", GetPrivateField<object>(task, "_state")?.ToString());
        Assert.Equal(3, GetPrivateField<int>(task, "_inviteRetryAttempts"));
    }

    [Fact]
    public void Update_WaitForInviteRetry_WithLargeVerticalOffset_TriggersReapproachNavigation()
    {
        var pathRequests = 0;
        var (ctx, om, stack) = AtomicTaskTestHelpers.CreateContext(configurePathfinding: pathfinding =>
        {
            pathfinding
                .Setup(p => p.GetPath(It.IsAny<uint>(), It.IsAny<Position>(), It.IsAny<Position>(), It.IsAny<bool>()))
                .Callback(() => pathRequests++)
                .Returns((uint mapId, Position start, Position end, bool smoothPath) =>
                [
                    new Position(start.X, start.Y, start.Z),
                    new Position(end.X, end.Y, end.Z)
                ]);
        });
        var player = AtomicTaskTestHelpers.CreatePlayer(new Position(-8424.55f, 342.81f, 131.2f));
        player.Setup(p => p.MapId).Returns(0u);
        player.Setup(p => p.Race).Returns(Race.Human);
        player.Setup(p => p.Gender).Returns(Gender.Female);

        om.Setup(o => o.Player).Returns(player.Object);

        var task = new BattlegroundQueueTask(ctx.Object, BattlemasterData.BattlegroundType.AlteracValley, 30);
        SetPrivateField(task, "_state", Enum.Parse(GetPrivateField<object>(task, "_state")!.GetType(), "WaitForInvite"));
        SetPrivateField(task, "_stateEnteredAt", DateTime.UtcNow - TimeSpan.FromSeconds(50));
        stack.Push(task);
        ResetSharedWaits(task);

        task.Update();

        Assert.Equal("FindBattlemaster", GetPrivateField<object>(task, "_state")?.ToString());
        Assert.Equal(1, GetPrivateField<int>(task, "_inviteRetryAttempts"));
        Assert.True(pathRequests > 0, "Expected a re-approach navigation path request when bot is vertically offset near battlemaster.");
    }

    [Fact]
    public void Update_WaitForInviteRetry_WithoutVerticalOffset_DoesNotTriggerReapproachNavigation()
    {
        var pathRequests = 0;
        var (ctx, om, stack) = AtomicTaskTestHelpers.CreateContext(configurePathfinding: pathfinding =>
        {
            pathfinding
                .Setup(p => p.GetPath(It.IsAny<uint>(), It.IsAny<Position>(), It.IsAny<Position>(), It.IsAny<bool>()))
                .Callback(() => pathRequests++)
                .Returns((uint mapId, Position start, Position end, bool smoothPath) =>
                [
                    new Position(start.X, start.Y, start.Z),
                    new Position(end.X, end.Y, end.Z)
                ]);
        });
        var player = AtomicTaskTestHelpers.CreatePlayer(new Position(-8424.55f, 342.81f, 121.1f));
        player.Setup(p => p.MapId).Returns(0u);
        player.Setup(p => p.Race).Returns(Race.Human);
        player.Setup(p => p.Gender).Returns(Gender.Female);

        om.Setup(o => o.Player).Returns(player.Object);

        var task = new BattlegroundQueueTask(ctx.Object, BattlemasterData.BattlegroundType.AlteracValley, 30);
        SetPrivateField(task, "_state", Enum.Parse(GetPrivateField<object>(task, "_state")!.GetType(), "WaitForInvite"));
        SetPrivateField(task, "_stateEnteredAt", DateTime.UtcNow - TimeSpan.FromSeconds(50));
        stack.Push(task);
        ResetSharedWaits(task);

        task.Update();

        Assert.Equal("FindBattlemaster", GetPrivateField<object>(task, "_state")?.ToString());
        Assert.Equal(1, GetPrivateField<int>(task, "_inviteRetryAttempts"));
        Assert.Equal(0, pathRequests);
    }

    [Fact]
    public void Update_WaitForInvitePastInviteTimeout_RequeuesBeforePop()
    {
        var (ctx, om, stack) = AtomicTaskTestHelpers.CreateContext();
        var player = AtomicTaskTestHelpers.CreatePlayer(new Position(1981f, -4788f, 56f));
        player.Setup(p => p.MapId).Returns(1u);

        om.Setup(o => o.Player).Returns(player.Object);

        var task = new BattlegroundQueueTask(ctx.Object, BattlemasterData.BattlegroundType.AlteracValley, 30);
        SetPrivateField(task, "_bmGuid", 0xDEADBEEFul);
        SetPrivateField(task, "_state", Enum.Parse(GetPrivateField<object>(task, "_state")!.GetType(), "WaitForInvite"));
        SetPrivateField(task, "_stateEnteredAt", DateTime.UtcNow - TimeSpan.FromSeconds(301));
        stack.Push(task);
        ResetSharedWaits(task);

        task.Update();

        Assert.Equal("FindBattlemaster", GetPrivateField<object>(task, "_state")?.ToString());
        Assert.Equal(1, GetPrivateField<int>(task, "_inviteTimeoutRequeues"));
        Assert.Equal(0UL, GetPrivateField<ulong>(task, "_bmGuid"));
        Assert.Single(stack);
    }

    [Fact]
    public void Update_WaitForInvitePastInviteTimeoutAtRequeueCap_PopsTask()
    {
        var (ctx, om, stack) = AtomicTaskTestHelpers.CreateContext();
        var player = AtomicTaskTestHelpers.CreatePlayer(new Position(1981f, -4788f, 56f));
        player.Setup(p => p.MapId).Returns(1u);

        om.Setup(o => o.Player).Returns(player.Object);

        var task = new BattlegroundQueueTask(ctx.Object, BattlemasterData.BattlegroundType.AlteracValley, 30);
        SetPrivateField(task, "_state", Enum.Parse(GetPrivateField<object>(task, "_state")!.GetType(), "WaitForInvite"));
        SetPrivateField(task, "_stateEnteredAt", DateTime.UtcNow - TimeSpan.FromSeconds(301));
        SetPrivateField(task, "_inviteTimeoutRequeues", 2);
        stack.Push(task);
        ResetSharedWaits(task);

        task.Update();

        Assert.Empty(stack);
    }

    private static Mock<IWoWUnit> CreateBattlemaster(
        ulong guid,
        uint entry,
        string name,
        Position position,
        NPCFlags npcFlags)
    {
        var unit = new Mock<IWoWUnit>();
        unit.Setup(u => u.Guid).Returns(guid);
        unit.Setup(u => u.Entry).Returns(entry);
        unit.Setup(u => u.Name).Returns(name);
        unit.Setup(u => u.Position).Returns(position);
        unit.Setup(u => u.Health).Returns(100u);
        unit.Setup(u => u.NpcFlags).Returns(npcFlags);
        return unit;
    }

    private static T GetPrivateField<T>(object instance, string fieldName)
    {
        var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(instance.GetType().FullName, fieldName);
        return (T)field.GetValue(instance)!;
    }

    private static void SetPrivateField(object instance, string fieldName, object value)
    {
        var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(instance.GetType().FullName, fieldName);
        field.SetValue(instance, value);
    }

    private static bool InvokePrivateBool(object instance, string methodName)
    {
        var method = instance.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(instance.GetType().FullName, methodName);
        return (bool)(method.Invoke(instance, null) ?? false);
    }

    private static void ResetSharedWaits(BotTask instance)
    {
        var waitProperty = typeof(BotTask).GetProperty("Wait", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMemberException(typeof(BotTask).FullName, "Wait");
        var wait = waitProperty.GetValue(instance) ?? throw new InvalidOperationException("BotTask.Wait was null.");
        wait.GetType().GetMethod("RemoveAll", BindingFlags.Instance | BindingFlags.Public)
            ?.Invoke(wait, null);
    }
}
