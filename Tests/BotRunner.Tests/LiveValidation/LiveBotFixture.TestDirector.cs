using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace BotRunner.Tests.LiveValidation;

public partial class LiveBotFixture
{
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
}
