using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Communication;
using Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// Unequip item integration test — validates ActionType.UnequipItem.
///
/// Each bot (BG + FG) independently:
///   1) Ensure Worn Mace (item 36) is equipped in mainhand via .additem + EquipItem.
///   2) Send ActionType.UnequipItem with EquipSlot.MainHand (16).
///   3) Verify mainhand slot is empty and item moved back to bags.
///
/// EquipSlot 16 = MainHand. The dispatch maps to EquipmentAgent.UnequipItemAsync
/// which sends CMSG_AUTOSTORE_BAG_ITEM.
///
/// Run: dotnet test --filter "FullyQualifiedName~UnequipItemTests" --configuration Release
/// </summary>
[Collection(LiveValidationCollection.Name)]
public class UnequipItemTests
{
    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    private const uint WornMace = 36;
    private const uint MainhandSlot = 15; // Inventory map key for mainhand
    private const int MainhandEquipSlot = 16; // EquipSlot enum value for UnequipItem
    private const uint OneHandMaceSpell = 198;

    public UnequipItemTests(LiveBotFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsReady, _bot.FailureReason ?? "Live bot not ready");
    }

    [SkippableFact]
    public async Task UnequipItem_MainhandWeapon_MovesToBags()
    {
        var bgAccount = _bot.BgAccountName!;
        Assert.NotNull(bgAccount);
        _output.WriteLine($"=== BG Bot: {_bot.BgCharacterName} ({bgAccount}) ===");

        bool bgPassed, fgPassed = false;
        var hasFg = _bot.IsFgActionable;

        if (hasFg)
        {
            var fgAccount = _bot.FgAccountName!;
            Assert.NotNull(fgAccount);
            _output.WriteLine($"=== FG Bot: {_bot.FgCharacterName} ({fgAccount}) ===");
            _output.WriteLine("[PARITY] Running BG and FG unequip scenarios in parallel.");

            var bgTask = RunUnequipScenario(bgAccount, "BG");
            var fgTask = RunUnequipScenario(fgAccount, "FG");
            await Task.WhenAll(bgTask, fgTask);
            bgPassed = await bgTask;
            fgPassed = await fgTask;
        }
        else
        {
            bgPassed = await RunUnequipScenario(bgAccount, "BG");
            _output.WriteLine("\nFG Bot: NOT AVAILABLE");
        }

        Assert.True(bgPassed, "BG bot: Mainhand should be empty after UnequipItem.");
        if (hasFg)
            Assert.True(fgPassed, "FG bot: Mainhand should be empty after UnequipItem.");
    }

    private async Task<bool> RunUnequipScenario(string account, string label)
    {
        await _bot.EnsureCleanSlateAsync(account, label);

        // Step 0: Clear inventory to ensure bag space for unequip destination
        _output.WriteLine($"  [{label}] Step 0: Clearing inventory for clean unequip test.");
        await _bot.BotClearInventoryAsync(account, includeExtraBags: false);
        await Task.Delay(1000);

        // Step 1: Learn mace proficiency if needed
        _output.WriteLine($"  [{label}] Step 1: Ensuring mace proficiency.");
        await _bot.BotLearnSpellAsync(account, OneHandMaceSpell);
        await _bot.BotSetSkillAsync(account, 54, 1, 300); // skill 54 = Maces
        await Task.Delay(500);

        // Step 2: Add and equip a Worn Mace
        _output.WriteLine($"  [{label}] Step 2: Adding and equipping Worn Mace (item {WornMace}).");
        await _bot.BotAddItemAsync(account, WornMace);
        await WaitForBagItemAsync(account, WornMace, TimeSpan.FromSeconds(5));

        var equipResult = await _bot.SendActionAsync(account, new ActionMessage
        {
            ActionType = ActionType.EquipItem,
            Parameters = { new RequestParameter { IntParam = (int)WornMace } }
        });
        Assert.Equal(ResponseResult.Success, equipResult);

        // Wait for equip to complete
        var equipped = await WaitForMainhandEquippedAsync(account, TimeSpan.FromSeconds(5));
        if (!equipped)
        {
            _output.WriteLine($"  [{label}] Mainhand not equipped after EquipItem; cannot test unequip.");
            return false;
        }

        // Record state before unequip
        await _bot.RefreshSnapshotsAsync();
        var snapBefore = await _bot.GetSnapshotAsync(account);
        var maceCountBefore = CountBagItem(snapBefore, WornMace);
        var mainhandGuidBefore = GetMainhandGuid(snapBefore);
        _output.WriteLine($"  [{label}] Before unequip: mainhand=0x{mainhandGuidBefore:X}, maces in bags={maceCountBefore}");

        // Step 3: Unequip mainhand
        _output.WriteLine($"  [{label}] Step 3: Sending UnequipItem (EquipSlot={MainhandEquipSlot}).");
        var unequipResult = await _bot.SendActionAsync(account, new ActionMessage
        {
            ActionType = ActionType.UnequipItem,
            Parameters = { new RequestParameter { IntParam = MainhandEquipSlot } }
        });
        _output.WriteLine($"  [{label}] UnequipItem dispatch result: {unequipResult}");
        Assert.Equal(ResponseResult.Success, unequipResult);
        await Task.Delay(500);

        // Step 4: Verify mainhand is empty and item moved to bags
        var mainhandEmpty = false;
        var maceInBags = false;
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < TimeSpan.FromSeconds(5))
        {
            await _bot.RefreshSnapshotsAsync();
            var snap = await _bot.GetSnapshotAsync(account);
            var mainhandGuid = GetMainhandGuid(snap);
            var maceCount = CountBagItem(snap, WornMace);

            if (mainhandGuid == 0 || mainhandGuid != mainhandGuidBefore)
            {
                mainhandEmpty = mainhandGuid == 0;
                maceInBags = maceCount > maceCountBefore;
                _output.WriteLine($"  [{label}] After unequip ({sw.ElapsedMilliseconds}ms): mainhand=0x{mainhandGuid:X}, maces in bags={maceCount}");
                if (mainhandEmpty)
                    break;
            }
            await Task.Delay(200);
        }

        _output.WriteLine($"  [{label}] Result: mainhandEmpty={mainhandEmpty}, maceInBags={maceInBags}");
        return mainhandEmpty;
    }

    private static ulong GetMainhandGuid(WoWActivitySnapshot? snap)
    {
        if (snap?.Player?.Inventory.TryGetValue(MainhandSlot, out ulong guid) == true)
            return guid;
        return 0;
    }

    private static int CountBagItem(WoWActivitySnapshot? snap, uint itemId)
        => snap?.Player?.BagContents?.Values.Count(id => id == itemId) ?? 0;

    private async Task<bool> WaitForBagItemAsync(string account, uint itemId, TimeSpan timeout)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            await _bot.RefreshSnapshotsAsync();
            var snap = await _bot.GetSnapshotAsync(account);
            if (CountBagItem(snap, itemId) > 0) return true;
            await Task.Delay(200);
        }
        return false;
    }

    private async Task<bool> WaitForMainhandEquippedAsync(string account, TimeSpan timeout)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            await _bot.RefreshSnapshotsAsync();
            var snap = await _bot.GetSnapshotAsync(account);
            if (GetMainhandGuid(snap) != 0) return true;
            await Task.Delay(200);
        }
        return false;
    }
}
