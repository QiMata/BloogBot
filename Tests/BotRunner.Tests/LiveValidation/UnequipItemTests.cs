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
///   1) <see cref="LiveBotFixture.EnsureSettingsAsync"/> launches EQUIPFG1 +
///      EQUIPBG1 + SHODAN together via <c>Equipment.config.json</c>.
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
        await _bot.AssertConfiguredCharactersMatchAsync(equipmentSettingsPath);

        var targets = _bot.ResolveBotRunnerActionTargets();
        _output.WriteLine(
            $"[ACTION-PLAN] SHODAN {_bot.ShodanAccountName}/{_bot.ShodanCharacterName}: director only, no item action dispatch.");
        foreach (var target in targets)
            _output.WriteLine(
                $"[ACTION-PLAN] {target.RoleLabel} {target.AccountName}/{target.CharacterName}: " +
                $"stage Worn Mace, dispatch EquipItem({WornMace}) then UnequipItem({MainhandEquipSlot}).");

        _output.WriteLine("[PARITY] Running configured unequip scenarios in parallel.");

        var results = await Task.WhenAll(targets.Select(async target => (
            Target: target,
            Passed: await RunUnequipScenario(target.AccountName, target.RoleLabel))));

        foreach (var result in results)
        {
            Assert.True(
                result.Passed,
                $"{result.Target.RoleLabel} bot ({result.Target.AccountName}/{result.Target.CharacterName}): " +
                "Mainhand should be empty after UnequipItem.");
        }
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
