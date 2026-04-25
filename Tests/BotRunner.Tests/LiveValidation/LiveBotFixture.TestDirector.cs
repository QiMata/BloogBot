using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace BotRunner.Tests.LiveValidation;

public partial class LiveBotFixture
{
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
    /// Explicit action target for Shodan-migrated tests. Shodan is deliberately
    /// excluded; these are the BotRunner accounts that receive ActionType dispatches.
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

        foreach (var element in document.RootElement.EnumerateArray())
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
    /// Internally the helper still routes through the existing bot-chat helpers
    /// for <c>.learn</c> / <c>.setskill</c> / <c>.additem</c>. Those MaNGOS
    /// commands resolve against the sender's own character, so they have to
    /// originate from the target bot. A follow-up pass can switch to Shodan
    /// cross-targeting (<c>.target &lt;char&gt;</c> then <c>.additem</c>) or to
    /// SOAP name-targeted command variants, but that migration is orthogonal to
    /// moving the setup out of the test body.
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
        bool clearInventoryFirst = true)
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
            "spells={Spells} skills={Skills} items={Items}",
            targetRoleLabel,
            targetAccountName,
            cleanSlate,
            clearInventoryFirst,
            spellsToLearn?.Count ?? 0,
            skillsToSet?.Count ?? 0,
            itemsToAdd?.Count ?? 0);

        if (cleanSlate)
            await EnsureCleanSlateAsync(targetAccountName, targetRoleLabel);

        if (clearInventoryFirst)
        {
            await BotClearInventoryAsync(targetAccountName, includeExtraBags: false);
            await Task.Delay(1000);
        }

        if (spellsToLearn is { Count: > 0 })
        {
            foreach (var spellId in spellsToLearn)
                await BotLearnSpellAsync(targetAccountName, spellId);
        }

        if (skillsToSet is { Count: > 0 })
        {
            foreach (var directive in skillsToSet)
            {
                await BotSetSkillAsync(
                    targetAccountName,
                    directive.SkillId,
                    directive.CurrentValue,
                    directive.MaxValue);
            }
        }

        if (itemsToAdd is { Count: > 0 })
        {
            foreach (var directive in itemsToAdd)
                await BotAddItemAsync(targetAccountName, directive.ItemId, directive.Count);
        }
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
}
