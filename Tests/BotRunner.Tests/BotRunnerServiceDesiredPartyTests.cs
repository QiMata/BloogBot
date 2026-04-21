using BotRunner.Clients;
using BotRunner.Interfaces;
using Communication;
using GameData.Core.Enums;
using GameData.Core.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Reflection;
using WoWSharpClient.Models;
using WoWSharpClient.Networking.ClientComponents.I;
using Xas.FluentBehaviourTree;

namespace BotRunner.Tests;

public class BotRunnerServiceDesiredPartyTests
{
    [Fact]
    public void TryBuildDesiredPartyBehaviorTree_LeaderInvitesMissingMember()
    {
        var service = CreateService(out var objectManager);
        var player = new WoWLocalPlayer(new GameData.Core.Models.HighGuid(0x111UL)) { MapId = 1 };
        objectManager.SetupGet(o => o.Player).Returns(player);
        objectManager.SetupGet(o => o.PartyLeaderGuid).Returns(0UL);

        var snapshot = new WoWActivitySnapshot
        {
            CharacterName = "Leader",
            DesiredPartyLeaderName = "Leader",
        };
        snapshot.DesiredPartyMembers.Add("Follower");

        Assert.True(InvokeTryBuildDesiredPartyBehaviorTree(service, snapshot));
        Assert.Equal(BehaviourTreeStatus.Success, ReadBehaviorTree(service).Tick(new TimeData(0.1f)));
        objectManager.Verify(o => o.InviteByName("Follower"), Times.Once);
    }

    [Fact]
    public void TryBuildDesiredPartyBehaviorTree_LeaderFallsBackToGeneratedCharacterName_WhenSnapshotCharacterNameMissing()
    {
        var service = CreateService(out var objectManager, accountName: "ORWRBOT1");
        var player = new WoWLocalPlayer(new GameData.Core.Models.HighGuid(0x155UL)) { MapId = 1 };
        objectManager.SetupGet(o => o.Player).Returns(player);
        objectManager.SetupGet(o => o.PartyLeaderGuid).Returns(0UL);

        var snapshot = new WoWActivitySnapshot
        {
            DesiredPartyLeaderName = WoWNameGenerator.GenerateName(Race.Orc, Gender.Female, "ORWRBOT1"),
        };
        snapshot.DesiredPartyMembers.Add("Follower");

        Assert.True(InvokeTryBuildDesiredPartyBehaviorTree(service, snapshot));
        Assert.Equal(BehaviourTreeStatus.Success, ReadBehaviorTree(service).Tick(new TimeData(0.1f)));
        objectManager.Verify(o => o.InviteByName("Follower"), Times.Once);
    }

    [Fact]
    public void TryBuildDesiredPartyBehaviorTree_FollowerAcceptsPendingInvite()
    {
        var service = CreateService(out var objectManager);
        var player = new WoWLocalPlayer(new GameData.Core.Models.HighGuid(0x222UL)) { MapId = 1 };
        objectManager.SetupGet(o => o.Player).Returns(player);
        objectManager.Setup(o => o.HasPendingGroupInvite()).Returns(true);

        var snapshot = new WoWActivitySnapshot
        {
            CharacterName = "Follower",
            DesiredPartyLeaderName = "Leader",
        };

        Assert.True(InvokeTryBuildDesiredPartyBehaviorTree(service, snapshot));
        Assert.Equal(BehaviourTreeStatus.Success, ReadBehaviorTree(service).Tick(new TimeData(0.1f)));
        objectManager.Verify(o => o.AcceptGroupInvite(), Times.Once);
    }

    [Fact]
    public void TryBuildDesiredPartyBehaviorTree_FollowerFallsBackToGeneratedCharacterName_WhenSnapshotCharacterNameMissing()
    {
        var service = CreateService(out var objectManager, accountName: "UDPRBOT2");
        var player = new WoWLocalPlayer(new GameData.Core.Models.HighGuid(0x255UL)) { MapId = 1 };
        objectManager.SetupGet(o => o.Player).Returns(player);
        objectManager.Setup(o => o.HasPendingGroupInvite()).Returns(true);

        var snapshot = new WoWActivitySnapshot
        {
            DesiredPartyLeaderName = WoWNameGenerator.GenerateName(Race.Orc, Gender.Female, "ORWRBOT1"),
        };

        Assert.True(InvokeTryBuildDesiredPartyBehaviorTree(service, snapshot));
        Assert.Equal(BehaviourTreeStatus.Success, ReadBehaviorTree(service).Tick(new TimeData(0.1f)));
        objectManager.Verify(o => o.AcceptGroupInvite(), Times.Once);
    }

    [Fact]
    public void TryBuildDesiredPartyBehaviorTree_LeaderLeavesWrongExistingGroup()
    {
        var partyAgent = new Mock<IPartyNetworkClientComponent>(MockBehavior.Loose);
        partyAgent.SetupGet(agent => agent.IsInGroup).Returns(true);
        partyAgent.SetupGet(agent => agent.LeaderGuid).Returns(0xCAUL);
        partyAgent.Setup(agent => agent.GetGroupMembers()).Returns([
            new GroupMember { Name = "WrongLeader", Guid = 0xCAUL, IsLeader = true }
        ]);

        var service = CreateService(out var objectManager, partyAgent: partyAgent);
        var player = new WoWLocalPlayer(new GameData.Core.Models.HighGuid(0x333UL)) { MapId = 1 };
        objectManager.SetupGet(o => o.Player).Returns(player);

        var snapshot = new WoWActivitySnapshot
        {
            CharacterName = "Leader",
            DesiredPartyLeaderName = "Leader",
        };
        snapshot.DesiredPartyMembers.Add("Follower");

        Assert.True(InvokeTryBuildDesiredPartyBehaviorTree(service, snapshot));
        Assert.Equal(BehaviourTreeStatus.Success, ReadBehaviorTree(service).Tick(new TimeData(0.1f)));
        partyAgent.Verify(agent => agent.LeaveGroupAsync(default), Times.Once);
        objectManager.Verify(o => o.InviteByName(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void TryBuildDesiredPartyBehaviorTree_FollowerLeavesWrongExistingGroup()
    {
        var partyAgent = new Mock<IPartyNetworkClientComponent>(MockBehavior.Loose);
        partyAgent.SetupGet(agent => agent.IsInGroup).Returns(true);
        partyAgent.SetupGet(agent => agent.LeaderGuid).Returns(0xCAUL);
        partyAgent.Setup(agent => agent.GetGroupMembers()).Returns([
            new GroupMember { Name = "WrongLeader", Guid = 0xCAUL, IsLeader = true }
        ]);

        var service = CreateService(out var objectManager, partyAgent: partyAgent);
        var player = new WoWLocalPlayer(new GameData.Core.Models.HighGuid(0x444UL)) { MapId = 1 };
        objectManager.SetupGet(o => o.Player).Returns(player);

        var snapshot = new WoWActivitySnapshot
        {
            CharacterName = "Follower",
            DesiredPartyLeaderName = "Leader",
        };

        Assert.True(InvokeTryBuildDesiredPartyBehaviorTree(service, snapshot));
        Assert.Equal(BehaviourTreeStatus.Success, ReadBehaviorTree(service).Tick(new TimeData(0.1f)));
        partyAgent.Verify(agent => agent.LeaveGroupAsync(default), Times.Once);
        objectManager.Verify(o => o.AcceptGroupInvite(), Times.Never);
    }

    [Fact]
    public void TryBuildDesiredPartyBehaviorTree_FollowerDisbandsUnexpectedSelfLedGroup()
    {
        var partyAgent = new Mock<IPartyNetworkClientComponent>(MockBehavior.Loose);
        partyAgent.SetupGet(agent => agent.IsInGroup).Returns(true);
        partyAgent.SetupGet(agent => agent.IsGroupLeader).Returns(true);
        partyAgent.SetupGet(agent => agent.LeaderGuid).Returns(0UL);
        partyAgent.SetupGet(agent => agent.GroupSize).Returns(1U);
        partyAgent.Setup(agent => agent.GetGroupMembers()).Returns([
            new GroupMember { Name = "Follower2", Guid = 0x555UL }
        ]);

        var service = CreateService(out var objectManager, partyAgent: partyAgent);
        var player = new WoWLocalPlayer(new GameData.Core.Models.HighGuid(0x555UL)) { MapId = 1 };
        objectManager.SetupGet(o => o.Player).Returns(player);

        var snapshot = new WoWActivitySnapshot
        {
            CharacterName = "Follower",
            DesiredPartyLeaderName = "Leader",
        };

        Assert.True(InvokeTryBuildDesiredPartyBehaviorTree(service, snapshot));
        Assert.Equal(BehaviourTreeStatus.Success, ReadBehaviorTree(service).Tick(new TimeData(0.1f)));
        partyAgent.Verify(agent => agent.DisbandGroupAsync(default), Times.Once);
        objectManager.Verify(o => o.AcceptGroupInvite(), Times.Never);
    }

    [Fact]
    public void TryBuildDesiredPartyBehaviorTree_LeaderConvertsToRaidWhenPartyIsFull()
    {
        var service = CreateService(out var objectManager);
        var player = new WoWLocalPlayer(new GameData.Core.Models.HighGuid(0x333UL)) { MapId = 1 };
        objectManager.SetupGet(o => o.Player).Returns(player);
        objectManager.SetupGet(o => o.PartyLeaderGuid).Returns(player.Guid);
        objectManager.SetupGet(o => o.Party1Guid).Returns(0x1UL);
        objectManager.SetupGet(o => o.Party2Guid).Returns(0x2UL);
        objectManager.SetupGet(o => o.Party3Guid).Returns(0x3UL);
        objectManager.SetupGet(o => o.Party4Guid).Returns(0x4UL);

        var snapshot = new WoWActivitySnapshot
        {
            CharacterName = "Leader",
            DesiredPartyLeaderName = "Leader",
            DesiredPartyIsRaid = true,
        };
        snapshot.DesiredPartyMembers.Add("RaidMember");

        Assert.True(InvokeTryBuildDesiredPartyBehaviorTree(service, snapshot));
        Assert.Equal(BehaviourTreeStatus.Success, ReadBehaviorTree(service).Tick(new TimeData(0.1f)));
        objectManager.Verify(o => o.ConvertToRaid(), Times.Once);
    }

    [Fact]
    public void TryBuildDesiredPartyBehaviorTree_LeaderConvertsToRaidWhenPartyAgentReportsFourOtherMembers()
    {
        var partyAgent = new Mock<IPartyNetworkClientComponent>(MockBehavior.Loose);
        partyAgent.SetupGet(agent => agent.IsInGroup).Returns(true);
        partyAgent.SetupGet(agent => agent.IsInRaid).Returns(false);
        partyAgent.SetupGet(agent => agent.IsGroupLeader).Returns(true);
        partyAgent.SetupGet(agent => agent.LeaderGuid).Returns(0x333UL);
        partyAgent.SetupGet(agent => agent.GroupSize).Returns(4U);
        partyAgent.Setup(agent => agent.GetGroupMembers()).Returns([
            new GroupMember { Name = "Follower1", Guid = 0x1UL },
            new GroupMember { Name = "Follower2", Guid = 0x2UL },
            new GroupMember { Name = "Follower3", Guid = 0x3UL },
            new GroupMember { Name = "Follower4", Guid = 0x4UL },
        ]);

        var service = CreateService(out var objectManager, partyAgent: partyAgent);
        var player = new WoWLocalPlayer(new GameData.Core.Models.HighGuid(0x333UL)) { MapId = 1 };
        objectManager.SetupGet(o => o.Player).Returns(player);

        var snapshot = new WoWActivitySnapshot
        {
            CharacterName = "Leader",
            DesiredPartyLeaderName = "Leader",
            DesiredPartyIsRaid = true,
        };
        snapshot.DesiredPartyMembers.Add("RaidMember");

        Assert.True(InvokeTryBuildDesiredPartyBehaviorTree(service, snapshot));
        Assert.Equal(BehaviourTreeStatus.Success, ReadBehaviorTree(service).Tick(new TimeData(0.1f)));
        objectManager.Verify(o => o.ConvertToRaid(), Times.Once);
        partyAgent.Verify(agent => agent.ConvertToRaidAsync(default), Times.Never);
    }

    [Fact]
    public void TryBuildDesiredPartyBehaviorTree_LeaderInvitesAfterRaidUpgradeAssumption()
    {
        var service = CreateService(out var objectManager);
        var player = new WoWLocalPlayer(new GameData.Core.Models.HighGuid(0x444UL)) { MapId = 1 };
        objectManager.SetupGet(o => o.Player).Returns(player);
        objectManager.SetupGet(o => o.PartyLeaderGuid).Returns(player.Guid);
        objectManager.SetupGet(o => o.Party1Guid).Returns(0x1UL);
        objectManager.SetupGet(o => o.Party2Guid).Returns(0x2UL);
        objectManager.SetupGet(o => o.Party3Guid).Returns(0x3UL);
        objectManager.SetupGet(o => o.Party4Guid).Returns(0x4UL);

        var snapshot = new WoWActivitySnapshot
        {
            CharacterName = "Leader",
            DesiredPartyLeaderName = "Leader",
            DesiredPartyIsRaid = true,
        };
        snapshot.DesiredPartyMembers.Add("RaidMember");

        Assert.True(InvokeTryBuildDesiredPartyBehaviorTree(service, snapshot));
        Assert.Equal(BehaviourTreeStatus.Success, ReadBehaviorTree(service).Tick(new TimeData(0.1f)));

        Assert.True(InvokeTryBuildDesiredPartyBehaviorTree(service, snapshot));
        Assert.Equal(BehaviourTreeStatus.Success, ReadBehaviorTree(service).Tick(new TimeData(0.1f)));

        objectManager.Verify(o => o.ConvertToRaid(), Times.Once);
        objectManager.Verify(o => o.InviteByName("RaidMember"), Times.Once);
    }

    private static BotRunnerService CreateService(
        out Mock<IObjectManager> objectManager,
        string? accountName = null,
        Mock<IPartyNetworkClientComponent>? partyAgent = null)
    {
        objectManager = new Mock<IObjectManager>(MockBehavior.Loose);
        objectManager.SetupGet(o => o.EventHandler).Returns(new Mock<IWoWEventHandler>(MockBehavior.Loose).Object);
        objectManager.SetupGet(o => o.Objects).Returns(System.Array.Empty<IWoWObject>());
        objectManager.SetupGet(o => o.Units).Returns(System.Array.Empty<IWoWUnit>());
        objectManager.Setup(o => o.GetContainedItems()).Returns(System.Array.Empty<IWoWItem>());

        var dependencies = new Mock<IDependencyContainer>(MockBehavior.Loose);
        var updateClient = new CharacterStateUpdateClient(NullLogger.Instance);
        Func<IAgentFactory?>? agentFactoryAccessor = null;
        if (partyAgent != null)
        {
            var agentFactory = new Mock<IAgentFactory>(MockBehavior.Loose);
            agentFactory.SetupGet(factory => factory.PartyAgent).Returns(partyAgent.Object);
            agentFactoryAccessor = () => agentFactory.Object;
        }

        return new BotRunnerService(
            objectManager.Object,
            updateClient,
            dependencies.Object,
            agentFactoryAccessor,
            accountName: accountName);
    }

    private static bool InvokeTryBuildDesiredPartyBehaviorTree(BotRunnerService service, WoWActivitySnapshot snapshot)
    {
        var method = typeof(BotRunnerService).GetMethod("TryBuildDesiredPartyBehaviorTree", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return Assert.IsType<bool>(method!.Invoke(service, [snapshot]));
    }

    private static IBehaviourTreeNode ReadBehaviorTree(BotRunnerService service)
    {
        var field = typeof(BotRunnerService).GetField("_behaviorTree", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsAssignableFrom<IBehaviourTreeNode>(field!.GetValue(service));
    }
}
