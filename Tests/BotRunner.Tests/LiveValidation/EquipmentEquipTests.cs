using System;
<<<<<<< HEAD
=======
using System.Diagnostics;
>>>>>>> cpp_physics_system
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
<<<<<<< HEAD
[RequiresMangosStack]
=======
>>>>>>> cpp_physics_system
[Collection(LiveValidationCollection.Name)]
public class EquipmentEquipTests
{
    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

<<<<<<< HEAD
    private const uint WornMace = 36;
    private const uint MainhandSlot = 15;
    private const uint OneHandMaceSpell = 198;

    private const uint PlayerFlagGhost = 0x10; // PLAYER_FLAGS_GHOST
    private const uint StandStateMask = 0xFF;
    private const uint StandStateDead = 7; // UNIT_STAND_STATE_DEAD

=======
    private const uint MainhandSlot = 15;
    private const uint OneHandMaceSpell = 198;

>>>>>>> cpp_physics_system
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
<<<<<<< HEAD
        bool bgPassed = await RunEquipScenario(bgAccount, "BG");

        bool fgPassed = false;
        if (_bot.ForegroundBot != null)
        {
            var fgAccount = _bot.FgAccountName!;
            Assert.NotNull(fgAccount);
            _output.WriteLine($"\n=== FG Bot: {_bot.FgCharacterName} ({fgAccount}) ===");
            fgPassed = await RunEquipScenario(fgAccount, "FG");
        }
        else
        {
            _output.WriteLine("\nFG Bot: NOT AVAILABLE (WoW.exe not running or injection failed)");
        }

        Assert.True(bgPassed, "BG bot: Worn Mace should move from bag snapshot to MAINHAND slot.");
        if (_bot.ForegroundBot != null)
=======

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
>>>>>>> cpp_physics_system
            Assert.True(fgPassed, "FG bot: Worn Mace should move from bag snapshot to MAINHAND slot.");
    }

    private async Task<bool> RunEquipScenario(string account, string label)
    {
<<<<<<< HEAD
=======
        // Standardized setup (BT-SETUP-001): revive + safe zone + GM on
        await _bot.EnsureCleanSlateAsync(account, label);

>>>>>>> cpp_physics_system
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(account);
        if (snap?.Player == null)
            return false;

<<<<<<< HEAD
        // Strict-alive guard to avoid dead-state GM command rejections.
        if (!IsStrictAlive(snap))
        {
            _output.WriteLine($"  [{label}] Not strict-alive; reviving before equipment setup.");
            await _bot.RevivePlayerAsync(snap.CharacterName);
            await Task.Delay(2000);
            await _bot.RefreshSnapshotsAsync();
            snap = await _bot.GetSnapshotAsync(account) ?? snap;
            if (snap.Player == null)
                return false;
        }

        var playerBefore = snap.Player;
        bool mainhandBeforeEquipped = playerBefore.Inventory.TryGetValue(MainhandSlot, out ulong mainhandBeforeGuid) && mainhandBeforeGuid != 0;
        int maceCountBeforeSetup = CountBagItem(playerBefore, WornMace);
=======
        var playerBefore = snap.Player;
        bool mainhandBeforeEquipped = playerBefore.Inventory.TryGetValue(MainhandSlot, out ulong mainhandBeforeGuid) && mainhandBeforeGuid != 0;
        int maceCountBeforeSetup = CountBagItem(playerBefore, LiveBotFixture.TestItems.WornMace);
>>>>>>> cpp_physics_system

        _output.WriteLine($"  [{label}] Mainhand before: {(mainhandBeforeEquipped ? $"GUID=0x{mainhandBeforeGuid:X}" : "EMPTY")}");
        _output.WriteLine($"  [{label}] Worn Mace count in bags before setup: {maceCountBeforeSetup}");

<<<<<<< HEAD
        // Learn proficiency only if missing.
=======
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
            maceCountBeforeSetup = CountBagItem(playerBefore, LiveBotFixture.TestItems.WornMace);
            _output.WriteLine($"  [{label}] Mainhand after reset: {(mainhandBeforeEquipped ? $"GUID=0x{mainhandBeforeGuid:X}" : "EMPTY")}");
        }

        // Grant mace proficiency: .learn adds the spell, .setskill adds the weapon skill.
        // .setskill requires a selected target, so BotSetSkillAsync auto-selects self first.
>>>>>>> cpp_physics_system
        bool hasMaceProficiency = playerBefore.SpellList.Contains(OneHandMaceSpell);
        if (!hasMaceProficiency)
        {
            _output.WriteLine($"  [{label}] Learning missing 1H mace proficiency (spell {OneHandMaceSpell}).");
<<<<<<< HEAD
            await _bot.SendGmChatCommandAsync(account, ".gm on");
            await _bot.BotLearnSpellAsync(account, OneHandMaceSpell);
            await Task.Delay(1200);
            await _bot.RefreshSnapshotsAsync();
            snap = await _bot.GetSnapshotAsync(account) ?? snap;
=======
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
>>>>>>> cpp_physics_system
            if (snap.Player == null)
                return false;
            playerBefore = snap.Player;
        }

        // Ensure at least one Worn Mace is present in bags.
<<<<<<< HEAD
        int maceCountBeforeEquip = CountBagItem(playerBefore, WornMace);
=======
        int maceCountBeforeEquip = CountBagItem(playerBefore, LiveBotFixture.TestItems.WornMace);
>>>>>>> cpp_physics_system
        if (maceCountBeforeEquip == 0)
        {
            var bagItemCount = playerBefore.BagContents.Count;
            if (bagItemCount >= 15)
            {
                _output.WriteLine($"  [{label}] Bag nearly full ({bagItemCount}); clearing inventory before additem.");
                await _bot.BotClearInventoryAsync(account, includeExtraBags: false);
<<<<<<< HEAD
                await Task.Delay(1200);
            }

            _output.WriteLine($"  [{label}] Adding Worn Mace (item {WornMace}).");
            await _bot.BotAddItemAsync(account, WornMace);
            await Task.Delay(1800);
            await _bot.RefreshSnapshotsAsync();
            snap = await _bot.GetSnapshotAsync(account) ?? snap;
            if (snap.Player == null)
                return false;
            playerBefore = snap.Player;
            maceCountBeforeEquip = CountBagItem(playerBefore, WornMace);
=======
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

            _output.WriteLine($"  [{label}] Adding Worn Mace (item {LiveBotFixture.TestItems.WornMace}).");
            await _bot.BotAddItemAsync(account, LiveBotFixture.TestItems.WornMace);
            var addItemSw = Stopwatch.StartNew();
            // SOAP .additem can take 10+ seconds to propagate through the
            // server → SMSG_UPDATE_OBJECT → BG client pipeline.
            while (addItemSw.Elapsed < TimeSpan.FromSeconds(15))
            {
                await Task.Delay(200);
                await _bot.RefreshSnapshotsAsync();
                snap = await _bot.GetSnapshotAsync(account) ?? snap;
                if (snap.Player != null && CountBagItem(snap.Player, LiveBotFixture.TestItems.WornMace) > 0)
                    break;
            }
            if (snap.Player == null)
                return false;
            playerBefore = snap.Player;
            maceCountBeforeEquip = CountBagItem(playerBefore, LiveBotFixture.TestItems.WornMace);
>>>>>>> cpp_physics_system
        }

        if (maceCountBeforeEquip == 0)
        {
            _output.WriteLine($"  [{label}] Worn Mace was not present after setup; cannot validate equip.");
            _bot.DumpSnapshotDiagnostics(snap, label);
            return false;
        }

<<<<<<< HEAD
        // Equip and verify transition.
        _output.WriteLine($"  [{label}] Equipping Worn Mace.");
        await _bot.SendActionAndWaitAsync(account, new ActionMessage
        {
            ActionType = ActionType.EquipItem,
            Parameters = { new RequestParameter { IntParam = (int)WornMace } }
        }, delayMs: 4000);

        await _bot.RefreshSnapshotsAsync();
        var after = await _bot.GetSnapshotAsync(account);
        var playerAfter = after?.Player;
=======
        // GM mode stays ON — equip actions work with GM mode enabled.
        // Previous .gm off here corrupted GM state for downstream tests and risked BG disconnect.

        // Equip and verify transition — poll for mainhand slot change instead of fixed delay.
        _output.WriteLine($"  [{label}] Equipping Worn Mace.");
        var equipResult = await _bot.SendActionAsync(account, new ActionMessage
        {
            ActionType = ActionType.EquipItem,
            Parameters = { new RequestParameter { IntParam = (int)LiveBotFixture.TestItems.WornMace } }
        });
        Assert.Equal(ResponseResult.Success, equipResult);
        await Task.Delay(500);

        WoWActivitySnapshot? after = null;
        Game.WoWPlayer? playerAfter = null;
        var equipSw = Stopwatch.StartNew();
        while (equipSw.Elapsed < TimeSpan.FromSeconds(8))
        {
            await _bot.RefreshSnapshotsAsync();
            after = await _bot.GetSnapshotAsync(account);
            playerAfter = after?.Player;
            if (playerAfter != null)
            {
                bool slotFilled = playerAfter.Inventory.TryGetValue(MainhandSlot, out ulong mhGuid) && mhGuid != 0;
                bool guidDiffers = mhGuid != mainhandBeforeGuid;
                bool bagCountDropped = CountBagItem(playerAfter, LiveBotFixture.TestItems.WornMace) < maceCountBeforeEquip;
                if (slotFilled && (guidDiffers || bagCountDropped))
                {
                    _output.WriteLine($"  [{label}] Equip detected after {equipSw.ElapsedMilliseconds}ms");
                    break;
                }
            }
            await Task.Delay(200);
        }

>>>>>>> cpp_physics_system
        if (playerAfter == null)
            return false;

        bool mainhandEquipped = playerAfter.Inventory.TryGetValue(MainhandSlot, out ulong mainhandAfterGuid) && mainhandAfterGuid != 0;
<<<<<<< HEAD
        int maceCountAfterEquip = CountBagItem(playerAfter, WornMace);
        bool maceMovedFromBags = maceCountAfterEquip < maceCountBeforeEquip;

        _output.WriteLine($"  [{label}] Mainhand after: {(mainhandEquipped ? $"GUID=0x{mainhandAfterGuid:X}" : "EMPTY")}");
        _output.WriteLine($"  [{label}] Worn Mace in bags before/after equip: {maceCountBeforeEquip} -> {maceCountAfterEquip}");
        _output.WriteLine($"  [{label}] Transition checks: mainhandEquipped={mainhandEquipped}, movedFromBags={maceMovedFromBags}");

        if (!mainhandEquipped || !maceMovedFromBags)
            _bot.DumpSnapshotDiagnostics(after, label);

        return mainhandEquipped && maceMovedFromBags;
=======
        int maceCountAfterEquip = CountBagItem(playerAfter, LiveBotFixture.TestItems.WornMace);
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
>>>>>>> cpp_physics_system
    }

    private static int CountBagItem(Game.WoWPlayer player, uint itemId)
        => player.BagContents.Values.Count(id => id == itemId);

<<<<<<< HEAD
    private static bool IsStrictAlive(WoWActivitySnapshot? snap)
    {
        var player = snap?.Player;
        var unit = player?.Unit;
        if (player == null || unit == null)
            return false;

        var hasGhostFlag = (player.PlayerFlags & PlayerFlagGhost) != 0;
        var standState = unit.Bytes1 & StandStateMask;
        return unit.Health > 0 && !hasGhostFlag && standState != StandStateDead;
    }
=======
>>>>>>> cpp_physics_system
}
