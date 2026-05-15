using GameData.Core.Enums;
using Moq;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WoWSharpClient.Frames;
using WoWSharpClient.Networking.ClientComponents.I;
using WoWSharpClient.Networking.ClientComponents.Models;
using Xunit;

namespace WoWSharpClient.Tests.Frames;

/// <summary>
/// Unit coverage for the S1.19 BG TrainerFrame / TalentFrame / GossipFrame
/// implementations. Every test stubs the matching network agent and asserts
/// the IXxxFrame contract routes through the packet path. The wire-up half;
/// LiveValidation TrainerParity / TalentParity / GossipParity tests would
/// close the end-to-end half.
/// </summary>
public class NetworkTrainerFrameTests
{
    private static NetworkTrainerFrame WithAgent(ITrainerNetworkClientComponent? agent)
        => new(() => agent);

    private static Mock<ITrainerNetworkClientComponent> MockOpenTrainer(ulong guid = 0x100, int serviceCount = 3)
    {
        var mock = new Mock<ITrainerNetworkClientComponent>();
        mock.SetupGet(t => t.IsTrainerWindowOpen).Returns(true);
        mock.SetupGet(t => t.CurrentTrainerGuid).Returns(guid);
        var services = Enumerable.Range(0, serviceCount)
            .Select(i => new TrainerServiceData { ServiceIndex = (uint)i, SpellId = (uint)(1000 + i), Cost = 100 })
            .ToArray();
        mock.Setup(t => t.GetAvailableServices()).Returns(services);
        return mock;
    }

    [Fact]
    public void IsOpen_AgentNull_ReturnsFalse()
        => Assert.False(WithAgent(null).IsOpen);

    [Fact]
    public void IsOpen_AgentOpen_ReturnsTrue()
        => Assert.True(WithAgent(MockOpenTrainer().Object).IsOpen);

    [Fact]
    public void Spells_VendorOpen_ReturnsServiceCountPlaceholders()
    {
        var frame = WithAgent(MockOpenTrainer(serviceCount: 5).Object);
        Assert.Equal(5, frame.Spells.Count());
        // Every placeholder has Cost==0 so the dispatcher's "Has Enough Gold"
        // gate proceeds. Server-side CMSG_TRAINER_BUY_SPELL is the cost authority.
        Assert.All(frame.Spells, item => Assert.Equal(0, item.Cost));
    }

    [Fact]
    public void Spells_AgentNull_ReturnsEmpty()
        => Assert.Empty(WithAgent(null).Spells);

    [Fact]
    public void TrainSpell_RoutesToLearnSpellByIndexAsync()
    {
        var mock = MockOpenTrainer(guid: 0xABCD);
        mock.Setup(t => t.LearnSpellByIndexAsync(0xABCDu, 2u, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask).Verifiable();

        WithAgent(mock.Object).TrainSpell(2);

        mock.Verify(t => t.LearnSpellByIndexAsync(0xABCDu, 2u, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void TrainSpell_AgentNull_DoesNotThrow()
        => WithAgent(null).TrainSpell(0);

    [Fact]
    public void TrainSpell_NoTrainerGuid_NoOp()
    {
        var mock = new Mock<ITrainerNetworkClientComponent>();
        mock.SetupGet(t => t.CurrentTrainerGuid).Returns((ulong?)null);

        WithAgent(mock.Object).TrainSpell(0);

        mock.Verify(t => t.LearnSpellByIndexAsync(It.IsAny<ulong>(), It.IsAny<uint>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void Close_AgentOpen_CallsCloseTrainerAsync()
    {
        var mock = MockOpenTrainer();
        mock.Setup(t => t.CloseTrainerAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask).Verifiable();

        WithAgent(mock.Object).Close();

        mock.Verify(t => t.CloseTrainerAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Close_AgentClosed_NoOp()
    {
        var mock = new Mock<ITrainerNetworkClientComponent>();
        mock.SetupGet(t => t.IsTrainerWindowOpen).Returns(false);

        WithAgent(mock.Object).Close();

        mock.Verify(t => t.CloseTrainerAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void Update_DoesNotThrow()
        => WithAgent(null).Update();
}

public class NetworkTalentFrameTests
{
    private static NetworkTalentFrame WithAgent(ITalentNetworkClientComponent? agent)
        => new(() => agent);

    [Fact]
    public void IsOpen_AgentNull_ReturnsFalse()
        => Assert.False(WithAgent(null).IsOpen);

    [Fact]
    public void IsOpen_AgentPresent_ReturnsTrue()
    {
        var mock = new Mock<ITalentNetworkClientComponent>();
        Assert.True(WithAgent(mock.Object).IsOpen);
    }

    [Fact]
    public void TalentPointsAvailable_RoutesToAgent()
    {
        var mock = new Mock<ITalentNetworkClientComponent>();
        mock.SetupGet(t => t.AvailableTalentPoints).Returns(7u);
        Assert.Equal(7, WithAgent(mock.Object).TalentPointsAvailable);
    }

    [Fact]
    public void TalentPointsSpent_RoutesToAgent()
    {
        var mock = new Mock<ITalentNetworkClientComponent>();
        mock.SetupGet(t => t.TotalTalentPointsSpent).Returns(13u);
        Assert.Equal(13, WithAgent(mock.Object).TalentPointsSpent);
    }

    [Fact]
    public void TalentPointsAll_SumsAvailableAndSpent()
    {
        var mock = new Mock<ITalentNetworkClientComponent>();
        mock.SetupGet(t => t.AvailableTalentPoints).Returns(7u);
        mock.SetupGet(t => t.TotalTalentPointsSpent).Returns(13u);
        Assert.Equal(20, WithAgent(mock.Object).TalentPointsAll);
    }

    [Fact]
    public void TalentPoints_AgentNull_AllZero()
    {
        var frame = WithAgent(null);
        Assert.Equal(0, frame.TalentPointsAvailable);
        Assert.Equal(0, frame.TalentPointsSpent);
        Assert.Equal(0, frame.TalentPointsAll);
    }

    [Fact]
    public void Tabs_ReturnsEmpty()
        => Assert.Empty(WithAgent(null).Tabs);

    [Fact]
    public void LearnTalent_RoutesToLearnTalentAsync()
    {
        var mock = new Mock<ITalentNetworkClientComponent>();
        mock.Setup(t => t.LearnTalentAsync(8027u, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask).Verifiable();

        WithAgent(mock.Object).LearnTalent(8027);

        mock.Verify(t => t.LearnTalentAsync(8027u, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void LearnTalent_AgentNull_DoesNotThrow()
        => WithAgent(null).LearnTalent(1);

    [Fact]
    public void Close_RoutesToCloseTalentWindowAsync()
    {
        var mock = new Mock<ITalentNetworkClientComponent>();
        mock.Setup(t => t.CloseTalentWindowAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask).Verifiable();

        WithAgent(mock.Object).Close();

        mock.Verify(t => t.CloseTalentWindowAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}

public class NetworkGossipFrameTests
{
    private static NetworkGossipFrame WithAgent(IGossipNetworkClientComponent? agent)
        => new(() => agent);

    [Fact]
    public void IsOpen_AgentNull_ReturnsFalse()
        => Assert.False(WithAgent(null).IsOpen);

    [Fact]
    public void IsOpen_AgentOpen_ReturnsTrue()
    {
        var mock = new Mock<IGossipNetworkClientComponent>();
        mock.SetupGet(g => g.IsGossipWindowOpen).Returns(true);
        Assert.True(WithAgent(mock.Object).IsOpen);
    }

    [Fact]
    public void NPCGuid_AgentNull_ReturnsZero()
        => Assert.Equal(0UL, WithAgent(null).NPCGuid);

    [Fact]
    public void NPCGuid_AgentReturnsGuid_PassesThrough()
    {
        var mock = new Mock<IGossipNetworkClientComponent>();
        mock.SetupGet(g => g.CurrentNpcGuid).Returns(0xCAFE_BEEFUL);
        Assert.Equal(0xCAFE_BEEFUL, WithAgent(mock.Object).NPCGuid);
    }

    [Fact]
    public void SelectGossipOption_RoutesToSelectGossipOptionAsync()
    {
        var mock = new Mock<IGossipNetworkClientComponent>();
        mock.Setup(g => g.SelectGossipOptionAsync(3u, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask).Verifiable();

        WithAgent(mock.Object).SelectGossipOption(3);

        mock.Verify(g => g.SelectGossipOptionAsync(3u, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void SelectGossipOption_AgentNull_DoesNotThrow()
        => WithAgent(null).SelectGossipOption(0);

    [Fact]
    public void Close_AgentOpen_CallsCloseGossipAsync()
    {
        var mock = new Mock<IGossipNetworkClientComponent>();
        mock.SetupGet(g => g.IsGossipWindowOpen).Returns(true);
        mock.Setup(g => g.CloseGossipAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask).Verifiable();

        WithAgent(mock.Object).Close();

        mock.Verify(g => g.CloseGossipAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Options_NoMenu_ReturnsEmpty()
    {
        var mock = new Mock<IGossipNetworkClientComponent>();
        mock.Setup(g => g.GetCurrentGossipMenu()).Returns((GossipMenuData?)null);
        Assert.Empty(WithAgent(mock.Object).Options);
    }

    [Fact]
    public void Options_LiveMenu_ReflectsServerOptions()
    {
        var mock = new Mock<IGossipNetworkClientComponent>();
        var menu = new GossipMenuData(npcGuid: 0x100, menuId: 1, textId: 2)
        {
            Options = new List<GossipOptionData>
            {
                new(0, "Train me!", GossipTypes.Trainer),
                new(1, "I want to buy.", GossipTypes.Vendor),
            },
        };
        mock.Setup(g => g.GetCurrentGossipMenu()).Returns(menu);

        var options = WithAgent(mock.Object).Options;

        Assert.Equal(2, options.Count);
        Assert.Equal(GossipTypes.Trainer, options[0].Type);
        Assert.Equal("Train me!", options[0].Text);
        Assert.Equal(GossipTypes.Vendor, options[1].Type);
    }

    [Fact]
    public void SelectFirstGossipOfType_FindsMatchingOptionAndSelects()
    {
        var mock = new Mock<IGossipNetworkClientComponent>();
        var menu = new GossipMenuData(npcGuid: 0x100, menuId: 1, textId: 2)
        {
            Options = new List<GossipOptionData>
            {
                new(0, "Greetings.", GossipTypes.Gossip),
                new(1, "Train me!", GossipTypes.Trainer),
            },
        };
        mock.Setup(g => g.GetCurrentGossipMenu()).Returns(menu);
        mock.Setup(g => g.SelectGossipOptionAsync(1u, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask).Verifiable();

        WithAgent(mock.Object).SelectFirstGossipOfType(DialogType.trainer);

        mock.Verify(g => g.SelectGossipOptionAsync(1u, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void SelectFirstGossipOfType_NoMatch_NoOp()
    {
        var mock = new Mock<IGossipNetworkClientComponent>();
        var menu = new GossipMenuData(npcGuid: 0x100, menuId: 1, textId: 2)
        {
            Options = new List<GossipOptionData> { new(0, "Greetings.", GossipTypes.Gossip) },
        };
        mock.Setup(g => g.GetCurrentGossipMenu()).Returns(menu);

        WithAgent(mock.Object).SelectFirstGossipOfType(DialogType.vendor);

        mock.Verify(g => g.SelectGossipOptionAsync(It.IsAny<uint>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void Quests_ReturnsEmpty()
        => Assert.Empty(WithAgent(null).Quests);
}
