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
/// Equipment equip integration test.
///
/// Migrated to the Shodan test-director shape:
///   1) launch EQUIPFG1 + EQUIPBG1 + SHODAN with Equipment.config.json,
///   2) stage each BotRunner target through StageBotRunnerLoadoutAsync,
///   3) dispatch only ActionType.EquipItem from the test body,
///   4) assert on snapshot inventory/equipment changes.
/// </summary>
[Collection(LiveValidationCollection.Name)]
public class EquipmentEquipTests
{
    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    private const uint WornMace = LiveBotFixture.TestItems.WornMace;
    private const uint MainhandSlot = 15;
    private const uint OneHandMaceSpell = 198;
    private const uint MacesSkillId = 54;

    public EquipmentEquipTests(LiveBotFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
    }

    [SkippableFact]
    public async Task EquipItem_AddWeaponAndEquip_AppearsInEquipmentSlot()
    {
        var equipmentSettingsPath = ResolveRepoPath(
            "Services", "WoWStateManager", "Settings", "Configs", "Equipment.config.json");

        await _bot.EnsureSettingsAsync(equipmentSettingsPath);
        _bot.SetOutput(_output);
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsReady, _bot.FailureReason ?? "Live bot not ready");
        await _bot.AssertConfiguredCharactersMatchAsync(equipmentSettingsPath);

        var targets = _bot.ResolveBotRunnerActionTargets();
        _output.WriteLine(
            $"[ACTION-PLAN] SHODAN {_bot.ShodanAccountName}/{_bot.ShodanCharacterName}: director only, no EquipItem dispatch.");
        foreach (var target in targets)
            _output.WriteLine(
                $"[ACTION-PLAN] {target.RoleLabel} {target.AccountName}/{target.CharacterName}: " +
                $"stage Worn Mace, dispatch EquipItem({WornMace}).");

        _output.WriteLine("[PARITY] Running configured equip scenarios in parallel.");

        var results = await Task.WhenAll(targets.Select(async target => (
            Target: target,
            Passed: await RunEquipScenario(target.AccountName, target.RoleLabel))));

        foreach (var result in results)
        {
            Assert.True(
                result.Passed,
                $"{result.Target.RoleLabel} bot ({result.Target.AccountName}/{result.Target.CharacterName}): " +
                "Worn Mace should move from bag snapshot to MAINHAND slot.");
        }
    }

    private async Task<bool> RunEquipScenario(string account, string label)
    {
        await _bot.StageBotRunnerLoadoutAsync(
            account,
            label,
            spellsToLearn: new[] { OneHandMaceSpell },
            skillsToSet: new[] { new LiveBotFixture.SkillDirective(MacesSkillId, 1, 300) },
            itemsToAdd: new[] { new LiveBotFixture.ItemDirective(WornMace, 1) });

        if (!await WaitForBagItemAsync(account, WornMace, TimeSpan.FromSeconds(8)))
        {
            _output.WriteLine($"  [{label}] Worn Mace never observed in bags after staging; aborting.");
            return false;
        }

        await _bot.RefreshSnapshotsAsync();
        var before = await _bot.GetSnapshotAsync(account);
        if (before?.Player == null)
            return false;

        var mainhandBeforeGuid = GetMainhandGuid(before);
        var maceCountBeforeEquip = CountBagItem(before, WornMace);
        _output.WriteLine(
            $"  [{label}] Before equip: mainhand=0x{mainhandBeforeGuid:X}, maces in bags={maceCountBeforeEquip}");

        _output.WriteLine($"  [{label}] Dispatching EquipItem for Worn Mace ({WornMace}).");
        var equipResult = await _bot.SendActionAsync(account, new ActionMessage
        {
            ActionType = ActionType.EquipItem,
            Parameters = { new RequestParameter { IntParam = (int)WornMace } }
        });
        Assert.Equal(ResponseResult.Success, equipResult);

        WoWActivitySnapshot? after = null;
        var equipped = false;
        var equipSw = Stopwatch.StartNew();
        while (equipSw.Elapsed < TimeSpan.FromSeconds(8))
        {
            await _bot.RefreshSnapshotsAsync();
            after = await _bot.GetSnapshotAsync(account);
            if (after?.Player != null)
            {
                var mainhandAfterGuid = GetMainhandGuid(after);
                var maceCountAfterEquip = CountBagItem(after, WornMace);
                var mainhandEquipped = mainhandAfterGuid != 0;
                var mainhandGuidChanged = mainhandAfterGuid != mainhandBeforeGuid;
                var maceMovedFromBags = maceCountAfterEquip < maceCountBeforeEquip;

                if (mainhandEquipped && (mainhandGuidChanged || maceMovedFromBags))
                {
                    _output.WriteLine(
                        $"  [{label}] Equip detected after {equipSw.ElapsedMilliseconds}ms: " +
                        $"mainhand=0x{mainhandAfterGuid:X}, maces in bags={maceCountAfterEquip}");
                    equipped = true;
                    break;
                }
            }

            await Task.Delay(200);
        }

        if (!equipped)
        {
            _output.WriteLine($"  [{label}] Equip transition not observed within timeout.");
            _bot.DumpSnapshotDiagnostics(after, label);
        }

        return equipped;
    }

    private static ulong GetMainhandGuid(WoWActivitySnapshot? snap)
    {
        if (snap?.Player?.Inventory.TryGetValue(MainhandSlot, out var guid) == true)
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
            if (CountBagItem(snap, itemId) > 0)
                return true;

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

        throw new FileNotFoundException($"Could not locate repo path: {Path.Combine(segments)}");
    }
}
