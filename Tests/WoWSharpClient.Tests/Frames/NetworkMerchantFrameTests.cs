using GameData.Core.Enums;
using Moq;
using System.Threading;
using System.Threading.Tasks;
using WoWSharpClient.Frames;
using WoWSharpClient.Networking.ClientComponents.I;
using WoWSharpClient.Networking.ClientComponents.Models;
using Xunit;

namespace WoWSharpClient.Tests.Frames;

/// <summary>
/// Unit coverage for the S1.17 BG MerchantFrame implementation. Stubs
/// <see cref="IVendorNetworkClientComponent"/> and asserts the IMerchantFrame
/// contract routes through the vendor packet path. The wire-up half;
/// LiveValidation VendorParityTests would close the end-to-end half.
/// </summary>
public class NetworkMerchantFrameTests
{
    private static NetworkMerchantFrame WithAgent(IVendorNetworkClientComponent? agent)
        => new(() => agent);

    private static Mock<IVendorNetworkClientComponent> MockOpenVendor(
        ulong vendorGuid = 0xDEADBEEF,
        bool canRepair = true,
        uint repairCost = 0)
    {
        var mock = new Mock<IVendorNetworkClientComponent>();
        mock.SetupGet(v => v.IsVendorWindowOpen).Returns(true);
        mock.SetupGet(v => v.CurrentVendor).Returns(new VendorInfo
        {
            VendorGuid = vendorGuid,
            CanRepair = canRepair,
            IsWindowOpen = true,
        });
        mock.Setup(v => v.GetRepairCostAsync(vendorGuid, It.IsAny<CancellationToken>())).ReturnsAsync(repairCost);
        return mock;
    }

    [Fact]
    public void IsOpen_AgentNull_ReturnsFalse()
        => Assert.False(WithAgent(null).IsOpen);

    [Fact]
    public void IsOpen_AgentOpen_ReturnsTrue()
        => Assert.True(WithAgent(MockOpenVendor().Object).IsOpen);

    [Fact]
    public void Ready_MirrorsIsOpen()
        => Assert.True(WithAgent(MockOpenVendor().Object).Ready);

    [Fact]
    public void CanRepair_VendorRepairsTrue_ReturnsTrue()
        => Assert.True(WithAgent(MockOpenVendor(canRepair: true).Object).CanRepair);

    [Fact]
    public void CanRepair_VendorRepairsFalse_ReturnsFalse()
        => Assert.False(WithAgent(MockOpenVendor(canRepair: false).Object).CanRepair);

    [Fact]
    public void CanRepair_AgentNull_ReturnsFalse()
        => Assert.False(WithAgent(null).CanRepair);

    [Fact]
    public void Close_AgentOpen_CallsCloseVendorAsync()
    {
        var mock = MockOpenVendor();
        mock.Setup(v => v.CloseVendorAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask).Verifiable();

        WithAgent(mock.Object).Close();

        mock.Verify(v => v.CloseVendorAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Close_AgentClosed_DoesNotCallClose()
    {
        var mock = new Mock<IVendorNetworkClientComponent>();
        mock.SetupGet(v => v.IsVendorWindowOpen).Returns(false);

        WithAgent(mock.Object).Close();

        mock.Verify(v => v.CloseVendorAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void Close_AgentNull_DoesNotThrow()
        => WithAgent(null).Close();

    [Fact]
    public void TotalRepairCost_VendorOpen_ReturnsAsyncResult()
    {
        var mock = MockOpenVendor(repairCost: 12345u);
        Assert.Equal(12345, WithAgent(mock.Object).TotalRepairCost);
    }

    [Fact]
    public void TotalRepairCost_VendorClosed_ReturnsZero()
        => Assert.Equal(0, WithAgent(null).TotalRepairCost);

    [Fact]
    public void RepairCost_VendorCantRepair_ReturnsZero()
        => Assert.Equal(0, WithAgent(MockOpenVendor(canRepair: false, repairCost: 999u).Object).RepairCost(EquipSlot.Chest));

    [Fact]
    public void RepairCost_VendorRepairs_ReturnsTotalCost()
        => Assert.Equal(750, WithAgent(MockOpenVendor(canRepair: true, repairCost: 750u).Object).RepairCost(EquipSlot.Chest));

    [Fact]
    public void RepairByEquipSlot_CostPositive_CallsRepairAllItemsAsync()
    {
        var mock = MockOpenVendor(canRepair: true, repairCost: 500u);
        mock.Setup(v => v.RepairAllItemsAsync(0xDEADBEEFu, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask).Verifiable();

        WithAgent(mock.Object).RepairByEquipSlot(EquipSlot.MainHand);

        mock.Verify(v => v.RepairAllItemsAsync(0xDEADBEEFu, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void RepairByEquipSlot_CostZero_DoesNotRepair()
    {
        var mock = MockOpenVendor(canRepair: true, repairCost: 0u);

        WithAgent(mock.Object).RepairByEquipSlot(EquipSlot.MainHand);

        mock.Verify(v => v.RepairAllItemsAsync(It.IsAny<ulong>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void RepairByEquipSlot_VendorCantRepair_DoesNotRepair()
    {
        var mock = MockOpenVendor(canRepair: false, repairCost: 999u);

        WithAgent(mock.Object).RepairByEquipSlot(EquipSlot.MainHand);

        mock.Verify(v => v.RepairAllItemsAsync(It.IsAny<ulong>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void RepairAll_VendorOpen_CallsRepairAllItemsAsync()
    {
        var mock = MockOpenVendor();
        mock.Setup(v => v.RepairAllItemsAsync(0xDEADBEEFu, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask).Verifiable();

        WithAgent(mock.Object).RepairAll();

        mock.Verify(v => v.RepairAllItemsAsync(0xDEADBEEFu, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void RepairAll_AgentNull_DoesNotThrow()
        => WithAgent(null).RepairAll();

    [Fact]
    public void BuybackItem_RoutesToBuybackItemAsyncWithSlot()
    {
        var mock = MockOpenVendor();
        mock.Setup(v => v.BuybackItemAsync(0xDEADBEEFu, 3u, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask).Verifiable();

        WithAgent(mock.Object).BuybackItem(itemGuid: 3, itemCount: 1);

        mock.Verify(v => v.BuybackItemAsync(0xDEADBEEFu, 3u, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void BuybackItem_AgentNull_DoesNotThrow()
        => WithAgent(null).BuybackItem(0, 1);

    [Fact]
    public void BuyItem_ConvertsOneBasedSlotToZeroBased()
    {
        var mock = MockOpenVendor();
        // FG Lua passes 1-based slot index; BG packet expects 0-based vendorSlot.
        mock.Setup(v => v.BuyItemBySlotAsync(0xDEADBEEFu, 2, 5u, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask).Verifiable();

        WithAgent(mock.Object).BuyItem(itemGuid: 3, itemCount: 5);

        mock.Verify(v => v.BuyItemBySlotAsync(0xDEADBEEFu, 2, 5u, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void SellItem_RoutesToSellItemAsync()
    {
        var mock = MockOpenVendor();
        mock.Setup(v => v.SellItemAsync(0xDEADBEEFu, 1, 4, 1u, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask).Verifiable();

        WithAgent(mock.Object).SellItem(bagId: 1, slotId: 4, quantity: 1);

        mock.Verify(v => v.SellItemAsync(0xDEADBEEFu, 1, 4, 1u, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void IsItemAvaible_AgentReturnsItem_True()
    {
        var mock = MockOpenVendor();
        mock.Setup(v => v.FindVendorItem(2589u)).Returns(new VendorItem { ItemId = 2589 });
        Assert.True(WithAgent(mock.Object).IsItemAvaible(2589));
    }

    [Fact]
    public void IsItemAvaible_AgentReturnsNull_False()
    {
        var mock = MockOpenVendor();
        mock.Setup(v => v.FindVendorItem(It.IsAny<uint>())).Returns((VendorItem?)null);
        Assert.False(WithAgent(mock.Object).IsItemAvaible(99999));
    }

    [Fact]
    public void Items_VendorClosed_ReturnsEmpty()
        => Assert.Empty(WithAgent(null).Items);

    [Fact]
    public void ItemCallback_DoesNotThrow()
        => WithAgent(null).ItemCallback(42);

    [Fact]
    public void VendorByGuid_GuidWithinIntRange_RoutesToBuyItem()
    {
        var mock = MockOpenVendor();
        mock.Setup(v => v.BuyItemBySlotAsync(0xDEADBEEFu, 4, 2u, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask).Verifiable();

        WithAgent(mock.Object).VendorByGuid(5UL, 2u);

        mock.Verify(v => v.BuyItemBySlotAsync(0xDEADBEEFu, 4, 2u, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void VendorByGuid_GuidOverflowsInt_NoOp()
    {
        var mock = MockOpenVendor();

        WithAgent(mock.Object).VendorByGuid(ulong.MaxValue, 1u);

        mock.Verify(v => v.BuyItemBySlotAsync(It.IsAny<ulong>(), It.IsAny<byte>(), It.IsAny<uint>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
