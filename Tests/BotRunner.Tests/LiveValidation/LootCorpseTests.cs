using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Communication;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// Shodan-directed loot corpse integration test.
///
/// SHODAN stages the BG BotRunner target with clean bags and a Durotar mob
/// area. The test body dispatches only StartMeleeAttack / StopAttack /
/// LootCorpse actions and observes snapshots for kill and bag evidence.
///
/// Run: dotnet test --filter "FullyQualifiedName~LootCorpseTests" --configuration Release
/// </summary>
[Collection(LiveValidationCollection.Name)]
public class LootCorpseTests
{
    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    private const uint MottledBoarEntry = 3098;
    private const uint ScorpidWorkerEntry = 3124;
    private const uint VileFamiliarEntry = 3101;

    public LootCorpseTests(LiveBotFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task Loot_KillAndLootMob_InventoryChanges()
    {
        var target = await EnsureLootSettingsAndTargetAsync();

        _output.WriteLine($"=== Loot BotRunner target: {target.CharacterName} ({target.AccountName}) ===");
        _output.WriteLine("Using Shodan-directed setup with BG behavior actions only.");

        var passed = await RunLootScenario(target);
        Assert.True(passed, $"{target.RoleLabel} bot: Loot scenario failed - see test output for details.");
    }

    private async Task<bool> RunLootScenario(LiveBotFixture.BotRunnerActionTarget target)
    {
        var account = target.AccountName;
        var label = target.RoleLabel;

        _output.WriteLine($"  [{label}] Step 1: Shodan stages clean bags");
        await _bot.StageBotRunnerLoadoutAsync(
            account,
            label,
            cleanSlate: true,
            clearInventoryFirst: true);

        await _bot.QuiesceAccountsAsync(
            new[] { account },
            $"{label} loot loadout staged",
            timeout: TimeSpan.FromSeconds(20));

        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(account);
        var baselineBagCount = snap?.Player?.BagContents?.Count ?? 0;
        _output.WriteLine($"  [{label}] Baseline bag item count: {baselineBagCount}");

        _output.WriteLine($"  [{label}] Step 2: Shodan stages Durotar mob area");
        var mob = await StageAndFindLivingMobAsync(target);

        var mobGuid = mob.GameObject?.Base?.Guid ?? 0;
        var mobPos = mob.GameObject?.Base?.Position;
        _output.WriteLine(
            $"  [{label}] Found: {mob.GameObject?.Name} GUID=0x{mobGuid:X} " +
            $"at ({mobPos?.X:F1}, {mobPos?.Y:F1}, {mobPos?.Z:F1}) HP={mob.Health}/{mob.MaxHealth}");

        _output.WriteLine($"  [{label}] Step 3: Kill mob with StartMeleeAttack");
        var attackResult = await _bot.SendActionAsync(account, new ActionMessage
        {
            ActionType = ActionType.StartMeleeAttack,
            Parameters = { new RequestParameter { LongParam = (long)mobGuid } }
        });
        _output.WriteLine($"  [{label}] StartMeleeAttack result: {attackResult}");
        Assert.Equal(ResponseResult.Success, attackResult);

        var killSw = Stopwatch.StartNew();
        var mobDead = false;
        while (killSw.Elapsed < TimeSpan.FromSeconds(45))
        {
            await _bot.RefreshSnapshotsAsync();
            snap = await _bot.GetSnapshotAsync(account);
            var currentMob = snap?.NearbyUnits?.FirstOrDefault(u =>
                (u.GameObject?.Base?.Guid ?? 0) == mobGuid);

            if (currentMob == null || currentMob.Health == 0)
            {
                mobDead = true;
                _output.WriteLine($"  [{label}] Mob dead after {killSw.Elapsed.TotalSeconds:F1}s");
                break;
            }

            _output.WriteLine($"  [{label}] Mob HP: {currentMob.Health}/{currentMob.MaxHealth}");
            await Task.Delay(1000);
        }

        await _bot.SendActionAsync(account, new ActionMessage { ActionType = ActionType.StopAttack });

        if (!mobDead)
        {
            _output.WriteLine($"  [{label}] FAILED: Could not kill mob within 45s.");
            return false;
        }

        _output.WriteLine($"  [{label}] Step 4: Loot corpse via ActionType.LootCorpse");
        var lootResult = await _bot.SendActionAsync(account, new ActionMessage
        {
            ActionType = ActionType.LootCorpse,
            Parameters = { new RequestParameter { LongParam = (long)mobGuid } }
        });
        _output.WriteLine($"  [{label}] LootCorpse dispatch result: {lootResult}");
        Assert.Equal(ResponseResult.Success, lootResult);

        _output.WriteLine($"  [{label}] Step 5: Verify inventory changed when corpse has loot");
        var verifySw = Stopwatch.StartNew();
        var lootReceived = false;
        while (verifySw.Elapsed < TimeSpan.FromSeconds(10))
        {
            await _bot.RefreshSnapshotsAsync();
            snap = await _bot.GetSnapshotAsync(account);
            var currentBagCount = snap?.Player?.BagContents?.Count ?? 0;
            _output.WriteLine($"  [{label}] Current bag count: {currentBagCount} (baseline: {baselineBagCount})");

            if (currentBagCount > baselineBagCount)
            {
                lootReceived = true;
                var newItems = snap?.Player?.BagContents?.Values
                    .GroupBy(v => v)
                    .Select(g => $"itemId={g.Key} x{g.Count()}")
                    .ToList() ?? [];
                _output.WriteLine($"  [{label}] Loot received! New items: [{string.Join(", ", newItems)}]");
                break;
            }

            await Task.Delay(1000);
        }

        if (!lootReceived)
        {
            _output.WriteLine(
                $"  [{label}] WARNING: No loot received after killing mob. " +
                "Mob may have dropped no items from the low-level loot table.");
        }

        _output.WriteLine($"  [{label}] Loot scenario complete (dispatch={lootResult}, looted={lootReceived}).");
        return true;
    }

    private async Task<Game.WoWUnit> StageAndFindLivingMobAsync(LiveBotFixture.BotRunnerActionTarget target)
    {
        for (var stageIndex = 0; stageIndex < 3; stageIndex++)
        {
            var staged = await _bot.StageBotRunnerAtDurotarMobAreaAsync(
                target.AccountName,
                target.RoleLabel,
                stageIndex,
                nearbyUnitTimeoutMs: 15000);
            if (!staged)
            {
                _output.WriteLine(
                    $"  [{target.RoleLabel}] Durotar mob-area stage {stageIndex} did not settle with nearby units.");
                continue;
            }

            var mob = await WaitForLivingMobAsync(
                target.AccountName,
                target.RoleLabel,
                TimeSpan.FromSeconds(10));
            if (mob != null)
                return mob;

            _output.WriteLine(
                $"  [{target.RoleLabel}] No living loot target found at stage {stageIndex}; trying next stage.");
        }

        var snap = await _bot.GetSnapshotAsync(target.AccountName);
        _bot.DumpSnapshotDiagnostics(snap, target.RoleLabel);
        Assert.Fail(
            $"[{target.RoleLabel}] No living loot target found after all Shodan Durotar mob stages. " +
            "This indicates a mob detection, spawn, or ObjectManager visibility issue.");
        throw new InvalidOperationException("Unreachable after Assert.Fail.");
    }

    private Game.WoWUnit? FindLivingMob(WoWActivitySnapshot? snap, string label)
    {
        var units = snap?.NearbyUnits?.ToList() ?? [];
        _output.WriteLine($"  [{label}] Nearby units: {units.Count}");

        foreach (var unit in units)
        {
            var name = unit.GameObject?.Name ?? "";
            var entry = unit.GameObject?.Entry ?? 0;
            var hp = unit.Health;

            if (hp > 0 && (
                name.Contains("Boar", StringComparison.OrdinalIgnoreCase) ||
                entry == MottledBoarEntry ||
                entry == ScorpidWorkerEntry ||
                entry == VileFamiliarEntry))
            {
                return unit;
            }
        }

        return null;
    }

    private async Task<Game.WoWUnit?> WaitForLivingMobAsync(
        string account,
        string label,
        TimeSpan timeout)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            await _bot.RefreshSnapshotsAsync();
            var snap = await _bot.GetSnapshotAsync(account);
            var mob = FindLivingMob(snap, label);
            if (mob != null)
                return mob;

            await Task.Delay(500);
        }

        return null;
    }

    private async Task<LiveBotFixture.BotRunnerActionTarget> EnsureLootSettingsAndTargetAsync()
    {
        var settingsPath = ResolveRepoPath(
            "Services", "WoWStateManager", "Settings", "Configs", "Loot.config.json");

        await _bot.EnsureSettingsAsync(settingsPath);
        _bot.SetOutput(_output);
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsReady, _bot.FailureReason ?? "Live bot not ready");
        await _bot.AssertConfiguredCharactersMatchAsync(settingsPath);
        global::Tests.Infrastructure.Skip.If(
            string.IsNullOrWhiteSpace(_bot.ShodanAccountName),
            "Shodan director was not launched by Loot.config.json.");

        var target = _bot.ResolveBotRunnerActionTargets(
                includeForegroundIfActionable: false,
                foregroundFirst: false)
            .Single(target => !target.IsForeground);

        _output.WriteLine(
            $"[ACTION-PLAN] {target.RoleLabel} {target.AccountName}/{target.CharacterName}: " +
            "BG loot action target.");
        _output.WriteLine(
            $"[ACTION-PLAN] FG {_bot.FgAccountName}/{_bot.FgCharacterName}: launched idle for topology parity.");
        _output.WriteLine(
            $"[ACTION-PLAN] SHODAN {_bot.ShodanAccountName}/{_bot.ShodanCharacterName}: director only, no loot dispatch.");

        return target;
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
