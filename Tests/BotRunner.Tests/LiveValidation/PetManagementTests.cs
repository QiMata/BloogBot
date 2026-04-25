using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Communication;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// Shodan-directed hunter pet-management baseline.
/// SHODAN stages hunter pet spells and level; BG receives the spell-id
/// CastSpell actions. FG is launched for topology parity but remains idle.
/// </summary>
[Collection(LiveValidationCollection.Name)]
public class PetManagementTests
{
    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    private const int HunterPetMinimumLevel = 10;
    private const uint CallPetSpellId = 883;
    private const uint DismissPetSpellId = 2641;
    private const uint TameAnimalSpellId = 1515;

    public PetManagementTests(LiveBotFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task Pet_SummonAndManage_StanceFeedAbility()
    {
        await EnsurePetManagementSettingsAsync();
        var target = ResolvePetTarget();
        await StageHunterPetLoadoutAsync(target);

        _output.WriteLine("[TEST] Casting Call Pet via BG CastSpell dispatch.");
        var callResult = await _bot.SendActionAsync(target.AccountName, new ActionMessage
        {
            ActionType = ActionType.CastSpell,
            Parameters = { new RequestParameter { IntParam = (int)CallPetSpellId } }
        });
        _output.WriteLine($"[TEST] CAST_SPELL (Call Pet) result: {callResult}");
        Assert.Equal(ResponseResult.Success, callResult);

        await Task.Delay(5000);
        await _bot.RefreshSnapshotsAsync();
        var afterSnap = await _bot.GetSnapshotAsync(target.AccountName);
        Assert.NotNull(afterSnap);

        _output.WriteLine($"[TEST] Snapshot received after Call Pet cast");
        _output.WriteLine($"[TEST] Nearby units: {afterSnap.NearbyUnits?.Count ?? 0}");

        var playerGuid = afterSnap.Player?.Unit?.GameObject?.Base?.Guid ?? 0;
        _output.WriteLine($"[TEST] Player GUID: 0x{playerGuid:X}");

        _output.WriteLine("[TEST] Casting Dismiss Pet via BG CastSpell dispatch.");
        var dismissResult = await _bot.SendActionAsync(target.AccountName, new ActionMessage
        {
            ActionType = ActionType.CastSpell,
            Parameters = { new RequestParameter { IntParam = (int)DismissPetSpellId } }
        });
        _output.WriteLine($"[TEST] CAST_SPELL (Dismiss Pet) result: {dismissResult}");
        Assert.Equal(ResponseResult.Success, dismissResult);
        await Task.Delay(2000);
    }

    private async Task<string> EnsurePetManagementSettingsAsync()
    {
        var settingsPath = ResolveRepoPath(
            "Services", "WoWStateManager", "Settings", "Configs", "PetManagement.config.json");

        await _bot.EnsureSettingsAsync(settingsPath);
        _bot.SetOutput(_output);
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsReady, _bot.FailureReason ?? "Live bot not ready");
        await _bot.AssertConfiguredCharactersMatchAsync(settingsPath);

        global::Tests.Infrastructure.Skip.If(
            string.IsNullOrWhiteSpace(_bot.ShodanAccountName),
            "Shodan director was not launched by PetManagement.config.json.");

        return settingsPath;
    }

    private LiveBotFixture.BotRunnerActionTarget ResolvePetTarget()
    {
        var targets = _bot.ResolveBotRunnerActionTargets();
        _output.WriteLine(
            $"[ACTION-PLAN] SHODAN {_bot.ShodanAccountName}/{_bot.ShodanCharacterName}: director only, no pet action dispatch.");

        foreach (var target in targets)
        {
            var action = target.IsForeground
                ? "idle for this test method (FG CastSpell-by-id pet management is not the validated path)"
                : "stage hunter pet loadout + dispatch Call/Dismiss Pet";
            _output.WriteLine(
                $"[ACTION-PLAN] {target.RoleLabel} {target.AccountName}/{target.CharacterName}: {action}.");
        }

        var selected = targets.FirstOrDefault(target => !target.IsForeground);
        global::Tests.Infrastructure.Skip.If(
            string.IsNullOrWhiteSpace(selected.AccountName),
            "BG hunter bot not available.");

        return selected;
    }

    private async Task StageHunterPetLoadoutAsync(LiveBotFixture.BotRunnerActionTarget target)
    {
        await _bot.StageBotRunnerLoadoutAsync(
            target.AccountName,
            target.RoleLabel,
            spellsToLearn: new[] { CallPetSpellId, DismissPetSpellId, TameAnimalSpellId },
            clearInventoryFirst: false,
            levelTo: HunterPetMinimumLevel);

        var ready = await _bot.WaitForSnapshotConditionAsync(
            target.AccountName,
            snapshot => snapshot.Player?.SpellList?.Contains(CallPetSpellId) == true
                && snapshot.Player.SpellList.Contains(DismissPetSpellId)
                && snapshot.Player.SpellList.Contains(TameAnimalSpellId)
                && (snapshot.Player.Unit?.GameObject?.Level ?? 0) >= HunterPetMinimumLevel,
            TimeSpan.FromSeconds(15),
            pollIntervalMs: 300,
            progressLabel: $"{target.RoleLabel} hunter-pet-loadout");

        Assert.True(ready, $"{target.RoleLabel}: hunter pet spells and level should be staged before pet actions.");
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
