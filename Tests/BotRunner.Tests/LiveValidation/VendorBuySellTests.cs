using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Communication;
using GameData.Core.Enums;
using Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// Shodan-directed BG vendor packet baselines for explicit buy/sell behavior.
/// SHODAN stages the BG action target at Grimtak, with FG launched but idle for
/// topology parity. The test body dispatches only vendor ActionTypes.
/// </summary>
[Collection(LiveValidationCollection.Name)]
public class VendorBuySellTests
{
    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    private const float MaxVendorDistance = 12f;

    private const uint BuyTestItemId = LiveBotFixture.TestItems.RefreshingSpringWater;
    private const uint SellTestItemId = LiveBotFixture.TestItems.LinenCloth;
    private const uint BuySetupCopper = 1000;

    public VendorBuySellTests(LiveBotFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
    }

    [SkippableFact]
    public async Task Vendor_BuyItem_AppearsInInventory()
    {
        await EnsureEconomySettingsAsync();
        var target = ResolveVendorActionTarget();
        var metrics = await RunBuyScenarioAsync(target);

        Assert.True(metrics.VendorFound, $"{target.RoleLabel}: vendor with UNIT_NPC_FLAG_VENDOR should be visible near Razor Hill.");
        Assert.InRange(metrics.VendorDistanceYards, 0f, MaxVendorDistance);
        Assert.Equal(0, metrics.ItemCountBefore);
        Assert.Equal(1, metrics.ItemCountAfter);
        Assert.True(metrics.CoinageAfter < metrics.CoinageBefore,
            $"{target.RoleLabel}: coinage should decrease after buying item {BuyTestItemId}. Before={metrics.CoinageBefore}, after={metrics.CoinageAfter}");
        Assert.InRange(metrics.InventoryLatencyMs, 1, 12000);
    }

    [SkippableFact]
    public async Task Vendor_SellItem_RemovedFromInventory()
    {
        await EnsureEconomySettingsAsync();
        var target = ResolveVendorActionTarget();
        var metrics = await RunSellScenarioAsync(target);

        Assert.True(metrics.VendorFound, $"{target.RoleLabel}: vendor with UNIT_NPC_FLAG_VENDOR should be visible near Razor Hill.");
        Assert.InRange(metrics.VendorDistanceYards, 0f, MaxVendorDistance);
        Assert.Equal(1, metrics.ItemCountBefore);
        Assert.Equal(0, metrics.ItemCountAfter);
        Assert.True(metrics.CoinageAfter > metrics.CoinageBefore,
            $"{target.RoleLabel}: coinage should increase after selling item {SellTestItemId}. Before={metrics.CoinageBefore}, after={metrics.CoinageAfter}");
        Assert.InRange(metrics.InventoryLatencyMs, 1, 8000);
    }

    private async Task<VendorMetrics> RunBuyScenarioAsync(LiveBotFixture.BotRunnerActionTarget target)
    {
        await _bot.StageBotRunnerLoadoutAsync(
            target.AccountName,
            target.RoleLabel,
            cleanSlate: true,
            clearInventoryFirst: true);

        var vendor = await StageVendorAsync(target);
        await _bot.StageBotRunnerCoinageAsync(target.AccountName, target.RoleLabel, BuySetupCopper);

        await _bot.RefreshSnapshotsAsync();
        var before = await _bot.GetSnapshotAsync(target.AccountName);
        var itemCountBefore = CountItemSlots(before, BuyTestItemId);
        var coinageBefore = before?.Player?.Coinage ?? 0;

        _output.WriteLine($"[{target.RoleLabel}] Buying item {BuyTestItemId} from vendor 0x{vendor.Guid:X}.");
        var dispatch = await _bot.SendActionAsync(target.AccountName, new ActionMessage
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
            target.AccountName,
            snapshot => CountItemSlots(snapshot, BuyTestItemId) == itemCountBefore + 1,
            TimeSpan.FromSeconds(12),
            pollIntervalMs: 300,
            progressLabel: $"{target.RoleLabel} vendor buy item");
        var coinageChanged = await _bot.WaitForSnapshotConditionAsync(
            target.AccountName,
            snapshot => (snapshot.Player?.Coinage ?? coinageBefore) < coinageBefore,
            TimeSpan.FromSeconds(5),
            pollIntervalMs: 300,
            progressLabel: $"{target.RoleLabel} vendor buy coinage");
        timer.Stop();

        await _bot.RefreshSnapshotsAsync();
        var after = await _bot.GetSnapshotAsync(target.AccountName);
        var itemCountAfter = CountItemSlots(after, BuyTestItemId);
        var coinageAfter = after?.Player?.Coinage ?? coinageBefore;

        _output.WriteLine(
            $"[{target.RoleLabel}] buy metrics: vendorFound={vendor.Guid != 0}, vendorDistance={vendor.DistanceYards:F1}, " +
            $"itemCount {itemCountBefore}->{itemCountAfter}, coinage {coinageBefore}->{coinageAfter}, " +
            $"itemChanged={itemChanged}, coinageChanged={coinageChanged}, latencyMs={timer.ElapsedMilliseconds}");

        if (!itemChanged || !coinageChanged)
            _bot.DumpSnapshotDiagnostics(after, target.RoleLabel);

        await DestroyItemByIdAsync(target, BuyTestItemId);

        return new VendorMetrics(
            vendor.Guid != 0,
            vendor.DistanceYards,
            itemCountBefore,
            itemCountAfter,
            coinageBefore,
            coinageAfter,
            (int)timer.ElapsedMilliseconds);
    }

    private async Task<VendorMetrics> RunSellScenarioAsync(LiveBotFixture.BotRunnerActionTarget target)
    {
        await _bot.StageBotRunnerLoadoutAsync(
            target.AccountName,
            target.RoleLabel,
            itemsToAdd: new[] { new LiveBotFixture.ItemDirective(SellTestItemId, 1) },
            cleanSlate: true,
            clearInventoryFirst: true);

        var vendor = await StageVendorAsync(target);

        var staged = await _bot.WaitForSnapshotConditionAsync(
            target.AccountName,
            snapshot => CountItemSlots(snapshot, SellTestItemId) == 1,
            TimeSpan.FromSeconds(5),
            pollIntervalMs: 300,
            progressLabel: $"{target.RoleLabel} vendor sell stage");
        Assert.True(staged, $"[{target.RoleLabel}] sell item {SellTestItemId} should appear in bags before sell.");

        await _bot.RefreshSnapshotsAsync();
        var before = await _bot.GetSnapshotAsync(target.AccountName);
        var itemCountBefore = CountItemSlots(before, SellTestItemId);
        var coinageBefore = before?.Player?.Coinage ?? 0;
        var (bagId, slotId) = FindItemBagSlot(before, SellTestItemId);
        Assert.True(bagId >= 0, $"[{target.RoleLabel}] sell item {SellTestItemId} should resolve to a bag slot.");

        _output.WriteLine($"[{target.RoleLabel}] Selling item {SellTestItemId} from bag={bagId} slot={slotId} to vendor 0x{vendor.Guid:X}.");
        var dispatch = await _bot.SendActionAsync(target.AccountName, new ActionMessage
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
            target.AccountName,
            snapshot => CountItemSlots(snapshot, SellTestItemId) == 0,
            TimeSpan.FromSeconds(8),
            pollIntervalMs: 300,
            progressLabel: $"{target.RoleLabel} vendor sell item");
        var coinageChanged = await _bot.WaitForSnapshotConditionAsync(
            target.AccountName,
            snapshot => (snapshot.Player?.Coinage ?? coinageBefore) > coinageBefore,
            TimeSpan.FromSeconds(5),
            pollIntervalMs: 300,
            progressLabel: $"{target.RoleLabel} vendor sell coinage");
        timer.Stop();

        await _bot.RefreshSnapshotsAsync();
        var after = await _bot.GetSnapshotAsync(target.AccountName);
        var itemCountAfter = CountItemSlots(after, SellTestItemId);
        var coinageAfter = after?.Player?.Coinage ?? coinageBefore;

        _output.WriteLine(
            $"[{target.RoleLabel}] sell metrics: vendorFound={vendor.Guid != 0}, vendorDistance={vendor.DistanceYards:F1}, " +
            $"itemCount {itemCountBefore}->{itemCountAfter}, coinage {coinageBefore}->{coinageAfter}, " +
            $"itemChanged={itemChanged}, coinageChanged={coinageChanged}, latencyMs={timer.ElapsedMilliseconds}");

        if (!itemChanged || !coinageChanged)
            _bot.DumpSnapshotDiagnostics(after, target.RoleLabel);

        return new VendorMetrics(
            vendor.Guid != 0,
            vendor.DistanceYards,
            itemCountBefore,
            itemCountAfter,
            coinageBefore,
            coinageAfter,
            (int)timer.ElapsedMilliseconds);
    }

    private async Task<VendorTarget> StageVendorAsync(LiveBotFixture.BotRunnerActionTarget target)
    {
        var staged = await _bot.StageBotRunnerAtRazorHillVendorAsync(
            target.AccountName,
            target.RoleLabel,
            cleanSlate: false);
        Assert.True(staged, $"{target.RoleLabel}: expected to stage near Grimtak with a visible vendor.");

        var vendorUnit = await _bot.WaitForNearbyUnitAsync(
            target.AccountName,
            (uint)NPCFlags.UNIT_NPC_FLAG_VENDOR,
            timeoutMs: 5000,
            progressLabel: $"{target.RoleLabel} vendor lookup");

        Assert.NotNull(vendorUnit);
        var vendorGuid = vendorUnit!.GameObject?.Base?.Guid ?? 0;
        var vendorPos = vendorUnit.GameObject?.Base?.Position;

        await _bot.RefreshSnapshotsAsync();
        var snapshot = await _bot.GetSnapshotAsync(target.AccountName);
        var playerPos = snapshot?.Player?.Unit?.GameObject?.Base?.Position;
        var vendorDistance = playerPos == null || vendorPos == null
            ? float.MaxValue
            : LiveBotFixture.Distance3D(playerPos.X, playerPos.Y, playerPos.Z, vendorPos.X, vendorPos.Y, vendorPos.Z);

        _output.WriteLine(
            $"[{target.RoleLabel}] vendor target: guid=0x{vendorGuid:X}, name={vendorUnit.GameObject?.Name}, " +
            $"flags={vendorUnit.NpcFlags}, distance={vendorDistance:F1}y");

        return new VendorTarget(vendorGuid, vendorDistance);
    }

    private async Task EnsureEconomySettingsAsync()
    {
        var settingsPath = ResolveRepoPath(
            "Services", "WoWStateManager", "Settings", "Configs", "Economy.config.json");

        await _bot.EnsureSettingsAsync(settingsPath);
        _bot.SetOutput(_output);
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsReady, _bot.FailureReason ?? "Live bot not ready");
        await _bot.AssertConfiguredCharactersMatchAsync(settingsPath);

        global::Tests.Infrastructure.Skip.If(
            string.IsNullOrWhiteSpace(_bot.ShodanAccountName),
            "Shodan director was not launched by Economy.config.json.");
    }

    private LiveBotFixture.BotRunnerActionTarget ResolveVendorActionTarget()
    {
        var targets = _bot.ResolveBotRunnerActionTargets(includeForegroundIfActionable: false);
        var target = targets.Single(target => !target.IsForeground);

        _output.WriteLine(
            $"[ACTION-PLAN] SHODAN {_bot.ShodanAccountName}/{_bot.ShodanCharacterName}: director only, no vendor action dispatch.");
        if (!string.IsNullOrWhiteSpace(_bot.FgAccountName))
        {
            _output.WriteLine(
                $"[ACTION-PLAN] FG {_bot.FgAccountName}/{_bot.FgCharacterName}: launched for Shodan topology parity; vendor packet baseline remains BG-only.");
        }
        _output.WriteLine(
            $"[ACTION-PLAN] BG {target.AccountName}/{target.CharacterName}: stage at Razor Hill vendor and dispatch BuyItem/SellItem.");

        return target;
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

    private async Task DestroyItemByIdAsync(LiveBotFixture.BotRunnerActionTarget target, uint itemId)
    {
        await _bot.RefreshSnapshotsAsync();
        var snapshot = await _bot.GetSnapshotAsync(target.AccountName);
        var (bagId, slotId) = FindItemBagSlot(snapshot, itemId);
        if (bagId < 0)
            return;

        _output.WriteLine($"[{target.RoleLabel}] Cleanup: destroying item {itemId} at bag={bagId} slot={slotId}.");
        await _bot.SendActionAsync(target.AccountName, new ActionMessage
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

    private static string ResolveRepoPath(params string[] segments)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine([dir.FullName, .. segments]);
            if (File.Exists(candidate))
                return candidate;

            dir = dir.Parent;
        }

        throw new FileNotFoundException($"Could not locate repo path: {Path.Combine(segments)}");
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
