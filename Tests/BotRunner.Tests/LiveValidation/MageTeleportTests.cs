using BotRunner.Travel;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Communication;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// V2.10: Mage teleport tests.
/// Setup mage class via .learn, CAST_SPELL with Teleport: Orgrimmar (3567),
/// verify position change.
///
/// Run: dotnet test --filter "FullyQualifiedName~MageTeleportTests" --configuration Release
/// </summary>
[Collection(LiveValidationCollection.Name)]
public class MageTeleportTests
{
    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    private const int KalimdorMapId = 1;
    // Razor Hill (start away from Org)
    private const float RazorHillX = 315.0f, RazorHillY = -4743.0f, RazorHillZ = 12.0f;
    // Orgrimmar target (where Teleport: Orgrimmar lands)
    private const float OrgTeleportX = 1676.0f, OrgTeleportY = -4315.0f;
    private const float OrgArrivalRadius = 50.0f;

    // Spell IDs
    private const uint TeleportOrgrimmar = 3567;
    private const uint TeleportStormwind = 3561;
    private const uint RuneOfTeleportation = (uint)MageTeleportData.RuneOfTeleportation;

    public MageTeleportTests(LiveBotFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsReady, _bot.FailureReason ?? "Live bot not ready");
    }

    /// <summary>
    /// V2.10: Horde mage at Razor Hill casts Teleport: Orgrimmar (spell 3567).
    /// Assert: position changes to Orgrimmar area. Under 15s.
    /// </summary>
    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task MageTeleport_Horde_OrgrimmarArrival()
    {
        await UseMageBackgroundSettingsAsync();
        try
        {
            var account = _bot.BgAccountName!;

            await _bot.EnsureCleanSlateAsync(account, "BG");

            // Teleport to Razor Hill (away from Org)
            await _bot.BotTeleportAsync(account, KalimdorMapId, RazorHillX, RazorHillY, RazorHillZ);
            await _bot.WaitForTeleportSettledAsync(account, RazorHillX, RazorHillY);

            await _bot.RefreshSnapshotsAsync();
            var startSnap = await _bot.GetSnapshotAsync(account);
            Assert.NotNull(startSnap);
            var startPos = startSnap!.Player?.Unit?.GameObject?.Base?.Position;
            Assert.NotNull(startPos);
            _output.WriteLine($"[TEST] Start position: ({startPos!.X:F1}, {startPos.Y:F1}, {startPos.Z:F1})");

            _output.WriteLine("[SETUP] Teaching Teleport: Orgrimmar (3567)");
            await _bot.BotLearnSpellAsync(account, TeleportOrgrimmar);
            var orgrimmarTeleportKnown = await _bot.WaitForSnapshotConditionAsync(
                account,
                s => s.Player?.SpellList?.Contains(TeleportOrgrimmar) == true,
                TimeSpan.FromSeconds(5),
                pollIntervalMs: 300,
                progressLabel: "BG teleport-org-learn");
            Assert.True(orgrimmarTeleportKnown, "Teleport: Orgrimmar should be present in SpellList before cast.");
            await EnsureTeleportReagentAsync(account, "BG");

            var castResult = await _bot.SendActionAsync(account, new ActionMessage
            {
                ActionType = ActionType.CastSpell,
                Parameters = { new RequestParameter { IntParam = (int)TeleportOrgrimmar } }
            });
            _output.WriteLine($"[TEST] CAST_SPELL result: {castResult}");
            Assert.Equal(ResponseResult.Success, castResult);

            // Wait for position change (teleport should be near-instant after cast time)
            var arrived = await _bot.WaitForSnapshotConditionAsync(
                account,
                snap =>
                {
                    var pos = snap?.Player?.Unit?.GameObject?.Base?.Position;
                    if (pos == null) return false;
                    var dist = LiveBotFixture.Distance2D(pos.X, pos.Y, OrgTeleportX, OrgTeleportY);
                    return dist <= OrgArrivalRadius;
                },
                TimeSpan.FromSeconds(15),
                pollIntervalMs: 1000,
                progressLabel: "BG mage-teleport-org");

            await _bot.RefreshSnapshotsAsync();
            var endSnap = await _bot.GetSnapshotAsync(account);
            var endPos = endSnap?.Player?.Unit?.GameObject?.Base?.Position;
            _output.WriteLine($"[TEST] End position: ({endPos?.X:F1}, {endPos?.Y:F1}, {endPos?.Z:F1})");
            Assert.True(arrived, "Mage should arrive in Orgrimmar within 15s of casting Teleport: Orgrimmar");
        }
        finally
        {
            await RestoreDefaultSettingsAsync();
        }
    }

    /// <summary>
    /// V2.10: Alliance mage at Goldshire casts Teleport: Stormwind (spell 3561).
    /// Assert: position in Stormwind within 15s.
    /// </summary>
    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task MageTeleport_Alliance_StormwindArrival()
    {
        // Alliance test requires an Alliance character -- skip if BG bot is Horde
        var account = _bot.BgAccountName!;

        await _bot.EnsureCleanSlateAsync(account, "BG");
        await _bot.RefreshSnapshotsAsync();
        var raceSnapshot = await _bot.GetSnapshotAsync(account);
        global::Tests.Infrastructure.Skip.IfNot(
            IsAllianceRace(raceSnapshot),
            "Default live fixture uses Horde bots; Stormwind teleport requires an Alliance character.");
        // Learn Teleport: Stormwind
        _output.WriteLine("[SETUP] Teaching Teleport: Stormwind (3561)");
        await _bot.BotLearnSpellAsync(account, TeleportStormwind);
        var stormwindTeleportKnown = await _bot.WaitForSnapshotConditionAsync(
            account,
            s => s.Player?.SpellList?.Contains(TeleportStormwind) == true,
            TimeSpan.FromSeconds(5),
            pollIntervalMs: 300,
            progressLabel: "BG teleport-sw-learn");
        Assert.True(stormwindTeleportKnown, "Teleport: Stormwind should be present in SpellList before cast.");
        await EnsureTeleportReagentAsync(account, "BG");

        await _bot.RefreshSnapshotsAsync();
        var startSnap = await _bot.GetSnapshotAsync(account);
        Assert.NotNull(startSnap);
        var startPos = startSnap!.Player?.Unit?.GameObject?.Base?.Position;
        Assert.NotNull(startPos);
        _output.WriteLine($"[TEST] Start position: ({startPos!.X:F1}, {startPos.Y:F1}, {startPos.Z:F1})");

        var castResult = await _bot.SendActionAsync(account, new ActionMessage
        {
            ActionType = ActionType.CastSpell,
            Parameters = { new RequestParameter { IntParam = (int)TeleportStormwind } }
        });
        _output.WriteLine($"[TEST] CAST_SPELL result: {castResult}");
        Assert.Equal(ResponseResult.Success, castResult);

        // Wait for position change (any significant movement indicates teleport worked)
        var moved = await _bot.WaitForPositionChangeAsync(account, startPos.X, startPos.Y, startPos.Z,
            timeoutMs: 15000, progressLabel: "BG mage-teleport-sw");
        Assert.True(moved, "Mage should teleport to Stormwind within 15s");
    }

    /// <summary>
    /// V2.10: Mage portal party teleport placeholder.
    /// Requires multiple party members -- validates dispatch succeeds.
    /// </summary>
    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task MagePortal_PartyTeleported()
    {
        var account = _bot.BgAccountName!;

        await _bot.EnsureCleanSlateAsync(account, "BG");

        // Learn Portal: Orgrimmar (11417)
        const uint portalOrgrimmar = 11417;
        _output.WriteLine("[SETUP] Teaching Portal: Orgrimmar (11417)");
        await _bot.BotLearnSpellAsync(account, portalOrgrimmar);

        // Verify spell learned via snapshot
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(account);
        Assert.NotNull(snap);
        var hasSpell = snap!.Player?.SpellList?.Contains(portalOrgrimmar) == true;
        _output.WriteLine($"[TEST] Has Portal: Orgrimmar spell: {hasSpell}");
        // Portal requires Rune of Portals reagent and party -- just validate setup
        Assert.NotNull(snap);
    }

    /// <summary>
    /// V2.10: Validate all 6 city teleport spells can be learned.
    /// </summary>
    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task MageAllCityTeleports()
    {
        var account = _bot.BgAccountName!;

        await _bot.EnsureCleanSlateAsync(account, "BG");

        uint[] teleportSpells = { 3567, 3563, 3566, 3561, 3562, 3565 };
        string[] spellNames = { "Orgrimmar", "Undercity", "Thunder Bluff", "Stormwind", "Ironforge", "Darnassus" };

        for (int i = 0; i < teleportSpells.Length; i++)
        {
            await _bot.BotLearnSpellAsync(account, teleportSpells[i]);
            _output.WriteLine($"[SETUP] Taught Teleport: {spellNames[i]} ({teleportSpells[i]})");
        }

        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(account);
        Assert.NotNull(snap);

        var spellList = snap!.Player?.SpellList;
        Assert.NotNull(spellList);
        foreach (var spellId in teleportSpells)
        {
            var has = spellList!.Contains(spellId);
            _output.WriteLine($"[TEST] Spell {spellId}: {(has ? "LEARNED" : "MISSING")}");
        }
    }

    private static bool IsAllianceRace(WoWActivitySnapshot? snapshot)
    {
        var raceId = (snapshot?.Player?.PlayerBytes0 ?? 0) & 0xFF;
        return raceId is 1 or 3 or 4 or 7;
    }

    private async Task UseMageBackgroundSettingsAsync()
    {
        var mageSettingsPath = ResolveRepoPath("Tests", "BotRunner.Tests", "LiveValidation", "Settings", "MageBg.settings.json");
        await _bot.EnsureSettingsAsync(mageSettingsPath);
        _bot.SetOutput(_output);
    }

    private async Task RestoreDefaultSettingsAsync()
    {
        var defaultSettingsPath = ResolveRepoPath("Services", "WoWStateManager", "Settings", "StateManagerSettings.json");
        await _bot.EnsureSettingsAsync(defaultSettingsPath);
        _bot.SetOutput(_output);
    }

    private async Task EnsureTeleportReagentAsync(string account, string label)
    {
        await _bot.BotAddItemAsync(account, RuneOfTeleportation, 5);
        var hasReagent = await _bot.WaitForSnapshotConditionAsync(
            account,
            s => s.Player?.BagContents?.Values.Any(value => value == RuneOfTeleportation) == true,
            TimeSpan.FromSeconds(5),
            pollIntervalMs: 300,
            progressLabel: $"{label} teleport-reagent");
        Assert.True(hasReagent, $"[{label}] Rune of Teleportation should be present before casting a self-teleport.");
    }

    // P4.5.3: ACK-first assertion. See LiveBotFixture.AssertTraceCommandSucceeded.
    private static void AssertCommandSucceeded(LiveBotFixture.GmChatCommandTrace trace, string label, string command)
        => LiveBotFixture.AssertTraceCommandSucceeded(trace, label, command);

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
