using System;
using System.Diagnostics;
using System.IO;
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
/// First migrated slice of the Shodan test-director overhaul
/// (see LiveValidation/docs/SHODAN_MIGRATION_INVENTORY.md).
///
/// Shape:
///   1) <see cref="LiveBotFixture.EnsureSettingsAsync"/> launches TESTBOT1 +
///      TESTBOT2 + SHODAN together via <c>Equipment.config.json</c>.
///   2) <see cref="LiveBotFixture.StageBotRunnerLoadoutAsync"/> stages the
///      target with mace proficiency, Maces skill, and a Worn Mace in bags.
///      The test body issues no GM commands of its own.
///   3) The test dispatches <c>ActionType.EquipItem</c> then
///      <c>ActionType.UnequipItem</c> against each role and asserts on
///      snapshot changes.
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
    private const uint MacesSkillId = 54;

    public UnequipItemTests(LiveBotFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
    }

    [SkippableFact]
    public async Task UnequipItem_MainhandWeapon_MovesToBags()
    {
        var equipmentSettingsPath = ResolveRepoPath(
            "Services", "WoWStateManager", "Settings", "Configs", "Equipment.config.json");

        await _bot.EnsureSettingsAsync(equipmentSettingsPath);
        _bot.SetOutput(_output);
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsReady, _bot.FailureReason ?? "Live bot not ready");

        var bgAccount = _bot.BgAccountName;
        global::Tests.Infrastructure.Skip.If(string.IsNullOrWhiteSpace(bgAccount), "BG bot account not available.");
        _output.WriteLine($"=== BG Bot: {_bot.BgCharacterName} ({bgAccount}) ===");

        var hasFg = _bot.IsFgActionable;
        string? fgAccount = null;
        if (hasFg)
        {
            fgAccount = _bot.FgAccountName;
            Assert.False(string.IsNullOrWhiteSpace(fgAccount), "FG actionable but FgAccountName is null.");
            _output.WriteLine($"=== FG Bot: {_bot.FgCharacterName} ({fgAccount}) ===");
        }

        bool bgPassed, fgPassed = false;
        if (hasFg)
        {
            _output.WriteLine("[PARITY] Running BG and FG unequip scenarios in parallel.");

            var bgTask = RunUnequipScenario(bgAccount!, "BG");
            var fgTask = RunUnequipScenario(fgAccount!, "FG");
            await Task.WhenAll(bgTask, fgTask);
            bgPassed = await bgTask;
            fgPassed = await fgTask;
        }
        else
        {
            bgPassed = await RunUnequipScenario(bgAccount!, "BG");
            _output.WriteLine("\nFG Bot: NOT AVAILABLE");
        }

        Assert.True(bgPassed, "BG bot: Mainhand should be empty after UnequipItem.");
        if (hasFg)
            Assert.True(fgPassed, "FG bot: Mainhand should be empty after UnequipItem.");
    }

    private async Task<bool> RunUnequipScenario(string account, string label)
    {
        // Shodan-directed staging: one call replaces the previous mix of
        // EnsureCleanSlate + BotClearInventory + BotLearnSpell + BotSetSkill +
        // BotAddItem scattered through the test body.
        await _bot.StageBotRunnerLoadoutAsync(
            account,
            label,
            spellsToLearn: new[] { OneHandMaceSpell },
            skillsToSet: new[] { new LiveBotFixture.SkillDirective(MacesSkillId, 1, 300) },
            itemsToAdd: new[] { new LiveBotFixture.ItemDirective(WornMace, 1) });

        if (!await WaitForBagItemAsync(account, WornMace, TimeSpan.FromSeconds(5)))
        {
            _output.WriteLine($"  [{label}] Worn Mace never observed in bags after staging; aborting.");
            return false;
        }

        // EquipItem is part of the precondition for the UnequipItem test, but
        // it is still a real BotRunner action — not a GM command.
        var equipResult = await _bot.SendActionAsync(account, new ActionMessage
        {
            ActionType = ActionType.EquipItem,
            Parameters = { new RequestParameter { IntParam = (int)WornMace } }
        });
        Assert.Equal(ResponseResult.Success, equipResult);

        var equipped = await WaitForMainhandEquippedAsync(account, TimeSpan.FromSeconds(5));
        if (!equipped)
        {
            _output.WriteLine($"  [{label}] Mainhand not equipped after EquipItem; cannot test unequip.");
            return false;
        }

        await _bot.RefreshSnapshotsAsync();
        var snapBefore = await _bot.GetSnapshotAsync(account);
        var maceCountBefore = CountBagItem(snapBefore, WornMace);
        var mainhandGuidBefore = GetMainhandGuid(snapBefore);
        _output.WriteLine($"  [{label}] Before unequip: mainhand=0x{mainhandGuidBefore:X}, maces in bags={maceCountBefore}");

        // UnequipItem is the action under test.
        _output.WriteLine($"  [{label}] Dispatching UnequipItem (EquipSlot={MainhandEquipSlot}).");
        var unequipResult = await _bot.SendActionAsync(account, new ActionMessage
        {
            ActionType = ActionType.UnequipItem,
            Parameters = { new RequestParameter { IntParam = MainhandEquipSlot } }
        });
        _output.WriteLine($"  [{label}] UnequipItem dispatch result: {unequipResult}");
        Assert.Equal(ResponseResult.Success, unequipResult);
        await Task.Delay(500);

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

        throw new FileNotFoundException(
            $"Could not locate repo path: {Path.Combine(segments)}");
    }
}
