using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Communication;
using GameData.Core.Enums;
using Microsoft.Extensions.Logging;

namespace BotRunner.Tests.LiveValidation;

public partial class LiveBotFixture
{
    private readonly SemaphoreSlim _shodanDirectedCommandSemaphore = new(1, 1);
    private readonly record struct DurotarMobStage(int MapId, float X, float Y, float Z);

    private static readonly DurotarMobStage[] DurotarMobStages =
    [
        new(1, -620f, -4385f, 44f),
        new(1, -555f, -4385f, 45f),
        new(1, -515f, -4415f, 52f),
    ];

    /// <summary>
    /// Declarative skill directive for <see cref="StageBotRunnerLoadoutAsync"/>.
    /// Matches the <c>.setskill &lt;id&gt; &lt;cur&gt; &lt;max&gt;</c> signature.
    /// </summary>
    public readonly record struct SkillDirective(uint SkillId, int CurrentValue, int MaxValue);

    /// <summary>
    /// Declarative item directive for <see cref="StageBotRunnerLoadoutAsync"/>.
    /// Matches the <c>.additem &lt;id&gt; &lt;count&gt;</c> signature.
    /// </summary>
    public readonly record struct ItemDirective(uint ItemId, int Count);

    /// <summary>
    /// Explicit action target for Shodan-shaped tests. Shodan is the production
    /// GM-admin liaison (it lets human players on the live server request
    /// on-demand activities from the WoWStateManager), and the LiveValidation
    /// suite reuses it for setup tasks that require GM targeting — *only* for
    /// setup. ActionType.* dispatches must go to a dedicated test account
    /// (TESTBOT1/TESTBOT2 or a category-specific sibling), never to Shodan.
    /// <see cref="ResolveBotRunnerActionTargets"/> enforces that invariant.
    /// </summary>
    public readonly record struct BotRunnerActionTarget(
        string RoleLabel,
        string AccountName,
        string? CharacterName,
        bool IsForeground);

    private static readonly IReadOnlyDictionary<string, byte> CharacterClassIds =
        new Dictionary<string, byte>(StringComparer.OrdinalIgnoreCase)
        {
            ["Warrior"] = 1,
            ["Paladin"] = 2,
            ["Hunter"] = 3,
            ["Rogue"] = 4,
            ["Priest"] = 5,
            ["Shaman"] = 7,
            ["Mage"] = 8,
            ["Warlock"] = 9,
            ["Druid"] = 11,
        };

    private static readonly IReadOnlyDictionary<string, byte> CharacterRaceIds =
        new Dictionary<string, byte>(StringComparer.OrdinalIgnoreCase)
        {
            ["Human"] = 1,
            ["Orc"] = 2,
            ["Dwarf"] = 3,
            ["NightElf"] = 4,
            ["Night Elf"] = 4,
            ["Undead"] = 5,
            ["Tauren"] = 6,
            ["Gnome"] = 7,
            ["Troll"] = 8,
        };

    private static readonly IReadOnlyDictionary<string, byte> CharacterGenderIds =
        new Dictionary<string, byte>(StringComparer.OrdinalIgnoreCase)
        {
            ["Male"] = 0,
            ["Female"] = 1,
        };

    public IReadOnlyList<BotRunnerActionTarget> ResolveBotRunnerActionTargets(
        bool includeForegroundIfActionable = true,
        bool foregroundFirst = false)
    {
        global::Tests.Infrastructure.Skip.If(
            string.IsNullOrWhiteSpace(ShodanAccountName),
            "Shodan admin bot not available.");
        global::Tests.Infrastructure.Skip.If(
            string.IsNullOrWhiteSpace(BgAccountName),
            "BG bot account not available.");

        var targets = new List<BotRunnerActionTarget>();

        if (includeForegroundIfActionable && IsFgActionable)
        {
            global::Tests.Infrastructure.Skip.If(
                string.IsNullOrWhiteSpace(FgAccountName),
                "FG bot is actionable but FgAccountName is missing.");
            targets.Add(new BotRunnerActionTarget("FG", FgAccountName!, FgCharacterName, IsForeground: true));
        }

        targets.Add(new BotRunnerActionTarget("BG", BgAccountName!, BgCharacterName, IsForeground: false));

        if (!foregroundFirst)
            targets = targets.OrderBy(target => target.IsForeground ? 1 : 0).ToList();

        foreach (var target in targets)
        {
            if (string.Equals(target.AccountName, ShodanAccountName, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "[SHODAN-ACTION-PLAN] Shodan resolved as a BotRunner action target. " +
                    "Use a separate FG/BG account for actions under test.");
            }
        }

        _logger.LogInformation(
            "[SHODAN-ACTION-PLAN] director={Director} targets={Targets}",
            ShodanAccountName,
            string.Join(", ", targets.Select(target =>
                $"{target.RoleLabel}:{target.AccountName}/{target.CharacterName ?? "?"}")));

        return targets;
    }

    /// <summary>
    /// Read-only guard that catches stale live accounts whose existing character
    /// does not match the category config. This prevents migrations from staging
    /// a class-specific loadout (for example a wand) on the wrong character.
    /// </summary>
    public async Task AssertConfiguredCharactersMatchAsync(string settingsPath)
    {
        if (string.IsNullOrWhiteSpace(settingsPath))
            throw new ArgumentException("Settings path is required.", nameof(settingsPath));

        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(settingsPath));
        var mismatches = new List<string>();

        // F-1: configs may be either the legacy bare-array shape or the new
        // { "Mode": ..., "Characters": [...] } wrapper. Mirror the loader in
        // SeedExpectedAccountsFromStateManagerSettings so migrated tests can
        // still call this guard.
        JsonElement charactersElement;
        if (document.RootElement.ValueKind == JsonValueKind.Array)
        {
            charactersElement = document.RootElement;
        }
        else if (document.RootElement.ValueKind == JsonValueKind.Object
            && document.RootElement.TryGetProperty("Characters", out var wrappedCharacters)
            && wrappedCharacters.ValueKind == JsonValueKind.Array)
        {
            charactersElement = wrappedCharacters;
        }
        else
        {
            throw new InvalidOperationException(
                $"[SHODAN-ACTION-PLAN] Unexpected settings shape ({document.RootElement.ValueKind}); " +
                "expected JSON array or { Mode, Characters }.");
        }

        foreach (var element in charactersElement.EnumerateArray())
        {
            if (element.TryGetProperty("ShouldRun", out var shouldRun)
                && shouldRun.ValueKind == JsonValueKind.False)
            {
                continue;
            }

            if (!element.TryGetProperty("AccountName", out var accountProperty))
                continue;

            var accountName = accountProperty.GetString();
            if (string.IsNullOrWhiteSpace(accountName))
                continue;

            var characters = await QueryCharactersForAccountAsync(accountName);
            if (characters.Count == 0)
            {
                mismatches.Add($"{accountName}: no character row exists after launch");
                continue;
            }

            var character = characters[0];
            CompareConfiguredByte(element, "CharacterClass", CharacterClassIds, character.ClassId, accountName, character.Name, mismatches);
            CompareConfiguredByte(element, "CharacterRace", CharacterRaceIds, character.RaceId, accountName, character.Name, mismatches);
            CompareConfiguredByte(element, "CharacterGender", CharacterGenderIds, character.GenderId, accountName, character.Name, mismatches);
        }

        if (mismatches.Count > 0)
        {
            throw new InvalidOperationException(
                "[SHODAN-ACTION-PLAN] Configured account/character mismatch: " +
                string.Join("; ", mismatches));
        }
    }

    private static void CompareConfiguredByte(
        JsonElement element,
        string propertyName,
        IReadOnlyDictionary<string, byte> expectedIds,
        byte actualValue,
        string accountName,
        string characterName,
        List<string> mismatches)
    {
        if (!element.TryGetProperty(propertyName, out var property))
            return;

        var configured = property.GetString();
        if (string.IsNullOrWhiteSpace(configured))
            return;

        if (!expectedIds.TryGetValue(configured, out var expectedValue))
            throw new InvalidOperationException($"Unknown {propertyName} value '{configured}' for account '{accountName}'.");

        if (actualValue != expectedValue)
        {
            mismatches.Add(
                $"{accountName}/{characterName}: {propertyName} is {actualValue}, expected {configured}({expectedValue})");
        }
    }

    /// <summary>
    /// Shodan-directed per-test loadout staging for a BotRunner target (FG or BG).
    ///
    /// This is the per-test analogue of <see cref="EnsureShodanAdminLoadoutAsync"/>:
    /// it lets a migrated test body express "stage the target with these spells /
    /// skills / items" in a single call, instead of scattering
    /// <see cref="BotLearnSpellAsync"/> / <see cref="BotSetSkillAsync"/> /
    /// <see cref="BotAddItemAsync"/> / <see cref="EnsureCleanSlateAsync"/> calls
    /// throughout the test.
    ///
    /// After this helper returns, the test body should only dispatch the
    /// <c>ActionType</c> under test and assert on snapshot / task markers. It
    /// should not issue further GM commands.
    ///
    /// Internally the helper uses Shodan as the command sender for
    /// <c>.learn</c> / <c>.setskill</c> / <c>.additem</c>: it first sends the
    /// BotRunner-only <c>.targetguid</c> command so Shodan's server-side
    /// selection points at the FG/BG target, then issues the selected-target
    /// MaNGOS setup command. SOAP is still used only for command forms that are
    /// actually name-targeted, such as <c>.character level</c>.
    /// </summary>
    /// <param name="targetAccountName">
    /// Account of the BotRunner under test. Must match <see cref="FgAccountName"/>
    /// or <see cref="BgAccountName"/> (never Shodan).
    /// </param>
    /// <param name="targetRoleLabel">Short label used in logs, e.g. "FG" / "BG".</param>
    /// <param name="spellsToLearn">Spell ids to teach via <c>.learn</c>.</param>
    /// <param name="skillsToSet">Skill directives to apply via <c>.setskill</c>.</param>
    /// <param name="itemsToAdd">Items to place in the target's bags via <c>.additem</c>.</param>
    /// <param name="cleanSlate">
    /// When true, call <see cref="EnsureCleanSlateAsync"/> first so the target
    /// is alive, ungrouped, teleported to the Orgrimmar safe zone, and out of
    /// GM mode before staging.
    /// </param>
    /// <param name="clearInventoryFirst">
    /// When true, dispatch <c>ActionType.DestroyItem</c> across bag 0 so the
    /// target has deterministic bag contents before items are added.
    /// </param>
    public async Task StageBotRunnerLoadoutAsync(
        string targetAccountName,
        string targetRoleLabel,
        IReadOnlyList<uint>? spellsToLearn = null,
        IReadOnlyList<SkillDirective>? skillsToSet = null,
        IReadOnlyList<ItemDirective>? itemsToAdd = null,
        bool cleanSlate = true,
        bool clearInventoryFirst = true,
        int? levelTo = null)
    {
        if (string.IsNullOrWhiteSpace(targetAccountName))
            throw new InvalidOperationException("[SHODAN-STAGE] Target account name is required.");

        if (string.Equals(targetAccountName, ShodanAccountName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "[SHODAN-STAGE] Shodan is the test director, not a BotRunner target. " +
                "Use EnsureShodanAdminLoadoutAsync for Shodan's own loadout.");
        }

        _logger.LogInformation(
            "[SHODAN-STAGE] {Role} account='{Account}' cleanSlate={Clean} clearBags={ClearBags} " +
            "spells={Spells} skills={Skills} items={Items} levelTo={Level}",
            targetRoleLabel,
            targetAccountName,
            cleanSlate,
            clearInventoryFirst,
            spellsToLearn?.Count ?? 0,
            skillsToSet?.Count ?? 0,
            itemsToAdd?.Count ?? 0,
            levelTo?.ToString() ?? "(unchanged)");

        if (cleanSlate)
            await EnsureCleanSlateAsync(targetAccountName, targetRoleLabel);

        if (levelTo is int targetLevel)
        {
            var characterName = GetKnownCharacterNameForAccount(targetAccountName);
            if (string.IsNullOrWhiteSpace(characterName))
            {
                throw new InvalidOperationException(
                    $"[SHODAN-STAGE] Cannot set level for '{targetAccountName}': character name not yet observed.");
            }

            await SetLevelAsync(characterName, targetLevel);
            await WaitForSnapshotConditionAsync(
                targetAccountName,
                snap => (snap.Player?.Unit?.GameObject?.Level ?? 0) >= targetLevel,
                TimeSpan.FromSeconds(8),
                pollIntervalMs: 300,
                progressLabel: $"{targetRoleLabel} level={targetLevel}");
        }

        if (clearInventoryFirst)
        {
            await BotClearInventoryAsync(targetAccountName, includeExtraBags: false);
            await Task.Delay(1000);
        }

        var hasDirectorSelectedTargetCommands =
            spellsToLearn is { Count: > 0 }
            || skillsToSet is { Count: > 0 }
            || itemsToAdd is { Count: > 0 };

        if (!hasDirectorSelectedTargetCommands)
            return;

        // Shodan's selected target is session-scoped. Keep target selection and
        // selected-target GM setup serialized when tests stage FG/BG in parallel.
        await _shodanDirectedCommandSemaphore.WaitAsync();
        try
        {
            if (spellsToLearn is { Count: > 0 })
            {
                foreach (var spellId in spellsToLearn)
                    await StageBotRunnerSpellViaDirectorAsync(targetAccountName, targetRoleLabel, spellId);
            }

            if (skillsToSet is { Count: > 0 })
            {
                foreach (var directive in skillsToSet)
                {
                    await StageBotRunnerSkillViaDirectorAsync(
                        targetAccountName,
                        targetRoleLabel,
                        directive.SkillId,
                        directive.CurrentValue,
                        directive.MaxValue);
                }
            }

            if (itemsToAdd is { Count: > 0 })
            {
                foreach (var directive in itemsToAdd)
                    await StageBotRunnerItemViaDirectorAsync(targetAccountName, targetRoleLabel, directive.ItemId, directive.Count);
            }
        }
        finally
        {
            _shodanDirectedCommandSemaphore.Release();
        }
    }

    private async Task StageBotRunnerSpellViaDirectorAsync(
        string targetAccountName,
        string targetRoleLabel,
        uint spellId)
    {
        await SelectBotRunnerTargetFromDirectorAsync(targetAccountName, targetRoleLabel);
        await SendShodanDirectedGmCommandAsync(targetRoleLabel, $".learn {spellId}");

        var verified = await WaitForSnapshotConditionAsync(
            targetAccountName,
            snapshot => snapshot.Player?.SpellList?.Contains(spellId) == true,
            TimeSpan.FromSeconds(5),
            pollIntervalMs: 300,
            progressLabel: $"{targetRoleLabel} learn {spellId}");
        if (!verified)
            _testOutput?.WriteLine($"[WARN] [{targetAccountName}] Spell {spellId} not confirmed in SpellList after Shodan .learn");
    }

    private async Task StageBotRunnerSkillViaDirectorAsync(
        string targetAccountName,
        string targetRoleLabel,
        uint skillId,
        int currentValue,
        int maxValue)
    {
        await SelectBotRunnerTargetFromDirectorAsync(targetAccountName, targetRoleLabel);
        await SendShodanDirectedGmCommandAsync(targetRoleLabel, $".setskill {skillId} {currentValue} {maxValue}");

        var verified = await WaitForSnapshotConditionAsync(
            targetAccountName,
            snapshot => snapshot.Player?.SkillInfo != null
                && snapshot.Player.SkillInfo.TryGetValue(skillId, out var skillValue)
                && skillValue >= currentValue,
            TimeSpan.FromSeconds(5),
            pollIntervalMs: 300,
            progressLabel: $"{targetRoleLabel} skill {skillId}={currentValue}");
        if (!verified)
            _testOutput?.WriteLine($"[WARN] [{targetAccountName}] Skill {skillId} not confirmed at {currentValue}/{maxValue} after Shodan .setskill");
    }

    private async Task StageBotRunnerItemViaDirectorAsync(
        string targetAccountName,
        string targetRoleLabel,
        uint itemId,
        int count)
    {
        await SelectBotRunnerTargetFromDirectorAsync(targetAccountName, targetRoleLabel);
        await SendShodanDirectedGmCommandAsync(targetRoleLabel, $".additem {itemId} {count}");

        var verified = await WaitForSnapshotConditionAsync(
            targetAccountName,
            snapshot => snapshot.Player?.BagContents?.Values.Any(value => value == itemId) == true,
            TimeSpan.FromSeconds(5),
            pollIntervalMs: 300,
            progressLabel: $"{targetRoleLabel} add item {itemId}");
        if (!verified)
            _testOutput?.WriteLine($"[WARN] [{targetAccountName}] Item {itemId} not confirmed in bags after Shodan .additem");
    }

    private async Task SelectBotRunnerTargetFromDirectorAsync(
        string targetAccountName,
        string targetRoleLabel)
    {
        ValidateBotRunnerStageTarget(targetAccountName);

        var shodanAccount = RequireShodanDirectorAccount();
        await RefreshSnapshotsAsync();

        var targetSnapshot = await GetSnapshotAsync(targetAccountName);
        var targetGuid = targetSnapshot?.Player?.Unit?.GameObject?.Base?.Guid ?? 0UL;
        if (targetGuid == 0UL)
        {
            throw new InvalidOperationException(
                $"[SHODAN-STAGE] Cannot select {targetRoleLabel} account '{targetAccountName}': target player GUID is not available.");
        }

        var selected = await SendGmChatCommandAndAwaitServerAckAsync(
            shodanAccount,
            $".targetguid 0x{targetGuid:X}",
            timeoutMs: 5000,
            pollIntervalMs: 100);
        if (!selected)
        {
            throw new InvalidOperationException(
                $"[SHODAN-STAGE] Shodan did not confirm target selection for {targetRoleLabel} account '{targetAccountName}' GUID=0x{targetGuid:X}.");
        }
    }

    private async Task SendShodanDirectedGmCommandAsync(string targetRoleLabel, string command)
    {
        var shodanAccount = RequireShodanDirectorAccount();
        var dispatched = await SendGmChatCommandAndAwaitServerAckAsync(
            shodanAccount,
            command,
            timeoutMs: 7000,
            pollIntervalMs: 100);
        if (!dispatched)
            _testOutput?.WriteLine($"[WARN] [SHODAN] {targetRoleLabel} command '{command}' was not confirmed in previousAction/ACK.");
    }

    public async Task<GmChatCommandTrace> StageBotRunnerAckCaptureCommandAsync(
        string targetAccountName,
        string targetRoleLabel,
        string command,
        bool captureResponse = true,
        int delayMs = 1000)
    {
        ValidateBotRunnerStageTarget(targetAccountName);

        if (string.IsNullOrWhiteSpace(command))
            throw new ArgumentException("ACK capture command is required.", nameof(command));

        _logger.LogInformation(
            "[SHODAN-STAGE] {Role} account='{Account}' ACK capture command: {Command}",
            targetRoleLabel,
            targetAccountName,
            command);

        return await SendGmChatCommandTrackedAsync(
            targetAccountName,
            command,
            captureResponse,
            delayMs: delayMs);
    }

    public async Task StageBotRunnerConsumableStateAsync(
        string targetAccountName,
        string targetRoleLabel,
        uint itemId,
        int itemCount,
        IReadOnlyList<uint> auraSpellIds)
    {
        ValidateBotRunnerStageTarget(targetAccountName);

        await StageBotRunnerLoadoutAsync(
            targetAccountName,
            targetRoleLabel,
            spellsToLearn: null,
            skillsToSet: null,
            itemsToAdd: [new ItemDirective(itemId, itemCount)],
            cleanSlate: true,
            clearInventoryFirst: true);

        await StageBotRunnerAurasAbsentAsync(targetAccountName, targetRoleLabel, auraSpellIds);
    }

    public async Task StageBotRunnerAurasAbsentAsync(
        string targetAccountName,
        string targetRoleLabel,
        IReadOnlyList<uint> auraSpellIds)
    {
        ValidateBotRunnerStageTarget(targetAccountName);

        foreach (var auraSpellId in auraSpellIds.Distinct())
        {
            var trace = await SendGmChatCommandTrackedAsync(
                targetAccountName,
                $".unaura {auraSpellId}",
                captureResponse: true,
                delayMs: 500);
            AssertTraceCommandSucceeded(trace, targetRoleLabel, $".unaura {auraSpellId}");
        }

        var cleared = await WaitForSnapshotConditionAsync(
            targetAccountName,
            snapshot =>
            {
                var auras = snapshot.Player?.Unit?.Auras;
                return auras == null || auraSpellIds.All(id => !auras.Contains(id));
            },
            TimeSpan.FromSeconds(8),
            pollIntervalMs: 300,
            progressLabel: $"{targetRoleLabel} consumable aura cleanup");

        if (!cleared)
            throw new InvalidOperationException($"[SHODAN-STAGE] {targetRoleLabel} consumable aura cleanup failed.");
    }

    /// <summary>
    /// Stage warrior rage for self-buff spell-cast tests. The command stays
    /// behind the fixture boundary so migrated test bodies only dispatch the
    /// BotRunner action under test.
    /// </summary>
    public async Task StageBotRunnerRageAsync(
        string targetAccountName,
        string targetRoleLabel,
        int rageInternalUnits)
    {
        ValidateBotRunnerStageTarget(targetAccountName);

        if (rageInternalUnits < 0)
            throw new ArgumentOutOfRangeException(nameof(rageInternalUnits), "Rage must be non-negative.");

        _logger.LogInformation(
            "[SHODAN-STAGE] {Role} account='{Account}' setting rage={RageInternalUnits}",
            targetRoleLabel,
            targetAccountName,
            rageInternalUnits);

        await BotSelectSelfAsync(targetAccountName);
        await Task.Delay(300);

        var trace = await SendGmChatCommandTrackedAsync(
            targetAccountName,
            $".modify rage {rageInternalUnits}",
            captureResponse: false,
            delayMs: 500);
        AssertTraceCommandSucceeded(trace, targetRoleLabel, $".modify rage {rageInternalUnits}");
    }

    public async Task StageBotRunnerMountLoadoutAsync(
        string targetAccountName,
        string targetRoleLabel,
        uint ridingSkillId,
        int ridingValue,
        uint apprenticeRidingSpellId,
        uint mountSpellId,
        bool cleanSlate = true)
    {
        ValidateBotRunnerStageTarget(targetAccountName);

        await StageBotRunnerLoadoutAsync(
            targetAccountName,
            targetRoleLabel,
            spellsToLearn: [apprenticeRidingSpellId, mountSpellId],
            skillsToSet: [new SkillDirective(ridingSkillId, ridingValue, ridingValue)],
            itemsToAdd: null,
            cleanSlate: cleanSlate,
            clearInventoryFirst: false);

        await StageBotRunnerUnmountedAsync(targetAccountName, targetRoleLabel, mountSpellId);
    }

    public async Task StageBotRunnerUnmountedAsync(
        string targetAccountName,
        string targetRoleLabel,
        uint mountAuraSpellId)
    {
        ValidateBotRunnerStageTarget(targetAccountName);

        await RefreshSnapshotsAsync();
        var baseline = await GetSnapshotAsync(targetAccountName);
        if ((baseline?.Player?.Unit?.MountDisplayId ?? 0) == 0)
            return;

        _logger.LogInformation(
            "[SHODAN-STAGE] {Role} account='{Account}' clearing mount state aura={SpellId}",
            targetRoleLabel,
            targetAccountName,
            mountAuraSpellId);

        var dismountTrace = await SendGmChatCommandTrackedAsync(
            targetAccountName,
            ".dismount",
            captureResponse: true,
            delayMs: 750);
        AssertTraceCommandSucceeded(dismountTrace, targetRoleLabel, ".dismount");

        var unauraTrace = await SendGmChatCommandTrackedAsync(
            targetAccountName,
            $".unaura {mountAuraSpellId}",
            captureResponse: true,
            delayMs: 750);
        AssertTraceCommandSucceeded(unauraTrace, targetRoleLabel, ".unaura");

        var unmounted = await WaitForSnapshotConditionAsync(
            targetAccountName,
            snapshot => (snapshot.Player?.Unit?.MountDisplayId ?? 0) == 0,
            TimeSpan.FromSeconds(10),
            pollIntervalMs: 300,
            progressLabel: $"{targetRoleLabel} unmounted");

        if (!unmounted)
        {
            await RefreshSnapshotsAsync();
            var snapshot = await GetSnapshotAsync(targetAccountName);
            var mountDisplayId = snapshot?.Player?.Unit?.MountDisplayId ?? 0;
            var position = snapshot?.Player?.Unit?.GameObject?.Base?.Position;
            throw new InvalidOperationException(
                $"[SHODAN-STAGE] {targetRoleLabel} remained mounted " +
                $"mountDisplayId={mountDisplayId} map={snapshot?.CurrentMapId ?? 0} " +
                $"pos=({position?.X:F1},{position?.Y:F1},{position?.Z:F1}) indoors={snapshot?.IsIndoors}");
        }
    }

    public async Task<bool> StageBotRunnerAtMountEnvironmentLocationAsync(
        string targetAccountName,
        string targetRoleLabel,
        string locationLabel,
        int mapId,
        float x,
        float y,
        float z,
        bool cleanSlate = false)
    {
        ValidateBotRunnerStageTarget(targetAccountName);

        if (cleanSlate)
            await EnsureCleanSlateAsync(targetAccountName, targetRoleLabel);

        const int maxAttempts = 2;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            _logger.LogInformation(
                "[SHODAN-STAGE] {Role} account='{Account}' mount-env stage {Location} attempt={Attempt} map={Map} pos=({X:F1},{Y:F1},{Z:F1})",
                targetRoleLabel,
                targetAccountName,
                locationLabel,
                attempt,
                mapId,
                x,
                y,
                z);

            await BotTeleportAsync(targetAccountName, mapId, x, y, z);

            var settled = await WaitForTeleportSettledAsync(
                targetAccountName,
                x,
                y,
                timeoutMs: 10000,
                progressLabel: $"{targetRoleLabel} {locationLabel} mount-env stage attempt {attempt}",
                xyToleranceYards: 60f);
            if (!settled)
                continue;

            await WaitForZStabilizationAsync(targetAccountName, waitMs: 2000);

            var remainedSettled = await WaitForTeleportSettledAsync(
                targetAccountName,
                x,
                y,
                timeoutMs: 4000,
                progressLabel: $"{targetRoleLabel} {locationLabel} mount-env stage post-z",
                xyToleranceYards: 60f);
            if (remainedSettled)
                return true;
        }

        return false;
    }

    public async Task<bool> StageBotRunnerAtTravelPlannerStartAsync(
        string targetAccountName,
        string targetRoleLabel,
        bool cleanSlate = true)
    {
        ValidateBotRunnerStageTarget(targetAccountName);

        if (cleanSlate)
            await EnsureCleanSlateAsync(targetAccountName, targetRoleLabel);

        const int kalimdorMapId = 1;
        // Street-level Orgrimmar approach also used by CornerNavigationTests.
        // The flight-master perch above the city can resolve to a different
        // collision layer and prevents TravelTo from producing movement.
        const float orgrimmarX = 1614.1f;
        const float orgrimmarY = -4382.4f;
        const float orgrimmarZ = 14.8f;

        _logger.LogInformation(
            "[SHODAN-STAGE] {Role} account='{Account}' travel-planner start map={Map} pos=({X:F1},{Y:F1},{Z:F1})",
            targetRoleLabel,
            targetAccountName,
            kalimdorMapId,
            orgrimmarX,
            orgrimmarY,
            orgrimmarZ);

        await BotTeleportAsync(targetAccountName, kalimdorMapId, orgrimmarX, orgrimmarY, orgrimmarZ);

        var settled = await WaitForTeleportSettledAsync(
            targetAccountName,
            orgrimmarX,
            orgrimmarY,
            timeoutMs: 10000,
            progressLabel: $"{targetRoleLabel} travel-planner start",
            xyToleranceYards: 8f);

        if (settled)
            await WaitForZStabilizationAsync(targetAccountName, waitMs: 1000);

        return settled;
    }

    public async Task<bool> StageBotRunnerAtNavigationPointAsync(
        string targetAccountName,
        string targetRoleLabel,
        int mapId,
        float x,
        float y,
        float z,
        string locationLabel,
        bool cleanSlate = true,
        float xyToleranceYards = 8f,
        int zStabilizationWaitMs = 1000)
    {
        ValidateBotRunnerStageTarget(targetAccountName);

        if (cleanSlate)
            await EnsureCleanSlateAsync(targetAccountName, targetRoleLabel);

        _logger.LogInformation(
            "[SHODAN-STAGE] {Role} account='{Account}' navigation point '{Label}' map={Map} pos=({X:F1},{Y:F1},{Z:F1})",
            targetRoleLabel,
            targetAccountName,
            locationLabel,
            mapId,
            x,
            y,
            z);

        await BotTeleportAsync(targetAccountName, mapId, x, y, z);

        var settled = await WaitForTeleportSettledAsync(
            targetAccountName,
            x,
            y,
            timeoutMs: 10000,
            progressLabel: $"{targetRoleLabel} {locationLabel}",
            xyToleranceYards: xyToleranceYards);

        if (settled && zStabilizationWaitMs > 0)
            await WaitForZStabilizationAsync(targetAccountName, waitMs: zStabilizationWaitMs);

        return settled;
    }

    /// <summary>
    /// Stage a BotRunner target near the Valley of Trials creature cluster for
    /// action-driven combat tests. The arbitrary-coordinate teleport is kept in
    /// the fixture because MaNGOS exposes it as the sender-relative <c>.go xyz</c>
    /// bot-chat command; test bodies should call this helper instead of issuing
    /// GM teleports inline.
    /// </summary>
    public async Task<bool> StageBotRunnerAtDurotarMobAreaAsync(
        string targetAccountName,
        string targetRoleLabel,
        int stageIndex = 0,
        int nearbyUnitTimeoutMs = 15000)
    {
        if (string.IsNullOrWhiteSpace(targetAccountName))
            throw new InvalidOperationException("[SHODAN-STAGE] Target account name is required.");

        if (string.Equals(targetAccountName, ShodanAccountName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "[SHODAN-STAGE] Shodan is the test director, not a BotRunner target.");
        }

        var stage = DurotarMobStages[Math.Abs(stageIndex) % DurotarMobStages.Length];

        _logger.LogInformation(
            "[SHODAN-STAGE] {Role} account='{Account}' mob-area stage={StageIndex} map={Map} pos=({X:F1},{Y:F1},{Z:F1})",
            targetRoleLabel,
            targetAccountName,
            stageIndex,
            stage.MapId,
            stage.X,
            stage.Y,
            stage.Z);

        await BotTeleportAsync(
            targetAccountName,
            stage.MapId,
            stage.X,
            stage.Y,
            stage.Z);

        var settled = await WaitForTeleportSettledAsync(
            targetAccountName,
            stage.X,
            stage.Y,
            timeoutMs: 10000,
            progressLabel: $"{targetRoleLabel} mob-area stage",
            xyToleranceYards: 60f);

        var hasUnits = await WaitForNearbyUnitsPopulatedAsync(
            targetAccountName,
            nearbyUnitTimeoutMs,
            progressLabel: $"{targetRoleLabel} mob-area units");

        return settled && hasUnits;
    }

    /// <summary>
    /// Stage a BotRunner target at Razor Hill (Durotar) for mage city-teleport
    /// validation. Razor Hill is far enough from Orgrimmar that
    /// <c>Teleport: Orgrimmar</c> produces a clear position delta. Like
    /// <see cref="StageBotRunnerAtDurotarMobAreaAsync"/>, the
    /// <c>.go xyz</c> teleport stays inside the fixture so the test body
    /// remains GM-free.
    /// </summary>
    public async Task<bool> StageBotRunnerAtRazorHillAsync(
        string targetAccountName,
        string targetRoleLabel)
    {
        if (string.IsNullOrWhiteSpace(targetAccountName))
            throw new InvalidOperationException("[SHODAN-STAGE] Target account name is required.");

        if (string.Equals(targetAccountName, ShodanAccountName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "[SHODAN-STAGE] Shodan is the test director, not a BotRunner target.");
        }

        const int RazorHillMapId = 1;
        const float RazorHillX = 315.0f;
        const float RazorHillY = -4743.0f;
        const float RazorHillZ = 12.0f;

        _logger.LogInformation(
            "[SHODAN-STAGE] {Role} account='{Account}' Razor Hill stage map={Map} pos=({X:F1},{Y:F1},{Z:F1})",
            targetRoleLabel,
            targetAccountName,
            RazorHillMapId,
            RazorHillX,
            RazorHillY,
            RazorHillZ);

        await BotTeleportAsync(
            targetAccountName,
            RazorHillMapId,
            RazorHillX,
            RazorHillY,
            RazorHillZ);

        return await WaitForTeleportSettledAsync(
            targetAccountName,
            RazorHillX,
            RazorHillY,
            timeoutMs: 10000,
            progressLabel: $"{targetRoleLabel} razor-hill stage",
            xyToleranceYards: 60f);
    }

    public Task<bool> StageBotRunnerAtRazorHillInnAsync(
        string targetAccountName,
        string targetRoleLabel,
        bool cleanSlate = true)
        => StageBotRunnerAtQuestLocationAsync(
            targetAccountName,
            targetRoleLabel,
            "Razor Hill inn",
            mapId: 1,
            x: 338.0f,
            y: -4689.0f,
            z: 15.0f,
            cleanSlate);

    public Task<bool> StageBotRunnerAtValleyOfTrialsQuestGiverAsync(
        string targetAccountName,
        string targetRoleLabel,
        bool cleanSlate = true)
        => StageBotRunnerAtQuestLocationAsync(
            targetAccountName,
            targetRoleLabel,
            "Valley of Trials Kaltunk",
            mapId: 1,
            x: -607.43f,
            y: -4251.33f,
            z: 42.04f,
            cleanSlate);

    public Task<bool> StageBotRunnerAtValleyOfTrialsQuestTurnInAsync(
        string targetAccountName,
        string targetRoleLabel,
        bool cleanSlate = false)
        => StageBotRunnerAtQuestLocationAsync(
            targetAccountName,
            targetRoleLabel,
            "Valley of Trials Gornek",
            mapId: 1,
            x: -600.13f,
            y: -4186.19f,
            z: 44.27f,
            cleanSlate);

    public Task<bool> StageBotRunnerAtDurotarQuestObjectiveAreaAsync(
        string targetAccountName,
        string targetRoleLabel,
        bool cleanSlate = true)
        => StageBotRunnerAtQuestLocationAsync(
            targetAccountName,
            targetRoleLabel,
            "Durotar quest objective",
            mapId: 1,
            x: -620.0f,
            y: -4385.0f,
            z: 44.0f,
            cleanSlate);

    public Task<bool> StageBotRunnerAtRazorHillHunterTrainerAsync(
        string targetAccountName,
        string targetRoleLabel,
        bool cleanSlate = true)
        => StageBotRunnerAtQuestLocationAsync(
            targetAccountName,
            targetRoleLabel,
            "Razor Hill hunter trainer",
            mapId: 1,
            x: 275.341f,
            y: -4704.0f,
            z: 14.712f,
            cleanSlate);

    public Task<bool> StageBotRunnerAtOrgrimmarFlightMasterAsync(
        string targetAccountName,
        string targetRoleLabel,
        bool cleanSlate = true)
        => StageBotRunnerAtQuestLocationAsync(
            targetAccountName,
            targetRoleLabel,
            "Orgrimmar flight master",
            mapId: 1,
            x: 1676.25f,
            y: -4313.45f,
            z: 64.72f,
            cleanSlate);

    public async Task<ulong> StageBotRunnerTaxiReadinessAsync(
        string targetAccountName,
        string targetRoleLabel,
        bool cleanSlate = true,
        bool enableAllTaxiNodes = true,
        long minimumCopper = 50000)
    {
        ValidateBotRunnerStageTarget(targetAccountName);

        if (cleanSlate)
            await EnsureCleanSlateAsync(targetAccountName, targetRoleLabel);

        await QuiesceAccountsAsync(
            new[] { targetAccountName },
            $"{targetRoleLabel} taxi-readiness pre-stage");

        if (minimumCopper > 0)
            await StageBotRunnerCoinageAsync(targetAccountName, targetRoleLabel, minimumCopper);

        if (enableAllTaxiNodes)
            await EnsureTaxiNodesEnabledAsync(targetAccountName, targetRoleLabel);

        var staged = await StageBotRunnerAtOrgrimmarFlightMasterAsync(
            targetAccountName,
            targetRoleLabel,
            cleanSlate: false);
        if (!staged)
            throw new InvalidOperationException($"[SHODAN-STAGE] {targetRoleLabel} Orgrimmar flight-master staging failed.");

        var fmUnit = await WaitForNearbyUnitAsync(
            targetAccountName,
            (uint)NPCFlags.UNIT_NPC_FLAG_FLIGHTMASTER,
            timeoutMs: 15000,
            progressLabel: $"{targetRoleLabel} flight-master lookup");
        if (fmUnit == null)
            throw new InvalidOperationException($"[SHODAN-STAGE] {targetRoleLabel} did not see an Orgrimmar flight master.");

        var fmGuid = fmUnit.GameObject?.Base?.Guid ?? 0UL;
        if (fmGuid == 0)
            throw new InvalidOperationException($"[SHODAN-STAGE] {targetRoleLabel} flight master had no guid.");

        _logger.LogInformation(
            "[SHODAN-STAGE] {Role} account='{Account}' flight master ready guid=0x{Guid:X} name='{Name}'",
            targetRoleLabel,
            targetAccountName,
            fmGuid,
            fmUnit.GameObject?.Name);

        return fmGuid;
    }

    public async Task<bool> StageBotRunnerAtOrgrimmarZeppelinTowerAsync(
        string targetAccountName,
        string targetRoleLabel,
        bool cleanSlate = true)
    {
        await QuiesceAccountsAsync(
            new[] { targetAccountName },
            $"{targetRoleLabel} Orgrimmar zeppelin pre-stage");

        return await StageBotRunnerAtNavigationPointAsync(
            targetAccountName,
            targetRoleLabel,
            mapId: 1,
            x: 1340.98f,
            y: -4638.58f,
            z: 53.5445f,
            locationLabel: "Orgrimmar zeppelin tower",
            cleanSlate,
            xyToleranceYards: 20f,
            zStabilizationWaitMs: 1000);
    }

    public async Task<bool> StageBotRunnerAtRatchetDockAsync(
        string targetAccountName,
        string targetRoleLabel,
        bool cleanSlate = true)
    {
        await QuiesceAccountsAsync(
            new[] { targetAccountName },
            $"{targetRoleLabel} Ratchet dock pre-stage");

        return await StageBotRunnerAtNavigationPointAsync(
            targetAccountName,
            targetRoleLabel,
            mapId: 1,
            x: -996.0f,
            y: -3827.0f,
            z: 8.0f,
            locationLabel: "Ratchet dock",
            cleanSlate,
            xyToleranceYards: 20f,
            zStabilizationWaitMs: 1000);
    }

    public async Task<bool> StageBotRunnerAtUndercityElevatorUpperAsync(
        string targetAccountName,
        string targetRoleLabel,
        bool cleanSlate = true)
    {
        await QuiesceAccountsAsync(
            new[] { targetAccountName },
            $"{targetRoleLabel} Undercity elevator pre-stage");

        return await StageBotRunnerAtNavigationPointAsync(
            targetAccountName,
            targetRoleLabel,
            mapId: 0,
            x: 1544.24f,
            y: 240.77f,
            z: 55.40f,
            locationLabel: "Undercity elevator upper",
            cleanSlate,
            xyToleranceYards: 20f,
            zStabilizationWaitMs: 1000);
    }

    public async Task<bool> StageBotRunnerAtThunderBluffElevatorAsync(
        string targetAccountName,
        string targetRoleLabel,
        bool cleanSlate = true)
    {
        await QuiesceAccountsAsync(
            new[] { targetAccountName },
            $"{targetRoleLabel} Thunder Bluff elevator pre-stage");

        return await StageBotRunnerAtNavigationPointAsync(
            targetAccountName,
            targetRoleLabel,
            mapId: 1,
            x: -1898.0f,
            y: -287.0f,
            z: 92.0f,
            locationLabel: "Thunder Bluff elevator",
            cleanSlate,
            xyToleranceYards: 30f,
            zStabilizationWaitMs: 1000);
    }

    public Task<bool> StageBotRunnerAtOrgrimmarWarsongBattlemasterAsync(
        string targetAccountName,
        string targetRoleLabel,
        bool cleanSlate = true)
    {
        var battlemaster = global::BotRunner.Travel.BattlemasterData.OrgrimmarWsg;
        return StageBotRunnerAtQuestLocationAsync(
            targetAccountName,
            targetRoleLabel,
            "Orgrimmar Warsong Gulch battlemaster",
            mapId: (int)battlemaster.MapId,
            x: battlemaster.Position.X,
            y: battlemaster.Position.Y,
            z: battlemaster.Position.Z,
            cleanSlate);
    }

    public Task<bool> StageBotRunnerAtValleySpiritHealerAsync(
        string targetAccountName,
        string targetRoleLabel,
        bool cleanSlate = true)
        => StageBotRunnerAtQuestLocationAsync(
            targetAccountName,
            targetRoleLabel,
            "Valley spirit healer",
            mapId: 1,
            x: -637.768f,
            y: -4300.84f,
            z: 43.909f,
            cleanSlate);

    public async Task<bool> StageBotRunnerAtIronforgeTramEntranceAsync(
        string targetAccountName,
        string targetRoleLabel,
        bool cleanSlate = true)
    {
        ValidateBotRunnerStageTarget(targetAccountName);

        if (cleanSlate)
            await EnsureCleanSlateAsync(targetAccountName, targetRoleLabel);

        const int easternKingdomsMapId = 0;
        const float ironforgeTramX = -4838f;
        const float ironforgeTramY = -1317f;
        const float ironforgeTramZ = 505f;

        _logger.LogInformation(
            "[SHODAN-STAGE] {Role} account='{Account}' Ironforge tram stage map={Map} pos=({X:F1},{Y:F1},{Z:F1})",
            targetRoleLabel,
            targetAccountName,
            easternKingdomsMapId,
            ironforgeTramX,
            ironforgeTramY,
            ironforgeTramZ);

        await BotTeleportAsync(
            targetAccountName,
            easternKingdomsMapId,
            ironforgeTramX,
            ironforgeTramY,
            ironforgeTramZ);

        return await WaitForTeleportSettledAsync(
            targetAccountName,
            ironforgeTramX,
            ironforgeTramY,
            timeoutMs: 10000,
            progressLabel: $"{targetRoleLabel} Ironforge tram stage",
            xyToleranceYards: 60f);
    }

    public async Task<bool> TriggerBotRunnerRejectedDeeprunTramTransitionAsync(
        string targetAccountName,
        string targetRoleLabel)
    {
        ValidateBotRunnerStageTarget(targetAccountName);

        const string deeprunTramCommand = ".go xyz -4838 -1317 502 369";
        _logger.LogInformation(
            "[SHODAN-STAGE] {Role} account='{Account}' Deeprun Tram rejected-transition command={Command}",
            targetRoleLabel,
            targetAccountName,
            deeprunTramCommand);

        var trace = await SendGmChatCommandTrackedAsync(
            targetAccountName,
            deeprunTramCommand,
            captureResponse: true,
            delayMs: 1000,
            allowWhenDead: true);
        AssertTraceCommandSucceeded(trace, targetRoleLabel, "Deeprun Tram transition");

        return await WaitForSnapshotConditionAsync(
            targetAccountName,
            snapshot => string.Equals(snapshot.ScreenState, "InWorld", StringComparison.OrdinalIgnoreCase)
                && snapshot.ConnectionState == BotConnectionState.BotInWorld
                && !snapshot.IsMapTransition
                && snapshot.Player?.Unit?.GameObject?.Base?.Position != null,
            TimeSpan.FromSeconds(12),
            pollIntervalMs: 500,
            progressLabel: $"{targetRoleLabel} Deeprun Tram bounce");
    }

    public async Task<DeathInductionResult> StageBotRunnerCorpseAtValleySpiritHealerAsync(
        string targetAccountName,
        string targetRoleLabel,
        bool cleanSlate = true)
    {
        ValidateBotRunnerStageTarget(targetAccountName);

        var staged = await StageBotRunnerAtValleySpiritHealerAsync(
            targetAccountName,
            targetRoleLabel,
            cleanSlate);
        if (!staged)
            throw new InvalidOperationException($"[SHODAN-STAGE] {targetRoleLabel} Valley spirit healer staging failed.");

        await RefreshSnapshotsAsync();
        var snapshot = await GetSnapshotAsync(targetAccountName);
        var characterName = snapshot?.CharacterName ?? GetKnownCharacterNameForAccount(targetAccountName);
        if (string.IsNullOrWhiteSpace(characterName))
            throw new InvalidOperationException($"[SHODAN-STAGE] {targetRoleLabel} character name is required for death staging.");

        var death = await InduceDeathForTestAsync(
            targetAccountName,
            characterName,
            timeoutMs: 15000,
            requireCorpseTransition: true);
        if (!death.Succeeded)
            throw new InvalidOperationException($"[SHODAN-STAGE] {targetRoleLabel} death staging failed: {death.Details}");

        return death;
    }

    public async Task<DeathInductionResult> StageBotRunnerCorpseAtNavigationPointAsync(
        string targetAccountName,
        string targetRoleLabel,
        int mapId,
        float x,
        float y,
        float z,
        string locationLabel,
        bool cleanSlate = true,
        float xyToleranceYards = 8f)
    {
        ValidateBotRunnerStageTarget(targetAccountName);

        if (cleanSlate)
            await EnsureCleanSlateAsync(targetAccountName, targetRoleLabel, teleportToSafeZone: false);

        var staged = await StageBotRunnerAtNavigationPointAsync(
            targetAccountName,
            targetRoleLabel,
            mapId,
            x,
            y,
            z,
            locationLabel,
            cleanSlate: false,
            xyToleranceYards: xyToleranceYards);
        if (!staged)
            throw new InvalidOperationException($"[SHODAN-STAGE] {targetRoleLabel} {locationLabel} corpse-run staging failed.");

        await RefreshSnapshotsAsync();
        var snapshot = await GetSnapshotAsync(targetAccountName);
        var characterName = snapshot?.CharacterName ?? GetKnownCharacterNameForAccount(targetAccountName);
        if (string.IsNullOrWhiteSpace(characterName))
            throw new InvalidOperationException($"[SHODAN-STAGE] {targetRoleLabel} character name is required for corpse-run staging.");

        var death = await InduceDeathForTestAsync(
            targetAccountName,
            characterName,
            timeoutMs: 15000,
            requireCorpseTransition: true);
        if (!death.Succeeded)
            throw new InvalidOperationException($"[SHODAN-STAGE] {targetRoleLabel} death staging failed: {death.Details}");

        return death;
    }

    public async Task RestoreBotRunnerAliveAtValleySpiritHealerAsync(
        string targetAccountName,
        string targetRoleLabel)
    {
        ValidateBotRunnerStageTarget(targetAccountName);

        await RefreshSnapshotsAsync();
        var snapshot = await GetSnapshotAsync(targetAccountName);
        var characterName = snapshot?.CharacterName ?? GetKnownCharacterNameForAccount(targetAccountName);
        if (string.IsNullOrWhiteSpace(characterName))
            return;

        if (!IsStrictAlive(snapshot))
            await RevivePlayerAsync(characterName);

        await WaitForSnapshotConditionAsync(
            targetAccountName,
            IsStrictAlive,
            TimeSpan.FromSeconds(10),
            pollIntervalMs: 500,
            progressLabel: $"{targetRoleLabel} spirit-healer cleanup alive");
    }

    public async Task RestoreBotRunnerAliveAtNavigationPointAsync(
        string targetAccountName,
        string targetRoleLabel,
        int mapId,
        float x,
        float y,
        float z,
        string locationLabel,
        float xyToleranceYards = 8f)
    {
        ValidateBotRunnerStageTarget(targetAccountName);

        await RefreshSnapshotsAsync();
        var snapshot = await GetSnapshotAsync(targetAccountName);
        var characterName = snapshot?.CharacterName ?? GetKnownCharacterNameForAccount(targetAccountName);
        if (!string.IsNullOrWhiteSpace(characterName) && !IsStrictAlive(snapshot))
            await RevivePlayerAsync(characterName);

        await WaitForSnapshotConditionAsync(
            targetAccountName,
            IsStrictAlive,
            TimeSpan.FromSeconds(10),
            pollIntervalMs: 500,
            progressLabel: $"{targetRoleLabel} {locationLabel} cleanup alive");

        var staged = await StageBotRunnerAtNavigationPointAsync(
            targetAccountName,
            targetRoleLabel,
            mapId,
            x,
            y,
            z,
            locationLabel,
            cleanSlate: false,
            xyToleranceYards: xyToleranceYards);
        if (!staged)
            throw new InvalidOperationException($"[SHODAN-STAGE] {targetRoleLabel} {locationLabel} cleanup staging failed.");
    }

    public async Task<bool> StageBotRunnerQuestAbsentAsync(
        string targetAccountName,
        string targetRoleLabel,
        uint questId)
    {
        ValidateBotRunnerStageTarget(targetAccountName);

        _logger.LogInformation(
            "[SHODAN-STAGE] {Role} account='{Account}' removing quest {QuestId}",
            targetRoleLabel,
            targetAccountName,
            questId);

        await BotSelectSelfAsync(targetAccountName);
        await Task.Delay(500);
        var trace = await SendGmChatCommandTrackedAsync(
            targetAccountName,
            $".quest remove {questId}",
            captureResponse: true,
            delayMs: 1500);
        AssertTraceCommandSucceeded(trace, targetRoleLabel, ".quest remove");

        return await WaitForSnapshotConditionAsync(
            targetAccountName,
            snap => !HasQuestInSnapshot(snap, questId),
            TimeSpan.FromSeconds(10),
            pollIntervalMs: 300,
            progressLabel: $"{targetRoleLabel} quest {questId} absent");
    }

    public async Task<bool> StageBotRunnerQuestAddedAsync(
        string targetAccountName,
        string targetRoleLabel,
        uint questId)
    {
        ValidateBotRunnerStageTarget(targetAccountName);

        _logger.LogInformation(
            "[SHODAN-STAGE] {Role} account='{Account}' adding quest {QuestId}",
            targetRoleLabel,
            targetAccountName,
            questId);

        await BotSelectSelfAsync(targetAccountName);
        await Task.Delay(500);
        var trace = await SendGmChatCommandTrackedAsync(
            targetAccountName,
            $".quest add {questId}",
            captureResponse: true,
            delayMs: 1500);
        AssertTraceCommandSucceeded(trace, targetRoleLabel, ".quest add");

        return await WaitForSnapshotConditionAsync(
            targetAccountName,
            snap => HasQuestInSnapshot(snap, questId),
            TimeSpan.FromSeconds(12),
            pollIntervalMs: 300,
            progressLabel: $"{targetRoleLabel} quest {questId} added");
    }

    public async Task<bool> StageBotRunnerQuestCompletedAsync(
        string targetAccountName,
        string targetRoleLabel,
        uint questId)
    {
        ValidateBotRunnerStageTarget(targetAccountName);

        await RefreshSnapshotsAsync();
        var before = FindQuestEntry(await GetSnapshotAsync(targetAccountName), questId);
        var baselineLog2 = before?.QuestLog2 ?? 0;
        var baselineLog3 = before?.QuestLog3 ?? 0;

        _logger.LogInformation(
            "[SHODAN-STAGE] {Role} account='{Account}' completing quest {QuestId}",
            targetRoleLabel,
            targetAccountName,
            questId);

        await BotSelectSelfAsync(targetAccountName);
        await Task.Delay(500);
        var trace = await SendGmChatCommandTrackedAsync(
            targetAccountName,
            $".quest complete {questId}",
            captureResponse: true,
            delayMs: 1500);
        AssertTraceCommandSucceeded(trace, targetRoleLabel, ".quest complete");

        return await WaitForSnapshotConditionAsync(
            targetAccountName,
            snap =>
            {
                var quest = FindQuestEntry(snap, questId);
                return quest == null
                    || quest.QuestLog2 != baselineLog2
                    || quest.QuestLog3 != baselineLog3;
            },
            TimeSpan.FromSeconds(12),
            pollIntervalMs: 300,
            progressLabel: $"{targetRoleLabel} quest {questId} complete");
    }

    public async Task<bool> StageBotRunnerSpellAbsentAsync(
        string targetAccountName,
        string targetRoleLabel,
        uint spellId)
    {
        ValidateBotRunnerStageTarget(targetAccountName);

        _logger.LogInformation(
            "[SHODAN-STAGE] {Role} account='{Account}' unlearning spell {SpellId}",
            targetRoleLabel,
            targetAccountName,
            spellId);

        await BotSelectSelfAsync(targetAccountName);
        await Task.Delay(300);
        var trace = await SendGmChatCommandTrackedAsync(
            targetAccountName,
            $".unlearn {spellId}",
            captureResponse: true,
            delayMs: 1000);
        AssertTraceCommandSucceeded(trace, targetRoleLabel, ".unlearn");

        return await WaitForSnapshotConditionAsync(
            targetAccountName,
            snap => snap.Player?.SpellList?.Contains(spellId) != true,
            TimeSpan.FromSeconds(12),
            pollIntervalMs: 300,
            progressLabel: $"{targetRoleLabel} unlearn {spellId}");
    }

    private async Task<bool> StageBotRunnerAtQuestLocationAsync(
        string targetAccountName,
        string targetRoleLabel,
        string locationLabel,
        int mapId,
        float x,
        float y,
        float z,
        bool cleanSlate)
    {
        ValidateBotRunnerStageTarget(targetAccountName);

        if (cleanSlate)
            await EnsureCleanSlateAsync(targetAccountName, targetRoleLabel);

        _logger.LogInformation(
            "[SHODAN-STAGE] {Role} account='{Account}' {Location} stage map={Map} pos=({X:F1},{Y:F1},{Z:F1})",
            targetRoleLabel,
            targetAccountName,
            locationLabel,
            mapId,
            x,
            y,
            z);

        await BotTeleportAsync(targetAccountName, mapId, x, y, z);

        var settled = await WaitForTeleportSettledAsync(
            targetAccountName,
            x,
            y,
            timeoutMs: 10000,
            progressLabel: $"{targetRoleLabel} {locationLabel} stage",
            xyToleranceYards: 60f);

        var hasUnits = await WaitForNearbyUnitsPopulatedAsync(
            targetAccountName,
            timeoutMs: 15000,
            progressLabel: $"{targetRoleLabel} {locationLabel} units");

        return settled && hasUnits;
    }

    private void ValidateBotRunnerStageTarget(string targetAccountName)
    {
        if (string.IsNullOrWhiteSpace(targetAccountName))
            throw new InvalidOperationException("[SHODAN-STAGE] Target account name is required.");

        if (string.Equals(targetAccountName, ShodanAccountName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "[SHODAN-STAGE] Shodan is the test director, not a BotRunner target.");
        }
    }

    private string RequireShodanDirectorAccount()
    {
        if (string.IsNullOrWhiteSpace(ShodanAccountName))
        {
            throw new InvalidOperationException(
                "[SHODAN-STAGE] Shodan admin bot not available; use a Shodan-enabled config for directed staging.");
        }

        return ShodanAccountName;
    }

    private static Game.QuestLogEntry? FindQuestEntry(WoWActivitySnapshot? snapshot, uint questId)
        => snapshot?.Player?.QuestLogEntries?.FirstOrDefault(q =>
            q.QuestLog1 == questId || q.QuestId == questId);

    private static bool HasQuestInSnapshot(WoWActivitySnapshot? snapshot, uint questId)
        => FindQuestEntry(snapshot, questId) != null;

    /// <summary>
    /// Stage a BotRunner target beside Grimtak at Razor Hill for vendor
    /// buy/sell packet baselines. The coordinate teleport stays inside the
    /// fixture so migrated test bodies do not issue GM movement commands inline.
    /// </summary>
    public async Task<bool> StageBotRunnerAtRazorHillVendorAsync(
        string targetAccountName,
        string targetRoleLabel,
        bool cleanSlate = true)
    {
        if (string.IsNullOrWhiteSpace(targetAccountName))
            throw new InvalidOperationException("[SHODAN-STAGE] Target account name is required.");

        if (string.Equals(targetAccountName, ShodanAccountName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "[SHODAN-STAGE] Shodan is the test director, not a BotRunner target.");
        }

        if (cleanSlate)
            await EnsureCleanSlateAsync(targetAccountName, targetRoleLabel);

        const int RazorHillVendorMapId = 1;
        const float GrimtakX = 305.722f;
        const float GrimtakY = -4665.87f;
        const float GrimtakZ = 19.527f;

        _logger.LogInformation(
            "[SHODAN-STAGE] {Role} account='{Account}' Razor Hill vendor stage map={Map} pos=({X:F1},{Y:F1},{Z:F1})",
            targetRoleLabel,
            targetAccountName,
            RazorHillVendorMapId,
            GrimtakX,
            GrimtakY,
            GrimtakZ);

        await BotTeleportAsync(
            targetAccountName,
            RazorHillVendorMapId,
            GrimtakX,
            GrimtakY,
            GrimtakZ);

        var settled = await WaitForTeleportSettledAsync(
            targetAccountName,
            GrimtakX,
            GrimtakY,
            timeoutMs: 10000,
            progressLabel: $"{targetRoleLabel} razor-hill vendor stage",
            xyToleranceYards: 60f);

        var hasUnits = await WaitForNearbyUnitsPopulatedAsync(
            targetAccountName,
            timeoutMs: 15000,
            progressLabel: $"{targetRoleLabel} razor-hill vendor units");

        var vendor = await WaitForNearbyUnitAsync(
            targetAccountName,
            (uint)NPCFlags.UNIT_NPC_FLAG_VENDOR,
            timeoutMs: 5000,
            progressLabel: $"{targetRoleLabel} razor-hill vendor lookup");

        return settled && hasUnits && vendor != null;
    }

    /// <summary>
    /// Ensure a BotRunner target has enough copper for economy action tests.
    /// The GM money command stays behind the Shodan staging helper boundary so
    /// migrated test bodies only dispatch the action under test.
    /// </summary>
    public async Task<long> StageBotRunnerCoinageAsync(
        string targetAccountName,
        string targetRoleLabel,
        long minimumCopper)
    {
        if (string.IsNullOrWhiteSpace(targetAccountName))
            throw new InvalidOperationException("[SHODAN-STAGE] Target account name is required.");

        if (string.Equals(targetAccountName, ShodanAccountName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "[SHODAN-STAGE] Shodan is the test director, not a BotRunner target.");
        }

        await RefreshSnapshotsAsync();
        var snapshot = await GetSnapshotAsync(targetAccountName);
        var currentCopper = snapshot?.Player?.Coinage ?? 0L;
        if (currentCopper >= minimumCopper)
        {
            _logger.LogInformation(
                "[SHODAN-STAGE] {Role} account='{Account}' coinage already sufficient: {Copper}c >= {Minimum}c",
                targetRoleLabel,
                targetAccountName,
                currentCopper,
                minimumCopper);
            return currentCopper;
        }

        var delta = minimumCopper - currentCopper;
        _logger.LogInformation(
            "[SHODAN-STAGE] {Role} account='{Account}' adding {Delta} copper for minimum {Minimum}c",
            targetRoleLabel,
            targetAccountName,
            delta,
            minimumCopper);

        await BotSelectSelfAsync(targetAccountName);
        await Task.Delay(300);
        var trace = await SendGmChatCommandTrackedAsync(
            targetAccountName,
            $".modify money {delta}",
            captureResponse: false,
            delayMs: 500);

        if (trace.DispatchResult != ResponseResult.Success)
        {
            throw new InvalidOperationException(
                $"[SHODAN-STAGE] {targetRoleLabel} money staging dispatch failed: {trace.DispatchResult}");
        }

        var funded = await WaitForSnapshotConditionAsync(
            targetAccountName,
            snap => (snap.Player?.Coinage ?? 0L) >= minimumCopper,
            TimeSpan.FromSeconds(5),
            pollIntervalMs: 300,
            progressLabel: $"{targetRoleLabel} coinage >= {minimumCopper}");

        if (!funded)
            throw new InvalidOperationException($"[SHODAN-STAGE] {targetRoleLabel} coinage did not reach {minimumCopper}c.");

        await RefreshSnapshotsAsync();
        return (await GetSnapshotAsync(targetAccountName))?.Player?.Coinage ?? minimumCopper;
    }

    /// <summary>
    /// Stage a BotRunner target at the Orgrimmar auction house for
    /// economy-interaction tests. The coordinate teleport stays inside the
    /// fixture so Shodan-migrated test bodies do not issue GM movement
    /// commands inline.
    /// </summary>
    public async Task<bool> StageBotRunnerAtOrgrimmarAuctionHouseAsync(
        string targetAccountName,
        string targetRoleLabel,
        bool cleanSlate = true)
    {
        if (string.IsNullOrWhiteSpace(targetAccountName))
            throw new InvalidOperationException("[SHODAN-STAGE] Target account name is required.");

        if (string.Equals(targetAccountName, ShodanAccountName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "[SHODAN-STAGE] Shodan is the test director, not a BotRunner target.");
        }

        if (cleanSlate)
            await EnsureCleanSlateAsync(targetAccountName, targetRoleLabel);

        _logger.LogInformation(
            "[SHODAN-STAGE] {Role} account='{Account}' Orgrimmar AH stage map={Map} pos=({X:F1},{Y:F1},{Z:F1})",
            targetRoleLabel,
            targetAccountName,
            OrgrimmarServiceLocations.MapId,
            OrgrimmarServiceLocations.AuctionHouseX,
            OrgrimmarServiceLocations.AuctionHouseY,
            OrgrimmarServiceLocations.AuctionHouseZ);

        await BotTeleportAsync(
            targetAccountName,
            OrgrimmarServiceLocations.MapId,
            OrgrimmarServiceLocations.AuctionHouseX,
            OrgrimmarServiceLocations.AuctionHouseY,
            OrgrimmarServiceLocations.AuctionHouseZ);

        var settled = await WaitForTeleportSettledAsync(
            targetAccountName,
            OrgrimmarServiceLocations.AuctionHouseX,
            OrgrimmarServiceLocations.AuctionHouseY,
            timeoutMs: 10000,
            progressLabel: $"{targetRoleLabel} org-ah stage",
            xyToleranceYards: 60f);

        var hasUnits = await WaitForNearbyUnitsPopulatedAsync(
            targetAccountName,
            timeoutMs: 15000,
            progressLabel: $"{targetRoleLabel} org-ah units");

        return settled && hasUnits;
    }

    /// <summary>
    /// Stage a BotRunner target at the Orgrimmar bank for economy-interaction
    /// tests. The coordinate teleport stays inside the fixture so migrated
    /// test bodies do not issue GM movement commands inline.
    /// </summary>
    public async Task<bool> StageBotRunnerAtOrgrimmarBankAsync(
        string targetAccountName,
        string targetRoleLabel,
        bool cleanSlate = true)
    {
        if (string.IsNullOrWhiteSpace(targetAccountName))
            throw new InvalidOperationException("[SHODAN-STAGE] Target account name is required.");

        if (string.Equals(targetAccountName, ShodanAccountName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "[SHODAN-STAGE] Shodan is the test director, not a BotRunner target.");
        }

        if (cleanSlate)
            await EnsureCleanSlateAsync(targetAccountName, targetRoleLabel);

        _logger.LogInformation(
            "[SHODAN-STAGE] {Role} account='{Account}' Orgrimmar bank stage map={Map} pos=({X:F1},{Y:F1},{Z:F1})",
            targetRoleLabel,
            targetAccountName,
            OrgrimmarServiceLocations.MapId,
            OrgrimmarServiceLocations.BankX,
            OrgrimmarServiceLocations.BankY,
            OrgrimmarServiceLocations.BankZ);

        await BotTeleportAsync(
            targetAccountName,
            OrgrimmarServiceLocations.MapId,
            OrgrimmarServiceLocations.BankX,
            OrgrimmarServiceLocations.BankY,
            OrgrimmarServiceLocations.BankZ);

        var settled = await WaitForTeleportSettledAsync(
            targetAccountName,
            OrgrimmarServiceLocations.BankX,
            OrgrimmarServiceLocations.BankY,
            timeoutMs: 10000,
            progressLabel: $"{targetRoleLabel} org-bank stage",
            xyToleranceYards: 60f);

        var hasUnits = await WaitForNearbyUnitsPopulatedAsync(
            targetAccountName,
            timeoutMs: 15000,
            progressLabel: $"{targetRoleLabel} org-bank units");

        return settled && hasUnits;
    }

    /// <summary>
    /// Stage a BotRunner target at the Orgrimmar mailbox used by economy and
    /// mail interaction tests. The teleport stays in the fixture so test bodies
    /// do not issue GM movement commands inline.
    /// </summary>
    public async Task<bool> StageBotRunnerAtOrgrimmarMailboxAsync(
        string targetAccountName,
        string targetRoleLabel,
        bool cleanSlate = true)
    {
        if (string.IsNullOrWhiteSpace(targetAccountName))
            throw new InvalidOperationException("[SHODAN-STAGE] Target account name is required.");

        if (string.Equals(targetAccountName, ShodanAccountName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "[SHODAN-STAGE] Shodan is the test director, not a BotRunner target.");
        }

        if (cleanSlate)
            await EnsureCleanSlateAsync(targetAccountName, targetRoleLabel);

        _logger.LogInformation(
            "[SHODAN-STAGE] {Role} account='{Account}' Orgrimmar mailbox stage map={Map} pos=({X:F1},{Y:F1},{Z:F1})",
            targetRoleLabel,
            targetAccountName,
            OrgrimmarServiceLocations.MapId,
            OrgrimmarServiceLocations.MailboxX,
            OrgrimmarServiceLocations.MailboxY,
            OrgrimmarServiceLocations.MailboxZ);

        await BotTeleportAsync(
            targetAccountName,
            OrgrimmarServiceLocations.MapId,
            OrgrimmarServiceLocations.MailboxX,
            OrgrimmarServiceLocations.MailboxY,
            OrgrimmarServiceLocations.MailboxZ);

        var settled = await WaitForTeleportSettledAsync(
            targetAccountName,
            OrgrimmarServiceLocations.MailboxX,
            OrgrimmarServiceLocations.MailboxY,
            timeoutMs: 10000,
            progressLabel: $"{targetRoleLabel} org-mailbox stage",
            xyToleranceYards: 60f);

        await RefreshSnapshotsAsync();
        var objectsVisible = await WaitForSnapshotConditionAsync(
            targetAccountName,
            snap => snap.NearbyObjects.Any(go => go.GameObjectType == 19
                || (go.Name ?? string.Empty).Contains("mail", StringComparison.OrdinalIgnoreCase)),
            TimeSpan.FromSeconds(8),
            pollIntervalMs: 300,
            progressLabel: $"{targetRoleLabel} org-mailbox object");

        return settled && objectsVisible;
    }

    /// <summary>
    /// Stage a BotRunner target in the Orgrimmar safe-zone trade spot. The
    /// coordinate teleport stays inside the fixture so trade tests can keep
    /// their bodies focused on OfferTrade / OfferGold / OfferItem / AcceptTrade
    /// dispatches.
    /// </summary>
    public async Task<bool> StageBotRunnerAtOrgrimmarTradeSpotAsync(
        string targetAccountName,
        string targetRoleLabel,
        float xOffset = 0f,
        bool cleanSlate = true)
    {
        if (string.IsNullOrWhiteSpace(targetAccountName))
            throw new InvalidOperationException("[SHODAN-STAGE] Target account name is required.");

        if (string.Equals(targetAccountName, ShodanAccountName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "[SHODAN-STAGE] Shodan is the test director, not a BotRunner target.");
        }

        if (cleanSlate)
            await EnsureCleanSlateAsync(targetAccountName, targetRoleLabel);

        var tradeX = OrgrimmarServiceLocations.TradeX + xOffset;
        _logger.LogInformation(
            "[SHODAN-STAGE] {Role} account='{Account}' Orgrimmar trade stage map={Map} pos=({X:F1},{Y:F1},{Z:F1})",
            targetRoleLabel,
            targetAccountName,
            OrgrimmarServiceLocations.MapId,
            tradeX,
            OrgrimmarServiceLocations.TradeY,
            OrgrimmarServiceLocations.TradeZ);

        await BotTeleportAsync(
            targetAccountName,
            OrgrimmarServiceLocations.MapId,
            tradeX,
            OrgrimmarServiceLocations.TradeY,
            OrgrimmarServiceLocations.TradeZ);

        return await WaitForTeleportSettledAsync(
            targetAccountName,
            tradeX,
            OrgrimmarServiceLocations.TradeY,
            timeoutMs: 10000,
            progressLabel: $"{targetRoleLabel} org-trade stage",
            xyToleranceYards: 20f);
    }

    /// <summary>
    /// Send copper to a BotRunner target mailbox via SOAP. This keeps mail
    /// staging out of the migrated test body while still using a named target,
    /// server-side GM command.
    /// </summary>
    public async Task StageBotRunnerMailboxMoneyAsync(
        string targetAccountName,
        string targetRoleLabel,
        uint copper,
        string subject = "Test Gold",
        string body = "Mail collection test")
    {
        if (string.IsNullOrWhiteSpace(targetAccountName))
            throw new InvalidOperationException("[SHODAN-STAGE] Target account name is required.");

        if (string.Equals(targetAccountName, ShodanAccountName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "[SHODAN-STAGE] Shodan is the test director, not a BotRunner target.");
        }

        var characterName = GetKnownCharacterNameForAccount(targetAccountName);
        if (string.IsNullOrWhiteSpace(characterName))
        {
            await WaitForSnapshotConditionAsync(
                targetAccountName,
                snap => !string.IsNullOrWhiteSpace(snap.CharacterName),
                TimeSpan.FromSeconds(5),
                pollIntervalMs: 250,
                progressLabel: $"{targetRoleLabel} mail-character");

            characterName = (await GetSnapshotAsync(targetAccountName))?.CharacterName;
        }

        if (string.IsNullOrWhiteSpace(characterName))
            throw new InvalidOperationException($"[SHODAN-STAGE] {targetRoleLabel} character name is required for mail staging.");

        _logger.LogInformation(
            "[SHODAN-STAGE] {Role} account='{Account}' sending {Copper} copper to {Character} via SOAP mail",
            targetRoleLabel,
            targetAccountName,
            copper,
            characterName);

        var result = await ExecuteGMCommandAsync($".send money {characterName} \"{subject}\" \"{body}\" {copper}");
        _logger.LogInformation("[SHODAN-STAGE] {Role} SOAP mail result: {Result}", targetRoleLabel, result);
        await Task.Delay(5000);
    }

    /// <summary>
    /// Send an item to a BotRunner target mailbox via SOAP. This mirrors the
    /// money-mail helper for item-delivery tests.
    /// </summary>
    public async Task StageBotRunnerMailboxItemAsync(
        string targetAccountName,
        string targetRoleLabel,
        uint itemId,
        uint count,
        string subject = "Item Test",
        string body = "Mail item test")
    {
        if (string.IsNullOrWhiteSpace(targetAccountName))
            throw new InvalidOperationException("[SHODAN-STAGE] Target account name is required.");

        if (string.Equals(targetAccountName, ShodanAccountName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "[SHODAN-STAGE] Shodan is the test director, not a BotRunner target.");
        }

        var characterName = GetKnownCharacterNameForAccount(targetAccountName);
        if (string.IsNullOrWhiteSpace(characterName))
        {
            await WaitForSnapshotConditionAsync(
                targetAccountName,
                snap => !string.IsNullOrWhiteSpace(snap.CharacterName),
                TimeSpan.FromSeconds(5),
                pollIntervalMs: 250,
                progressLabel: $"{targetRoleLabel} mail-character");

            characterName = (await GetSnapshotAsync(targetAccountName))?.CharacterName;
        }

        if (string.IsNullOrWhiteSpace(characterName))
            throw new InvalidOperationException($"[SHODAN-STAGE] {targetRoleLabel} character name is required for item mail staging.");

        _logger.LogInformation(
            "[SHODAN-STAGE] {Role} account='{Account}' sending item {ItemId} x{Count} to {Character} via SOAP mail",
            targetRoleLabel,
            targetAccountName,
            itemId,
            count,
            characterName);

        var result = await ExecuteGMCommandAsync($".send items {characterName} \"{subject}\" \"{body}\" {itemId}:{count}");
        _logger.LogInformation("[SHODAN-STAGE] {Role} SOAP item-mail result: {Result}", targetRoleLabel, result);
        await Task.Delay(5000);
    }

    /// <summary>
    /// Stage a BotRunner target at the Valley of Trials copper route start for
    /// action-driven gathering tests. The teleport stays in the fixture so
    /// Shodan-migrated test bodies do not issue GM movement commands inline.
    /// </summary>
    public async Task<bool> StageBotRunnerAtValleyCopperRouteStartAsync(
        string targetAccountName,
        string targetRoleLabel)
    {
        if (string.IsNullOrWhiteSpace(targetAccountName))
            throw new InvalidOperationException("[SHODAN-STAGE] Target account name is required.");

        if (string.Equals(targetAccountName, ShodanAccountName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "[SHODAN-STAGE] Shodan is the test director, not a BotRunner target.");
        }

        await RefreshSnapshotsAsync();
        var snap = await GetSnapshotAsync(targetAccountName);
        var pos = snap?.Player?.Unit?.GameObject?.Base?.Position;
        if (pos != null)
        {
            var distanceToRoute = Distance2D(
                pos.X,
                pos.Y,
                GatheringRouteSelection.ValleyCopperRouteStartX,
                GatheringRouteSelection.ValleyCopperRouteStartY);
            _logger.LogInformation(
                "[SHODAN-STAGE] {Role} current pos=({X:F0},{Y:F0},{Z:F0}) distToValleyCopper={Distance:F0}y",
                targetRoleLabel,
                pos.X,
                pos.Y,
                pos.Z,
                distanceToRoute);

            if (distanceToRoute <= 80f)
                return true;
        }

        _logger.LogInformation(
            "[SHODAN-STAGE] {Role} account='{Account}' coordinate teleport Valley copper start map={Map} pos=({X:F1},{Y:F1},{Z:F1})",
            targetRoleLabel,
            targetAccountName,
            GatheringRouteSelection.DurotarMap,
            GatheringRouteSelection.ValleyCopperRouteStartX,
            GatheringRouteSelection.ValleyCopperRouteStartY,
            GatheringRouteSelection.ValleyCopperRouteStartZ);
        await BotTeleportAsync(
            targetAccountName,
            GatheringRouteSelection.DurotarMap,
            GatheringRouteSelection.ValleyCopperRouteStartX,
            GatheringRouteSelection.ValleyCopperRouteStartY,
            GatheringRouteSelection.ValleyCopperRouteStartZ);

        var nearRoute = await WaitForTeleportSettledAsync(
            targetAccountName,
            GatheringRouteSelection.ValleyCopperRouteStartX,
            GatheringRouteSelection.ValleyCopperRouteStartY,
            timeoutMs: 10000,
            progressLabel: $"{targetRoleLabel} valley-copper stage",
            xyToleranceYards: 80f);

        if (nearRoute)
            await WaitForZStabilizationAsync(targetAccountName, waitMs: 2000);

        return nearRoute;
    }

    /// <summary>
    /// Stage a BotRunner target at the Durotar herb route start for
    /// action-driven herbalism tests.
    /// </summary>
    public async Task<bool> StageBotRunnerAtDurotarHerbRouteStartAsync(
        string targetAccountName,
        string targetRoleLabel)
    {
        if (string.IsNullOrWhiteSpace(targetAccountName))
            throw new InvalidOperationException("[SHODAN-STAGE] Target account name is required.");

        if (string.Equals(targetAccountName, ShodanAccountName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "[SHODAN-STAGE] Shodan is the test director, not a BotRunner target.");
        }

        _logger.LogInformation(
            "[SHODAN-STAGE] {Role} account='{Account}' Durotar herb stage map={Map} pos=({X:F1},{Y:F1},{Z:F1})",
            targetRoleLabel,
            targetAccountName,
            GatheringRouteSelection.DurotarMap,
            GatheringRouteSelection.DurotarHerbRouteStartX,
            GatheringRouteSelection.DurotarHerbRouteStartY,
            GatheringRouteSelection.DurotarHerbRouteStartZ);

        await BotTeleportAsync(
            targetAccountName,
            GatheringRouteSelection.DurotarMap,
            GatheringRouteSelection.DurotarHerbRouteStartX,
            GatheringRouteSelection.DurotarHerbRouteStartY,
            GatheringRouteSelection.DurotarHerbRouteStartZ);

        return await WaitForTeleportSettledAsync(
            targetAccountName,
            GatheringRouteSelection.DurotarHerbRouteStartX,
            GatheringRouteSelection.DurotarHerbRouteStartY,
            timeoutMs: 10000,
            progressLabel: $"{targetRoleLabel} durotar-herb stage",
            xyToleranceYards: 80f);
    }

    /// <summary>
    /// Best-effort cleanup helper for migrated gathering tests. This preserves
    /// the old "return to Orgrimmar" behavior while keeping GM movement
    /// commands out of the test body.
    /// </summary>
    public async Task ReturnBotRunnerToOrgrimmarSafeZoneAsync(
        string targetAccountName,
        string targetRoleLabel)
    {
        if (string.IsNullOrWhiteSpace(targetAccountName))
            return;

        if (string.Equals(targetAccountName, ShodanAccountName, StringComparison.OrdinalIgnoreCase))
            return;

        try
        {
            await RefreshSnapshotsAsync();
            var snap = await GetSnapshotAsync(targetAccountName);
            var pos = snap?.Player?.Unit?.GameObject?.Base?.Position;
            if (pos == null)
                return;

            var distanceToSafeZone = Distance3D(pos.X, pos.Y, pos.Z, SafeZoneX, SafeZoneY, SafeZoneZ);
            if (distanceToSafeZone <= 80f)
                return;

            _logger.LogInformation(
                "[SHODAN-STAGE] {Role} cleanup returning to Orgrimmar safe zone (dist={Distance:F0}y)",
                targetRoleLabel,
                distanceToSafeZone);
            await BotTeleportAsync(targetAccountName, SafeZoneMap, SafeZoneX, SafeZoneY, SafeZoneZ);
            await WaitForTeleportSettledAsync(
                targetAccountName,
                SafeZoneX,
                SafeZoneY,
                timeoutMs: 10000,
                progressLabel: $"{targetRoleLabel} safe-zone cleanup",
                xyToleranceYards: 80f);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[SHODAN-STAGE] {Role} safe-zone cleanup failed", targetRoleLabel);
        }
    }

    /// <summary>
    /// Shodan-owned pool refresh and active-spawn prioritization for gathering
    /// route tests. The returned route candidates prefer active `.pool spawns`
    /// coordinates when MaNGOS reports them, otherwise they preserve the static
    /// DB route order.
    /// </summary>
    public async Task<IReadOnlyList<(int map, float x, float y, float z, float distance2D, uint? poolEntry, string? poolDescription)>> RefreshAndPrioritizeGatheringPoolsWithShodanAsync(
        string shodanAccountName,
        string label,
        IReadOnlyList<(int map, float x, float y, float z, float distance2D, uint? poolEntry, string? poolDescription)> routeCandidates,
        float originX,
        float originY,
        float searchRadius,
        int maxPoolUpdates = 8)
    {
        if (string.IsNullOrWhiteSpace(shodanAccountName))
            throw new InvalidOperationException("[SHODAN-STAGE] Shodan account name is required for gathering pool refresh.");

        if (!string.Equals(shodanAccountName, ShodanAccountName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "[SHODAN-STAGE] Gathering pool refresh must be issued by the Shodan director.");
        }

        var refreshPlan = routeCandidates
            .Where(candidate => candidate.poolEntry.HasValue)
            .OrderBy(candidate => candidate.distance2D)
            .Select(candidate => candidate.poolEntry!.Value)
            .Distinct()
            .Take(maxPoolUpdates)
            .ToArray();

        if (refreshPlan.Length == 0)
            return routeCandidates;

        _logger.LogInformation(
            "[SHODAN-STAGE] {Label} gathering pool refresh plan: {Pools}",
            label,
            string.Join(", ", refreshPlan));

        var spawnedPools = new HashSet<uint>();
        var activeSpawnCandidates = new List<(int map, float x, float y, float z, float distance2D, uint? poolEntry, string? poolDescription)>();

        foreach (var poolEntry in refreshPlan)
        {
            var updateTrace = await SendGmChatCommandTrackedAsync(
                shodanAccountName,
                $".pool update {poolEntry}",
                captureResponse: true,
                delayMs: 750);
            var updateResponses = updateTrace.ChatMessages.Concat(updateTrace.ErrorMessages).ToArray();
            _logger.LogInformation(
                "[SHODAN-STAGE] {Label} .pool update {Pool}: {Evidence}",
                label,
                poolEntry,
                FormatCommandEvidence(updateTrace.DispatchResult, updateResponses));

            var spawnTrace = await SendGmChatCommandTrackedAsync(
                shodanAccountName,
                $".pool spawns {poolEntry}",
                captureResponse: true,
                delayMs: 750);
            var spawnResponses = spawnTrace.ChatMessages.Concat(spawnTrace.ErrorMessages).ToArray();
            var evidence = updateResponses.Concat(spawnResponses).ToArray();
            var state = FishingPoolActivationAnalyzer.ClassifyPoolSpawnStateResponses(poolEntry, evidence);

            activeSpawnCandidates.AddRange(GatheringRouteSelection.SelectActivePoolSpawnCandidates(
                spawnResponses,
                originX,
                originY,
                searchRadius));

            _logger.LogInformation(
                "[SHODAN-STAGE] {Label} .pool spawns {Pool}: {Evidence} => {State}",
                label,
                poolEntry,
                FormatCommandEvidence(spawnTrace.DispatchResult, spawnResponses, "no active spawns reported"),
                state);

            if (state == FishingPoolActivationState.Spawned)
                spawnedPools.Add(poolEntry);
        }

        var activeRouteCandidates = activeSpawnCandidates
            .DistinctBy(candidate => $"{candidate.map}:{candidate.x:F1}:{candidate.y:F1}:{candidate.z:F1}:{candidate.poolEntry?.ToString() ?? "none"}")
            .OrderBy(candidate => candidate.distance2D)
            .Take(Math.Min(24, Math.Max(1, routeCandidates.Count)))
            .ToList();

        if (activeRouteCandidates.Count > 0)
        {
            _logger.LogInformation(
                "[SHODAN-STAGE] {Label} using active gathering pool coordinates: {Candidates}",
                label,
                string.Join(" | ", activeRouteCandidates.Take(8).Select(candidate =>
                    $"pool={candidate.poolEntry?.ToString() ?? "none"} dist={candidate.distance2D:F1} pos=({candidate.x:F1},{candidate.y:F1},{candidate.z:F1})")));
            return activeRouteCandidates;
        }

        if (spawnedPools.Count == 0)
        {
            _logger.LogInformation(
                "[SHODAN-STAGE] {Label} no refreshed gathering pools reported active spawns; preserving DB-distance route order.",
                label);
            return routeCandidates;
        }

        var prioritized = GatheringRouteSelection.PrioritizeSpawnedPools(routeCandidates, spawnedPools);
        _logger.LogInformation(
            "[SHODAN-STAGE] {Label} prioritized gathering candidates from spawned pools [{Pools}]: {Candidates}",
            label,
            string.Join(", ", spawnedPools.OrderBy(pool => pool)),
            string.Join(" | ", prioritized.Take(6).Select(candidate =>
                $"pool={candidate.poolEntry?.ToString() ?? "none"} dist={candidate.distance2D:F1} pos=({candidate.x:F1},{candidate.y:F1},{candidate.z:F1})")));
        return prioritized;
    }

    private static string FormatCommandEvidence(
        ResponseResult dispatchResult,
        IReadOnlyCollection<string> responses,
        string emptyDetail = "")
    {
        if (responses.Count == 0)
            return string.IsNullOrWhiteSpace(emptyDetail)
                ? dispatchResult.ToString()
                : $"{dispatchResult} ({emptyDetail})";

        const int maxResponses = 4;
        var formatted = responses
            .Take(maxResponses)
            .Select(CollapseResponse)
            .ToArray();
        var suffix = responses.Count > maxResponses ? $" || ... ({responses.Count - maxResponses} more)" : "";
        return string.Join(" || ", formatted) + suffix;
    }

    private static string CollapseResponse(string response)
        => string.Join(" ", response.Split(['\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
}
