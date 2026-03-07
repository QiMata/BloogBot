using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Communication;
using GameData.Core.Enums;
using Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// Vendor buy/sell/repair integration tests — validates packet-based vendor operations.
///
/// Uses Razor Hill general goods vendor (Grimtak, entry 3165):
///   - Buy: purchase a cheap item (Weak Flux, itemId 2512, cost 10 copper)
///   - Sell: sell a previously added junk item back
///   - Repair: repair all items at an armorer vendor
///
/// Flow per client:
///   1) Teleport to Razor Hill vendor area
///   2) Find vendor NPC with UNIT_NPC_FLAG_VENDOR
///   3) Add test items via GM command
///   4) Execute buy/sell via ActionType dispatch with vendorGuid param
///   5) Verify inventory changes in snapshot
///
/// Run: dotnet test --filter "FullyQualifiedName~VendorBuySellTests" --configuration Release
/// </summary>
[RequiresMangosStack]
[Collection(LiveValidationCollection.Name)]
public class VendorBuySellTests
{
    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    private const int MapId = 1; // Kalimdor
    // Razor Hill general goods vendor area
    private const float VendorX = 340.36f, VendorY = -4686.29f, VendorZ = 16.54f;

    // Weak Flux — cheap vendor item (1 silver) sold by Wuark (blacksmith at Razor Hill)
    private const uint WeakFluxItemId = 2880;

    // Linen Cloth — common junk item for sell test
    private const uint LinenClothItemId = 2589;

    public VendorBuySellTests(LiveBotFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsReady, _bot.FailureReason ?? "Live bot not ready");
    }

    [SkippableFact]
    public async Task Vendor_BuyItem_AppearsInInventory()
    {
        var account = _bot.BgAccountName!;
        var label = "BG";
        await _bot.EnsureStrictAliveAsync(account, label);

        // Step 0: Clean inventory to ensure a known starting state
        var setupSnap = await _bot.GetSnapshotAsync(account);
        var charName = setupSnap?.CharacterName ?? "Lokgaka";
        await _bot.ExecuteGMCommandAsync($".reset items {charName}");
        await Task.Delay(1500);

        // Step 1: Teleport to vendor area
        _output.WriteLine($"  [{label}] Step 1: Teleporting to Razor Hill vendor area");
        await _bot.BotTeleportAsync(account, MapId, VendorX, VendorY, VendorZ + 3);
        await Task.Delay(3000);

        // Step 2: Find vendor NPC
        var (vendorGuid, npcX, npcY, npcZ) = await FindNpcByFlagAsync(account, label, (uint)NPCFlags.UNIT_NPC_FLAG_VENDOR, "vendor");
        Assert.True(vendorGuid != 0, $"[{label}] Should find a vendor NPC near Razor Hill.");

        // Step 2b: Teleport directly to the NPC (within interaction range)
        _output.WriteLine($"  [{label}] Step 2b: Teleporting to NPC position ({npcX:F1}, {npcY:F1}, {npcZ:F1})");
        await _bot.BotTeleportAsync(account, MapId, npcX, npcY, npcZ + 1);
        await Task.Delay(2000);

        // Step 3: Ensure money for purchase
        await EnsureMoneyAsync(account, label, 1000); // 10 silver should be enough

        // Step 4: Record initial inventory
        await _bot.RefreshSnapshotsAsync();
        var snapBefore = await _bot.GetSnapshotAsync(account);
        var itemCountBefore = CountItemInBags(snapBefore, WeakFluxItemId);
        _output.WriteLine($"  [{label}] Weak Flux count before buy: {itemCountBefore}");

        // Step 5: Buy item via ActionType with vendorGuid
        _output.WriteLine($"  [{label}] Step 5: Buying Weak Flux (ID={WeakFluxItemId}) from vendor 0x{vendorGuid:X}");
        var buyResult = await _bot.SendActionAsync(account, new ActionMessage
        {
            ActionType = ActionType.BuyItem,
            Parameters =
            {
                new RequestParameter { LongParam = (long)vendorGuid },
                new RequestParameter { IntParam = (int)WeakFluxItemId },
                new RequestParameter { IntParam = 1 } // quantity
            }
        });
        _output.WriteLine($"  [{label}] BuyItem dispatch result: {buyResult}");
        Assert.Equal(ResponseResult.Success, buyResult);

        // Step 6: Verify item appears in inventory
        var itemAppeared = await WaitForItemCountChangeAsync(account, WeakFluxItemId, itemCountBefore, TimeSpan.FromSeconds(12));
        Assert.True(itemAppeared, $"[{label}] Weak Flux should appear in inventory after BuyItem.");
        _output.WriteLine($"  [{label}] Weak Flux confirmed in inventory after purchase.");

        // Cleanup: destroy the purchased item
        await DestroyItemByIdAsync(account, label, WeakFluxItemId);
    }

    [SkippableFact]
    public async Task Vendor_SellItem_RemovedFromInventory()
    {
        var account = _bot.BgAccountName!;
        var label = "BG";
        await _bot.EnsureStrictAliveAsync(account, label);

        // Step 1: Teleport to vendor area
        _output.WriteLine($"  [{label}] Step 1: Teleporting to Razor Hill vendor area");
        await _bot.BotTeleportAsync(account, MapId, VendorX, VendorY, VendorZ + 3);
        await Task.Delay(3000);

        // Step 2: Find vendor NPC
        var (vendorGuid, npcX, npcY, npcZ) = await FindNpcByFlagAsync(account, label, (uint)NPCFlags.UNIT_NPC_FLAG_VENDOR, "vendor");
        Assert.True(vendorGuid != 0, $"[{label}] Should find a vendor NPC near Razor Hill.");

        // Step 2b: Teleport directly to the NPC (within interaction range)
        _output.WriteLine($"  [{label}] Step 2b: Teleporting to NPC position ({npcX:F1}, {npcY:F1}, {npcZ:F1})");
        await _bot.BotTeleportAsync(account, MapId, npcX, npcY, npcZ + 1);
        await Task.Delay(2000);

        // Step 3: Add a Linen Cloth to sell
        _output.WriteLine($"  [{label}] Step 3: Adding Linen Cloth to inventory");
        await _bot.BotSelectSelfAsync(account);
        await Task.Delay(300);
        await _bot.SendGmChatCommandTrackedAsync(account, $".additem {LinenClothItemId} 1", captureResponse: true, delayMs: 1500);

        // Wait for item to appear
        var hasItem = await WaitForItemPresentAsync(account, LinenClothItemId, TimeSpan.FromSeconds(5));
        Assert.True(hasItem, $"[{label}] Linen Cloth should be in inventory after .additem.");

        // Step 4: Find the item's bag/slot for sell command
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(account);
        var (bagId, slotId) = FindItemBagSlot(snap, LinenClothItemId);
        Assert.True(bagId >= 0, $"[{label}] Should find Linen Cloth in a bag slot.");
        _output.WriteLine($"  [{label}] Linen Cloth found at bag={bagId} slot={slotId}");

        // Step 5: Sell item via ActionType with vendorGuid
        _output.WriteLine($"  [{label}] Step 5: Selling Linen Cloth to vendor 0x{vendorGuid:X}");
        var sellResult = await _bot.SendActionAsync(account, new ActionMessage
        {
            ActionType = ActionType.SellItem,
            Parameters =
            {
                new RequestParameter { LongParam = (long)vendorGuid },
                new RequestParameter { IntParam = bagId },
                new RequestParameter { IntParam = slotId },
                new RequestParameter { IntParam = 1 } // quantity
            }
        });
        _output.WriteLine($"  [{label}] SellItem dispatch result: {sellResult}");
        Assert.Equal(ResponseResult.Success, sellResult);

        // Step 6: Verify item removed from inventory
        var itemRemoved = await WaitForItemAbsentAsync(account, LinenClothItemId, TimeSpan.FromSeconds(8));
        Assert.True(itemRemoved, $"[{label}] Linen Cloth should be removed from inventory after SellItem.");
        _output.WriteLine($"  [{label}] Linen Cloth confirmed sold.");
    }

    private async Task<(ulong guid, float x, float y, float z)> FindNpcByFlagAsync(string account, string label, uint npcFlag, string npcType)
    {
        for (int attempt = 0; attempt < 3; attempt++)
        {
            await _bot.RefreshSnapshotsAsync();
            var snap = await _bot.GetSnapshotAsync(account);
            var units = snap?.NearbyUnits?.Where(u => (u.NpcFlags & npcFlag) != 0).ToList() ?? [];

            if (units.Count > 0)
            {
                var npc = units[0];
                var guid = npc.GameObject?.Base?.Guid ?? 0;
                var pos = npc.GameObject?.Base?.Position;
                float nx = pos?.X ?? 0, ny = pos?.Y ?? 0, nz = pos?.Z ?? 0;
                _output.WriteLine($"  [{label}] Found {npcType}: {npc.GameObject?.Name} GUID=0x{guid:X} NpcFlags={npc.NpcFlags} at ({nx:F1}, {ny:F1}, {nz:F1})");
                return (guid, nx, ny, nz);
            }

            if (attempt < 2)
            {
                _output.WriteLine($"  [{label}] No {npcType} found on attempt {attempt + 1}, retrying in 2s...");
                await Task.Delay(2000);
            }
        }

        return (0, 0, 0, 0);
    }

    private async Task EnsureMoneyAsync(string account, string label, uint copperNeeded)
    {
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(account);
        var copper = snap?.Player?.Coinage ?? 0;
        if (copper < copperNeeded)
        {
            _output.WriteLine($"  [{label}] Adding money: have {copper}c, need {copperNeeded}c");
            await _bot.BotSelectSelfAsync(account);
            await Task.Delay(300);
            await _bot.SendGmChatCommandTrackedAsync(account, $".modify money {copperNeeded}", captureResponse: false, delayMs: 500);
        }
    }

    /// <summary>
    /// BagContents is MapField&lt;uint, uint&gt; — key=absolute slot index, value=itemId.
    /// Backpack slots are 23-38 (INVENTORY_SLOT_ITEM_START).
    /// </summary>
    private static int CountItemInBags(WoWActivitySnapshot? snap, uint itemId)
        => snap?.Player?.BagContents?.Values.Count(v => v == itemId) ?? 0;

    /// <summary>
    /// Finds bag/slot for an item. BagContents key is absolute inventory slot.
    /// For CMSG_SELL_ITEM: bagId=0xFF (backpack), slotId=absolute slot index.
    /// </summary>
    private static (int bagId, int slotId) FindItemBagSlot(WoWActivitySnapshot? snap, uint itemId)
    {
        var bags = snap?.Player?.BagContents;
        if (bags == null) return (-1, -1);

        foreach (var kvp in bags)
        {
            if (kvp.Value == itemId)
                return (0xFF, (int)kvp.Key); // 0xFF = INVENTORY_SLOT_BAG_0 (backpack)
        }
        return (-1, -1);
    }

    private async Task<bool> WaitForItemCountChangeAsync(string account, uint itemId, int previousCount, TimeSpan timeout)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            await _bot.RefreshSnapshotsAsync();
            var snap = await _bot.GetSnapshotAsync(account);
            var currentCount = CountItemInBags(snap, itemId);
            if (currentCount > previousCount)
                return true;
            await Task.Delay(500);
        }
        return false;
    }

    private async Task<bool> WaitForItemPresentAsync(string account, uint itemId, TimeSpan timeout)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            await _bot.RefreshSnapshotsAsync();
            var snap = await _bot.GetSnapshotAsync(account);
            if (CountItemInBags(snap, itemId) > 0)
                return true;
            await Task.Delay(500);
        }
        return false;
    }

    private async Task<bool> WaitForItemAbsentAsync(string account, uint itemId, TimeSpan timeout)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            await _bot.RefreshSnapshotsAsync();
            var snap = await _bot.GetSnapshotAsync(account);
            if (CountItemInBags(snap, itemId) == 0)
                return true;
            await Task.Delay(500);
        }
        return false;
    }

    private async Task DestroyItemByIdAsync(string account, string label, uint itemId)
    {
        try
        {
            await _bot.RefreshSnapshotsAsync();
            var snap = await _bot.GetSnapshotAsync(account);
            var (bagId, slotId) = FindItemBagSlot(snap, itemId);
            if (bagId >= 0)
            {
                _output.WriteLine($"  [{label}] Cleanup: destroying item {itemId} at bag={bagId} slot={slotId}");
                await _bot.SendActionAsync(account, new ActionMessage
                {
                    ActionType = ActionType.DestroyItem,
                    Parameters =
                    {
                        new RequestParameter { IntParam = bagId },
                        new RequestParameter { IntParam = slotId },
                        new RequestParameter { IntParam = 1 }
                    }
                });
            }
        }
        catch (Exception ex)
        {
            _output.WriteLine($"  [{label}] Cleanup warning: {ex.Message}");
        }
    }
}
