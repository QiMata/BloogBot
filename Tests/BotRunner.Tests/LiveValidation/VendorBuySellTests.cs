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
/// BG-first vendor packet baselines for explicit buy/sell behavior.
///
/// Current production path under test:
/// - Exports/BotRunner/BotRunnerService.ActionDispatch.cs
/// - Exports/WoWSharpClient/WoWSharpObjectManager.Inventory.cs
/// - Exports/WoWSharpClient/Networking/ClientComponents/VendorNetworkClientComponent.cs
///
/// FG parity is intentionally excluded. The foreground merchant-frame path is still legacy Lua/UI logic,
/// while the live overhaul is prioritizing the packet-driven BG implementation first.
/// </summary>
[RequiresMangosStack]
[Collection(LiveValidationCollection.Name)]
public class VendorBuySellTests
{
    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    private const int MapId = 1;
    private const float GrimtakX = 305.722f;
    private const float GrimtakY = -4665.87f;
    // Z+3 offset applied to spawn table Z to avoid UNDERMAP detection
    private const float GrimtakZ = 19.527f;
    private const float MaxVendorDistance = 12f;

    private const uint BuyTestItemId = LiveBotFixture.TestItems.RefreshingSpringWater;
    private const uint SellTestItemId = LiveBotFixture.TestItems.LinenCloth;
    private const uint BuySetupCopper = 1000;

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
        var metrics = await RunBuyScenarioAsync(_bot.BgAccountName!, "BG");

        Assert.True(metrics.VendorFound, "BG: vendor with UNIT_NPC_FLAG_VENDOR should be visible near Razor Hill.");
        Assert.InRange(metrics.VendorDistanceYards, 0f, MaxVendorDistance);
        Assert.Equal(0, metrics.ItemCountBefore);
        Assert.Equal(1, metrics.ItemCountAfter);
        Assert.True(metrics.CoinageAfter < metrics.CoinageBefore,
            $"BG: coinage should decrease after buying item {BuyTestItemId}. Before={metrics.CoinageBefore}, after={metrics.CoinageAfter}");
        Assert.InRange(metrics.InventoryLatencyMs, 1, 12000);
    }

    [SkippableFact]
    public async Task Vendor_SellItem_RemovedFromInventory()
    {
        var metrics = await RunSellScenarioAsync(_bot.BgAccountName!, "BG");

        Assert.True(metrics.VendorFound, "BG: vendor with UNIT_NPC_FLAG_VENDOR should be visible near Razor Hill.");
        Assert.InRange(metrics.VendorDistanceYards, 0f, MaxVendorDistance);
        Assert.Equal(1, metrics.ItemCountBefore);
        Assert.Equal(0, metrics.ItemCountAfter);
        Assert.True(metrics.CoinageAfter > metrics.CoinageBefore,
            $"BG: coinage should increase after selling item {SellTestItemId}. Before={metrics.CoinageBefore}, after={metrics.CoinageAfter}");
        Assert.InRange(metrics.InventoryLatencyMs, 1, 8000);
    }

    private async Task<VendorMetrics> RunBuyScenarioAsync(string account, string label)
    {
        await _bot.EnsureCleanSlateAsync(account, label);
        await _bot.BotClearInventoryAsync(account);

        var vendor = await StageVendorAsync(account, label);
        await EnsureMoneyAsync(account, label, BuySetupCopper);

        await _bot.RefreshSnapshotsAsync();
        var before = await _bot.GetSnapshotAsync(account);
        var itemCountBefore = CountItemSlots(before, BuyTestItemId);
        var coinageBefore = before?.Player?.Coinage ?? 0;

        _output.WriteLine($"[{label}] Buying item {BuyTestItemId} from vendor 0x{vendor.Guid:X}.");
        var dispatch = await _bot.SendActionAsync(account, new ActionMessage
        {
            ActionType = ActionType.BuyItem,
            Parameters =
            {
                new RequestParameter { LongParam = (long)vendor.Guid },
                new RequestParameter { IntParam = (int)BuyTestItemId },
                new RequestParameter { IntParam = 1 }
            }
        });
        Assert.Equal(ResponseResult.Success, dispatch);

        var timer = Stopwatch.StartNew();
        var itemChanged = await _bot.WaitForSnapshotConditionAsync(
            account,
            snapshot => CountItemSlots(snapshot, BuyTestItemId) == itemCountBefore + 1,
            TimeSpan.FromSeconds(12),
            pollIntervalMs: 300,
            progressLabel: $"{label} vendor buy item");
        var coinageChanged = await _bot.WaitForSnapshotConditionAsync(
            account,
            snapshot => (snapshot.Player?.Coinage ?? coinageBefore) < coinageBefore,
            TimeSpan.FromSeconds(5),
            pollIntervalMs: 300,
            progressLabel: $"{label} vendor buy coinage");
        timer.Stop();

        await _bot.RefreshSnapshotsAsync();
        var after = await _bot.GetSnapshotAsync(account);
        var itemCountAfter = CountItemSlots(after, BuyTestItemId);
        var coinageAfter = after?.Player?.Coinage ?? coinageBefore;

        _output.WriteLine(
            $"[{label}] buy metrics: vendorFound={vendor.Guid != 0}, vendorDistance={vendor.DistanceYards:F1}, " +
            $"itemCount {itemCountBefore}->{itemCountAfter}, coinage {coinageBefore}->{coinageAfter}, " +
            $"itemChanged={itemChanged}, coinageChanged={coinageChanged}, latencyMs={timer.ElapsedMilliseconds}");

        if (!itemChanged || !coinageChanged)
            _bot.DumpSnapshotDiagnostics(after, label);

        await DestroyItemByIdAsync(account, label, BuyTestItemId);

        return new VendorMetrics(
            vendor.Guid != 0,
            vendor.DistanceYards,
            itemCountBefore,
            itemCountAfter,
            coinageBefore,
            coinageAfter,
            (int)timer.ElapsedMilliseconds);
    }

    private async Task<VendorMetrics> RunSellScenarioAsync(string account, string label)
    {
        await _bot.EnsureCleanSlateAsync(account, label);
        await _bot.BotClearInventoryAsync(account);

        var vendor = await StageVendorAsync(account, label);

        _output.WriteLine($"[{label}] Adding one sell item ({SellTestItemId}) to bags.");
        await _bot.BotAddItemAsync(account, SellTestItemId, 1);
        var staged = await _bot.WaitForSnapshotConditionAsync(
            account,
            snapshot => CountItemSlots(snapshot, SellTestItemId) == 1,
            TimeSpan.FromSeconds(5),
            pollIntervalMs: 300,
            progressLabel: $"{label} vendor sell stage");
        Assert.True(staged, $"[{label}] sell item {SellTestItemId} should appear in bags before sell.");

        await _bot.RefreshSnapshotsAsync();
        var before = await _bot.GetSnapshotAsync(account);
        var itemCountBefore = CountItemSlots(before, SellTestItemId);
        var coinageBefore = before?.Player?.Coinage ?? 0;
        var (bagId, slotId) = FindItemBagSlot(before, SellTestItemId);
        Assert.True(bagId >= 0, $"[{label}] sell item {SellTestItemId} should resolve to a bag slot.");

        _output.WriteLine($"[{label}] Selling item {SellTestItemId} from bag={bagId} slot={slotId} to vendor 0x{vendor.Guid:X}.");
        var dispatch = await _bot.SendActionAsync(account, new ActionMessage
        {
            ActionType = ActionType.SellItem,
            Parameters =
            {
                new RequestParameter { LongParam = (long)vendor.Guid },
                new RequestParameter { IntParam = bagId },
                new RequestParameter { IntParam = slotId },
                new RequestParameter { IntParam = 1 }
            }
        });
        Assert.Equal(ResponseResult.Success, dispatch);

        var timer = Stopwatch.StartNew();
        var itemChanged = await _bot.WaitForSnapshotConditionAsync(
            account,
            snapshot => CountItemSlots(snapshot, SellTestItemId) == 0,
            TimeSpan.FromSeconds(8),
            pollIntervalMs: 300,
            progressLabel: $"{label} vendor sell item");
        var coinageChanged = await _bot.WaitForSnapshotConditionAsync(
            account,
            snapshot => (snapshot.Player?.Coinage ?? coinageBefore) > coinageBefore,
            TimeSpan.FromSeconds(5),
            pollIntervalMs: 300,
            progressLabel: $"{label} vendor sell coinage");
        timer.Stop();

        await _bot.RefreshSnapshotsAsync();
        var after = await _bot.GetSnapshotAsync(account);
        var itemCountAfter = CountItemSlots(after, SellTestItemId);
        var coinageAfter = after?.Player?.Coinage ?? coinageBefore;

        _output.WriteLine(
            $"[{label}] sell metrics: vendorFound={vendor.Guid != 0}, vendorDistance={vendor.DistanceYards:F1}, " +
            $"itemCount {itemCountBefore}->{itemCountAfter}, coinage {coinageBefore}->{coinageAfter}, " +
            $"itemChanged={itemChanged}, coinageChanged={coinageChanged}, latencyMs={timer.ElapsedMilliseconds}");

        if (!itemChanged || !coinageChanged)
            _bot.DumpSnapshotDiagnostics(after, label);

        return new VendorMetrics(
            vendor.Guid != 0,
            vendor.DistanceYards,
            itemCountBefore,
            itemCountAfter,
            coinageBefore,
            coinageAfter,
            (int)timer.ElapsedMilliseconds);
    }

    private async Task<VendorTarget> StageVendorAsync(string account, string label)
    {
        _output.WriteLine($"[{label}] Teleporting to Grimtak's Razor Hill position.");
        await _bot.BotTeleportAsync(account, MapId, GrimtakX, GrimtakY, GrimtakZ);
        await _bot.WaitForTeleportSettledAsync(account, GrimtakX, GrimtakY, progressLabel: $"{label} vendor teleport");

        var vendorUnit = await _bot.WaitForNearbyUnitAsync(
            account,
            (uint)NPCFlags.UNIT_NPC_FLAG_VENDOR,
            timeoutMs: 5000,
            progressLabel: $"{label} vendor lookup");

        Assert.NotNull(vendorUnit);
        var vendorGuid = vendorUnit!.GameObject?.Base?.Guid ?? 0;
        var vendorPos = vendorUnit.GameObject?.Base?.Position;

        await _bot.RefreshSnapshotsAsync();
        var snapshot = await _bot.GetSnapshotAsync(account);
        var playerPos = snapshot?.Player?.Unit?.GameObject?.Base?.Position;
        var vendorDistance = playerPos == null || vendorPos == null
            ? float.MaxValue
            : LiveBotFixture.Distance3D(playerPos.X, playerPos.Y, playerPos.Z, vendorPos.X, vendorPos.Y, vendorPos.Z);

        _output.WriteLine(
            $"[{label}] vendor target: guid=0x{vendorGuid:X}, name={vendorUnit.GameObject?.Name}, " +
            $"flags={vendorUnit.NpcFlags}, distance={vendorDistance:F1}y");

        return new VendorTarget(vendorGuid, vendorDistance);
    }

    private async Task EnsureMoneyAsync(string account, string label, uint copperNeeded)
    {
        await _bot.RefreshSnapshotsAsync();
        var snapshot = await _bot.GetSnapshotAsync(account);
        var currentCopper = snapshot?.Player?.Coinage ?? 0;
        if (currentCopper >= copperNeeded)
        {
            _output.WriteLine($"[{label}] Coinage already sufficient: {currentCopper}c.");
            return;
        }

        _output.WriteLine($"[{label}] Adding {copperNeeded} copper for vendor purchase baseline.");
        await _bot.BotSelectSelfAsync(account);
        await Task.Delay(300);
        await _bot.SendGmChatCommandTrackedAsync(account, $".modify money {copperNeeded}", captureResponse: false, delayMs: 500);
    }

    private static int CountItemSlots(WoWActivitySnapshot? snapshot, uint itemId)
        => snapshot?.Player?.BagContents?.Values.Count(value => value == itemId) ?? 0;

    private static (int bagId, int slotId) FindItemBagSlot(WoWActivitySnapshot? snapshot, uint itemId)
    {
        var bags = snapshot?.Player?.BagContents;
        if (bags == null)
            return (-1, -1);

        foreach (var item in bags)
        {
            if (item.Value == itemId)
                return (0xFF, (int)item.Key);
        }

        return (-1, -1);
    }

    private async Task DestroyItemByIdAsync(string account, string label, uint itemId)
    {
        await _bot.RefreshSnapshotsAsync();
        var snapshot = await _bot.GetSnapshotAsync(account);
        var (bagId, slotId) = FindItemBagSlot(snapshot, itemId);
        if (bagId < 0)
            return;

        _output.WriteLine($"[{label}] Cleanup: destroying item {itemId} at bag={bagId} slot={slotId}.");
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

    private sealed record VendorTarget(ulong Guid, float DistanceYards);

    private sealed record VendorMetrics(
        bool VendorFound,
        float VendorDistanceYards,
        int ItemCountBefore,
        int ItemCountAfter,
        long CoinageBefore,
        long CoinageAfter,
        int InventoryLatencyMs);
}
