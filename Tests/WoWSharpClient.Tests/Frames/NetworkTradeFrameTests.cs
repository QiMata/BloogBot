using Moq;
using System.Threading;
using System.Threading.Tasks;
using WoWSharpClient.Frames;
using WoWSharpClient.Networking.ClientComponents.I;
using Xunit;

namespace WoWSharpClient.Tests.Frames;

/// <summary>
/// Unit coverage for the S1.15 BG TradeFrame implementation. Every test
/// stubs ITradeNetworkClientComponent and asserts the packet routing
/// without touching any real socket. Closes the wire-up half of S1.15;
/// LiveValidation TradeParityTests close the end-to-end half.
/// </summary>
public class NetworkTradeFrameTests
{
    private static NetworkTradeFrame WithAgent(ITradeNetworkClientComponent? agent)
        => new(() => agent);

    [Fact]
    public void IsOpen_AgentNull_ReturnsFalse()
    {
        var frame = WithAgent(null);
        Assert.False(frame.IsOpen);
    }

    [Fact]
    public void IsOpen_AgentOpen_ReturnsTrue()
    {
        var mock = new Mock<ITradeNetworkClientComponent>();
        mock.SetupGet(a => a.IsTradeOpen).Returns(true);
        Assert.True(WithAgent(mock.Object).IsOpen);
    }

    [Fact]
    public void IsOpen_AgentClosed_ReturnsFalse()
    {
        var mock = new Mock<ITradeNetworkClientComponent>();
        mock.SetupGet(a => a.IsTradeOpen).Returns(false);
        Assert.False(WithAgent(mock.Object).IsOpen);
    }

    [Fact]
    public void Close_AgentNull_DoesNotThrow()
    {
        WithAgent(null).Close();
    }

    [Fact]
    public void Close_AgentOpen_CallsCancelTradeAsync()
    {
        var mock = new Mock<ITradeNetworkClientComponent>();
        mock.SetupGet(a => a.IsTradeOpen).Returns(true);
        mock.Setup(a => a.CancelTradeAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask).Verifiable();

        WithAgent(mock.Object).Close();

        mock.Verify(a => a.CancelTradeAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Close_AgentClosed_DoesNotCallCancel()
    {
        var mock = new Mock<ITradeNetworkClientComponent>();
        mock.SetupGet(a => a.IsTradeOpen).Returns(false);

        WithAgent(mock.Object).Close();

        mock.Verify(a => a.CancelTradeAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void OfferMoney_PositiveCopper_RoutesToOfferMoneyAsync()
    {
        var mock = new Mock<ITradeNetworkClientComponent>();
        mock.Setup(a => a.OfferMoneyAsync(150u, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask).Verifiable();

        WithAgent(mock.Object).OfferMoney(150);

        mock.Verify(a => a.OfferMoneyAsync(150u, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void OfferMoney_NegativeCopper_ClampsToZero()
    {
        var mock = new Mock<ITradeNetworkClientComponent>();
        mock.Setup(a => a.OfferMoneyAsync(0u, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask).Verifiable();

        WithAgent(mock.Object).OfferMoney(-42);

        mock.Verify(a => a.OfferMoneyAsync(0u, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void OfferMoney_AgentNull_DoesNotThrow()
    {
        WithAgent(null).OfferMoney(50);
    }

    [Fact]
    public void OfferItem_Backpack_ConvertsBag0ToFFAndSlotPlus23()
    {
        var mock = new Mock<ITradeNetworkClientComponent>();
        // bagId=0 (backpack), slotId=3, tradeWindowSlot=1
        // expected packet: tradeSlot=1, packetBag=0xFF, packetSlot=23+3=26
        mock.Setup(a => a.OfferItemAsync(1, 0xFF, 26, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask).Verifiable();

        WithAgent(mock.Object).OfferItem(bagId: 0, slotId: 3, quantity: 1, tradeWindowSlot: 1);

        mock.Verify(a => a.OfferItemAsync(1, 0xFF, 26, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void OfferItem_EquippedContainer_ConvertsBagToBag18PlusN()
    {
        var mock = new Mock<ITradeNetworkClientComponent>();
        // bagId=2 (3rd equipped container), slotId=5, tradeWindowSlot=0
        // expected packet: tradeSlot=0, packetBag=18+2=20, packetSlot=5
        mock.Setup(a => a.OfferItemAsync(0, 20, 5, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask).Verifiable();

        WithAgent(mock.Object).OfferItem(bagId: 2, slotId: 5, quantity: 1, tradeWindowSlot: 0);

        mock.Verify(a => a.OfferItemAsync(0, 20, 5, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void OfferItem_AgentNull_DoesNotThrow()
    {
        WithAgent(null).OfferItem(0, 1, 1, 0);
    }

    [Fact]
    public void AcceptTrade_AgentPresent_RoutesToAcceptTradeAsync()
    {
        var mock = new Mock<ITradeNetworkClientComponent>();
        mock.Setup(a => a.AcceptTradeAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask).Verifiable();

        WithAgent(mock.Object).AcceptTrade();

        mock.Verify(a => a.AcceptTradeAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void AcceptTrade_AgentNull_DoesNotThrow()
    {
        WithAgent(null).AcceptTrade();
    }

    [Fact]
    public void DeclineTrade_AgentPresent_RoutesToCancelTradeAsync()
    {
        var mock = new Mock<ITradeNetworkClientComponent>();
        mock.Setup(a => a.CancelTradeAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask).Verifiable();

        WithAgent(mock.Object).DeclineTrade();

        mock.Verify(a => a.CancelTradeAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void DeclineTrade_AgentNull_DoesNotThrow()
    {
        WithAgent(null).DeclineTrade();
    }

    [Fact]
    public void OfferLockpick_NoOp_DoesNotThrow()
    {
        // Stub until SpellCastingAgent + trade-target wire lands. Must not
        // throw so InteractionSequenceBuilder's Rogue branch is safe on BG.
        WithAgent(null).OfferLockpick();
        var mock = new Mock<ITradeNetworkClientComponent>();
        WithAgent(mock.Object).OfferLockpick();
        mock.VerifyNoOtherCalls();
    }

    [Fact]
    public void OfferEnchant_NoOp_DoesNotThrow()
    {
        WithAgent(null).OfferEnchant(7426);
        var mock = new Mock<ITradeNetworkClientComponent>();
        WithAgent(mock.Object).OfferEnchant(7426);
        mock.VerifyNoOtherCalls();
    }

    [Fact]
    public void OfferedItems_ReturnsEmptyList()
    {
        Assert.Empty(WithAgent(null).OfferedItems);
    }

    [Fact]
    public void OtherPlayerItems_ReturnsEmptyList()
    {
        Assert.Empty(WithAgent(null).OtherPlayerItems);
    }
}
