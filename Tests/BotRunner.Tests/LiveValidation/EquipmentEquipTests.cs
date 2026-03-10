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
/// Equipment equip integration test - dual-client validation.
///
/// Scenario (per bot):
///   1) Read current snapshot state.
///   2) Apply only missing setup deltas (alive/proficiency/item-in-bag).
///   3) Equip Worn Mace (item 36) with ActionType.EquipItem.
///   4) Verify the mace moved from bag snapshot entries to mainhand equipment.
///
/// Run:
///   dotnet test --filter "FullyQualifiedName~EquipmentEquipTests" --configuration Release -v n
/// </summary>
[RequiresMangosStack]
[Collection(LiveValidationCollection.Name)]
public class EquipmentEquipTests
{
    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    private const uint WornMace = 36;
    private const uint MainhandSlot = 15;
    private const uint OneHandMaceSpell = 198;

    public EquipmentEquipTests(LiveBotFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsReady, _bot.FailureReason ?? "Live bot not ready");
    }

    [SkippableFact]
    public async Task EquipItem_AddWeaponAndEquip_AppearsInEquipmentSlot()
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
            _output.WriteLine("[PARITY] Running BG and FG equip scenarios in parallel.");

            var bgTask = RunEquipScenario(bgAccount, "BG");
            var fgTask = RunEquipScenario(fgAccount, "FG");
            await Task.WhenAll(bgTask, fgTask);
            bgPassed = await bgTask;
            fgPassed = await fgTask;
        }
        else
        {
            bgPassed = await RunEquipScenario(bgAccount, "BG");
            _output.WriteLine("\nFG Bot: NOT AVAILABLE");
        }

        Assert.True(bgPassed, "BG bot: Worn Mace should move from bag snapshot to MAINHAND slot.");
        if (hasFg)
            Assert.True(fgPassed, "FG bot: Worn Mace should move from bag snapshot to MAINHAND slot.");
    }

    private async Task<bool> RunEquipScenario(string account, string label)
    {
        // Standardized setup (BT-SETUP-001): revive + safe zone + GM on
        await _bot.EnsureCleanSlateAsync(account, label);

        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(account);
        if (snap?.Player == null)
            return false;

        var playerBefore = snap.Player;
        bool mainhandBeforeEquipped = playerBefore.Inventory.TryGetValue(MainhandSlot, out ulong mainhandBeforeGuid) && mainhandBeforeGuid != 0;
        int maceCountBeforeSetup = CountBagItem(playerBefore, WornMace);

        _output.WriteLine($"  [{label}] Mainhand before: {(mainhandBeforeEquipped ? $"GUID=0x{mainhandBeforeGuid:X}" : "EMPTY")}");
        _output.WriteLine($"  [{label}] Worn Mace count in bags before setup: {maceCountBeforeSetup}");

        // Clear mainhand if occupied — other tests may have equipped items.
        if (mainhandBeforeEquipped)
        {
            _output.WriteLine($"  [{label}] Clearing mainhand via .reset items before equip test.");
            await _bot.ExecuteGMCommandAsync($".reset items {snap.CharacterName}");
            await Task.Delay(1500);
            await _bot.RefreshSnapshotsAsync();
            snap = await _bot.GetSnapshotAsync(account) ?? snap;
            playerBefore = snap.Player!;
            mainhandBeforeEquipped = playerBefore.Inventory.TryGetValue(MainhandSlot, out mainhandBeforeGuid) && mainhandBeforeGuid != 0;
            maceCountBeforeSetup = CountBagItem(playerBefore, WornMace);
            _output.WriteLine($"  [{label}] Mainhand after reset: {(mainhandBeforeEquipped ? $"GUID=0x{mainhandBeforeGuid:X}" : "EMPTY")}");
        }

        // Grant mace proficiency: .learn adds the spell, .setskill adds the weapon skill.
        // .setskill requires a selected target, so BotSetSkillAsync auto-selects self first.
        bool hasMaceProficiency = playerBefore.SpellList.Contains(OneHandMaceSpell);
        if (!hasMaceProficiency)
        {
            _output.WriteLine($"  [{label}] Learning missing 1H mace proficiency (spell {OneHandMaceSpell}).");
            await _bot.BotLearnSpellAsync(account, OneHandMaceSpell);
            await _bot.BotSetSkillAsync(account, 54, 1, 300); // skill 54 = Maces
            var learnSw = Stopwatch.StartNew();
            while (learnSw.Elapsed < TimeSpan.FromSeconds(5))
            {
                await Task.Delay(200);
                await _bot.RefreshSnapshotsAsync();
                snap = await _bot.GetSnapshotAsync(account) ?? snap;
                if (snap.Player?.SpellList.Contains(OneHandMaceSpell) == true)
                    break;
            }
            if (snap.Player == null)
                return false;
            playerBefore = snap.Player;
        }

        // Ensure at least one Worn Mace is present in bags.
        int maceCountBeforeEquip = CountBagItem(playerBefore, WornMace);
        if (maceCountBeforeEquip == 0)
        {
            var bagItemCount = playerBefore.BagContents.Count;
            if (bagItemCount >= 15)
            {
                _output.WriteLine($"  [{label}] Bag nearly full ({bagItemCount}); clearing inventory before additem.");
                await _bot.BotClearInventoryAsync(account, includeExtraBags: false);
                var clearSw = Stopwatch.StartNew();
                while (clearSw.Elapsed < TimeSpan.FromSeconds(5))
                {
                    await Task.Delay(200);
                    await _bot.RefreshSnapshotsAsync();
                    var clearSnap = await _bot.GetSnapshotAsync(account);
                    if (clearSnap?.Player?.BagContents.Count < bagItemCount)
                        break;
                }
            }

            _output.WriteLine($"  [{label}] Adding Worn Mace (item {WornMace}).");
            await _bot.BotAddItemAsync(account, WornMace);
            var addItemSw = Stopwatch.StartNew();
            while (addItemSw.Elapsed < TimeSpan.FromSeconds(5))
            {
                await Task.Delay(200);
                await _bot.RefreshSnapshotsAsync();
                snap = await _bot.GetSnapshotAsync(account) ?? snap;
                if (snap.Player != null && CountBagItem(snap.Player, WornMace) > 0)
                    break;
            }
            if (snap.Player == null)
                return false;
            playerBefore = snap.Player;
            maceCountBeforeEquip = CountBagItem(playerBefore, WornMace);
        }

        if (maceCountBeforeEquip == 0)
        {
            _output.WriteLine($"  [{label}] Worn Mace was not present after setup; cannot validate equip.");
            _bot.DumpSnapshotDiagnostics(snap, label);
            return false;
        }

        // GM mode stays ON — equip actions work with GM mode enabled.
        // Previous .gm off here corrupted GM state for downstream tests and risked BG disconnect.

        // Equip and verify transition — poll for mainhand slot change instead of fixed delay.
        _output.WriteLine($"  [{label}] Equipping Worn Mace.");
        var equipResult = await _bot.SendActionAsync(account, new ActionMessage
        {
            ActionType = ActionType.EquipItem,
            Parameters = { new RequestParameter { IntParam = (int)WornMace } }
        });
        Assert.Equal(ResponseResult.Success, equipResult);
        await Task.Delay(500);

        WoWActivitySnapshot? after = null;
        Game.WoWPlayer? playerAfter = null;
        var equipSw = Stopwatch.StartNew();
        while (equipSw.Elapsed < TimeSpan.FromSeconds(3))
        {
            await _bot.RefreshSnapshotsAsync();
            after = await _bot.GetSnapshotAsync(account);
            playerAfter = after?.Player;
            if (playerAfter != null)
            {
                bool slotFilled = playerAfter.Inventory.TryGetValue(MainhandSlot, out ulong mhGuid) && mhGuid != 0;
                bool guidDiffers = mhGuid != mainhandBeforeGuid;
                bool bagCountDropped = CountBagItem(playerAfter, WornMace) < maceCountBeforeEquip;
                if (slotFilled && (guidDiffers || bagCountDropped))
                {
                    _output.WriteLine($"  [{label}] Equip detected after {equipSw.ElapsedMilliseconds}ms");
                    break;
                }
            }
            await Task.Delay(200);
        }

        if (playerAfter == null)
            return false;

        bool mainhandEquipped = playerAfter.Inventory.TryGetValue(MainhandSlot, out ulong mainhandAfterGuid) && mainhandAfterGuid != 0;
        int maceCountAfterEquip = CountBagItem(playerAfter, WornMace);
        bool maceMovedFromBags = maceCountAfterEquip < maceCountBeforeEquip;
        // If mainhand already had a Worn Mace (item 36), equipping another one swaps them —
        // bag count stays the same but mainhand GUID changes. Accept this as a pass.
        bool mainhandGuidChanged = mainhandAfterGuid != mainhandBeforeGuid;

        _output.WriteLine($"  [{label}] Mainhand after: {(mainhandEquipped ? $"GUID=0x{mainhandAfterGuid:X}" : "EMPTY")}");
        _output.WriteLine($"  [{label}] Worn Mace in bags before/after equip: {maceCountBeforeEquip} -> {maceCountAfterEquip}");
        _output.WriteLine($"  [{label}] Transition checks: mainhandEquipped={mainhandEquipped}, movedFromBags={maceMovedFromBags}, guidChanged={mainhandGuidChanged}");

        bool passed = mainhandEquipped && (maceMovedFromBags || mainhandGuidChanged);
        if (!passed)
            _bot.DumpSnapshotDiagnostics(after, label);

        return passed;
    }

    private static int CountBagItem(Game.WoWPlayer player, uint itemId)
        => player.BagContents.Values.Count(id => id == itemId);

}
