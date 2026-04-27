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
/// Two shapes:
///   * <see cref="EquipItem_AddWeaponAndEquip_AppearsInEquipmentSlot"/> — the
///     legacy Shodan test-director shape (load Equipment.config.json + each
///     target staged via StageBotRunnerLoadoutAsync).
///   * <see cref="EquipItem_AutomatedMode_LoadoutAppliesAndEquips"/> — the
///     F-1 Automated-mode shape (load Equipment.Automated.config.json; the
///     bot's CharacterSettings.Loadout block is dispatched as APPLY_LOADOUT
///     by AutomatedModeHandler.OnWorldEntryAsync at first IsObjectManagerValid;
///     the test body dispatches only ActionType.EquipItem and asserts).
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

    /// <summary>
    /// Phase E broader migration: same shape as the Onboarding pilot — load a
    /// wrapped-schema config with <c>Mode=Automated</c> and a per-character
    /// <c>Loadout</c> block, then assert the bot's <c>LoadoutStatus</c>
    /// transitions to <c>LoadoutReady</c> at world entry and the dispatched
    /// <c>EquipItem</c> moves the loadout-supplied Worn Mace into the mainhand
    /// slot. No fixture-side <c>StageBotRunnerLoadoutAsync</c> call.
    ///
    /// FG+BG since commit cb4fd977: <c>LearnSpellStep</c> now treats the
    /// server's "You already know this spell." system message as
    /// satisfaction, which unblocked the FG path that previously burned
    /// 20 retries on '.learn'-of-already-known-spells.
    /// </summary>
    [SkippableFact]
    public async Task EquipItem_AutomatedMode_LoadoutAppliesAndEquips()
    {
        var settingsPath = ResolveRepoPath(
            "Services", "WoWStateManager", "Settings", "Configs", "Equipment.Automated.config.json");

        await _bot.EnsureSettingsAsync(settingsPath);
        _bot.SetOutput(_output);
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsReady, _bot.FailureReason ?? "Live bot not ready");
        await _bot.AssertConfiguredCharactersMatchAsync(settingsPath);

        var targets = _bot.ResolveBotRunnerActionTargets(includeForegroundIfActionable: true);
        _output.WriteLine(
            $"[ACTION-PLAN] SHODAN {_bot.ShodanAccountName}/{_bot.ShodanCharacterName}: director only, no EquipItem dispatch.");
        foreach (var target in targets)
            _output.WriteLine(
                $"[ACTION-PLAN] {target.RoleLabel} {target.AccountName}/{target.CharacterName}: " +
                $"Automated mode applies loadout, then dispatch EquipItem({WornMace}).");

        _output.WriteLine("[PARITY] Running Automated-mode equip scenario.");

        var results = await Task.WhenAll(targets.Select(async target => (
            Target: target,
            Passed: await RunAutomatedEquipScenario(target.AccountName, target.RoleLabel))));

        foreach (var result in results)
        {
            Assert.True(
                result.Passed,
                $"{result.Target.RoleLabel} bot ({result.Target.AccountName}/{result.Target.CharacterName}): " +
                "Automated-mode loadout should apply Worn Mace, then EquipItem should move it to MAINHAND.");
        }
    }

    private async Task<bool> RunAutomatedEquipScenario(string account, string label)
    {
        // AutomatedModeHandler.OnWorldEntryAsync fires once per account at the
        // first IsObjectManagerValid=true and dispatches APPLY_LOADOUT off
        // CharacterSettings.Loadout. The bot's LoadoutTask then walks the plan
        // (SpellIdsToLearn=[198], Skills=[54/1/300], SupplementalItemIds=[36
        // Worn Mace]) and reports LoadoutReady on completion. No fixture-side
        // StageBotRunnerLoadoutAsync call is needed — this is the whole point.
        var loadoutLanded = await _bot.WaitForSnapshotConditionAsync(
            account,
            snap => snap.LoadoutStatus == LoadoutStatus.LoadoutReady
                || snap.Player?.BagContents?.Values.Any(itemId => itemId == WornMace) == true,
            TimeSpan.FromSeconds(90),
            pollIntervalMs: 500,
            progressLabel: $"automated-loadout {account}");

        if (!loadoutLanded)
        {
            await _bot.RefreshSnapshotsAsync();
            var diag = await _bot.GetSnapshotAsync(account);
            _output.WriteLine(
                $"  [{label}] Automated loadout never delivered Worn Mace within 90s. " +
                $"LoadoutStatus='{diag?.LoadoutStatus}', failureReason='{diag?.LoadoutFailureReason}'.");
            // Dump server-side system messages — '.learn'/'.additem'/'.setskill'
            // failures (e.g. "You already know this spell.", "There is no such command",
            // permission errors) flow back through the snapshot's RecentChatMessages
            // and are the single most useful signal on a stuck loadout step.
            if (diag?.RecentChatMessages?.Count > 0)
            {
                _output.WriteLine($"  [{label}] RecentChatMessages ({diag.RecentChatMessages.Count}):");
                foreach (var msg in diag.RecentChatMessages.TakeLast(20))
                    _output.WriteLine($"    {msg}");
            }
            if (diag?.RecentErrors?.Count > 0)
            {
                _output.WriteLine($"  [{label}] RecentErrors ({diag.RecentErrors.Count}):");
                foreach (var err in diag.RecentErrors.TakeLast(20))
                    _output.WriteLine($"    {err}");
            }
            return false;
        }

        if (!await WaitForBagItemAsync(account, WornMace, TimeSpan.FromSeconds(8)))
        {
            _output.WriteLine($"  [{label}] LoadoutReady but Worn Mace not visible in bags; aborting.");
            return false;
        }

        return await DispatchEquipAndAssertAsync(account, label);
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

        return await DispatchEquipAndAssertAsync(account, label);
    }

    private async Task<bool> DispatchEquipAndAssertAsync(string account, string label)
    {
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
